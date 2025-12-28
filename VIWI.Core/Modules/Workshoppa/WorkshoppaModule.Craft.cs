using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Linq;
using VIWI.Helpers;
using VIWI.Modules.Workshoppa.GameData;
using static VIWI.Core.VIWIContext;
using static VIWI.Modules.Workshoppa.WorkshoppaConfig;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace VIWI.Modules.Workshoppa;

internal sealed partial class WorkshoppaModule
{
    private uint? _contributingItemId;
    private const uint SpecificMaterialId = 5229; //Mudstones
    public int _turninCount = 0;

    /// <summary>
    /// Check if delivery window is open when we clicked resume.
    /// </summary>
    private unsafe bool CheckContinueWithDelivery()
    {
        if (_configuration.CurrentlyCraftedItem != null)
        {
            AtkUnitBase* addonMaterialDelivery = GetMaterialDeliveryAddon();
            if (addonMaterialDelivery == null)
                return false;

            PluginLog.Warning("Material delivery window is open, although unexpected... checking current craft");
            CraftState? craftState = ReadCraftState(addonMaterialDelivery);
            if (craftState == null || craftState.ResultItem == 0)
            {
                PluginLog.Error("Unable to read craft state");
                _continueAt = DateTime.Now.AddSeconds(1);
                return false;
            }

            var craft = _workshopCache.Crafts.SingleOrDefault(x => x.ResultItem == craftState.ResultItem);
            if (craft == null || craft.WorkshopItemId != _configuration.CurrentlyCraftedItem.WorkshopItemId)
            {
                PluginLog.Error("Unable to match currently crafted item with game state");
                _continueAt = DateTime.Now.AddSeconds(1);
                return false;
            }

            PluginLog.Information("Delivering materials for current active craft, switching to delivery");
            return true;
        }

        return false;
    }

    private void SelectCraftBranch()
    {
        if (!EzThrottler.Throttle("Workshoppa.SelectCraftBranch", 200))
            return;
        if (_turninCount >= 3 && _configuration.Mode == TurnInMode.Leveling && (SelectSelectString("Discontinue", 2, s => s.StartsWith("Discontinue project.", StringComparison.Ordinal))))
        {
            CurrentStage = Stage.DiscontinueProject;
            _continueAt = DateTime.Now.AddSeconds(1);
        }
        else if (SelectSelectString("contrib", 0, s => s.StartsWith("Contribute materials.", StringComparison.Ordinal)))
        {
            CurrentStage = Stage.ContributeMaterials;
            _continueAt = DateTime.Now.AddSeconds(1);
        }
        else if (SelectSelectString("advance", 0, s => s.StartsWith("Advance to the next phase of production.", StringComparison.Ordinal)))
        {
            PluginLog.Information("Phase is complete");

            _configuration.CurrentlyCraftedItem!.PhasesComplete++;
            _configuration.CurrentlyCraftedItem!.ContributedItemsInCurrentPhase = new();
            SaveConfig();

            CurrentStage = Stage.TargetFabricationStation;
            _continueAt = DateTime.Now.AddSeconds(3);
        }
        else if (SelectSelectString("complete", 0, s => s.StartsWith("Complete the construction of", StringComparison.Ordinal)))
        {
            PluginLog.Information("Item is almost complete, confirming last cutscene");
            CurrentStage = Stage.TargetFabricationStation;
            _continueAt = DateTime.Now.AddSeconds(3);
        }
        else if (SelectSelectString("collect", 0, s => s == "Collect finished product."))
        {
            PluginLog.Information("Item is complete");
            CurrentStage = Stage.ConfirmCollectProduct;
            _continueAt = DateTime.Now.AddSeconds(0.25);
        }
        else if (_configuration.Mode == TurnInMode.Leveling && (SelectSelectString("Nothing", 1, s => s == "Nothing.")))
        {
            PluginLog.Information("No Project Available, Restarting,");
            CurrentStage = Stage.TakeItemFromQueue;
            _continueAt = DateTime.Now.AddSeconds(2);
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
            PluginLog.Warning("Could not parse craft state");
            _continueAt = DateTime.Now.AddSeconds(1);
            return;
        }

        if (_configuration.CurrentlyCraftedItem!.UpdateFromCraftState(craftState))
        {
            PluginLog.Information("Saving updated current craft information");
            SaveConfig();
        }

        for (int i = 0; i < craftState.Items.Count; ++i)
        {
            var item = craftState.Items[i];
            if (item.Finished)
                continue;

            if (!HasItemInSingleSlot(item.ItemId, item.ItemCountPerStep))
            {
                PluginLog.Error(
                    $"Can't contribute item {item.ItemId} to craft, couldn't find {item.ItemCountPerStep}x in a single inventory slot");

                InventoryManager* inventoryManager = InventoryManager.Instance();
                int itemCount = 0;
                if (inventoryManager != null)
                {
                    itemCount = inventoryManager->GetInventoryItemCount(item.ItemId, true, false, false) +
                                inventoryManager->GetInventoryItemCount(item.ItemId, false, false, false);
                }

                if (itemCount < item.ItemCountPerStep)
                    ChatGui.PrintError(
                        $"[Workshoppa] You don't have the needed {item.ItemCountPerStep}x {item.ItemName} to continue.");
                else
                    ChatGui.PrintError(
                        $"[Workshoppa] You don't have {item.ItemCountPerStep}x {item.ItemName} in a single stack, merge manually to continue.");

                CurrentStage = Stage.RequestStop;
                break;
            }

            _externalPluginHandler.SaveTextAdvance();

            PluginLog.Information($"Contributing {item.ItemCountPerStep}x {item.ItemName}");
            _contributingItemId = item.ItemId;
            var contributeMaterial = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 0 },
                new() { Type = ValueType.UInt, Int = i },
                new() { Type = ValueType.UInt, UInt = item.ItemCountPerStep },
                new() { Type = 0, Int = 0 }
            };
            addonMaterialDelivery->FireCallback(4, contributeMaterial);
            _fallbackAt = DateTime.Now.AddSeconds(0.2);
            CurrentStage = Stage.OpenRequestItemWindow;
            break;
        }
    }
    private unsafe void ContributeSpecificMaterial()
    {
        AtkUnitBase* addonMaterialDelivery = GetMaterialDeliveryAddon();
        if (addonMaterialDelivery == null)
            return;

        CraftState? craftState = ReadCraftState(addonMaterialDelivery);
        if (craftState == null || craftState.ResultItem == 0)
        {
            PluginLog.Warning("Could not parse craft state");
            _continueAt = DateTime.Now.AddSeconds(1);
            return;
        }

        if (_configuration.CurrentlyCraftedItem!.UpdateFromCraftState(craftState))
        {
            PluginLog.Information("Saving updated current craft information");
        }

        int targetIndex = -1;

        if (craftState.Items.Count > 4 && craftState.Items[4].ItemId == SpecificMaterialId && !craftState.Items[4].Finished)
        {
            targetIndex = 4;
        }
        else
        {
            for (int i = 0; i < craftState.Items.Count; i++)
            {
                var it = craftState.Items[i];
                if (!it.Finished && it.ItemId == SpecificMaterialId)
                {
                    targetIndex = i;
                    break;
                }
            }
        }

        if (targetIndex == -1)
        {
            ChatGui.PrintError($"[Workshoppa] Could not find material {SpecificMaterialId}, stopping Workshoppa.");
            CurrentStage = Stage.RequestStop;
            return;
        }

        var item = craftState.Items[targetIndex];
        if (!HasItemInSingleSlot(item.ItemId, item.ItemCountPerStep))
        {
            PluginLog.Error(
                $"Can't contribute item {item.ItemId} to craft, couldn't find {item.ItemCountPerStep}x in a single inventory slot");

            InventoryManager* inventoryManager = InventoryManager.Instance();
            int itemCount = 0;
            if (inventoryManager != null)
            {
                itemCount = inventoryManager->GetInventoryItemCount(item.ItemId, true, false, false) +
                            inventoryManager->GetInventoryItemCount(item.ItemId, false, false, false);
            }

            if (itemCount < item.ItemCountPerStep)
                ChatGui.PrintError(
                    $"[Workshoppa] You don't have the needed {item.ItemCountPerStep}x {item.ItemName} to continue.");
            else
                ChatGui.PrintError(
                    $"[Workshoppa] You don't have {item.ItemCountPerStep}x {item.ItemName} in a single stack; merge manually to continue.");

            CurrentStage = Stage.RequestStop;
            return;
        }

        _externalPluginHandler.SaveTextAdvance();

        PluginLog.Information($"Contributing {item.ItemCountPerStep}x {item.ItemName} (itemId={item.ItemId})");
        _contributingItemId = item.ItemId;

        var contributeMaterial = stackalloc AtkValue[]
        {
        new() { Type = ValueType.Int,  Int = 0 },
        new() { Type = ValueType.UInt, Int = targetIndex },                // the row index in the craft list
        new() { Type = ValueType.UInt, UInt = item.ItemCountPerStep },     // quantity to contribute
        new() { Type = 0, Int = 0 }
        };
        addonMaterialDelivery->FireCallback(4, contributeMaterial);
        _fallbackAt = DateTime.Now.AddSeconds(0.2);
        CurrentStage = Stage.OpenRequestItemWindow;
    }
    private unsafe void CloseMaterialDelivery()
    {
        if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "SubmarinePartsMenu", out var addonMaterialDelivery) &&
            AddonState.IsAddonReady(addonMaterialDelivery))
        {
            PluginLog.Debug("Closing MaterialDelivery addon");
            addonMaterialDelivery->FireCallbackInt(-1);
            CurrentStage = Stage.TargetFabricationStation;
            _continueAt = DateTime.Now.AddSeconds(1);
        }
    }

    private unsafe void RequestPostSetup(AddonEvent type, AddonArgs addon)
    {
        var addonRequest = (AddonRequest*)addon.Addon.Address;
        PluginLog.Verbose($"{nameof(RequestPostSetup)}: {CurrentStage}, {addonRequest->EntryCount}");
        if (CurrentStage != Stage.OpenRequestItemWindow)
            return;

        if (addonRequest->EntryCount != 1)
            return;

        _fallbackAt = DateTime.MaxValue;
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
        ((AddonContextIconMenu*)addon.Addon.Address)->AtkUnitBase.FireCallback(5, selectSlot);
    }

    private unsafe void RequestPostRefresh(AddonEvent type, AddonArgs addon)
    {
        PluginLog.Verbose($"{nameof(RequestPostRefresh)}: {CurrentStage}");
        if (CurrentStage != Stage.ConfirmRequestItemWindow)
            return;

        var addonRequest = (AddonRequest*)addon.Addon.Address;
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
        _externalPluginHandler.RestoreTextAdvance();
    }
    private void EnqueueLevelingProject(uint workshopItemId, int quantity)
    {
        _configuration.ItemQueue.Add(new WorkshoppaConfig.QueuedItem
        {
            WorkshopItemId = workshopItemId,
            Quantity = quantity,
        });

        if (_configuration.CurrentlyCraftedItem == null)
            CurrentStage = Stage.TakeItemFromQueue;
    }
    private unsafe void ConfirmMaterialDeliveryFollowUp()
    {
        AtkUnitBase* addonMaterialDelivery = GetMaterialDeliveryAddon();
        if (addonMaterialDelivery == null)
            return;

        CraftState? craftState = ReadCraftState(addonMaterialDelivery);
        if (craftState == null || craftState.ResultItem == 0)
        {
            PluginLog.Warning("Could not parse craft state");
            _continueAt = DateTime.Now.AddSeconds(1);
            return;
        }

        var item = craftState.Items.SingleOrDefault(x => x.ItemId == _contributingItemId);
        item.StepsComplete++;
        if (item.ItemId == 0)
        {
            PluginLog.Warning($"Contributing item {_contributingItemId} not found in CraftState.");
            CurrentStage = Stage.RequestStop;
            return;
        }

        if (_configuration.Mode == WorkshoppaConfig.TurnInMode.Leveling)
        {
            _turninCount++;
            PluginLog.Information($"Turn-in landed. Count={_turninCount}/3");

            if (_turninCount >= 3)
            {
                CurrentStage = Stage.CloseDeliveryMenu;
                _continueAt = DateTime.Now.AddSeconds(2);
                return;
            }
        }
        SaveConfig();
        CurrentStage = Stage.ContributeMaterials;
        _continueAt = DateTime.Now.AddSeconds(0.2);
    }
}
