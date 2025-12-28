using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using InteropGenerator.Runtime;
using VIWI.Helpers;
using VIWI.Modules.Workshoppa.GameData;
using static VIWI.Core.VIWIContext;

namespace VIWI.Modules.Workshoppa;

internal sealed partial class WorkshoppaModule
{
    private unsafe void InteractWithTarget(IGameObject obj)
    {
        PluginLog.Information($"Setting target to {obj}");
        /*
        if (_targetManager.Target == null || _targetManager.Target != obj)
        {
            _targetManager.Target = obj;
        }
*/
        TargetSystem.Instance()->InteractWithObject(
            (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)obj.Address, false);
    }

    private float GetDistanceToEventObject(IReadOnlyList<uint> npcIds, out IGameObject? o)
    {
        Vector3? localPlayerPosition = ClientState.LocalPlayer?.Position;
        if (localPlayerPosition != null)
        {
            foreach (var obj in ObjectTable)
            {
                if (obj.ObjectKind == ObjectKind.EventObj)
                {
                    if (npcIds.Contains(obj.DataId))
                    {
                        o = obj;
                        float distance = Vector3.Distance(localPlayerPosition.Value,
                            obj.Position + new Vector3(0, -2, 0));
                        if (distance > 0.01)
                            return distance;
                    }
                }
            }
        }

        o = null;
        return float.MaxValue;
    }

    private unsafe AtkUnitBase* GetCompanyCraftingLogAddon()
    {
        if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "CompanyCraftRecipeNoteBook", out var addon) &&
            AddonState.IsAddonReady(addon))
            return addon;

        return null;
    }

    /// <summary>
    /// This actually has different addons depending on the craft, e.g. SubmarinePartsMenu.
    /// </summary>
    /// <returns></returns>
    private unsafe AtkUnitBase* GetMaterialDeliveryAddon()
    {
        var agentInterface = AgentModule.Instance()->GetAgentByInternalId(AgentId.CompanyCraftMaterial);
        if (agentInterface != null && agentInterface->IsAgentActive())
        {
            var addonId = agentInterface->GetAddonId();
            if (addonId == 0)
                return null;

            AtkUnitBase* addon = AddonState.GetAddonById((ushort)addonId);
            if (AddonState.IsAddonReady(addon))
                return addon;
        }

        return null;
    }

    private unsafe bool SelectSelectString(string marker, int choice, Predicate<string> predicate)
    {
        if (!AddonHelpers.TryGetAddonByName<AddonSelectString>(GameGui, "SelectString", out var addonSelectString))
            return false;
        if (!AddonState.IsAddonReady(&addonSelectString->AtkUnitBase))
            return false;

        var popup = addonSelectString->PopupMenu;
        var menu = popup.PopupMenu;

        int entries = menu.EntryCount;
        if (entries <= 0)
            return false;

        if (choice < 0 || choice >= entries)
            return false;

        var entryNames = menu.EntryNames;
        if (entryNames == null)
            return false;

        CStringPointer textPointer = entryNames[choice];
        if (!textPointer.HasValue)
            return false;

        var text = MemoryHelper.ReadSeStringNullTerminated(new nint(textPointer)).ToString();
        if (string.IsNullOrEmpty(text))
            return false;

        PluginLog.Verbose($"SelectSelectString({marker}): choice {choice}/{entries} '{text}'");

        if (!predicate(text))
            return false;

        addonSelectString->AtkUnitBase.FireCallbackInt(choice);
        return true;
    }

    private unsafe bool SelectSelectYesno(int choice, Predicate<string> predicate)
    {
        if (AddonHelpers.TryGetAddonByName<AddonSelectYesno>(GameGui, "SelectYesno", out var addonSelectYesno) &&
            AddonState.IsAddonReady(&addonSelectYesno->AtkUnitBase))
        {
            var text = MemoryHelper.ReadSeString(&addonSelectYesno->PromptText->NodeText).ToString();
            text = text
                .Replace("\n", "", StringComparison.Ordinal)
                .Replace("\r", "", StringComparison.Ordinal);
            if (predicate(text))
            {
                PluginLog.Information($"Selecting choice {choice} for '{text}'");
                addonSelectYesno->AtkUnitBase.FireCallbackInt(choice);
                return true;
            }
            else
            {
                PluginLog.Verbose($"Text {text} does not match");
            }
        }

        return false;
    }

    private unsafe CraftState? ReadCraftState(AtkUnitBase* addonMaterialDelivery)
    {
        try
        {
            var atkValues = addonMaterialDelivery->AtkValues;
            if (addonMaterialDelivery->AtkValuesCount == 157 && atkValues != null)
            {
                uint resultItem = atkValues[0].UInt;
                uint stepsComplete = atkValues[6].UInt;
                uint stepsTotal = atkValues[7].UInt;
                uint listItemCount = atkValues[11].UInt;
                List<CraftItem> items = Enumerable.Range(0, (int)listItemCount)
                    .Select(i => new CraftItem
                    {
                        ItemId = atkValues[12 + i].UInt,
                        IconId = atkValues[24 + i].UInt,
                        ItemName = atkValues[36 + i].ReadAtkString(),
                        CrafterIconId = atkValues[48 + i].Int,
                        ItemCountPerStep = atkValues[60 + i].UInt,
                        ItemCountNQ = atkValues[72 + i].UInt,
                        ItemCountHQ = ParseAtkItemCountHq(atkValues[84 + i]),
                        Experience = atkValues[96 + i].UInt,
                        StepsComplete = atkValues[108 + i].UInt,
                        StepsTotal = atkValues[120 + i].UInt,
                        Finished = atkValues[132 + i].UInt > 0,
                        CrafterMinimumLevel = atkValues[144 + i].UInt,
                    })
                    .ToList();

                return new CraftState
                {
                    ResultItem = resultItem,
                    StepsComplete = stepsComplete,
                    StepsTotal = stepsTotal,
                    Items = items,
                };
            }
        }
        catch (Exception e)
        {
            PluginLog.Warning(e, "Could not parse CompanyCraftMaterial info");
        }

        return null;
    }

    private static uint ParseAtkItemCountHq(AtkValue atkValue)
    {
        // NQ / HQ string
        // I have no clue, but it doesn't seem like the available HQ item count is stored anywhere in the atkvalues??
        string? s = atkValue.ReadAtkString();
        if (s != null)
        {
            var parts = s.Replace("\ue03c", "", StringComparison.Ordinal).Split('/');
            if (parts.Length > 1)
            {
                return uint.Parse(
                    parts[1]
                        .Replace(",", "", StringComparison.Ordinal)
                        .Replace(".", "", StringComparison.Ordinal)
                        .Trim(),
                    CultureInfo.InvariantCulture);
            }
        }

        return 0;
    }

    private unsafe bool HasItemInSingleSlot(uint itemId, uint count)
    {
        var inventoryManger = InventoryManager.Instance();
        if (inventoryManger == null)
            return false;

        for (InventoryType t = InventoryType.Inventory1; t <= InventoryType.Inventory4; ++t)
        {
            var container = inventoryManger->GetInventoryContainer(t);
            for (int i = 0; i < container->Size; ++i)
            {
                var item = container->GetInventorySlot(i);
                if (item == null)
                    continue;

                if (item->ItemId == itemId && item->Quantity >= count)
                    return true;
            }
        }

        return false;
    }
}
