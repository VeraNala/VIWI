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
        if (!initialized || !isEnabled) return;
        if (pluginLog == null) return;
        if (args.Addon.Address == nint.Zero) return;

        var addonSelectYesNo = (AddonSelectYesno*)args.Addon.Address;
        if (addonSelectYesNo == null) return;
        if (addonSelectYesNo->PromptText == null) return;

        var gs = gameStrings;
        if (gs == null) return;

        string text;
        try
        {
            text = MemoryHelper.ReadSeString(&addonSelectYesNo->PromptText->NodeText).ToString()
                .Replace("\n", "", StringComparison.Ordinal)
                .Replace("\r", "", StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            pluginLog.Verbose(ex, "[Workshoppa] SelectYesNo: failed to read prompt text");
            return;
        }

        pluginLog.Verbose($"[Workshoppa] YesNo prompt: '{text}'");

        var rk = workshoppaRepairKitWindow;
        if (rk != null && rk.IsOpen)
        {
            pluginLog.Verbose($"[Workshoppa] RepairKit YesNo? (AutoBuy={rk.AutoBuyEnabled}, Await={rk.IsAwaitingYesNo})");

            if (rk.AutoBuyEnabled && rk.IsAwaitingYesNo && gs.PurchaseItemForGil.IsMatch(text))
            {
                pluginLog.Information($"[Workshoppa] Confirming gil purchase: {text}");
                rk.IsAwaitingYesNo = false;
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
            }

            return;
        }

        var ct = workshoppaCeruleumTankWindow;
        if (ct != null && ct.IsOpen)
        {
            pluginLog.Verbose($"[Workshoppa] Ceruleum YesNo? (AutoBuy={ct.AutoBuyEnabled}, Await={ct.IsAwaitingYesNo})");

            if (ct.AutoBuyEnabled && ct.IsAwaitingYesNo && gs.PurchaseItemForCompanyCredits.IsMatch(text))
            {
                pluginLog.Information($"[Workshoppa] Confirming CC purchase: {text}");
                ct.IsAwaitingYesNo = false;
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
            }

            return;
        }

        if (CurrentStage == Stage.Stopped)
            return;

        if (CurrentStage == Stage.ConfirmMaterialDelivery)
        {
            if (gs.TurnInHighQualityItem == text)
            {
                pluginLog.Information($"[Workshoppa] Confirming HQ turn-in: {text}");
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
                return;
            }

            if (gs.ContributeItems.IsMatch(text))
            {
                pluginLog.Information($"[Workshoppa] Confirming contribute: {text}");
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);

                try { ConfirmMaterialDeliveryFollowUp(); }
                catch (Exception ex) { pluginLog.Error(ex, "[Workshoppa] ConfirmMaterialDeliveryFollowUp failed"); }

                return;
            }

            return;
        }

        if (CurrentStage == Stage.ConfirmCollectProduct && gs.RetrieveFinishedItem.IsMatch(text))
        {
            pluginLog.Information($"[Workshoppa] Confirming collect: {text}");
            addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);

            try { ConfirmCollectProductFollowUp(); }
            catch (Exception ex) { pluginLog.Error(ex, "[Workshoppa] ConfirmCollectProductFollowUp failed"); }
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
