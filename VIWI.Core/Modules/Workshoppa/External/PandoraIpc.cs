using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

namespace VIWI.Modules.Workshoppa.External;

internal sealed class PandoraIpc
{
    private const string AutoTurnInFeature = "Auto-select Turn-ins";

    private readonly IPluginLog _pluginLog;
    private readonly ICallGateSubscriber<string, bool?> _getEnabled;
    private readonly ICallGateSubscriber<string, bool, object?> _setEnabled;

    private bool _ipcUnavailable;

    public PandoraIpc(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
    {
        _pluginLog = pluginLog ?? throw new ArgumentNullException(nameof(pluginLog));

        if (pluginInterface == null) throw new ArgumentNullException(nameof(pluginInterface));

        _getEnabled = pluginInterface.GetIpcSubscriber<string, bool?>("PandorasBox.GetFeatureEnabled");
        _setEnabled = pluginInterface.GetIpcSubscriber<string, bool, object?>("PandorasBox.SetFeatureEnabled");
    }

    public bool? DisableIfNecessary()
    {
        if (_ipcUnavailable) return null;

        try
        {
            var enabled = _getEnabled.InvokeFunc(AutoTurnInFeature);
            _pluginLog.Debug($"[Workshoppa] Pandora '{AutoTurnInFeature}' enabled = {enabled?.ToString() ?? "null"}");

            if (enabled == true)
                _setEnabled.InvokeAction(AutoTurnInFeature, false);

            return enabled;
        }
        catch (IpcNotReadyError ex)
        {
            // Pandora installed but not ready yet.
            _pluginLog.Debug(ex, "[Workshoppa] Pandora IPC not ready.");
            return null;
        }
        catch (IpcError ex)
        {
            _pluginLog.Warning(ex, "[Workshoppa] Pandora IPC error; ignoring.");
            return null;
        }
        catch (Exception ex)
        {
            _pluginLog.Warning(ex, "[Workshoppa] Unexpected Pandora IPC exception; ignoring.");
            return null;
        }
    }

    public void Enable()
    {
        if (_ipcUnavailable) return;

        try
        {
            _setEnabled.InvokeAction(AutoTurnInFeature, true);
        }
        catch (IpcNotReadyError ex)
        {
            _pluginLog.Debug(ex, "[Workshoppa] Pandora IPC not ready while restoring.");
        }
        catch (IpcError ex)
        {
            _pluginLog.Warning(ex, "[Workshoppa] Pandora IPC error while restoring; ignoring.");
        }
        catch (Exception ex)
        {
            _pluginLog.Warning(ex, "[Workshoppa] Unexpected Pandora IPC exception while restoring; ignoring.");
        }
    }
}
