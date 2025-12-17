using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace VIWI.Modules.Workshoppa.External;

internal sealed class ExternalPluginHandler
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IPluginLog _pluginLog;
    private readonly PandoraIpc _pandoraIpc;

    private bool? _pandoraState;

    public ExternalPluginHandler(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
    {
        _pluginInterface = pluginInterface;
        _pluginLog = pluginLog;

        _pandoraIpc = new PandoraIpc(pluginInterface, pluginLog);
    }

    public bool Saved { get; private set; }

    public void Save()
    {
        if (Saved)
        {
            _pluginLog.Information("Not overwriting external plugin state");
            return;
        }

        _pluginLog.Information("Saving external plugin state...");
        SaveYesAlreadyState();
        SavePandoraState();
        Saved = true;
    }

    private void SaveYesAlreadyState()
    {
        if (_pluginInterface.TryGetData<HashSet<string>>("YesAlready.StopRequests", out var data) &&
            !data.Contains(nameof(Workshoppa)))
        {
            _pluginLog.Debug("Disabling YesAlready");
            data.Add(nameof(Workshoppa));
        }
    }

    private void SavePandoraState()
    {
        _pandoraState = _pandoraIpc.DisableIfNecessary();
        _pluginLog.Information($"Previous pandora feature state: {_pandoraState}");
    }

    /// <summary>
    /// Unlike Pandora/YesAlready, we only disable TextAdvance during the item turn-in so that the cutscene skip
    /// still works (if enabled).
    /// </summary>
    public void SaveTextAdvance()
    {
        if (_pluginInterface.TryGetData<HashSet<string>>("TextAdvance.StopRequests", out var data) &&
            !data.Contains(nameof(Workshoppa)))
        {
            _pluginLog.Debug("Disabling textadvance");
            data.Add(nameof(Workshoppa));
        }
    }

    public void Restore()
    {
        if (Saved)
        {
            RestoreYesAlready();
            RestorePandora();
        }

        Saved = false;
        _pandoraState = null;
    }

    private void RestoreYesAlready()
    {
        if (_pluginInterface.TryGetData<HashSet<string>>("YesAlready.StopRequests", out var data) &&
            data.Contains(nameof(Workshoppa)))
        {
            _pluginLog.Debug("Restoring YesAlready");
            data.Remove(nameof(Workshoppa));
        }
    }

    private void RestorePandora()
    {
        _pluginLog.Information($"Restoring previous pandora state: {_pandoraState}");
        if (_pandoraState == true)
            _pandoraIpc.Enable();
    }

    public void RestoreTextAdvance()
    {
        if (_pluginInterface.TryGetData<HashSet<string>>("TextAdvance.StopRequests", out var data) &&
            data.Contains(nameof(Workshoppa)))
        {
            _pluginLog.Debug("Restoring textadvance");
            data.Remove(nameof(Workshoppa));
        }
    }
}
