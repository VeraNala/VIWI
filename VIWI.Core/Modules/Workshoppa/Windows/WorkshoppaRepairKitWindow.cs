using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using VIWI.Modules.Workshoppa.External;
using VIWI.Modules.Workshoppa.Windows.Shop;
using VIWI.Helpers;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace VIWI.Modules.Workshoppa.Windows;

internal sealed unsafe class WorkshoppaRepairKitWindow : WorkshoppaShopWindowBase
{
    private const uint DarkMatterClusterItemId = 10335;     // Dark Matter Cluster
    private const uint Grade6DarkMatterItemId = 10386;      // Grade 6 Dark Matter (shop item id)

    private readonly IPluginLog _log;
    private readonly WorkshoppaConfig _config;

    public WorkshoppaRepairKitWindow(
        IPluginLog pluginLog,
        IGameGui gameGui,
        IAddonLifecycle addonLifecycle,
        WorkshoppaConfig config,
        ExternalPluginHandler externalPluginHandler)
        : base(
            "Repair Kits###WorkshoppaRepairKitWindow",
            "Shop", // addon name
            pluginLog,
            gameGui,
            addonLifecycle,
            externalPluginHandler)
    {
        _log = pluginLog;
        _config = config;
    }

    public override bool IsEnabled => _config.EnableRepairKitCalculator;

    public override int GetCurrencyCount() => Shop.GetItemCount(1); // 1 == Gil in InventoryManager count

    private int GetDarkMatterClusterCount() => Shop.GetItemCount(DarkMatterClusterItemId);

    public override void UpdateShopStock(AtkUnitBase* addon)
    {
        Shop.ItemForSale = null;

        if (GetDarkMatterClusterCount() == 0)
            return;

        if (addon->AtkValuesCount != 625)
        {
            _log.Error($"Unexpected amount of atkvalues for Shop addon ({addon->AtkValuesCount})");
            return;
        }

        var atkValues = addon->AtkValues;

        // Check if on 'Current Stock' tab?
        if (atkValues[0].UInt != 0)
            return;

        uint itemCount = atkValues[2].UInt;
        if (itemCount == 0)
            return;

        for (int i = 0; i < itemCount; i++)
        {
            uint itemId = atkValues[441 + i].UInt;
            if (itemId != Grade6DarkMatterItemId)
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

        int darkMatterClusters = GetDarkMatterClusterCount();
        if (Shop.ItemForSale == null || darkMatterClusters == 0)
        {
            // if the shop isn't the right one / not open / no clusters, close our helper window
            IsOpen = false;
            return;
        }

        var item = Shop.ItemForSale.Value;

        ImGui.Text("Inventory");
        ImGui.Indent();
        ImGui.Text($"Dark Matter Clusters: {darkMatterClusters:N0}");
        ImGui.Text($"Grade 6 Dark Matter: {item.OwnedItems:N0}");
        ImGui.Unindent();

        int missingItems = Math.Max(0, darkMatterClusters * 5 - (int)item.OwnedItems);

        ImGui.TextColored(
            missingItems == 0 ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed,
            $"Missing Grade 6 Dark Matter: {missingItems:N0}"
        );

        if (Shop.PurchaseState != null)
        {
            Shop.HandleNextPurchaseStep();

            if (Shop.PurchaseState != null)
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, "Cancel Auto-Buy"))
                    Shop.CancelAutoPurchase();
            }
        }
        else
        {
            int toPurchase = Math.Min(Shop.GetMaxItemsToPurchase(), missingItems);
            if (toPurchase > 0)
            {
                var cost = (long)item.Price * toPurchase;
                if (ImGuiComponents.IconButtonWithText(
                        FontAwesomeIcon.DollarSign,
                        $"Auto-Buy missing Dark Matter for {cost:N0}{SeIconChar.Gil.ToIconString()}"))
                {
                    Shop.StartAutoPurchase(toPurchase);
                    Shop.HandleNextPurchaseStep();
                }
            }
        }
        //DrawFollowControls();
    }

    public override void TriggerPurchase(AtkUnitBase* addonShop, int buyNow)
    {
        var item = Shop.ItemForSale;
        if (item == null) return;

        var buyItem = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 0 },
            new() { Type = ValueType.Int, Int = item.Value.Position },
            new() { Type = ValueType.Int, Int = buyNow },
            new() { Type = 0, Int = 0 }
        };

        addonShop->FireCallback(4, buyItem);
    }
}
