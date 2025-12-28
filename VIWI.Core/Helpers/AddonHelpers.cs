using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Linq;

namespace VIWI.Helpers;

internal static unsafe class AddonHelpers
{
    public static bool TryGetAddonByName<T>(IGameGui gameGui, string addonName, out T* addonPtr)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(gameGui);
        ArgumentException.ThrowIfNullOrEmpty(addonName);

        var ptr = gameGui.GetAddonByName(addonName);
        if (!ptr.IsNull)
        {
            addonPtr = (T*)ptr.Address;
            return true;
        }
        else
        {
            addonPtr = null;
            return false;
        }
    }
}
internal static unsafe class AddonState
{
    private const int UnitListCount = 18;
    public static unsafe AtkUnitBase* GetAddonById(uint id)
    {
        var unitManagers = &AtkStage.Instance()->RaptureAtkUnitManager->AtkUnitManager.DepthLayerOneList;
        for (var i = 0; i < UnitListCount; i++)
        {
            var unitManager = &unitManagers[i];
            foreach (var j in Enumerable.Range(0, Math.Min(unitManager->Count, unitManager->Entries.Length)))
            {
                var unitBase = unitManager->Entries[j].Value;
                if (unitBase != null && unitBase->Id == id)
                {
                    return unitBase;
                }
            }
        }

        return null;
    }
    public static unsafe bool IsAddonReady(AtkUnitBase* addon)
    {
        if (addon == null) return false;

        if (!addon->IsVisible) return false;

        if (addon->UldManager.LoadedState != AtkLoadState.Loaded) return false;

        if (addon->UldManager.NodeList == null) return false;
        if (addon->UldManager.NodeListCount == 0) return false;

        return true;
    }
}