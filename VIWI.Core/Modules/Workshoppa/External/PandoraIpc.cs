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

    public PandoraIpc(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
    {
        _pluginLog = pluginLog;
        _getEnabled = pluginInterface.GetIpcSubscriber<string, bool?>("PandorasBox.GetFeatureEnabled");
        _setEnabled = pluginInterface.GetIpcSubscriber<string, bool, object?>("PandorasBox.SetFeatureEnabled");
    }

    public bool? DisableIfNecessary()
    {
        try
        {
            bool? enabled = _getEnabled.InvokeFunc(AutoTurnInFeature);
            _pluginLog.Information($"Pandora's {AutoTurnInFeature} is {enabled?.ToString() ?? "null"}");
            if (enabled == true)
                _setEnabled.InvokeAction(AutoTurnInFeature, false);

            return enabled;
        }
        catch (IpcNotReadyError e)
        {
            _pluginLog.Information(e, "Unable to read pandora state");
            return null;
        }
    }

    public void Enable()
    {
        try
        {
            _setEnabled.InvokeAction(AutoTurnInFeature, true);
        }
        catch (IpcNotReadyError e)
        {
            _pluginLog.Error(e, "Unable to restore pandora state");
        }
    }
}
