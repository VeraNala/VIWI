using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using VIWI.Core;
using VIWI.Helpers;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace VIWI.Modules.Workshoppa;

internal sealed partial class WorkshoppaModule
{
    private void InteractWithFabricationStation(IGameObject fabricationStation)
        => InteractWithTarget(fabricationStation);

    private void TakeItemFromQueue()
    {
        if (Config.CurrentlyCraftedItem == null)
        {
            while (Config.ItemQueue.Count > 0 && Config.CurrentlyCraftedItem == null)
            {
                var firstItem = Config.ItemQueue[0];
                if (firstItem.Quantity > 0)
                {
                    Config.CurrentlyCraftedItem = new WorkshoppaConfig.CurrentItem
                    {
                        WorkshopItemId = firstItem.WorkshopItemId,
                    };

                    if (firstItem.Quantity > 1)
                        firstItem.Quantity--;
                    else
                        Config.ItemQueue.RemoveAt(0);
                }
                else
                    Config.ItemQueue.RemoveAt(0);
            }

            VIWIContext.PluginInterface.SavePluginConfig(Config);
            if (Config.CurrentlyCraftedItem != null)
                CurrentStage = Stage.TargetFabricationStation;
            else
                CurrentStage = Stage.RequestStop;
        }
        else
            CurrentStage = Stage.TargetFabricationStation;
    }

    private void OpenCraftingLog()
    {
        if (SelectSelectString("craftlog", 0, s => s == gameStrings.ViewCraftingLog))
            CurrentStage = Stage.SelectCraftCategory;
    }

    private unsafe void SelectCraftCategory()
    {
        AtkUnitBase* addonCraftingLog = GetCompanyCraftingLogAddon();
        if (addonCraftingLog == null)
            return;

        var craft = GetCurrentCraft();
        pluginLog.Information($"Selecting category {craft.Category} and type {craft.Type}");
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
        addonCraftingLog->FireCallback(8, selectCategory);
        CurrentStage = Stage.SelectCraft;
        continueAt = DateTime.Now.AddSeconds(0.1);
    }

    private unsafe void SelectCraft()
    {
        AtkUnitBase* addonCraftingLog = GetCompanyCraftingLogAddon();
        if (addonCraftingLog == null)
            return;

        var craft = GetCurrentCraft();
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
            pluginLog.Error($"Could not find {craft.Name} in current list, is it unlocked?");
            CurrentStage = Stage.RequestStop;
            return;
        }

        pluginLog.Information($"Selecting craft {craft.WorkshopItemId}");
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
        addonCraftingLog->FireCallback(8, selectCraft);
        CurrentStage = Stage.ConfirmCraft;
        continueAt = DateTime.Now.AddSeconds(0.1);
    }

    private void ConfirmCraft()
    {
        if (SelectSelectYesno(0, s => s.StartsWith("Craft ", StringComparison.Ordinal)))
        {
            Config.CurrentlyCraftedItem!.StartedCrafting = true;
            VIWIContext.PluginInterface.SavePluginConfig(Config);

            CurrentStage = Stage.TargetFabricationStation;
        }
    }
}
