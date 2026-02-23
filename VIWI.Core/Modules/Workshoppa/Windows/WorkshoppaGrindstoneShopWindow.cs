using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using ECommons.Configuration;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Linq;
using VIWI.Core;
using VIWI.Helpers;
using VIWI.Modules.Workshoppa.External;
using VIWI.Modules.Workshoppa.Windows.Shop; // ShopItemForSale
using static VIWI.Modules.Workshoppa.WorkshoppaHelpers;
using static VIWI.Modules.Workshoppa.WorkshoppaModule;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace VIWI.Modules.Workshoppa.Windows;

internal sealed unsafe class WorkshoppaGrindstoneShopWindow : WorkshoppaShopWindowBase
{
    private const uint MudstoneItemId = 5229;
    private const uint ElmLumberItemId = 5367;

    private enum VendorTarget
    {
        Mudstone,
        ElmLumber,
    }

    private VendorTarget _target = VendorTarget.Mudstone;

    private ShopItemForSale? _mudstoneForSale;
    private ShopItemForSale? _elmLumberForSale;

    private readonly IPluginLog _log;
    private readonly WorkshoppaConfig _config;
    private readonly IChatGui _chatGui;

    private int _buyItemCount;
    public WorkshoppaGrindstoneShopWindow(
        IPluginLog pluginLog,
        IGameGui gameGui,
        IAddonLifecycle addonLifecycle,
        WorkshoppaConfig config,
        ExternalPluginHandler externalPluginHandler,
        IChatGui chatGui)
        : base(
            "Grindstone Shop Window###WorkshoppaGrindstoneShopWindow",
            "Shop", // addon name
            pluginLog,
            gameGui,
            addonLifecycle,
            externalPluginHandler)
    {
        _log = pluginLog;
        _config = config;
        _chatGui = chatGui;
    }

    public override bool IsEnabled => _config.EnableGrindstoneShopCalculator;

    public override int GetCurrencyCount() => Shop.GetItemCount(1); // 1 == Gil in InventoryManager count
    private ShopItemForSale? GetSelectedForSale() => _target == VendorTarget.Mudstone ? _mudstoneForSale : _elmLumberForSale;
    public override void UpdateShopStock(AtkUnitBase* addon)
    {
        _mudstoneForSale = null;
        _elmLumberForSale = null;

        if (addon->AtkValuesCount != 625)
        {
            _log.Error($"Unexpected amount of atkvalues for Shop addon ({addon->AtkValuesCount})");
            return;
        }

        var atkValues = addon->AtkValues;

        // Check if on 'Current Stock' tab?
        if (atkValues[0].UInt != 0)
            return;

        uint itemCount = atkValues[2].UInt; // 2 = Gil Text
        if (itemCount == 0)
            return;

        for (int i = 0; i < itemCount; i++)
        {
            uint itemId = atkValues[441 + i].UInt;
            if (itemId != MudstoneItemId && itemId != ElmLumberItemId)
                continue;

            var entry = new ShopItemForSale
            {
                Position = i,
                ItemName = atkValues[14 + i].ReadAtkString(),
                Price = atkValues[75 + i].UInt,
                OwnedItems = atkValues[136 + i].UInt,
                ItemId = itemId,
            };

            if (itemId == MudstoneItemId)
                _mudstoneForSale = entry;
            else
                _elmLumberForSale = entry;
        }
        Shop.ItemForSale = GetSelectedForSale();
    }

    protected override void DrawContent()
    {
        if (!IsEnabled)
        {
            IsOpen = false;
            return;
        }

        if (Shop.PurchaseState != null && Shop.PurchaseItemId != null)
            _target = (Shop.PurchaseItemId.Value == MudstoneItemId) ? VendorTarget.Mudstone : VendorTarget.ElmLumber;

        Shop.ItemForSale = GetSelectedForSale();

        if (Shop.ItemForSale == null)
        {
            IsOpen = false;
            return;
        }

        uint activeItemId = _target == VendorTarget.Mudstone ? MudstoneItemId : ElmLumberItemId;
        string activeLabel = _target == VendorTarget.Mudstone ? "Mudstones" : "Elm Lumber";
        var item = Shop.ItemForSale.Value;

        int owned = Shop.GetItemCount(activeItemId);
        int freeInventorySlots = Shop.CountFreeInventorySlots();

        ImGui.Text("Inventory");
        ImGuiComponents.HelpMarker("This complements Workshoppa's experimental leveling feature that will\n" +
            "repeatedly start and discontinue projects while turning in items to level classes\n\n" +
            "This only requires you to be at least the minimum level shown in config to start,\n" +
            "Note that after level 90, Workshop projects no longer grant EXP.");

        ImGui.Indent();
        ImGui.Text($"{activeLabel}: {owned:N0} items");
        ImGui.Text($"Stacks: {FormatStackCount(owned)}");
        ImGui.Text($"Free Slots: {freeInventorySlots}");
        ImGui.Unindent();

        ImGui.Separator();

        if (Shop.PurchaseState == null)
        {
            ImGui.Text("Item");
            ImGui.SameLine();
            if (ImGui.RadioButton("Mudstone", _target == VendorTarget.Mudstone))
                _target = VendorTarget.Mudstone;
            ImGui.SameLine();
            if (ImGui.RadioButton("Elm Lumber", _target == VendorTarget.ElmLumber))
                _target = VendorTarget.ElmLumber;

            var lvling = WorkshoppaModule.Instance?.AnyLevelingTargetsEnabled() ?? false;
            if (lvling)
            {
                var dm = VIWIContext.DataManager;
                var ps = VIWIContext.PlayerState;

                var localPlayer = VIWIContext.ObjectTable.LocalPlayer;
                bool hasPreferredWorldBonus = localPlayer != null && WorkshoppaHelpers.HasStatus(localPlayer, WorkshoppaHelpers.PreferredWorldBonusStatusId);

                var crp = WorkshoppaHelpers.GetJobByAbbrev(dm, "CRP");
                var min = WorkshoppaHelpers.GetJobByAbbrev(dm, "MIN");
                var btn = WorkshoppaHelpers.GetJobByAbbrev(dm, "BTN");

                // Elm Lumber: 747 EXP each, turn-in size 55 => always multiple of 55
                var (crpQty, _, _) = WorkshoppaHelpers.ComputeRow(dm, ps, crp, _config.CrpTargetLevel, hasPreferredWorldBonus, minRequiredLevel: 16, expPerMaterial: 747, materialsPerTurnin: 55);
                // Mudstone: 498 EXP each, turn-in size 55 => always multiple of 55
                var (minQty, _, _) = WorkshoppaHelpers.ComputeRow(dm, ps, min, _config.MinTargetLevel, hasPreferredWorldBonus, minRequiredLevel: 20, expPerMaterial: 498, materialsPerTurnin: 55);
                // Spruce Log: 2334 EXP each, turn-in size 55 => always multiple of 55
                //var btnQtyText = WorkshoppaHelpers.ComputeRow(dm, ps, btn, _config.BtnTargetLevel, hasPreferredWorldBonus, minRequiredLevel: 50, expPerMaterial: 2334, materialsPerTurnin: 55);

                ImGui.Text($"Recommended for your level target of ({(_target == VendorTarget.Mudstone ? _config.MinTargetLevel : _config.CrpTargetLevel)}): "
                    + $"{(_target == VendorTarget.Mudstone ? minQty : crpQty)}");
            }
            else
            {
                ImGui.Text($"Note that you can enable level targets in the\n" +
                    $"VIWI dashboard to get calculations for the Grindstone shop.");
            }
            ImGui.SetNextItemWidth(100);
            ImGui.InputInt("Items to Buy", ref _buyItemCount);
            _buyItemCount = Math.Max(0, _buyItemCount);

            int spaceInPartials = Shop.SumFreeSpaceInPartials(activeItemId);
            int maxBuyBySpace = freeInventorySlots * 999 + spaceInPartials;

            _buyItemCount = Math.Min(_buyItemCount, maxBuyBySpace);

            bool tpToWS = _config.TeleToWorkshop;
            if (ImGui.Checkbox("Teleport to Workshop after purchase.", ref tpToWS))
            {
                _config.TeleToWorkshop = tpToWS;
                WorkshoppaModule.Instance?.SaveConfig();
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("This requires Lifestream to be enabled");
        }

        int missingItems = _buyItemCount;

        if (Shop.PurchaseState != null)
        {
            Shop.HandleNextPurchaseStep();
            if (Shop.PurchaseState != null)
            {
                ImGui.Text($"Buying {Shop.PurchaseState.ItemsLeftToBuy:N0} items...");
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, "Cancel Auto-Buy"))
                    Shop.CancelAutoPurchase();
            }
        }
        else
        {
            int toPurchase = Math.Min(Shop.GetMaxItemsToPurchase(), missingItems);
            if (toPurchase > 0)
            {
                ImGui.Spacing();
                long cost = (long)item.Price * toPurchase;

                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.DollarSign, $"Auto-Buy {toPurchase:N0} items for {cost:N0} Gil"))
                {
                    Shop.StartAutoPurchase(toPurchase);
                    Shop.HandleNextPurchaseStep();
                }
            }
        }
        //DrawFollowControls();
    }
    private static string FormatStackCount(int owned)
    {
        int fullStacks = owned / 999;
        int partials = owned % 999;
        string stacks = fullStacks == 1 ? "stack" : "stacks";
        if (partials > 0)
            return $"{fullStacks:N0} {stacks} + {partials}";
        return $"{fullStacks:N0} {stacks}";
    }
    public override void TriggerPurchase(AtkUnitBase* addonShop, int buyNow)
    {
        var item = Shop.ItemForSale;
        if (item == null) return;

        if (buyNow <= 0)
        {
            _log.Warning("[Workshoppa] Shop window stalled, canceling.");
            Shop.CancelAutoPurchase();
            return;
        }

        var buyItem = stackalloc AtkValue[]
        {
        new() { Type = ValueType.Int,  Int  = 0 },
        new() { Type = ValueType.UInt, UInt = (uint)item.Value.Position },
        new() { Type = ValueType.UInt, UInt = (uint)buyNow },
        new() { Type = 0,             Int  = 0 }, // terminator
    };

        addonShop->FireCallback(3, buyItem);
    }

    public bool TryParseBuyRequest(string arguments, out int missingQuantity)
    {
        if (!int.TryParse(arguments, out int itemCount) || itemCount <= 0)
        {
            missingQuantity = 0;
            return false;
        }

        int freeInventorySlots = Shop.CountFreeInventorySlots();
        int spaceInPartials = Shop.SumFreeSpaceInPartials(Shop.ItemForSale?.ItemId ?? 0);
        int maxBuyBySpace = freeInventorySlots * 999 + spaceInPartials;

        itemCount = Math.Min(itemCount, maxBuyBySpace);

        missingQuantity = Math.Min(Shop.GetMaxItemsToPurchase(), itemCount);
        return true;
    }

    public void StartPurchase(int quantity)
    {
        if (!IsOpen || Shop.ItemForSale == null)
        {
            _chatGui.PrintError("Could not start purchase, shop window is not open.");
            return;
        }

        if (quantity <= 0)
        {
            _chatGui.Print("Not buying item, you already have enough.");
            return;
        }

        _chatGui.Print($"Starting purchase of {quantity:N0} items.");
        Shop.StartAutoPurchase(quantity);
        Shop.HandleNextPurchaseStep();
    }
    protected override void OnAutoBuyCompleted(uint itemId, int desiredItems, int ownedItems)
    {
        if(_config.TeleToWorkshop)
        {
            Chat.ExecuteCommand("/li ws");
        }
    }
}
