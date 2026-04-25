using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Cabinet = Lumina.Excel.Sheets.Cabinet;

namespace VIWI.Modules.KitchenSink.Commands;

internal sealed class GlamourSetter : Window, IDisposable
{
    private sealed class GlamourSet
    {
        public required uint ItemId { get; init; }
        public required string Name { get; init; }
        public required ESetType SetType { get; init; }
        public required ReadOnlyCollection<GlamourItem> Items { get; init; }
    }

    private sealed class GlamourItem
    {
        public required uint ItemId { get; init; }
        public required string Name { get; init; }
        public required SpecialShopItem? ShopItem { get; init; }
    }

    private sealed class SpecialShopItem
    {
        public required uint ItemId { get; init; }
        public required uint CostItemId { get; init; }
        public required uint CostType { get; init; }
        public required string CostName { get; init; }
        public required uint CostQuantity { get; init; }
    }

    private enum ESetType
	{
		Default,
		MGP,
		PvP,
		AlliedSociety,
		Special,
		Unobtainable
	}

    private const uint ItemWolfMarks = 25;
    private const uint ItemMgp = 29;
    private const uint ItemTrophyCrystals = 36656;
    private const uint MostRecentPvpSet = 47704;

    private static readonly (uint ItemId, string Name)[] AlliedSocietyCurrencies =
    {
		(21074u, "Vanu Whitebone"),
		(21079u, "Black Copper Gil"),
		(21081u, "Kojin Sango")
	};

	private static readonly ImmutableHashSet<uint> MgpMakaiSets = new HashSet<uint>
	{
		45249u, 45466u, 45467u, 45255u, 45256u, 45257u, 45254u, 45259u, 45260u, 45261u,
		45258u, 45465u, 45464u, 45251u, 45253u, 45250u, 45252u
	}.ToImmutableHashSet();

	private static readonly ImmutableHashSet<uint> UndyedRathalosSets = new HashSet<uint> { 45324u, 45323u }.ToImmutableHashSet();

	private static readonly ImmutableHashSet<uint> EternalBondingSets = new HashSet<uint> { 45139u, 45140u, 45141u, 45142u, 45143u, 45144u }.ToImmutableHashSet();

	private static readonly ImmutableHashSet<uint> UnobtainableSets = new HashSet<uint>
	{
		45320u, 45248u, 45247u, 45529u, 45306u, 45340u, 45289u, 45339u, 45222u, 45330u,
		45223u, 45424u, 45423u, 45564u
	}.ToImmutableHashSet();


    private readonly IDalamudPluginInterface _pi;
    private readonly IDataManager _data;
    private readonly IClientState _clientState;
    private readonly IPlayerState _playerState;
    private readonly IChatGui _chat;
    private readonly ICommandManager _commands;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly System.Action _save;
    private readonly KitchenSinkConfig _cfg;

    private readonly ReadOnlyCollection<GlamourSet> _glamourSets;
    private readonly Dictionary<uint, int> _ownedCurrencies = new();
    private readonly List<InventoryType> _inventoryTypes;

    private KitchenSinkConfig.CharacterData? _character;


    public GlamourSetter(
        IDalamudPluginInterface pi,
        IDataManager data,
        IClientState clientState,
        IPlayerState playerState,
        IChatGui chat,
        ICommandManager commands,
        IAddonLifecycle addonLifecycle,
        KitchenSinkConfig cfg,
        System.Action saveConfig)
        : base("Glamour Sets###KsGlamourSets")
    {
        _pi = pi;
        _data = data;
        _clientState = clientState;
        _playerState = playerState;
        _chat = chat;
        _commands = commands;
        _addonLifecycle = addonLifecycle;
        _cfg = cfg;
        _save = saveConfig;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        IsOpen = false;
        _inventoryTypes = new List<InventoryType>
        {
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4,

            InventoryType.SaddleBag1,
            InventoryType.SaddleBag2,
            InventoryType.PremiumSaddleBag1,
            InventoryType.PremiumSaddleBag2,

            InventoryType.EquippedItems,

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
        };
        var armoireItems = BuildArmoireItemSet(_data);
        var specialShop = BuildSpecialShopItems(_data);
        _glamourSets = BuildGlamourSets(_data, armoireItems, specialShop);

        _commands.AddHandler("/glamoursets", new CommandInfo(ProcessCommand) { ShowInHelp = false });
        _addonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "MiragePrismPrismBox", UpdateFromGlamourDresser);

        _clientState.Logout += Reset;
        if (_clientState.IsLoggedIn)
            PreOpenCheck();
    }

    private static HashSet<uint> BuildArmoireItemSet(IDataManager data)
    {
        var sheet = data.GetExcelSheet<Cabinet>(null, null);
        if (sheet == null)
            return new HashSet<uint>();

        var set = new HashSet<uint>();
        foreach (var row in sheet)
        {
            if (row.RowId == 0)
                continue;

            var itemId = row.Item.RowId;
            if (itemId != 0)
                set.Add(itemId);
        }

        return set;
    }

    private void ProcessCommand(string command, string arguments)
    {
        IsOpen = !IsOpen;
    }

    public override void PreOpenCheck()
    {
        var cid = _playerState.ContentId;
        if (!_clientState.IsLoggedIn || cid == 0)
        {
            _character = null;
            return;
        }

        _character = _cfg.Characters.FirstOrDefault(x => x.LocalContentId == cid);
        if (_character == null)
        {
            _character = new KitchenSinkConfig.CharacterData { LocalContentId = cid };
            _cfg.Characters.Add(_character);
            _save();
        }
    }

    public unsafe override void Draw()
    {
        if (_character == null)
        {
            ImGui.TextUnformatted("You are not logged in.");
            return;
        }

        if (!_character.IsGlamourDresserInitialized)
        {
            ImGui.TextUnformatted("Please access your glamour dresser.");
            return;
        }

        UpdateCurrencies();

        var ownedSets = _glamourSets.Where(s => _character.GlamourDresserItems.Contains(s.ItemId)).ToList();

        var totalRelevant = _glamourSets.Count(s => s.SetType != ESetType.Unobtainable || ownedSets.Contains(s));

        ImGui.TextUnformatted($"Complete Sets: {ownedSets.Count} / {totalRelevant}");
        ImGui.TextUnformatted($"Space saved: {ownedSets.Sum(s => s.Items.Count - 1)} items");

        var showMissingOnly = _cfg.ShowOnlyMissingGlamourSets;
        if (ImGui.Checkbox("Show missing only", ref showMissingOnly))
        {
            _cfg.ShowOnlyMissingGlamourSets = showMissingOnly;
            _save();
        }

        ImGui.Separator();

        using var tabs = ImRaii.TabBar("Tabs");
        if (!tabs)
            return;

        DrawTab("Normal", ownedSets, ESetType.Default);
        DrawTab("PvP", ownedSets, ESetType.PvP);
        DrawTab("MGP", ownedSets, ESetType.MGP);
        DrawTab("Allied Societies", ownedSets, ESetType.AlliedSociety);
        DrawSpecialtyTab(ownedSets);
        DrawTab("Unobtainable", ownedSets, ESetType.Unobtainable);
    }

    private unsafe void UpdateCurrencies()
    {
        var im = InventoryManager.Instance();
        if (im == null)
        {
            _ownedCurrencies.Clear();
            return;
        }
        _ownedCurrencies[ItemMgp] = im->GetItemCountInContainer(ItemMgp, InventoryType.Currency, isHq: false, minCollectability: 0);
        _ownedCurrencies[ItemWolfMarks] = (int)im->GetWolfMarks();
        _ownedCurrencies[ItemTrophyCrystals] = im->GetInventoryItemCount(ItemTrophyCrystals, false, true, true, 0);

        foreach (var (itemId, _) in AlliedSocietyCurrencies)
            _ownedCurrencies[itemId] = im->GetInventoryItemCount(itemId, false, true, true, 0);
    }

    private void DrawTab(string name, List<GlamourSet> ownedSets, ESetType setType)
    {
        using var tab = ImRaii.TabItem(name);
        if (!tab)
            return;

        var sets = _glamourSets.Where(s => s.SetType == setType).ToList();
        if (_cfg.ShowOnlyMissingGlamourSets)
            sets = sets.Except(ownedSets).ToList();

        var ownedItems = GetOwnedItems();
        DrawMissingItemHeader(sets, setType, ownedSets, ownedItems);

        using var child = ImRaii.Child("Sets");
        DrawSetRange(sets, ownedSets, ownedItems);
    }

    private void DrawSpecialtyTab(List<GlamourSet> ownedSets)
    {
        using var tab = ImRaii.TabItem("Special");
        if (!tab)
            return;

        var sets = _glamourSets.Where(s => s.SetType == ESetType.Special).ToList();
        if (_cfg.ShowOnlyMissingGlamourSets)
            sets = sets.Except(ownedSets).ToList();

        var ownedItems = GetOwnedItems();
        DrawMissingItemHeader(sets, ESetType.Special, ownedSets, ownedItems);

        if (ImGui.CollapsingHeader("Eternal Bonding"))
            DrawSetRange(sets.Where(s => EternalBondingSets.Contains(s.ItemId)).ToList(), ownedSets, ownedItems);

        if (ImGui.CollapsingHeader("Makai Sets (MGP)"))
            DrawSetRange(sets.Where(s => MgpMakaiSets.Contains(s.ItemId)).ToList(), ownedSets, ownedItems);

        if (ImGui.CollapsingHeader("Rathalos Sets (undyed)"))
            DrawSetRange(sets.Where(s => UndyedRathalosSets.Contains(s.ItemId)).ToList(), ownedSets, ownedItems);
    }

    private void DrawMissingItemHeader(List<GlamourSet> sets, ESetType setType, List<GlamourSet> ownedSets, HashSet<uint> ownedItems)
    {
        var missingItems = sets
            .Except(ownedSets)
            .SelectMany(s => s.Items)
            .Where(i => !ownedItems.Contains(i.ItemId))
            .ToList();

        long Needed(uint costItemId) =>
            missingItems.Where(i => i.ShopItem?.CostItemId == costItemId).Sum(i => (long)(i.ShopItem?.CostQuantity ?? 0));

        if (setType == ESetType.PvP)
        {
            ImGui.TextUnformatted($"Wolf Marks: {_ownedCurrencies.GetValueOrDefault(ItemWolfMarks):N0} / {Needed(ItemWolfMarks):N0}");
            ImGui.TextUnformatted($"Trophy Crystals: {_ownedCurrencies.GetValueOrDefault(ItemTrophyCrystals):N0} / {Needed(ItemTrophyCrystals):N0}");
            ImGui.Separator();
            return;
        }

        if (setType == ESetType.MGP || setType == ESetType.Special)
        {
            ImGui.TextUnformatted($"MGP: {_ownedCurrencies.GetValueOrDefault(ItemMgp):N0} / {Needed(ItemMgp):N0}");
            ImGui.Separator();
            return;
        }

        if (setType == ESetType.AlliedSociety)
        {
            foreach (var (itemId, name) in AlliedSocietyCurrencies)
                ImGui.TextUnformatted($"{name}: {_ownedCurrencies.GetValueOrDefault(itemId):N0} / {Needed(itemId):N0}");

            ImGui.Separator();
        }
    }

    private void DrawSetRange(List<GlamourSet> sets, List<GlamourSet> ownedSets, HashSet<uint> ownedItems)
    {
        foreach (var set in sets)
        {
            if (ownedSets.Contains(set))
            {
                var c = ImGuiColors.ParsedGreen;
                ImGui.TextColored(c, set.Name);
                continue;
            }

            var ownedCount = set.Items.Count(i => ownedItems.Contains(i.ItemId));

            if (ownedCount == set.Items.Count)
            {
                var c = ImGuiColors.ParsedBlue;
                ImGui.TextColored(c, $"{set.Name} (Can be completed)");
            }
            else if (CanAffordAllMissingGearPieces(set, ownedItems))
            {
                var c = ImGuiColors.DalamudViolet;
                ImGui.TextColored(c, $"{set.Name} (Can afford)");
            }
            else if (ownedCount > 0)
            {
                var c = ImGuiColors.DalamudYellow;
                ImGui.TextColored(c, set.Name);
            }
            else
            {
                ImGui.TextUnformatted(set.Name);
            }

            using var indent = ImRaii.PushIndent();

            foreach (var item in set.Items)
            {
                if (ownedItems.Contains(item.ItemId))
                {
                    var c = ImGuiColors.ParsedGreen;
                    ImGui.TextColored(c, item.Name);
                }
                else if (item.ShopItem != null)
                {
                    ImGui.TextUnformatted($"{item.Name} ({item.ShopItem.CostQuantity:N0}x {item.ShopItem.CostName})");
                }
                else
                {
                    ImGui.TextUnformatted(item.Name);
                }

                if (ImGui.IsItemClicked())
                {
                    _chat.Print(SeString.CreateItemLink(item.ItemId, false, null), null, null);
                }
            }
        }
    }

    private unsafe HashSet<uint> GetOwnedItems()
    {
        var owned = new HashSet<uint>();

        foreach (var item in _character?.GlamourDresserItems ?? new HashSet<uint>())
            owned.Add(item);

        var im = InventoryManager.Instance();
        if (im == null)
            return owned;

        foreach (var inv in _inventoryTypes)
        {
            var container = im->GetInventoryContainer(inv);
            if (container == null)
                continue;

            for (int i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot == null)
                    continue;

                if (slot->ItemId != 0)
                    owned.Add(slot->ItemId % 1_000_000);
            }
        }

        return owned;
    }

    private bool CanAffordAllMissingGearPieces(GlamourSet set, HashSet<uint> ownedItems)
    {
        uint currency = 0;
        uint total = 0;

        foreach (var item in set.Items)
        {
            if (ownedItems.Contains(item.ItemId))
                continue;

            if (item.ShopItem == null)
                return false;

            currency = item.ShopItem.CostItemId;
            total += item.ShopItem.CostQuantity;
        }

        return total <= _ownedCurrencies.GetValueOrDefault(currency);
    }

    private unsafe void UpdateFromGlamourDresser(AddonEvent type, AddonArgs args)
    {
        if (_character == null)
            return;

        var mm = MirageManager.Instance();
        if (mm == null)
        {
            Reset(0, 0);
            return;
        }
        var ids = mm->PrismBoxItemIds.ToArray();

        var newSet = new HashSet<uint>();
        foreach (var id in ids)
        {
            if (id == 0)
                continue;
            newSet.Add(id % 1_000_000);
        }
        if (!_character.IsGlamourDresserInitialized || !_character.GlamourDresserItems.SetEquals(newSet))
        {
            _character.IsGlamourDresserInitialized = true;
            _character.GlamourDresserItems = newSet;
            _save();
        }
    }

    private void Reset(int type, int code)
    {
        _character = null;
    }

    public void Dispose()
    {
        _clientState.Logout -= Reset;
        _addonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "MiragePrismPrismBox", UpdateFromGlamourDresser);
        _commands.RemoveHandler("/glamoursets");
    }

    private static ReadOnlyCollection<GlamourSet> BuildGlamourSets(
        IDataManager dataManager,
        HashSet<uint> armoireItems,
        Dictionary<uint, SpecialShopItem> specialShopItems)
    {
        var itemSheet = dataManager.GetExcelSheet<Item>(null, null);
        var setSheet = dataManager.GetExcelSheet<MirageStoreSetItem>(null, null);

        if (itemSheet == null || setSheet == null)
            return new List<GlamourSet>().AsReadOnly();

        var sets = new List<GlamourSet>();

        foreach (var set in setSheet)
        {
            if (set.RowId == 0)
                continue;

            var refs = new[]
            {
                set.MainHand, set.OffHand, set.Head, set.Body, set.Hands, set.Legs, set.Feet,
                set.Earrings, set.Necklace, set.Bracelets, set.Ring
            };

            var items = refs
                .Where(r => r.RowId != 0)
                .Select(r => r.Value)
                .Select(it => new GlamourItem
                {
                    ItemId = it.RowId,
                    Name = it.Name.ToString(),
                    ShopItem = specialShopItems.GetValueOrDefault(it.RowId)
                })
                .Where(i => !string.IsNullOrEmpty(i.Name))
                .ToList()
                .AsReadOnly();

            if (items.Count == 0)
                continue;

            var setItem = itemSheet.GetRow(set.RowId);
            var setName = setItem.RowId != 0 ? setItem.Name.ToString() : $"Set {set.RowId}";
            var setType = DetermineSetType(set.RowId, items);

            if (!items.Any(i => !armoireItems.Contains(i.ItemId)))
                continue;

            sets.Add(new GlamourSet
            {
                ItemId = set.RowId,
                Name = setName,
                Items = items,
                SetType = setType
            });
        }

        return sets
            .OrderBy(s => s.Name)
            .ThenBy(s => s.ItemId)
            .ToList()
            .AsReadOnly();
    }

    private static ESetType DetermineSetType(uint setRowId, ReadOnlyCollection<GlamourItem> items)
    {
        if (setRowId == MostRecentPvpSet)
            return ESetType.PvP;

        if (UnobtainableSets.Contains(setRowId))
            return ESetType.Unobtainable;

        if (EternalBondingSets.Contains(setRowId) || UndyedRathalosSets.Contains(setRowId) || MgpMakaiSets.Contains(setRowId))
            return ESetType.Special;

        var costItemId = items.FirstOrDefault()?.ShopItem?.CostItemId;

        if (costItemId.HasValue && AlliedSocietyCurrencies.Any(x => x.ItemId == costItemId.Value))
            return ESetType.AlliedSociety;

        if (costItemId == ItemWolfMarks || costItemId == ItemTrophyCrystals)
            return ESetType.PvP;

        if (costItemId == ItemMgp)
            return ESetType.MGP;

        return ESetType.Default;
    }

    private static Dictionary<uint, SpecialShopItem> BuildSpecialShopItems(IDataManager dataManager)
    {
        var shopSheet = dataManager.GetExcelSheet<SpecialShop>(null, null);
        if (shopSheet == null)
            return new Dictionary<uint, SpecialShopItem>();

        var dict = new Dictionary<uint, SpecialShopItem>();

        foreach (var shop in shopSheet)
        {
            if (shop.RowId == 0)
                continue;

            if (string.IsNullOrWhiteSpace(shop.Name.ToString()))
                continue;
            foreach (var itemStruct in shop.Item)
            {
                foreach (var recv in itemStruct.ReceiveItems)
                {
                    var itemId = recv.Item.RowId;
                    if (itemId == 0)
                        continue;

                    var cost = itemStruct.ItemCosts[0];
                    var costItem = cost.ItemCost.Value;
                    var costItemId = costItem.RowId;
                    var costType = costItem.ItemUICategory.RowId;
                    var costName = costItem.Name.ToString();
                    var costQty = cost.CurrencyCost;

                    if (costItemId >= 100 && costType != 100)
                        continue;

                    if (!dict.ContainsKey(itemId))
                    {
                        dict[itemId] = new SpecialShopItem
                        {
                            ItemId = itemId,
                            CostItemId = costItemId,
                            CostType = costType,
                            CostName = costName,
                            CostQuantity = costQty
                        };
                    }
                }
            }
        }

        return dict;
    }
}