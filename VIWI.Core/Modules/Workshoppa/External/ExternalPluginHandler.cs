using System;
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
        _pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        _pluginLog = pluginLog ?? throw new ArgumentNullException(nameof(pluginLog));

        _pandoraIpc = new PandoraIpc(_pluginInterface, _pluginLog);
    }

    public bool Saved { get; private set; }

    public void Save()
    {
        if (Saved)
        {
            _pluginLog.Information("[Workshoppa] Not overwriting external plugin state");
            return;
        }

        try
        {
            _pluginLog.Information("[Workshoppa] Saving external plugin state...");
        }
        catch
        {
            // In the extremely unlikely case logging itself throws, don't crash.
        }

        Safe("YesAlready", SaveYesAlreadyState);
        Safe("Pandora", SavePandoraState);

        Saved = true;
    }

    private void SaveYesAlreadyState()
    {
        if (!_pluginInterface.TryGetData<HashSet<string>>("YesAlready.StopRequests", out var data) || data == null)
            return;

        // Use your module name key consistently; avoid nameof(Workshoppa) if that isn't a type in this assembly.
        const string key = "Workshoppa";

        if (!data.Contains(key))
        {
            _pluginLog.Debug("[Workshoppa] Disabling YesAlready");
            data.Add(key);
        }
    }

    private void SavePandoraState()
    {
        _pandoraState = _pandoraIpc.DisableIfNecessary();
        _pluginLog.Information($"[Workshoppa] Previous Pandora feature state: {_pandoraState}");
    }

    /// <summary>
    /// Unlike Pandora/YesAlready, we only disable TextAdvance during the item turn-in so that the cutscene skip
    /// still works (if enabled).
    /// </summary>
    public void SaveTextAdvance()
    {
        Safe("TextAdvance", () =>
        {
            if (!_pluginInterface.TryGetData<HashSet<string>>("TextAdvance.StopRequests", out var data) || data == null)
                return;

            const string key = "Workshoppa";

            if (!data.Contains(key))
            {
                _pluginLog.Debug("[Workshoppa] Disabling TextAdvance");
                data.Add(key);
            }
        });
    }
    public void Restore()
    {
        if (Saved)
        {
            Safe("YesAlready", RestoreYesAlready);
            Safe("Pandora", RestorePandora);
        }

        Saved = false;
        _pandoraState = null;
    }

    private void RestoreYesAlready()
    {
        if (!_pluginInterface.TryGetData<HashSet<string>>("YesAlready.StopRequests", out var data) || data == null)
            return;

        const string key = "Workshoppa";

        if (data.Contains(key))
        {
            _pluginLog.Debug("[Workshoppa] Restoring YesAlready");
            data.Remove(key);
        }
    }

    private void RestorePandora()
    {
        _pluginLog.Information($"[Workshoppa] Restoring previous Pandora state: {_pandoraState}");
        if (_pandoraState == true)
            _pandoraIpc.Enable();
    }

    public void RestoreTextAdvance()
    {
        Safe("TextAdvance", () =>
        {
            if (!_pluginInterface.TryGetData<HashSet<string>>("TextAdvance.StopRequests", out var data) || data == null)
                return;

            const string key = "Workshoppa";

            if (data.Contains(key))
            {
                _pluginLog.Debug("[Workshoppa] Restoring TextAdvance");
                data.Remove(key);
            }
        });
    }

    private void Safe(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _pluginLog.Warning(ex, $"[Workshoppa] External integration '{name}' failed; ignoring.");
        }
    }
}