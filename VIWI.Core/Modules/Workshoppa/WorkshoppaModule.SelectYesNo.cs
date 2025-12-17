using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using VIWI.Core;

namespace VIWI.Modules.Workshoppa;

internal sealed partial class WorkshoppaModule
{
    private unsafe void SelectYesNoPostSetup(AddonEvent type, AddonArgs args)
    {
        pluginLog.Verbose("SelectYesNo post-setup");

        AddonSelectYesno* addonSelectYesNo = (AddonSelectYesno*)args.Addon.Address;
        string text = MemoryHelper.ReadSeString(&addonSelectYesNo->PromptText->NodeText).ToString()
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal);
        pluginLog.Verbose($"YesNo prompt: '{text}'");

        if (workshoppaRepairKitWindow.IsOpen)
        {
            pluginLog.Verbose($"Checking for Repair Kit YesNo ({workshoppaRepairKitWindow.AutoBuyEnabled}, {workshoppaRepairKitWindow.IsAwaitingYesNo})");
            if (workshoppaRepairKitWindow.AutoBuyEnabled && workshoppaRepairKitWindow.IsAwaitingYesNo && gameStrings.PurchaseItemForGil.IsMatch(text))
            {
                pluginLog.Information($"Selecting 'yes' ({text})");
                workshoppaRepairKitWindow.IsAwaitingYesNo = false;
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
            }
            else
            {
                pluginLog.Verbose("Not a purchase confirmation match");
            }
        }
        else if (workshoppaCeruleumTankWindow.IsOpen)
        {
            pluginLog.Verbose($"Checking for Ceruleum Tank YesNo ({workshoppaCeruleumTankWindow.AutoBuyEnabled}, {workshoppaCeruleumTankWindow.IsAwaitingYesNo})");
            if (workshoppaCeruleumTankWindow.AutoBuyEnabled && workshoppaCeruleumTankWindow.IsAwaitingYesNo && gameStrings.PurchaseItemForCompanyCredits.IsMatch(text))
            {
                pluginLog.Information($"Selecting 'yes' ({text})");
                workshoppaCeruleumTankWindow.IsAwaitingYesNo = false;
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
            }
            else
            {
                pluginLog.Verbose("Not a purchase confirmation match");
            }
        }
        else if (CurrentStage != Stage.Stopped)
        {
            if (CurrentStage == Stage.ConfirmMaterialDelivery && gameStrings.TurnInHighQualityItem == text)
            {
                pluginLog.Information($"Selecting 'yes' ({text})");
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
            }
            else if (CurrentStage == Stage.ConfirmMaterialDelivery && gameStrings.ContributeItems.IsMatch(text))
            {
                pluginLog.Information($"Selecting 'yes' ({text})");
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);

                ConfirmMaterialDeliveryFollowUp();
            }
            else if (CurrentStage == Stage.ConfirmCollectProduct && gameStrings.RetrieveFinishedItem.IsMatch(text))
            {
                pluginLog.Information($"Selecting 'yes' ({text})");
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);

                ConfirmCollectProductFollowUp();
            }
        }
    }

    private void ConfirmCollectProductFollowUp()
    {
        Config.CurrentlyCraftedItem = null;
        VIWIContext.PluginInterface.SavePluginConfig(Config);

        CurrentStage = Stage.TakeItemFromQueue;
        continueAt = DateTime.Now.AddSeconds(0.5);
    }
}
