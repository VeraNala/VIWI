using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using System;
using System.Numerics;
using VIWI.Core;
using VIWI.Helpers;
using VIWI.Modules.AutoLogin;

namespace VIWI.UI.Pages
{
    public sealed class AutoLoginPage : IDashboardPage
    {
        public string DisplayName => "AutoLogin";
        public string Category => "Modules";
        public string Version => AutoLoginModule.ModuleVersion;
        public bool SupportsEnableToggle => true;
        public bool IsEnabled => AutoLoginModule.Enabled;
        public void SetEnabled(bool value) => AutoLoginModule.Instance?.SetEnabled(value);

        public bool debugEnabled = false;
        private string _newLoginCmd = string.Empty;
        private string _launchPathBuf = "";
        private string _launchArgsBuf = "";
        private bool _buffersInitialized = false;

        public void Draw()
        {
            var module = AutoLoginModule.Instance;
            var config = module?._configuration;
            if (config == null)
            {
                ImGui.TextDisabled("AutoLogin is not initialized yet.");
                return;
            }

            var snap = config.Current;

            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextUnformatted($"AutoLogin - V{Version}");
            ImGui.SameLine();
            ImGui.TextColored(GradientColor.Get(ImGuiHelper.RainbowColorStart, ImGuiHelper.RainbowColorEnd, 500), "DDoS Begone!");

            ImGui.TextUnformatted("Enabled:");
            ImGui.SameLine();
            ImGui.TextColored(
                config.Enabled ? new Vector4(0.3f, 1f, 0.3f, 1f) : new Vector4(1f, 0.3f, 0.3f, 1f),
                config.Enabled ? "Yes" : "No - Click the OFF button to Enable AutoLogin!!"
            );

            ImGuiHelpers.ScaledDummy(4f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            ImGui.TextUnformatted("Description:");
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextWrapped(
                "AutoLogin is an Anti-DDoS module that will store the details of your last-known active character " +
                "and attempt to automatically reconnect to them in the event of a sudden disconnect.\n" +
                "In addition, AutoLogin prevents your client from killing itself on any lobby/disconnection errors."
            );

            ImGuiHelpers.ScaledDummy(8f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            // ----------------------------
            // Current Snapshot
            // ----------------------------
            if (!string.IsNullOrWhiteSpace(snap.CharacterName))
            {
                if (config.ServiceAccountIndex > 0)
                ImGui.TextUnformatted($"Service Account Index: {config.ServiceAccountIndex}");
                ImGui.TextUnformatted($"Current Saved Character: {snap.CharacterName} @ {snap.HomeWorldName}");
                ImGui.TextUnformatted($"Home Data Center: {snap.DataCenterName} ({snap.DataCenterID})");

                if (snap.Visiting)
                    ImGui.TextUnformatted($"Currently Visiting: {snap.CurrentWorldName}, on {snap.vDataCenterName} ({snap.vDataCenterID})");

                if (config.CurrentRegion != LoginRegion.Unknown)
                    ImGui.TextUnformatted($"Detected Region: {config.CurrentRegion}");
            }
            else
            {
                ImGui.TextDisabled(
                    "No Character Detected" +
                    "\nAutoLogin updates character data on Logins, as well as World/Area changes" +
                    "\nIf you're logged in and seeing this message, move around!"
                );
            }

            // ----------------------------
            // Per-Region Snapshot Summary
            // ----------------------------
            ImGuiHelpers.ScaledDummy(8f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            ImGui.TextUnformatted("Most Recent Character Per Region:");
            ImGuiHelpers.ScaledDummy(4f);

            DrawRegionRow(config, LoginRegion.NA);
            DrawRegionRow(config, LoginRegion.EU);
            DrawRegionRow(config, LoginRegion.OCE);
            DrawRegionRow(config, LoginRegion.JP);

            ImGuiHelpers.ScaledDummy(4f);
            if (config.RestartingClient || config.PendingRestartRegion != LoginRegion.Unknown)
            {
                ImGui.TextUnformatted($"Restart State: RestartingClient={config.RestartingClient}, PendingRegion={config.PendingRestartRegion}");
            }

            // ----------------------------
            // Restart on Auth Error + Launch Settings
            // ----------------------------
            ImGuiHelpers.ScaledDummy(8f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            bool skipAuth = config.SkipAuthError;
            if (ImGui.Checkbox("Restart on Auth Error", ref skipAuth))
            {
                config.SkipAuthError = skipAuth;
                module?.SaveConfig();
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "Experimental: Will attempt to restart your client in the event of an Auth Error." +
                "\nSome notes on this:" +
                "\nIf you have an OTP on your account you will need to use the XIV Auth App" +
                "\nIf you do not enable \"Log in automatically\" in XIVLauncher this will not work." +
                "\nIf you do not set up commands or AR Multi on launch, this will only log you back into your last character, nothing else." +
                "\nYou are using this feature entirely at your own risk - It is literally accessing files on your PC to open clients."
            );
            ImGui.SameLine();
            bool canRestart = config.SkipAuthError && !string.IsNullOrWhiteSpace(_launchPathBuf);
            using (ImRaii.Disabled(!canRestart))
            {
                if (ImGui.Button("Restart Client"))
                {
                    config.ClientLaunchPath = _launchPathBuf;
                    config.ClientLaunchArgs = _launchArgsBuf;
                    module?.SaveConfig();
                    module?.RequestClientRestart(config.CurrentRegion);
                }
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("Launches a new client using your settings, and kills this one - resetting your Auth Token.");

            if (!_buffersInitialized)
            {
                _launchPathBuf = config.ClientLaunchPath ?? "";
                _launchArgsBuf = config.ClientLaunchArgs ?? "";
                _buffersInitialized = true;
            }

            (string statusText, Vector4 statusColor) = ValidateLaunchSettings(_launchPathBuf, _launchArgsBuf, skipAuth);

            ImGui.TextUnformatted("Status:");
            ImGui.SameLine();
            ImGui.TextColored(statusColor, statusText);

            ImGuiHelpers.ScaledDummy(4f);

            using (ImRaii.Disabled(!skipAuth))
            {
                ImGui.Text("Launch Path:");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint(
                    "##client_launch_path",
                    "example: C:\\Program Files (x86)\\XIVLauncher\\XIVLauncher.exe",
                    ref _launchPathBuf,
                    512,
                    ImGuiInputTextFlags.EnterReturnsTrue
                );
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    config.ClientLaunchPath = _launchPathBuf;
                    module?.SaveConfig();
                }

                ImGui.Text("Launch Arguments:");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint(
                    "##client_launch_args",
                    "example: --roamingPath=\"%appdata%\\XIVLauncher\" --account=yoship-False-False",
                    ref _launchArgsBuf,
                    512,
                    ImGuiInputTextFlags.EnterReturnsTrue
                );
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    config.ClientLaunchArgs = _launchArgsBuf;
                    module?.SaveConfig();
                }
            }

            // ----------------------------
            // Login Commands
            // ----------------------------
            /*ImGuiHelpers.ScaledDummy(8f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            ImGui.TextUnformatted("Login Chat Commands");
            ImGuiHelpers.ScaledDummy(4f);

            bool runLoginCommands = config.RunLoginCommands;
            if (ImGui.Checkbox("Run commands after successful disconnect recovery", ref runLoginCommands))
            {
                config.RunLoginCommands = runLoginCommands;
                module?.SaveConfig();
            }
            ImGuiComponents.HelpMarker(
                "These commands run once after the game reports a successful login.\n" +
                "Commands are executed with a small delay between each."
            );

            bool skipWhenAR = config.ARActiveSkipLoginCommands;
            if (ImGui.Checkbox("Skip login commands when AutoRetainer is active", ref skipWhenAR))
            {
                config.ARActiveSkipLoginCommands = skipWhenAR;
                module?.SaveConfig();
            }
            ImGuiComponents.HelpMarker("When Enabled, if AutoRetainer is Busy or in MultiMode, AutoLogin will not run custom login commands.");

            ImGuiHelpers.ScaledDummy(6f);

            config.LoginCommands ??= [];

            int? pendingRemove = null;
            int? pendingMoveFrom = null;
            int? pendingMoveTo = null;

            ImGui.PushID("login_cmd_add");
            bool submit = ImGui.InputTextWithHint("##new_login_cmd", "Add command (e.g. /viwi)", ref _newLoginCmd, 512, ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.SameLine();
            submit |= ImGuiComponents.IconButton("add", FontAwesomeIcon.Plus);

            if (submit)
            {
                var cmd = _newLoginCmd.Trim();

                if (!string.IsNullOrWhiteSpace(cmd))
                {
                    if (!cmd.StartsWith('/'))
                        cmd = "/" + cmd;

                    if (!config.LoginCommands.Contains(cmd))
                    {
                        config.LoginCommands.Add(cmd);
                        module?.SaveConfig();
                    }

                    _newLoginCmd = string.Empty;
                    ImGui.SetKeyboardFocusHere();
                }
            }
            ImGui.PopID();

            if (config.LoginCommands.Count == 0)
            {
                ImGui.TextDisabled("No login commands set.");
            }
            else
            {
                for (int i = 0; i < config.LoginCommands.Count; i++)
                {
                    var cmd = config.LoginCommands[i] ?? string.Empty;

                    ImGui.PushID(cmd);
                    bool actionQueued = pendingRemove.HasValue || pendingMoveFrom.HasValue;

                    if (actionQueued) ImGui.BeginDisabled();

                    // Up
                    bool canUp = i > 0;
                    if (!canUp) ImGui.BeginDisabled();
                    if (ImGuiComponents.IconButton("up", FontAwesomeIcon.ArrowUp))
                    {
                        pendingMoveFrom = i;
                        pendingMoveTo = i - 1;
                    }
                    if (!canUp) ImGui.EndDisabled();

                    ImGui.SameLine();

                    // Down
                    bool canDown = i < config.LoginCommands.Count - 1;
                    if (!canDown) ImGui.BeginDisabled();
                    if (ImGuiComponents.IconButton("down", FontAwesomeIcon.ArrowDown))
                    {
                        pendingMoveFrom = i;
                        pendingMoveTo = i + 1;
                    }
                    if (!canDown) ImGui.EndDisabled();

                    ImGui.SameLine();

                    if (ImGuiComponents.IconButton("delete", FontAwesomeIcon.Trash))
                    {
                        pendingRemove = i;
                    }
                    if (actionQueued) ImGui.EndDisabled();

                    ImGui.SameLine();
                    ImGui.TextUnformatted(cmd);

                    ImGui.PopID();
                }
            }

            if (pendingRemove.HasValue)
            {
                config.LoginCommands.RemoveAt(pendingRemove.Value);
                module?.SaveConfig();
            }
            else if (pendingMoveFrom.HasValue && pendingMoveTo.HasValue)
            {
                MoveItem(config.LoginCommands, pendingMoveFrom.Value, pendingMoveTo.Value);
                module?.SaveConfig();
            }*/

            // ----------------------------
            // Stats
            // ----------------------------
            ImGuiHelpers.ScaledDummy(8f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            ImGui.TextUnformatted($"VIWI has helped you recover from: {config.DCsRecovered} Connection Errors");
            if (config.DCsRecovered == 0)
            {
                ImGui.TextColored(GradientColor.Get(ImGuiHelper.RainbowColorStart, ImGuiHelper.RainbowColorEnd, 500), "What are you, an OCE player??");
            }
            else if (config.DCsRecovered > 0 && config.DCsRecovered < 5)
            {
                ImGui.TextColored(GradientColor.Get(ImGuiHelper.RainbowColorStart, ImGuiHelper.RainbowColorEnd, 500), "Typical NA Server day.");
            }
            else
            {
                ImGui.TextColored(GradientColor.Get(ImGuiHelper.RainbowColorStart, ImGuiHelper.RainbowColorEnd, 500), "YOSHIP SAVE US!! PLEASE WE'RE BEGGING YOU!!");
            }

            if (config.SkipAuthError)
            {
                ImGui.TextUnformatted($"VIWI has helped you recover from: {config.AuthsRecovered} Authentication Errors");
            }

            // ----------------------------
            // Dev debug button
            // ----------------------------
#pragma warning disable CS0162 // Unreachable code detected
            if (VIWIConfig.DEVMODE)
            {
                float toggleWidth = 60f;
                uint colBase = debugEnabled ? 0xFF27AE60u : 0xFF7F8C8Du;
                uint colHovered = debugEnabled ? 0xFF2ECC71u : 0xFF95A5A6u;
                uint colActive = debugEnabled ? 0xFF1E8449u : 0xFF7F8C8Du;

                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 999f);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10f, 3f) * ImGuiHelpers.GlobalScale);
                ImGui.PushStyleColor(ImGuiCol.Button, colBase);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colHovered);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, colActive);

                if (ImGui.Button("Debug", new Vector2(toggleWidth, 0)))
                {
                    debugEnabled = !debugEnabled;

                    if (module != null && debugEnabled == true)
                    {
                        // Use the new region-aware restart
                        module.RequestClientRestart(config.CurrentRegion);
                        PluginLog.Information("Triggered client restart (debug).");
                    }
                }
                ImGuiComponents.HelpMarker("Triggers the same restart path used for auth error recovery.");
                ImGui.PopStyleColor(3);
                ImGui.PopStyleVar(2);
            }
#pragma warning restore CS0162 // Unreachable code detected
        }

        private static (string Text, Vector4 Color) ValidateLaunchSettings(string launchPath, string launchArgs, bool featureEnabled)
        {
            var ok = new Vector4(0.3f, 1f, 0.3f, 1f);
            var warn = new Vector4(1f, 0.85f, 0.25f, 1f);
            var bad = new Vector4(1f, 0.3f, 0.3f, 1f);
            var off = new Vector4(0.7f, 0.7f, 0.7f, 1f);

            if (!featureEnabled)
                return ("Enable \"Restart on Auth Error\" to edit launch settings.", off);

            if (string.IsNullOrWhiteSpace(launchPath))
                return ("Missing Launch Path.", bad);

            var p = launchPath.Trim();
            bool isUri = p.Contains("://", StringComparison.OrdinalIgnoreCase);

            if (!isUri)
            {
                try
                {
                    if (!System.IO.File.Exists(p))
                        return ("Launch Path does not exist.", bad);
                }
                catch
                {
                    return ("Launch Path is invalid.", bad);
                }
            }

            bool looksLikeXivLauncher =
                p.EndsWith("XIVLauncher.exe", StringComparison.OrdinalIgnoreCase) ||
                p.IndexOf("xivlauncher", StringComparison.OrdinalIgnoreCase) >= 0;

            var args = (launchArgs ?? string.Empty).Trim();
            bool hasAccount = args.IndexOf("--account=", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasRoaming = args.IndexOf("--roamingPath=", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!looksLikeXivLauncher && !isUri)
            {
                return ("Path OK, but this doesn’t look like XIVLauncher (may not auto-login).", warn);
            }

            if (!hasAccount)
            {
                return ("Path OK. Missing --account=… (auto-login may not work).", warn);
            }

            if (!hasRoaming)
                return ("Looks OK. (Tip: add --roamingPath=… if you use a custom XIVLauncherData path.)", ok);

            return ("Looks OK.", ok);
        }
        private static void DrawRegionRow(AutoLoginConfig config, LoginRegion region)
        {
            config.LastByRegion ??= new();

            if (!config.LastByRegion.TryGetValue(region, out var snap) || snap == null || string.IsNullOrWhiteSpace(snap.CharacterName))
            {
                ImGui.TextDisabled($"{region}: (none)");
                return;
            }

            var visiting = snap.Visiting ? $" (visiting {snap.CurrentWorldName})" : string.Empty;
            ImGui.TextUnformatted($"{region}: {snap.CharacterName} @ {snap.HomeWorldName}{visiting}");
        }

        private static void MoveItem<T>(System.Collections.Generic.List<T> list, int from, int to)
        {
            if (from == to) return;
            if (from < 0 || from >= list.Count) return;
            if (to < 0 || to >= list.Count) return;

            (list[from], list[to]) = (list[to], list[from]);
        }
    }
}