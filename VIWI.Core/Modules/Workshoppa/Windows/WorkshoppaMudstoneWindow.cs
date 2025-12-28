using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Linq;
using VIWI.Helpers;
using VIWI.Modules.Workshoppa.External;
using VIWI.Modules.Workshoppa.Windows.Shop; // ShopItemForSale
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace VIWI.Modules.Workshoppa.Windows;

internal sealed unsafe class WorkshoppaMudstoneWindow : WorkshoppaShopWindowBase
{
    private const uint MudstoneItemId = 5229;      // Mudstone (shop item id)

    private readonly IPluginLog _log;
    private readonly WorkshoppaConfig _config;
    private readonly IChatGui _chatGui;

    private int _buyStackCount;
    private bool _buyPartialStacks = true;

    public WorkshoppaMudstoneWindow(
        IPluginLog pluginLog,
        IGameGui gameGui,
        IAddonLifecycle addonLifecycle,
        WorkshoppaConfig config,
        ExternalPluginHandler externalPluginHandler,
        IChatGui chatGui)
        : base(
            "Mudstone###WorkshoppaMudstoneWindow",
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

    public override bool IsEnabled => _config.EnableMudstoneCalculator;

    public override int GetCurrencyCount() => Shop.GetItemCount(1); // 1 == Gil in InventoryManager count

    public override void UpdateShopStock(AtkUnitBase* addon)
    {
        Shop.ItemForSale = null;

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
            if (itemId != MudstoneItemId)
                continue;

            Shop.ItemForSale = new ShopItemForSale
            {
                Position = i,
                ItemName = atkValues[14 + i].ReadAtkString(),
                Price = atkValues[75 + i].UInt,
                OwnedItems = atkValues[136 + i].UInt,
                ItemId = itemId,
            };

            return;
        }
    }

    protected override void DrawContent()
    {
        if (!IsEnabled)
        {
            IsOpen = false;
            return;
        }

        if (Shop.ItemForSale == null)
        {
            // if the shop isn't the right one / not open, close our helper window
            IsOpen = false;
            return;
        }

        var item = Shop.ItemForSale.Value;

        int mudstones = Shop.GetItemCount(MudstoneItemId);
        int freeInventorySlots = Shop.CountFreeInventorySlots();

        ImGui.Text("Inventory");
        ImGuiComponents.HelpMarker("This complements Workshoppa's experimental leveling feature that will\n" +
            "repeatedly start and discontinue projects while turning in Mudstones to level MIN\n\n" +
            "This only requires you to be at least level 20 MIN to start,\n" +
            "and is a bit costly in later levels, but takes minimal time and effort.\n\n" +
            "Some Common Breakpoints for Reference:\n" +
            "Lvl 20 -> 60 - 23,017 Mudstone, (23 Stacks) - 713k\n" +
            "Lvl 20 -> 70 - 66,061 Mudstone, (66 Stacks) - 2.04m\n" +
            "Lvl 20 -> 80 - 148,924 Mudstone, (149 Stacks) - 4.61m\n" +
            "Lvl 20 -> 90 - 329,422 Mudstone, (330 Stacks) - 10.2m\n" +
            "Lvl 20 -> 100 - 668,303 Mudstone, (669 Stacks) - 20.7m");

        ImGui.Indent();
        ImGui.Text($"Mudstones: {FormatStackCount(mudstones)}");
        ImGui.Text($"Free Slots: {freeInventorySlots}");
        ImGui.Unindent();

        ImGui.Separator();

        if (Shop.PurchaseState == null)
        {
            ImGui.SetNextItemWidth(100);
            ImGui.InputInt("Stacks to Buy", ref _buyStackCount);
            _buyStackCount = Math.Min(freeInventorySlots, Math.Max(0, _buyStackCount));

            if (mudstones % 999 > 0)
                ImGui.Checkbox($"Fill Partial Stacks (+{999 - mudstones % 999})", ref _buyPartialStacks);
        }

        int missingItems = _buyStackCount * 999;
        if (_buyPartialStacks && mudstones % 999 > 0)
            missingItems += (999 - mudstones % 999);

        if (Shop.PurchaseState != null)
        {
            Shop.HandleNextPurchaseStep();
            if (Shop.PurchaseState != null)
            {
                ImGui.Text($"Buying {FormatStackCount(Shop.PurchaseState.ItemsLeftToBuy)}...");
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

                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.DollarSign,
                        $"Auto-Buy {FormatStackCount(toPurchase)} for {cost:N0} Gil"))
                {
                    Shop.StartAutoPurchase(toPurchase);
                    Shop.HandleNextPurchaseStep();
                }
            }
        }
        //DrawFollowControls();
    }
    private static string FormatStackCount(int mudstones)
    {
        int fullStacks = mudstones / 999;
        int partials = mudstones % 999;
        string stacks = fullStacks == 1 ? "stack" : "stacks";
        if (partials > 0)
            return $"{fullStacks:N0} {stacks} + {partials}";
        return $"{fullStacks:N0} {stacks}";
    }
    public override void TriggerPurchase(AtkUnitBase* addonShop, int buyNow)
    {
        var item = Shop.ItemForSale;
        if (item == null) return;

        var buyItem = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int,  Int  = 0 },
            new() { Type = ValueType.UInt, UInt = (uint)item.Value.Position },
            new() { Type = ValueType.UInt, UInt = (uint)buyNow },
        };

        addonShop->FireCallback(3, buyItem);
    }

    public bool TryParseBuyRequest(string arguments, out int missingQuantity)
    {
        if (!int.TryParse(arguments, out int stackCount) || stackCount <= 0)
        {
            missingQuantity = 0;
            return false;
        }

        int freeInventorySlots = Shop.CountFreeInventorySlots();
        stackCount = Math.Min(freeInventorySlots, stackCount);
        missingQuantity = Math.Min(Shop.GetMaxItemsToPurchase(), stackCount * 999);
        return true;
    }

    public bool TryParseFillRequest(string arguments, out int missingQuantity)
    {
        if (!int.TryParse(arguments, out int stackCount) || stackCount < 0)
        {
            missingQuantity = 0;
            return false;
        }

        int freeInventorySlots = Shop.CountFreeInventorySlots();
        int partialStacks = Shop.CountInventorySlotsWithCondition(MudstoneItemId, q => q < 999);
        int fullStacks = Shop.CountInventorySlotsWithCondition(MudstoneItemId, q => q == 999);

        int tanks = Math.Min(
            (fullStacks + partialStacks + freeInventorySlots) * 999,
            Math.Max(stackCount * 999, (fullStacks + partialStacks) * 999)
        );

        int owned = Shop.GetItemCount(MudstoneItemId);

        if (tanks <= owned)
            missingQuantity = 0;
        else
            missingQuantity = Math.Min(Shop.GetMaxItemsToPurchase(), tanks - owned);

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
            _chatGui.Print("Not buying mudstones, you already have enough.");
            return;
        }

        _chatGui.Print($"Starting purchase of {FormatStackCount(quantity)} mudstones.");
        Shop.StartAutoPurchase(quantity);
        Shop.HandleNextPurchaseStep();
    }
}
