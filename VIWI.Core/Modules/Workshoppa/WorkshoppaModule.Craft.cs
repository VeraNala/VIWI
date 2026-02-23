using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
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

    private static readonly (uint itemId, int index)[] LevelingTargets =
    {
        (5367, 0), // Elm Lumber  -> CRP
        (5229, 4), // Mudstone    -> MIN
        (5395, 5), // Spruce Log  -> BTN
    };

    private const int MaxTurninsPerProject = 3;

    private sealed class TurninTracker
    {
        public int Remaining = MaxTurninsPerProject;
        public bool Exhausted = false;
    }

    private readonly Dictionary<uint, TurninTracker> _turnins = new()
    {
        [5367] = new TurninTracker(),
        [5229] = new TurninTracker(),
        [5395] = new TurninTracker(),
    };

    private bool IsLevelingTargetEnabled(uint itemId) => itemId switch
    {
        5367 => _configuration.CrpTargetActive,
        5229 => _configuration.MinTargetActive,
        5395 => _configuration.BtnTargetActive,
        _ => false
    };
    public bool AnyLevelingTargetsEnabled() => _configuration.CrpTargetActive || _configuration.MinTargetActive || _configuration.BtnTargetActive;
    public bool AllLevelingTargetsDisabled() => !_configuration.CrpTargetActive && !_configuration.MinTargetActive && !_configuration.BtnTargetActive;
    public bool AllLevelingMaterialsExhausted()
    {
        if (!AnyLevelingTargetsEnabled())
            return false;

        if (_configuration.CrpTargetActive && !_turnins[5367].Exhausted) return false; // Elm Lumber
        if (_configuration.MinTargetActive && !_turnins[5229].Exhausted) return false; // Mudstone
        if (_configuration.BtnTargetActive && !_turnins[5395].Exhausted) return false; // Spruce Log

        return true;
    }
    private void ResetLevelingProject()
    {
        foreach (var (itemId, _) in LevelingTargets)
        {
            if (_turnins.TryGetValue(itemId, out var st))
                st.Remaining = MaxTurninsPerProject;
        }

        _mergePending = false;
        _mergeAttempts = 0;
        _mergeItemId = 0;
        _mergeRequired = 0;
        _mergeItemName = string.Empty;
        _contributingItemId = null;
    }

    private void ResetLevelingRuntimeState()
    {
        ResetLevelingProject();

        foreach (var (itemId, _) in LevelingTargets)
        {
            if (_turnins.TryGetValue(itemId, out var st))
                st.Exhausted = false;
        }

        _stallTicks = 0;
        _lastProgressAt = DateTime.Now;
        PluginLog.Information("[Workshoppa] Leveling runtime state FULL reset.");
    }
    private void ClampLevelingTargetsByCurrentLevel(IPlayerState ps)
    {
        var crp = GetJobByAbbrev(DataManager, "CRP");
        var min = GetJobByAbbrev(DataManager, "MIN");
        var btn = GetJobByAbbrev(DataManager, "BTN");

        void Clamp(ref bool active, int current, int target, string label)
        {
            if (!active) return;
            if (current <= 0) return;
            if (current < target) return;

            active = false;
            SaveConfig();
            PluginLog.Information($"[Workshoppa] Auto-disabled {label}: current {current} >= target {target}.");
        }

        if (crp != null) Clamp(ref _configuration.CrpTargetActive, ps.GetClassJobLevel(crp.Value), _configuration.CrpTargetLevel, "CRP");
        if (min != null) Clamp(ref _configuration.MinTargetActive, ps.GetClassJobLevel(min.Value), _configuration.MinTargetLevel, "MIN");
        if (btn != null) Clamp(ref _configuration.BtnTargetActive, ps.GetClassJobLevel(btn.Value), _configuration.BtnTargetLevel, "BTN");
    }

    // ------------------------------
    // SELECTSTRING BRANCH
    // ------------------------------

    private void SelectCraftBranch()
    {
        if (_configuration.Mode == TurnInMode.Leveling
            && AnyLevelingTargetsEnabled()
            && ShouldDiscontinueLevelingProject()
            && SelectSelectString("Discontinue", 2, s => s.StartsWith("Discontinue project.", StringComparison.Ordinal)))
        {
            CurrentStage = Stage.DiscontinueProject;
            _continueAt = DateTime.Now.AddSeconds(1);
        }
        else if (_mergePending && SelectSelectString("Nothing", 3, s => s == "Nothing."))
        {
            PluginLog.Information("Merge Requested, Exiting menu.");
            CurrentStage = Stage.MergeStacks;
            _continueAt = DateTime.Now.AddSeconds(0.5);
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
        else if (_configuration.Mode == TurnInMode.Leveling && SelectSelectString("Nothing", 1, s => s == "Nothing."))
        {
            PluginLog.Information("No Project Available, Restarting,");
            CurrentStage = Stage.TakeItemFromQueue;
            _continueAt = DateTime.Now.AddSeconds(0.5);
        }
    }
    #region Leveling - Contribution
    // ------------------------------
    // CONTRIBUTION (LEVELING)
    // ------------------------------

    private unsafe void ContributeSpecificMaterial()
    {
        ClampLevelingTargetsByCurrentLevel(PlayerState);

        if (_configuration.Mode == TurnInMode.Leveling && AllLevelingTargetsDisabled())
        {
            ChatGui.Print("[Workshoppa] All leveling targets are complete/disabled. Stopping.");
            CurrentStage = Stage.RequestStop;
            _continueAt = DateTime.Now.AddSeconds(0.2);
            return;
        }

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

        int targetIndex = -1;
        uint targetItemId = 0;

        foreach (var (itemId, idx) in LevelingTargets)
        {
            if (!IsLevelingTargetEnabled(itemId))
                continue;

            var st = _turnins[itemId];
            if (st.Exhausted || st.Remaining <= 0)
                continue;

            if (idx < 0 || idx >= craftState.Items.Count)
                continue;

            var it = craftState.Items[idx];
            if (it.Finished) continue;
            if (it.ItemId != itemId) continue;

            targetIndex = idx;
            targetItemId = itemId;
            break;
        }

        if (targetIndex == -1)
        {
            PluginLog.Information("[Workshoppa] No eligible leveling item found in craft list; will proceed via discontinue/restart logic.");
            if (ShouldDiscontinueLevelingProject())
            CurrentStage = Stage.CloseDeliveryMenu;
            _continueAt = DateTime.Now.AddSeconds(2);
            return;
        }

        var item = craftState.Items[targetIndex];

        if (!HasItemInSingleSlot(item.ItemId, item.ItemCountPerStep))
        {
            PluginLog.Error($"Can't contribute item {item.ItemId} to craft, couldn't find {item.ItemCountPerStep}x in a single inventory slot");

            InventoryManager* inventoryManager = InventoryManager.Instance();
            int itemCount = 0;
            if (inventoryManager != null)
            {
                itemCount =
                    inventoryManager->GetInventoryItemCount(item.ItemId, true, false, false) +
                    inventoryManager->GetInventoryItemCount(item.ItemId, false, false, false);
            }

            if (itemCount < item.ItemCountPerStep)
            {
                ChatGui.PrintError($"[Workshoppa] Out of {item.ItemName} for this project; will continue with other enabled items.");
                _turnins[item.ItemId].Exhausted = true;
                _continueAt = DateTime.Now.AddSeconds(0.2);
                return;
            }
            if (_mergeItemId != item.ItemId)
                _mergeAttempts = 0;

            if (_mergeAttempts >= MaxMergeAttempts)
            {
                ChatGui.PrintError($"[Workshoppa] Couldn't auto-merge {item.ItemName} after {_mergeAttempts} attempts. Merge manually to continue.");
                CurrentStage = Stage.RequestStop;
                return;
            }

            _mergePending = true;
            _mergeItemId = item.ItemId;
            _mergeRequired = (uint)item.ItemCountPerStep;
            _mergeItemName = item.ItemName ?? item.ItemId.ToString();

            CurrentStage = Stage.CloseDeliveryMenu;
            return;
        }

        _externalPluginHandler.SaveTextAdvance();

        PluginLog.Information($"Contributing Specific {item.ItemCountPerStep}x {item.ItemName} (itemId={item.ItemId})");
        _contributingItemId = item.ItemId;

        var contributeMaterial = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int,  Int = 0 },
            new() { Type = ValueType.UInt, Int = targetIndex },
            new() { Type = ValueType.UInt, UInt = item.ItemCountPerStep },
            new() { Type = 0, Int = 0 }
        };

        addonMaterialDelivery->FireCallback(4, contributeMaterial);
        _fallbackAt = DateTime.Now.AddSeconds(0.2);
        CurrentStage = Stage.OpenRequestItemWindow;
    }
    #endregion
    #region Project - Contribution
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
                {
                    ChatGui.PrintError(
                        $"[Workshoppa] You don't have the needed {item.ItemCountPerStep}x {item.ItemName} to continue.");
                    CurrentStage = Stage.RequestStop;
                    break;
                }

                if (_mergeItemId != item.ItemId)
                    _mergeAttempts = 0;

                if (_mergeAttempts >= MaxMergeAttempts)
                {
                    ChatGui.PrintError($"[Workshoppa] Couldn't auto-merge {item.ItemName} after {_mergeAttempts} attempts. Merge manually to continue.");
                    CurrentStage = Stage.RequestStop;
                    break;
                }

                _mergePending = true;
                _mergeItemId = item.ItemId;
                _mergeRequired = (uint)item.ItemCountPerStep;
                _mergeItemName = item.ItemName ?? item.ItemId.ToString();
                CurrentStage = Stage.CloseDeliveryMenu;
                _mergeAttempts++;

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
    #endregion
    #region Contribution Confirm
    // ------------------------------
    // CONFIRM FOLLOW-UP (DECREMENT REMAINING)
    // ------------------------------

    private unsafe void ConfirmMaterialDeliveryFollowUp()
    {
        var addonMaterialDelivery = GetMaterialDeliveryAddon();
        if (addonMaterialDelivery == null)
            return;

        var craftState = ReadCraftState(addonMaterialDelivery);
        if (craftState == null || craftState.ResultItem == 0)
        {
            PluginLog.Warning("Could not parse craft state");
            _continueAt = DateTime.Now.AddSeconds(1);
            return;
        }

        var item = craftState.Items.SingleOrDefault(x => x.ItemId == _contributingItemId);
        if (item.ItemId == 0)
        {
            PluginLog.Warning($"Contributing item {_contributingItemId} not found in CraftState.");
            CurrentStage = Stage.RequestStop;
            return;
        }

        item.StepsComplete++;

        _configuration.CurrentlyCraftedItem!.ContributedItemsInCurrentPhase
            .Single(x => x.ItemId == item.ItemId)
            .QuantityComplete = item.QuantityComplete;

        if (_configuration.Mode == TurnInMode.Leveling && _contributingItemId != null)
        {
            ClampLevelingTargetsByCurrentLevel(PlayerState);
            if (AllLevelingTargetsDisabled())
            {
                ChatGui.Print("[Workshoppa] All leveling targets are complete/disabled. Stopping.");
                CurrentStage = Stage.RequestStop;
                _continueAt = DateTime.Now.AddSeconds(0.2);
                return;
            }

            var id = _contributingItemId.Value;
            if (_turnins.TryGetValue(id, out var st) && st.Remaining > 0)
            {
                st.Remaining--;
                PluginLog.Information($"Turn-in landed: itemId={id}. Remaining this project={st.Remaining}/{MaxTurninsPerProject}");
            }

            if (ShouldDiscontinueLevelingProject())
            {
                SaveConfig();
                CurrentStage = Stage.CloseDeliveryMenu;
                _continueAt = DateTime.Now.AddSeconds(2);
                return;
            }
        }

        if (craftState.IsPhaseComplete())
        {
            SaveConfig();
            CurrentStage = Stage.TargetFabricationStation;
            _continueAt = DateTime.Now.AddSeconds(0.5);
            return;
        }

        SaveConfig();
        CurrentStage = Stage.ContributeMaterials;
        _continueAt = DateTime.Now.AddSeconds(2);
    }
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

    #endregion
    #region Leveling - Exit
    // ------------------------------
    // DISCONTINUE LEVELING
    // ------------------------------

    private bool ShouldDiscontinueLevelingProject()
    {
        if (!AnyLevelingTargetsEnabled())
            return false;

        if (AllLevelingTargetsDisabled())
        {
            ChatGui.Print("[Workshoppa] All leveling targets disabled. Stopping.");
            CurrentStage = Stage.RequestStop;
            return true;
        }

        if (AllLevelingMaterialsExhausted())
        {
            ChatGui.PrintError("[Workshoppa] Out of materials for all enabled leveling targets. Stopping.");
            CurrentStage = Stage.RequestStop;
            return true;
        }

        foreach (var (itemId, _) in LevelingTargets)
        {
            if (!IsLevelingTargetEnabled(itemId))
                continue;

            var st = _turnins[itemId];
            if (st.Exhausted) continue;
            if (st.Remaining <= 0) continue;

            return false;
        }

        return true;
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
    #endregion

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
    
    private static ClassJob? GetJobByAbbrev(IDataManager dm, string abbrev)
    {
        var sheet = dm.GetExcelSheet<ClassJob>();
        if (sheet == null) return null;

        foreach (var row in sheet)
        {
            if (row.RowId == 0) continue;
            if (row.Abbreviation.ToString() == abbrev)
                return row;
        }

        return null;
    }
    private void MarkProgress()
    {
        _stallTicks = 0;
        _lastProgressAt = DateTime.Now;
    }
}
