using System;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using VIWI.Core;
using VIWI.Modules.Workshoppa.GameData;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace VIWI.Modules.Workshoppa;

internal sealed partial class WorkshoppaModule
{
    private uint? contributingItemId;

    /// <summary>
    /// Check if delivery window is open when we clicked resume.
    /// </summary>
    private unsafe bool CheckContinueWithDelivery()
    {
        if (Config.CurrentlyCraftedItem != null)
        {
            AtkUnitBase* addonMaterialDelivery = GetMaterialDeliveryAddon();
            if (addonMaterialDelivery == null)
                return false;

            pluginLog.Warning("Material delivery window is open, although unexpected... checking current craft");
            CraftState? craftState = ReadCraftState(addonMaterialDelivery);
            if (craftState == null || craftState.ResultItem == 0)
            {
                pluginLog.Error("Unable to read craft state");
                continueAt = DateTime.Now.AddSeconds(1);
                return false;
            }

            var craft = workshopCache.Crafts.SingleOrDefault(x => x.ResultItem == craftState.ResultItem);
            if (craft == null || craft.WorkshopItemId != Config.CurrentlyCraftedItem.WorkshopItemId)
            {
                pluginLog.Error("Unable to match currently crafted item with game state");
                continueAt = DateTime.Now.AddSeconds(1);
                return false;
            }

            pluginLog.Information("Delivering materials for current active craft, switching to delivery");
            return true;
        }

        return false;
    }
    private WorkshopCraft GetCurrentCraft()
    {
        var current = Config.CurrentlyCraftedItem
            ?? throw new InvalidOperationException("No CurrentlyCraftedItem set.");

        return workshopCache.Crafts.Single(x => x.WorkshopItemId == current.WorkshopItemId);
    }

    private void SelectCraftBranch()
    {
        if (SelectSelectString("contrib", 0, s => s.StartsWith("Contribute materials.", StringComparison.Ordinal)))
        {
            CurrentStage = Stage.ContributeMaterials;
            continueAt = DateTime.Now.AddSeconds(1);
        }
        else if (SelectSelectString("advance", 0, s => s.StartsWith("Advance to the next phase of production.", StringComparison.Ordinal)))
        {
            pluginLog.Information("Phase is complete");

            Config.CurrentlyCraftedItem!.PhasesComplete++;
            Config.CurrentlyCraftedItem!.ContributedItemsInCurrentPhase = new();
            VIWIContext.PluginInterface.SavePluginConfig(Config);

            CurrentStage = Stage.TargetFabricationStation;
            continueAt = DateTime.Now.AddSeconds(3);
        }
        else if (SelectSelectString("complete", 0, s => s.StartsWith("Complete the construction of", StringComparison.Ordinal)))
        {
            pluginLog.Information("Item is almost complete, confirming last cutscene");
            CurrentStage = Stage.TargetFabricationStation;
            continueAt = DateTime.Now.AddSeconds(3);
        }
        else if (SelectSelectString("collect", 0, s => s == "Collect finished product."))
        {
            pluginLog.Information("Item is complete");
            CurrentStage = Stage.ConfirmCollectProduct;
            continueAt = DateTime.Now.AddSeconds(0.25);
        }
    }

    private unsafe void ContributeMaterials()
    {
        AtkUnitBase* addonMaterialDelivery = GetMaterialDeliveryAddon();
        if (addonMaterialDelivery == null)
            return;

        CraftState? craftState = ReadCraftState(addonMaterialDelivery);
        if (craftState == null || craftState.ResultItem == 0)
        {
            pluginLog.Warning("Could not parse craft state");
            continueAt = DateTime.Now.AddSeconds(1);
            return;
        }

        if (Config.CurrentlyCraftedItem!.UpdateFromCraftState(craftState))
        {
            pluginLog.Information("Saving updated current craft information");
            VIWIContext.PluginInterface.SavePluginConfig(Config);
        }

        for (int i = 0; i < craftState.Items.Count; ++i)
        {
            var item = craftState.Items[i];
            if (item.Finished)
                continue;

            if (!HasItemInSingleSlot(item.ItemId, item.ItemCountPerStep))
            {
                pluginLog.Error(
                    $"Can't contribute item {item.ItemId} to craft, couldn't find {item.ItemCountPerStep}x in a single inventory slot");

                InventoryManager* inventoryManager = InventoryManager.Instance();
                int itemCount = 0;
                if (inventoryManager != null)
                {
                    itemCount = inventoryManager->GetInventoryItemCount(item.ItemId, true, false, false) +
                                inventoryManager->GetInventoryItemCount(item.ItemId, false, false, false);
                }

                if (itemCount < item.ItemCountPerStep)
                    chatGui.PrintError(
                        $"[Workshoppa] You don't have the needed {item.ItemCountPerStep}x {item.ItemName} to continue.");
                else
                    chatGui.PrintError(
                        $"[Workshoppa] You don't have {item.ItemCountPerStep}x {item.ItemName} in a single stack, you need to merge the items in your inventory manually to continue.");

                CurrentStage = Stage.RequestStop;
                break;
            }

            externalPluginHandler.SaveTextAdvance();

            pluginLog.Information($"Contributing {item.ItemCountPerStep}x {item.ItemName}");
            contributingItemId = item.ItemId;
            var contributeMaterial = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 0 },
                //new() { Type = ValueType.UInt, Int = i }, //What is this??
                new() { Type = ValueType.UInt, UInt = (uint)i },
                new() { Type = ValueType.UInt, UInt = item.ItemCountPerStep },
                new() { Type = 0, Int = 0 }
            };
            addonMaterialDelivery->FireCallback(4, contributeMaterial);
            fallbackAt = DateTime.Now.AddSeconds(0.2);
            CurrentStage = Stage.OpenRequestItemWindow;
            break;
        }
    }

    private unsafe void RequestPostSetup(AddonEvent type, AddonArgs addon)
    {
        var addonRequest = (AddonRequest*)(nint)addon.Addon.Address;
        pluginLog.Verbose($"{nameof(RequestPostSetup)}: {CurrentStage}, {addonRequest->EntryCount}");
        if (CurrentStage != Stage.OpenRequestItemWindow)
            return;

        if (addonRequest->EntryCount != 1)
            return;

        fallbackAt = DateTime.MaxValue;
        CurrentStage = Stage.OpenRequestItemSelect;
        var contributeMaterial = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 2 },
            new() { Type = ValueType.UInt, Int = 0 },
            new() { Type = ValueType.UInt, UInt = 44 },
            new() { Type = ValueType.UInt, UInt = 0 }
        };
        addonRequest->AtkUnitBase.FireCallback(4, contributeMaterial);
    }

    private unsafe void ContextIconMenuPostReceiveEvent(AddonEvent type, AddonArgs addon)
    {
        if (CurrentStage != Stage.OpenRequestItemSelect)
            return;

        CurrentStage = Stage.ConfirmRequestItemWindow;
        var selectSlot = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 0 },
            new() { Type = ValueType.Int, Int = 0 /* slot */ },
            new() { Type = ValueType.UInt, UInt = 20802 /* probably the item's icon */ },
            new() { Type = ValueType.UInt, UInt = 0 },
            new() { Type = 0, Int = 0 },
        };
        ((AddonContextIconMenu*)(nint)addon.Addon.Address)->AtkUnitBase.FireCallback(5, selectSlot);
    }

    private unsafe void RequestPostRefresh(AddonEvent type, AddonArgs addon)
    {
        pluginLog.Verbose($"{nameof(RequestPostRefresh)}: {CurrentStage}");
        if (CurrentStage != Stage.ConfirmRequestItemWindow)
            return;

        var addonRequest = (AddonRequest*)(nint)addon.Addon.Address;
        if (addonRequest->EntryCount != 1)
            return;

        CurrentStage = Stage.ConfirmMaterialDelivery;
        var closeWindow = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 0 },
            new() { Type = ValueType.UInt, UInt = 0 },
            new() { Type = ValueType.UInt, UInt = 0 },
            new() { Type = ValueType.UInt, UInt = 0 }
        };
        addonRequest->AtkUnitBase.FireCallback(4, closeWindow);
        addonRequest->AtkUnitBase.Close(false);
        externalPluginHandler.RestoreTextAdvance();
    }

    private unsafe void ConfirmMaterialDeliveryFollowUp()
    {
        AtkUnitBase* addonMaterialDelivery = GetMaterialDeliveryAddon();
        if (addonMaterialDelivery == null)
            return;

        CraftState? craftState = ReadCraftState(addonMaterialDelivery);
        if (craftState == null || craftState.ResultItem == 0)
        {
            pluginLog.Warning("Could not parse craft state");
            continueAt = DateTime.Now.AddSeconds(1);
            return;
        }

        var item = craftState.Items.Single(x => x.ItemId == contributingItemId);
        item.StepsComplete++;
        if (craftState.IsPhaseComplete())
        {
            CurrentStage = Stage.TargetFabricationStation;
            continueAt = DateTime.Now.AddSeconds(0.5);
        }
        else
        {
            Config.CurrentlyCraftedItem!.ContributedItemsInCurrentPhase
                .Single(x => x.ItemId == item.ItemId)
                .QuantityComplete = item.QuantityComplete;
            VIWIContext.PluginInterface.SavePluginConfig(Config);

            CurrentStage = Stage.ContributeMaterials;
            continueAt = DateTime.Now.AddSeconds(1);
        }
    }
}
