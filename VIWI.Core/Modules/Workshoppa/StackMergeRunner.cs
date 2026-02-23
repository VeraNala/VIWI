using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed unsafe class StackMergeRunner : IDisposable
{
    private readonly IFramework _framework;
    private readonly Func<bool> _canOperate;
    private readonly int _moveDelayMs;

    private readonly Queue<MoveOp> _queue = new();
    private DateTime _nextMoveAtUtc = DateTime.MinValue;

    public bool IsRunning => _queue.Count > 0;
    public int PendingOps => _queue.Count;

    public DateTime LastMoveUtc { get; private set; } = DateTime.MinValue;
    public DateTime StartedUtc { get; private set; } = DateTime.MinValue;

    public StackMergeRunner(IFramework framework, Func<bool> canOperate, int moveDelayMs = 175)
    {
        _framework = framework;
        _canOperate = canOperate;
        _moveDelayMs = Math.Max(50, moveDelayMs);
        _framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
        _queue.Clear();
    }

    public void Cancel()
    {
        _queue.Clear();
        _nextMoveAtUtc = DateTime.MinValue;
    }

    public bool TryStartMergeForItem(uint itemId)
    {
        if (IsRunning) return false;

        var inv = InventoryManager.Instance();
        if (inv == null) return false;

        var plan = BuildPlanForItem(inv, itemId);
        if (plan.Count == 0) return false;

        foreach (var op in plan)
            _queue.Enqueue(op);

        StartedUtc = DateTime.UtcNow;
        LastMoveUtc = DateTime.MinValue;
        _nextMoveAtUtc = DateTime.UtcNow;
        return true;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (_queue.Count == 0) return;

        var now = DateTime.UtcNow;
        if (now < _nextMoveAtUtc) return;

        if (!_canOperate()) return;

        var inv = InventoryManager.Instance();
        if (inv == null) return;

        var op = _queue.Dequeue();
        inv->MoveItemSlot(op.SrcContainer, op.SrcSlot, op.DstContainer, op.DstSlot, true);

        LastMoveUtc = now;
        _nextMoveAtUtc = now.AddMilliseconds(_moveDelayMs);
    }

    private static List<MoveOp> BuildPlanForItem(InventoryManager* inv, uint itemId)
    {
        const uint MaxStack = 999;

        var bagTypes = new[]
        {
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4,
        };

        var stacks = new List<ItemStackRef>();

        foreach (var t in bagTypes)
        {
            var c = inv->GetInventoryContainer(t);
            if (c == null) continue;

            for (var slot = 0; slot < c->Size; slot++)
            {
                var it = c->GetInventorySlot(slot);
                if (it == null) continue;
                if (it->ItemId == 0) continue;
                if (it->ItemId != itemId) continue;

                if (it->Flags.HasFlag(InventoryItem.ItemFlags.Collectable))
                    continue;

                var isHq = it->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality);
                var qty = it->Quantity;
                if (qty == 0) continue;

                stacks.Add(new ItemStackRef(t, (ushort)slot, (uint)qty, isHq));
            }
        }

        if (stacks.Count < 2)
            return new();

        var groups = stacks.GroupBy(s => s.IsHq)
            .Select(g => new { List = g.ToList(), PartialCount = g.Count(x => x.Qty < MaxStack), Total = g.Sum(x => (long)x.Qty) })
            .Where(g => g.List.Count > 1)
            .OrderByDescending(g => g.PartialCount)
            .ThenByDescending(g => g.Total)
            .ToList();

        if (groups.Count == 0)
            return new();

        var plan = new List<MoveOp>();
        foreach (var g in groups)
            plan.AddRange(BuildPlanForList(g.List));

        return plan;
    }

    private static List<MoveOp> BuildPlanForList(List<ItemStackRef> list)
    {
        const uint MaxStack = 999;

        var stacks = list
            .Where(s => s.Qty > 0)
            .OrderBy(s => ContainerSortKey(s.Container))
            .ThenBy(s => s.Slot)
            .ToList();

        if (stacks.Count < 2)
            return new();

        var simulatedQty = stacks.ToDictionary(s => (s.Container, s.Slot), s => s.Qty);
        var plan = new List<MoveOp>();

        var primaryStack = stacks[0];
        var primaryKey = (primaryStack.Container, primaryStack.Slot);

        foreach (var source in stacks.Skip(1))
        {
            var sourceKey = (source.Container, source.Slot);
            var sourceQty = simulatedQty[sourceKey];
            if (sourceQty == 0)
                continue;

            var targetQty = simulatedQty[primaryKey];
            if (targetQty >= MaxStack)
                break;

            var space = MaxStack - targetQty;
            var amountMoved = Math.Min(space, sourceQty);
            if (amountMoved == 0)
                continue;

            plan.Add(new MoveOp(source.Container, source.Slot, primaryStack.Container, primaryStack.Slot));

            simulatedQty[sourceKey] -= amountMoved;
            simulatedQty[primaryKey] += amountMoved;
        }

        return plan;
    }

    private static int ContainerSortKey(InventoryType t) => t switch
    {
        InventoryType.Inventory1 => 0,
        InventoryType.Inventory2 => 1,
        InventoryType.Inventory3 => 2,
        InventoryType.Inventory4 => 3,
        _ => 99
    };
    private readonly record struct MoveOp(InventoryType SrcContainer, ushort SrcSlot, InventoryType DstContainer, ushort DstSlot);
    private readonly record struct ItemStackRef(InventoryType Container, ushort Slot, uint Qty, bool IsHq);
}