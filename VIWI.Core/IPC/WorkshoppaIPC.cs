using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using System;
using VIWI.Modules.Workshoppa;

namespace VIWI.IPC;

internal sealed class WorkshoppaIPC : IDisposable
{
    private const string Ipc_AddQueueItem = "VIWI.Workshoppa.AddQueueItem";
    private const string Ipc_ClearQueue = "VIWI.Workshoppa.ClearQueue";

    private ICallGateProvider<uint, int, bool>? _addQueueItem;
    private ICallGateProvider<bool>? _clearQueue;

    public void Register()
    {
        _addQueueItem = Svc.PluginInterface.GetIpcProvider<uint, int, bool>(Ipc_AddQueueItem);
        _clearQueue = Svc.PluginInterface.GetIpcProvider<bool>(Ipc_ClearQueue);

        _addQueueItem.RegisterFunc(AddQueueItem);
        _clearQueue.RegisterFunc(ClearQueue);
    }

    public void Dispose()
    {
        try { _addQueueItem?.UnregisterFunc(); } catch { }
        try { _clearQueue?.UnregisterFunc(); } catch { }
        _addQueueItem = null;
        _clearQueue = null;
    }

    private static bool AddQueueItem(uint workshopItemId, int quantity)
    {
        try
        {
            var mod = WorkshoppaModule.Instance;
            if (mod == null || !WorkshoppaModule.Enabled) return false;
            return mod.IpcAddQueueItem(workshopItemId, quantity);
        }
        catch { return false; }
    }

    private static bool ClearQueue()
    {
        try
        {
            var mod = WorkshoppaModule.Instance;
            if (mod == null || !WorkshoppaModule.Enabled) return false;
            return mod.IpcClearQueue();
        }
        catch { return false; }
    }
}
