using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using static VIWI.Core.VIWIContext;

namespace VIWI.Modules.Workshoppa;

internal sealed partial class WorkshoppaModule
{
    private unsafe void SelectYesNoPostSetup(AddonEvent type, AddonArgs args)
    {
        PluginLog.Verbose("SelectYesNo post-setup");

        AddonSelectYesno* addonSelectYesNo = (AddonSelectYesno*)args.Addon.Address;
        string text = MemoryHelper.ReadSeString(&addonSelectYesNo->PromptText->NodeText).ToString()
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal);
        PluginLog.Verbose($"YesNo prompt: '{text}'");

        if (_repairKitWindow.IsOpen)
        {
            PluginLog.Verbose($"Checking for Repair Kit YesNo ({_repairKitWindow.AutoBuyEnabled}, {_repairKitWindow.IsAwaitingYesNo})");
            if (_repairKitWindow.AutoBuyEnabled && _repairKitWindow.IsAwaitingYesNo && _gameStrings.PurchaseItemForGil.IsMatch(text))
            {
                PluginLog.Information($"Selecting 'yes' ({text})");
                _repairKitWindow.IsAwaitingYesNo = false;
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
            }
            else
            {
                PluginLog.Verbose("Not a purchase confirmation match");
            }
        }
        else if (_ceruleumTankWindow.IsOpen)
        {
            PluginLog.Verbose($"Checking for Ceruleum Tank YesNo ({_ceruleumTankWindow.AutoBuyEnabled}, {_ceruleumTankWindow.IsAwaitingYesNo})");
            if (_ceruleumTankWindow.AutoBuyEnabled && _ceruleumTankWindow.IsAwaitingYesNo && _gameStrings.PurchaseItemForCompanyCredits.IsMatch(text))
            {
                PluginLog.Information($"Selecting 'yes' ({text})");
                _ceruleumTankWindow.IsAwaitingYesNo = false;
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
            }
            else
            {
                PluginLog.Verbose("Not a purchase confirmation match");
            }
        }
        else if (_grindstoneShopWindow.IsOpen)
        {
            PluginLog.Verbose($"Checking for Mudstone YesNo ({_grindstoneShopWindow.AutoBuyEnabled}, {_grindstoneShopWindow.IsAwaitingYesNo})");
            if (_grindstoneShopWindow.AutoBuyEnabled && _grindstoneShopWindow.IsAwaitingYesNo && _gameStrings.PurchaseItemForCompanyCredits.IsMatch(text))
            {
                PluginLog.Information($"Selecting 'yes' ({text})");
                _grindstoneShopWindow.IsAwaitingYesNo = false;
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
            }
            else
            {
                PluginLog.Verbose("Not a purchase confirmation match");
            }
        }
        else if (CurrentStage != Stage.Stopped)
        {
            if (CurrentStage == Stage.ConfirmMaterialDelivery && _gameStrings.TurnInHighQualityItem == text)
            {
                PluginLog.Information($"Selecting 'yes' ({text})");
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
            }
            else if (CurrentStage == Stage.ConfirmMaterialDelivery && _gameStrings.ContributeItems.IsMatch(text))
            {
                PluginLog.Information($"Selecting 'yes' ({text})");
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);

                ConfirmMaterialDeliveryFollowUp();
            }
            else if (CurrentStage == Stage.ConfirmCollectProduct && _gameStrings.RetrieveFinishedItem.IsMatch(text))
            {
                PluginLog.Information($"Selecting 'yes' ({text})");
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);

                ConfirmCollectProductFollowUp();
            }
            else if (CurrentStage == Stage.MergeStacks && _gameStrings.WorkshopMenuExit == text)
            {
                PluginLog.Information($"Selecting ({text})");
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
                _continueAt = DateTime.Now.AddSeconds(1);
            }
            else if (CurrentStage == Stage.DiscontinueProject && _gameStrings.DiscontinueItem.IsMatch(text) && _configuration.Mode == WorkshoppaConfig.TurnInMode.Leveling)
            {
                PluginLog.Information($"Selecting 'yes' ({text})");
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
                ResetLevelingProject();
                _configuration.CurrentlyCraftedItem = null;
                CurrentStage = Stage.SelectCraftBranch;
                _continueAt = DateTime.Now.AddSeconds(1);
            }
        }
    }

    private void ConfirmCollectProductFollowUp()
    {
        _configuration.CurrentlyCraftedItem = null;
        SaveConfig();

        CurrentStage = Stage.TakeItemFromQueue;
        _continueAt = DateTime.Now.AddSeconds(0.5);
    }
}
