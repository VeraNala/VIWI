using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using ECommons.Logging;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace VIWI.Modules.KitchenSink.Commands;

internal sealed class DropboxQueue : IDisposable
{
	private sealed record NeededItem(uint ItemId, int Needed)
	{
		public override string ToString()
		{
			return $"{ItemId}:{Needed}";
		}
	}

	private sealed record ItemCount(int NormalQualityQuantity, int HighQualityQuantity);

	private sealed class DropboxApi
	{

		private readonly IPluginLog _pluginLog;

		private readonly ICallGateSubscriber<object> _beginTradingQueue;

		private readonly ICallGateSubscriber<uint, bool, int> _getItemQuantity;

		private readonly ICallGateSubscriber<uint, bool, int, object> _setItemQuantity;

		public DropboxApi(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
		{
			_pluginLog = pluginLog;
			_beginTradingQueue = pluginInterface.GetIpcSubscriber<object>("Dropbox.BeginTradingQueue");
			_getItemQuantity = pluginInterface.GetIpcSubscriber<uint, bool, int>("Dropbox.GetItemQuantity");
			_setItemQuantity = pluginInterface.GetIpcSubscriber<uint, bool, int, object>("Dropbox.SetItemQuantity");
		}

		public void BeginTrade()
		{
			_beginTradingQueue.InvokeAction();
		}

		public void EnqueueItem(uint itemId, bool hq, int quantity)
		{
			_pluginLog.Verbose($"Preparing to queue {itemId}, {hq}, {quantity}", Array.Empty<object>());
			if (quantity >= 0)
			{
				int num = _getItemQuantity.InvokeFunc(itemId, hq);
				_setItemQuantity.InvokeAction(itemId, hq, quantity + num);
			}
		}

		public void ClearQueue()
		{
			if (TryGetItemQuantities(out IDalamudPlugin _, out IDictionary itemQuantities))
			{
				itemQuantities.Clear();
			}
		}
        private bool TryGetItemQuantities(
            [NotNullWhen(true)] out IDalamudPlugin? dropboxPlugin,
            [NotNullWhen(true)] out IDictionary? itemQuantities)
        {
            if (!DalamudReflector.TryGetDalamudPlugin("Dropbox", out var plugin, suppressErrors: true, ignoreCache: false))
            {
                dropboxPlugin = null;
                itemQuantities = null;
                return false;
            }

            dropboxPlugin = plugin;

            var type = dropboxPlugin.GetType().Assembly.GetType("Dropbox.ItemQueueUI");
            itemQuantities = (IDictionary?)type?
                .GetField("ItemQuantities", BindingFlags.Static | BindingFlags.Public)?
                .GetValue(null);

            return itemQuantities != null;
        }
    }

    private static readonly InventoryType[] DefaultInventoryTypes =
{
    InventoryType.Inventory1,
    InventoryType.Inventory2,
    InventoryType.Inventory3,
    InventoryType.Inventory4,
    InventoryType.ArmoryMainHand,
    InventoryType.ArmoryOffHand,
    InventoryType.ArmoryHead,
    InventoryType.ArmoryBody,
    InventoryType.ArmoryHands,
    InventoryType.ArmoryLegs,
    InventoryType.ArmoryFeets,
    InventoryType.ArmoryEar,
    InventoryType.ArmoryNeck,
    InventoryType.ArmoryWrist,
    InventoryType.ArmoryRings,
    InventoryType.ArmorySoulCrystal,
    InventoryType.Crystals,
    InventoryType.Currency,
};

    private readonly ICommandManager _commandManager;

	private readonly IChatGui _chatGui;

	private readonly DropboxApi _dropboxApi;

	public DropboxQueue(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IChatGui chatGui, IPluginLog pluginLog)
	{
		_commandManager = commandManager;
		_chatGui = chatGui;
		_dropboxApi = new DropboxApi(pluginInterface, pluginLog);
		_commandManager.AddHandler("/dbq", new CommandInfo(Queue) { ShowInHelp = false });
    }

	private void Queue(string command, string arguments)
	{
		string[] array = arguments.Split(" ", 2);
		switch (array[0])
		{
		case "*s":
		case "shards":
			BuildRequest(string.Join(" ", from x in Enumerable.Range(2, 6)
				select $"{x}:9999"));
			break;
		case "*c":
		case "crystals":
			BuildRequest(string.Join(" ", from x in Enumerable.Range(8, 6)
				select $"{x}:9999"));
			break;
		case "*x":
		case "clusters":
			BuildRequest(string.Join(" ", from x in Enumerable.Range(14, 6)
				select $"{x}:9999"));
			break;
		case "*sc":
		case "shards+crystals":
			BuildRequest(string.Join(" ", from x in Enumerable.Range(2, 12)
				select $"{x}:9999"));
			break;
		case "*cx":
		case "crystals+clusters":
			BuildRequest(string.Join(" ", from x in Enumerable.Range(8, 12)
				select $"{x}:9999"));
			break;
		case "*scx":
		case "shards+crystals+clusters":
			BuildRequest(string.Join(" ", from x in Enumerable.Range(2, 18)
				select $"{x}:9999"));
			break;
		case "request":
			BuildRequest((array.Length == 2) ? array[1] : string.Empty);
			break;
		case "clear":
			_dropboxApi.ClearQueue();
			break;
		default:
			AddToQueue(arguments);
			break;
		}
	}

    private unsafe void BuildRequest(string arguments)
    {
        if (string.IsNullOrEmpty(arguments))
        {
            _chatGui.PrintError("Usage: /dbq request item1:qty1 item2:qty2 [...]", null, null);
            return;
        }

        var im = InventoryManager.Instance();
        if (im == null)
            return;

        var parsed = ParseArguments(arguments);
        if (parsed == null)
            return;

        var needed = parsed
            .Select(item =>
            {
                var haveNq = DefaultInventoryTypes.Sum(t => im->GetItemCountInContainer(item.ItemId, t, isHq: false));
                var haveHq = DefaultInventoryTypes.Sum(t => im->GetItemCountInContainer(item.ItemId, t, isHq: true));
                var have = haveNq + haveHq;
                if (item.ItemId == 1) // Gil
                {
                    have = (int)Math.Min((ulong)int.MaxValue, im->GetGil());
                }
                return item with { Needed = item.Needed - have };
            })
            .Where(x => x.Needed > 0)
            .ToList();

        if (needed.Count == 0)
        {
            _chatGui.Print("No items need to be filled", null, null);
        }
        else
        {
            _chatGui.Print(
                new SeStringBuilder()
                    .AddUiForeground("[KitchenSink] ", 504)
                    .Append("/dbq " + string.Join(" ", needed))
                    .Build(),
                null,
                null);
        }
    }

    private void AddToQueue(string arguments)
	{
		IReadOnlyList<NeededItem> readOnlyList = ParseArguments(arguments);
		if (readOnlyList == null)
		{
			return;
		}
		Dictionary<uint, ItemCount> itemCounts = GetItemCounts();
		foreach (NeededItem item in readOnlyList)
		{
			if (itemCounts.TryGetValue(item.ItemId, out var value))
			{
				int num = Math.Min(item.Needed, value.NormalQualityQuantity);
				int num2 = Math.Min(item.Needed - num, value.HighQualityQuantity);
				if (num > 0)
				{
					_dropboxApi.EnqueueItem(item.ItemId, hq: false, num);
				}
				if (num2 > 0)
				{
					_dropboxApi.EnqueueItem(item.ItemId, hq: true, num2);
				}
				if (num > 0 || num2 > 0)
				{
					itemCounts[item.ItemId] = new ItemCount(value.NormalQualityQuantity - num, value.HighQualityQuantity - num2);
				}
			}
		}
		_dropboxApi.BeginTrade();
	}

	private ReadOnlyCollection<NeededItem>? ParseArguments(string arguments)
	{
        List<(string, NeededItem)> list = (from x in arguments.Split(' ') select x.Split(':')).Select(delegate (string[] x)
        {
            if (x.Length != 2)
		    {
			    return ((string, NeededItem?))("Unable to parse " + string.Join(" ", x) + ".", null);
		    }
		    if (!uint.TryParse(x[0], out var result))
		    {
			    return ((string, NeededItem?))("Unable to parse item id " + x[0] + ".", null);
		    }
		    int result2;
		    if (x[1] == "*")
		    {
			    result2 = int.MaxValue;
		    }
		    else if (!int.TryParse(x[1], out result2))
		    {
			    return ((string, NeededItem?))("Unable to parse quantity " + x[1] + ".", null);
		    }
            return (string.Empty, new NeededItem(result, result2));
        }).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		List<string> list2 = (from x in list
			where !string.IsNullOrEmpty(x.Item1)
			select x.Item1).ToList();
		if (list2.Count == 1)
		{
			_chatGui.PrintError("dbq: " + list2.First(), (string)null, (ushort?)null);
			return null;
		}
		if (list2.Count >= 2)
		{
			_chatGui.PrintError("dbq: Multiple errors occured:", (string)null, (ushort?)null);
			foreach (string item in list2)
			{
				_chatGui.PrintError(" - " + item, (string)null, (ushort?)null);
			}
			return null;
		}
		return list.Select(((string, NeededItem) x) => x.Item2).ToList().AsReadOnly();
	}

    private unsafe Dictionary<uint, ItemCount> GetItemCounts()
    {
        var dict = new Dictionary<uint, ItemCount>();

        var im = InventoryManager.Instance();
        if (im == null)
            return dict;

        foreach (var invType in DefaultInventoryTypes)
        {
            var container = im->GetInventoryContainer(invType);
            if (container == null)
                continue;

            for (int i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot == null)
                    continue;

                if (slot->ItemId == 0)
                    continue;

                if (slot->SpiritbondOrCollectability > 0)
                    continue;

                if (!dict.TryGetValue(slot->ItemId, out var count))
                    count = new ItemCount(0, 0);
                var isHq = slot->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality);

                count = isHq
                    ? count with { HighQualityQuantity = count.HighQualityQuantity + (int)slot->Quantity }
                    : count with { NormalQualityQuantity = count.NormalQualityQuantity + (int)slot->Quantity };

                dict[slot->ItemId] = count;
            }
        }

        return dict;
    }

    public void Dispose()
	{
		_commandManager.RemoveHandler("/dbq");
	}

}
