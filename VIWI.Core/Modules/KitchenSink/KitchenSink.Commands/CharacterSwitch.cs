using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VIWI.IPC;

namespace VIWI.Modules.KitchenSink.Commands;

internal sealed class CharacterSwitch : IDisposable
{
    private sealed record Target(string Name, string World)
    {
        public override string ToString() => $"{Name}@{World}";

        public string ToString(string? currentWorld)
            => string.Equals(currentWorld, World, StringComparison.OrdinalIgnoreCase) ? Name : ToString();
    }

    private const string DtrBarTitle = "Character Index";

    private readonly AutoRetainerIPC _autoRetainer;
    private readonly ICommandManager _commandManager;
    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;
    private readonly IChatGui _chatGui;
    private readonly INotificationManager _notificationManager;
    private readonly ICondition _condition;
    private readonly IPlayerState _playerState;
    private readonly IPluginLog _pluginLog;
    private readonly IFramework _framework;
    private readonly IDtrBarEntry _dtrBarEntry;
    private readonly HashSet<ulong> _dumpedCids = new();

    public CharacterSwitch(
        AutoRetainerIPC autoRetainer,
        ICommandManager commandManager,
        IClientState clientState,
        IObjectTable objectTable,
        IChatGui chatGui,
        INotificationManager notificationManager,
        IDtrBar dtrBar,
        ICondition condition,
        IPlayerState playerState,
        IPluginLog pluginLog,
        IFramework framework)
    {
        _autoRetainer = autoRetainer;
        _commandManager = commandManager;
        _clientState = clientState;
        _objectTable = objectTable;
        _chatGui = chatGui;
        _notificationManager = notificationManager;
        _condition = condition;
        _playerState = playerState;
        _pluginLog = pluginLog;
        _framework = framework;

        _commandManager.AddHandler("/k+", new CommandInfo(NextCharacter) { ShowInHelp = false });
        _commandManager.AddHandler("/k-", new CommandInfo(PreviousCharacter) { ShowInHelp = false });
        _commandManager.AddHandler("/ks", new CommandInfo(PickCharacter) { ShowInHelp = false });
        _dtrBarEntry = dtrBar.Get(DtrBarTitle, "Unknown Character Index");
        _dtrBarEntry.OnClick = OnDtrClick;
        if (_clientState.IsLoggedIn)
            _framework.RunOnTick(UpdateDtrBar, TimeSpan.FromSeconds(1));
    }
    private void RunNextTick(Action a)
    {
        _framework.RunOnTick(() =>
        {
            try { a(); }
            catch (Exception ex)
            {
                _pluginLog.Error(ex, "[KitchenSink] Scheduled CharacterSwitch action failed");
            }
        });
    }

    private void OnDtrClick(DtrInteractionEvent eventData)
    {
        RunNextTick(() =>
        {
            var homeId = _objectTable.LocalPlayer?.HomeWorld.RowId;
            var currentId = _objectTable.LocalPlayer?.CurrentWorld.RowId;
            if (homeId.HasValue && currentId.HasValue && homeId.Value != currentId.Value)
            {
                _commandManager.ProcessCommand("/li");
                return;
            }

            var dir = (int)eventData.ClickType != 1 ? 1 : -1;
            SwitchCharacter(FindCharacter(dir));
        });
    }

    private void NextCharacter(string command, string arguments)
        => RunNextTick(() => SwitchCharacter(FindCharacter(1)));

    private void PreviousCharacter(string command, string arguments)
        => RunNextTick(() => SwitchCharacter(FindCharacter(-1)));

    private void PickCharacter(string command, string arguments)
        => RunNextTick(() => PickCharacterImpl(arguments));

    private Target? FindCharacter(int direction, bool showError = true)
    {
        try
        {
            if (!_autoRetainer.Ready)
            {
                if (showError)
                    _chatGui.PrintError("[KitchenSink] AutoRetainer not ready.", null, null);
                return null;
            }

            var regChars = _autoRetainer.GetRegisteredCIDs();
            if (regChars == null || regChars.Count == 0)
            {
                if (showError)
                    _chatGui.PrintError("[KitchenSink] AutoRetainer returned 0 registered characters.", null, null);
                return null;
            }

            var startIdx = regChars.IndexOf(_playerState.ContentId);
            if (startIdx < 0)
            {
                if (showError)
                    _chatGui.PrintError("[KitchenSink] Current CID not found in AutoRetainer list.", null, null);
                return null;
            }

            var idx = startIdx;
            for (int step = 0; step < regChars.Count - 1; step++)
            {
                idx = (idx + direction + regChars.Count) % regChars.Count;

                if (idx == startIdx)
                    break;

                var info = _autoRetainer.GetOfflineCharacterInfo(regChars[idx]);
                if (info == null)
                    continue;

                if (info.CID == _playerState.ContentId)
                {
                    if (showError)
                        _chatGui.PrintError("[KitchenSink] No character to switch to found.", null, null);
                    return null;
                }
                if (info.ExcludeRetainer && info.ExcludeWorkshop)
                    continue;

                if (string.IsNullOrWhiteSpace(info.Name) || string.IsNullOrWhiteSpace(info.World))
                    continue;

                return new Target(info.Name.Trim(), info.World.Trim());
            }

            if (showError)
                _chatGui.PrintError("[KitchenSink] No character to switch to found.", null, null);

            return null;
        }
        catch (Exception ex)
        {
            _pluginLog.Error(ex, "[KitchenSink] FindCharacter failed");
            if (showError)
                _chatGui.PrintError("[KitchenSink] FindCharacter failed (see log).", null, null);
            return null;
        }
    }


    private void PickCharacterImpl(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            _chatGui.PrintError("[KitchenSink] Usage: /ks <world/name> [index]", null, null);
            return;
        }

        try
        {
            var cids = _autoRetainer.GetRegisteredCIDs();
            if (cids.Count == 0)
            {
                _chatGui.PrintError("[KitchenSink] AutoRetainer not ready or returned no registered characters.", null, null);
                return;
            }

            var args = arguments.Split(' ', 2);
            if (args.Length < 2 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var nth))
                nth = 1;

            var entries = new List<(string Name, string World)>(cids.Count);

            foreach (var cid in cids)
            {
                var info = _autoRetainer.GetOfflineCharacterInfo(cid);
                if (info == null)
                    continue;
                if (info.ExcludeRetainer && info.ExcludeWorkshop)
                    continue;

                if (!string.IsNullOrWhiteSpace(info.Name) && !string.IsNullOrWhiteSpace(info.World))
                    entries.Add((info.Name, info.World));
            }

            if (entries.Count == 0)
            {
                _chatGui.PrintError("[KitchenSink] AutoRetainer returned no usable characters.", null, null);
                return;
            }

            var pick =
                entries.Where(x => x.World.StartsWith(args[0], StringComparison.OrdinalIgnoreCase))
                       .Skip(Math.Max(0, nth - 1))
                       .Select(x => (Target?)new Target(x.Name, x.World))
                       .FirstOrDefault()
                ?? entries.Where(x => x.Name.Contains(arguments, StringComparison.OrdinalIgnoreCase))
                          .Select(x => (Target?)new Target(x.Name, x.World))
                          .FirstOrDefault();

            if (pick == null)
            {
                _chatGui.PrintError($"[KitchenSink] No character found for \"{arguments}\".", null, null);
                return;
            }

            SwitchCharacter(pick);
        }
        catch (IpcError)
        {
            _chatGui.PrintError("[KitchenSink] AutoRetainer IPC isn't available.", null, null);
        }
        catch (Exception ex)
        {
            _pluginLog.Error(ex, "[KitchenSink] PickCharacter failed");
            _chatGui.PrintError("[KitchenSink] PickCharacter failed (see logs).", null, null);
        }
    }

    private void SwitchCharacter(Target? target)
    {
        if (target == null)
            return;
        if (_condition[ConditionFlag.BoundByDuty] ||
            _condition[ConditionFlag.BetweenAreas] ||
            _condition[ConditionFlag.BetweenAreas51] ||
            _condition[ConditionFlag.InCombat] ||
            _condition[ConditionFlag.Casting] ||
            _condition[ConditionFlag.Occupied] ||
            _condition[ConditionFlag.Occupied30] ||
            _condition[ConditionFlag.Occupied33] ||
            _condition[ConditionFlag.Occupied38] ||
            _condition[ConditionFlag.Occupied39] ||
            _condition[ConditionFlag.OccupiedInQuestEvent] ||
            _condition[ConditionFlag.OccupiedInCutSceneEvent] ||
            _condition[ConditionFlag.WatchingCutscene] ||
            _condition[ConditionFlag.WatchingCutscene78] ||
            _condition[ConditionFlag.OccupiedSummoningBell])
        {
            _notificationManager.AddNotification(new Notification
            {
                Title = "KitchenSink",
                Content = "Can't switch characters (bound by duty or occupied)",
                Type = NotificationType.Error
            });
            return;
        }

        _notificationManager.AddNotification(new Notification
        {
            Title = "KitchenSink",
            Content = $"Switch to {target}.",
            Type = NotificationType.Info
        });

        _commandManager.ProcessCommand($"/ays relog {target}");
    }

    private void UpdateDtrBar()
    {
        if (((IReadOnlyDtrBarEntry)_dtrBarEntry).UserHidden)
            return;

        _framework.RunOnFrameworkThread(() =>
        {
            try
            {
                if (!_autoRetainer.Ready)
                {
                    _dtrBarEntry.Shown = false;
                    _dtrBarEntry.Tooltip = null;
                    return;
                }

                var regChars = _autoRetainer.GetRegisteredCIDs();
                if (regChars == null || regChars.Count == 0)
                {
                    _dtrBarEntry.Text = new SeStringBuilder().AddText("AR:0").Build();
                    _dtrBarEntry.Tooltip = new SeStringBuilder().AddText("AutoRetainer returned 0 registered characters.").Build();
                    _dtrBarEntry.Shown = true;
                    return;
                }

                var idx = regChars.IndexOf(_playerState.ContentId);
                if (idx < 0)
                {
                    _dtrBarEntry.Text = new SeStringBuilder().AddText("AR:?").Build();
                    _dtrBarEntry.Tooltip = new SeStringBuilder().AddText("Current CID was not found in AutoRetainer's list.").Build();
                    _dtrBarEntry.Shown = true;
                    return;
                }

                _dtrBarEntry.Text = new SeStringBuilder()
                    .AddText($"#{idx + 1}/{regChars.Count}")
                    .Build();

                var info = _autoRetainer.GetOfflineCharacterInfo(regChars[idx]);
                if (info != null)
                {
                    _dtrBarEntry.Tooltip = new SeStringBuilder()
                        .AddText($"{info.Name}@{info.World}")
                        .Build();
                }
                else
                {
                    _dtrBarEntry.Tooltip = new SeStringBuilder()
                        .AddText("Could not read OfflineCharacterInfo.\nSee /xllog for dump.")
                        .Build();
                }

                _dtrBarEntry.Shown = true;
            }
            catch (Exception ex)
            {
                _pluginLog.Error(ex, "[KitchenSink] UpdateDtrBar failed");
                _dtrBarEntry.Shown = false;
                _dtrBarEntry.Tooltip = null;
            }
        });
    }


    public void Dispose()
    {
        _dtrBarEntry.Remove();
        _commandManager.RemoveHandler("/ks");
        _commandManager.RemoveHandler("/k-");
        _commandManager.RemoveHandler("/k+");
    }
}
