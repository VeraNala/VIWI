using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Linq;
using VIWI.Helpers;
using static VIWI.Core.VIWIContext;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace VIWI.Modules.Workshoppa;

internal sealed partial class WorkshoppaModule
{
    private void InteractWithFabricationStation(IGameObject fabricationStation)
    {
        InteractWithTarget(fabricationStation);
        _continueAt = DateTime.Now.AddSeconds(0.5);
    }

    private void TakeItemFromQueue()
    {
        if (_configuration.CurrentlyCraftedItem == null)
        {
            while (_configuration.ItemQueue.Count > 0 && _configuration.CurrentlyCraftedItem == null)
            {
                var firstItem = _configuration.ItemQueue[0];
                if (firstItem.Quantity > 0)
                {
                    _configuration.CurrentlyCraftedItem = new WorkshoppaConfig.CurrentItem
                    {
                        WorkshopItemId = firstItem.WorkshopItemId,
                    };

                    if (firstItem.Quantity > 1)
                        firstItem.Quantity--;
                    else
                        _configuration.ItemQueue.Remove(firstItem);
                }
                else
                    _configuration.ItemQueue.Remove(firstItem);
            }

            SaveConfig();
            if (_configuration.CurrentlyCraftedItem != null)
                CurrentStage = Stage.TargetFabricationStation;
            else
                CurrentStage = Stage.RequestStop;
        }
        else
            CurrentStage = Stage.TargetFabricationStation;
    }

    private void OpenCraftingLog()
    {
        if (SelectSelectString("craftlog", 0, s => s == _gameStrings.ViewCraftingLog))
            CurrentStage = Stage.SelectCraftCategory;
    }

    private unsafe void SelectCraftCategory()
    {
        AtkUnitBase* addonCraftingLog = GetCompanyCraftingLogAddon();
        if (addonCraftingLog == null)
            return;

        if (!TryGetCurrentCraft(out var craft))
        {
            ChatGui.PrintError("[Workshoppa] Current craft was not found in WorkshopCache. Stopping.");
            PluginLog.Error("[Workshoppa] TryGetCurrentCraft failed in SelectCraftCategory.");
            CurrentStage = Stage.RequestStop;
            return;
        }
        PluginLog.Information($"Selecting category {craft.Category} and type {craft.Type}");
        var selectCategory = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 2 },
            new() { Type = 0, Int = 0 },
            new() { Type = ValueType.UInt, UInt = (uint)craft.Category },
            new() { Type = ValueType.UInt, UInt = craft.Type },
            new() { Type = ValueType.UInt, Int = 0 },
            new() { Type = ValueType.UInt, Int = 0 },
            new() { Type = ValueType.UInt, Int = 0 },
            new() { Type = 0, Int = 0 }
        };
        
        try
        {
            addonCraftingLog->FireCallback(8, selectCategory);
        }
        catch (Exception ex)
        {
            ChatGui.PrintError("[Workshoppa] That workshop category/type appears to be unavailable (likely not unlocked?). Stopping.");
            PluginLog.Error(ex, $"[Workshoppa] FireCallback failed for {craft.Name}, Category={craft.Category} Type={craft.Type}.");
            CurrentStage = Stage.RequestStop;
            _continueAt = DateTime.Now.AddSeconds(0.5);
            return;
        }

        CurrentStage = Stage.SelectCraft;
        _continueAt = DateTime.Now.AddSeconds(0.1);
    }

    private unsafe void SelectCraft()
    {
        AtkUnitBase* addonCraftingLog = GetCompanyCraftingLogAddon();
        if (addonCraftingLog == null)
            return;

        if (!TryGetCurrentCraft(out var craft))
        {
            ChatGui.PrintError("[Workshoppa] Current craft was not found in WorkshopCache. Stopping.");
            PluginLog.Error("[Workshoppa] TryGetCurrentCraft failed in SelectCraft.");
            CurrentStage = Stage.RequestStop;
            return;
        }
        var atkValues = addonCraftingLog->AtkValues;

        uint shownItemCount = atkValues[13].UInt;
        var visibleItems = Enumerable.Range(0, (int)shownItemCount)
            .Select(i => new
            {
                WorkshopItemId = atkValues[14 + 4 * i].UInt,
                Name = atkValues[17 + 4 * i].ReadAtkString(),
            })
            .ToList();

        if (visibleItems.All(x => x.WorkshopItemId != craft.WorkshopItemId))
        {
            PluginLog.Error($"Could not find {craft.Name} in current list, is it unlocked?");
            CurrentStage = Stage.RequestStop;
            return;
        }

        PluginLog.Information($"Selecting craft {craft.WorkshopItemId}");
        var selectCraft = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 1 },
            new() { Type = 0, Int = 0 },
            new() { Type = 0, Int = 0 },
            new() { Type = 0, Int = 0 },
            new() { Type = ValueType.UInt, UInt = craft.WorkshopItemId },
            new() { Type = 0, Int = 0 },
            new() { Type = 0, Int = 0 },
            new() { Type = 0, Int = 0 }
        };
        try
        {
            addonCraftingLog->FireCallback(8, selectCraft);
        }
        catch (Exception ex)
        {
            ChatGui.PrintError("[Workshoppa] That workshop craft appears to be unavailable (likely not unlocked?). Stopping.");
            PluginLog.Error(ex, $"[Workshoppa] FireCallback failed for {craft.Name}, Category={craft.Category} Type={craft.Type}.");
            CurrentStage = Stage.RequestStop;
            _continueAt = DateTime.Now.AddSeconds(0.5);
            return;
        }
        CurrentStage = Stage.ConfirmCraft;
        _continueAt = DateTime.Now.AddSeconds(0.1);
    }

    private void ConfirmCraft()
    {
        if (SelectSelectYesno(0, s => s.StartsWith("Craft ", StringComparison.Ordinal)))
        {
            _configuration.CurrentlyCraftedItem!.StartedCrafting = true;
            SaveConfig();

            CurrentStage = Stage.TargetFabricationStation;
        }
    }
}
