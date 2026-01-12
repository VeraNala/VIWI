using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using System.Numerics;
using TerraFX.Interop.Windows;
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


            if (!string.IsNullOrEmpty(config.CharacterName))
            {
                ImGui.TextUnformatted($"Last Logged In Character: {config.CharacterName} @ {config.HomeWorldName}");
                if (config.Visiting)
                {
                    ImGui.TextUnformatted($"Current World: {config.CurrentWorldName}, on {config.vDataCenterName} ({config.vDataCenterID})");
                }
                ImGui.TextUnformatted($"Home Data Center: {config.DataCenterName} ({config.DataCenterID})");
                ImGui.TextUnformatted($"Service Account Index: {config.ServiceAccountIndex}");
            }
            else
            {
                ImGui.TextDisabled("No Character Detected" +
                "\nAutoLogin updates character data on Logins, as well as World/Area changes" +
                "\nIf you're logged in and seeing this message, move around!");
            }

            ImGuiHelpers.ScaledDummy(8f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            bool HCMode = config.HCMode;

            if (ImGui.Checkbox("Hardcore Mode", ref HCMode))
            {
                config.HCMode = HCMode;
                config.HCCharacterName = config.CharacterName;
                config.HCHomeWorldName = config.HomeWorldName;
                config.HCDataCenterID = config.DataCenterID;
                config.HCDataCenterName = config.DataCenterName;
                config.HCVisiting = config.Visiting;
                config.HCCurrentWorldName = config.CurrentWorldName;
                config.HCvDataCenterID = config.vDataCenterID;
                config.HCvDataCenterName = config.vDataCenterName;
                module?.SaveConfig();
            }
            ImGuiComponents.HelpMarker("--- Hardcore Mode will save your preferred character ---\nThis prioritizes logging back into the stored character \nin the event of a disconnect rather than the one \nyou were currently on (unless its the same one, duh!).");
            if (HCMode)
            {
                if (!string.IsNullOrEmpty(config.HCCharacterName))
                {
                    ImGuiHelpers.ScaledDummy(8f);
                    ImGui.TextUnformatted($"Hard Saved Character: {config.HCCharacterName} @ {config.HCHomeWorldName}");
                    if (config.HCVisiting)
                    {
                        ImGui.TextUnformatted($"Current World: {config.HCCurrentWorldName}, on {config.HCvDataCenterName} ({config.HCvDataCenterID})");
                    }
                    ImGui.TextUnformatted($"Home Data Center: {config.HCDataCenterName} ({config.HCDataCenterID})");
                }
                else
                {
                    ImGui.TextDisabled("No Character Detected\n" +
                    "\nAutoLogin updates your preferred character when you check the box!" +
                    "\nMake sure you're logged into the character you want to save first!" +
                    "\nOtherwise, AutoLogin updates on World/Area changes" +
                    "\nSo if you're logged in and seeing this message, move around!");
                }
            }

            ImGuiHelpers.ScaledDummy(2f);

            if (VIWIContext.CoreConfig != null && VIWIContext.CoreConfig.Unlocked)
            {
                bool skipAuth = config.SkipAuthError;

                if (ImGui.Checkbox("Restart on Auth Error", ref skipAuth))
                {
                    config.SkipAuthError = skipAuth;
                    module?.SaveConfig();
                }
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("Experimental: Will attempt to restart your client in the event of an Auth Error." +
                    "\nSome notes on this:" +
                    "\nIf you have an OTP on your account you will need to use the XIV Auth App" +
                    "\nIf you do not enable \"Log in automatically\" in XIVLauncher this will not work." +
                    "\nIf you do not set up commands or AR Multi on launch, this will only log you back into your last character, nothing else." +
                    "\nYou are using this feature entirely at your own risk - It is literally accessing files on your PC to open clients.");
                var cfg = config;
                if (!_buffersInitialized)
                {
                    _launchPathBuf = cfg.ClientLaunchPath ?? "";
                    _launchArgsBuf = cfg.ClientLaunchArgs ?? "";
                    _buffersInitialized = true;
                }
                ImGui.Text("Launch Path:");
                ImGui.SetNextItemWidth(-1);
                var path = ImGui.InputTextWithHint("##client_launch_path", "example: C:\\Program Files (x86)\\XIVLauncher\\XIVLauncher.exe", ref _launchPathBuf, 512, ImGuiInputTextFlags.EnterReturnsTrue);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    cfg.ClientLaunchPath = _launchPathBuf;
                    module?.SaveConfig();
                }
                ImGui.Text("Launch Arguments:");
                ImGui.SetNextItemWidth(-1);
                var args = ImGui.InputTextWithHint("##client_launch_args", "example: --roamingPath=\"%appdata%\\XIVLauncher\" --account=yoship-False-False", ref _launchArgsBuf, 512, ImGuiInputTextFlags.EnterReturnsTrue);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    cfg.ClientLaunchArgs = _launchArgsBuf;
                    module?.SaveConfig();
                }
            }
            else
            {
                bool skipAuth = config.SkipAuthError;

                if (ImGui.Checkbox("Skip Close on Auth Error", ref skipAuth))
                {
                    config.SkipAuthError = skipAuth;
                    module?.SaveConfig();
                }
            }
            ImGuiHelpers.ScaledDummy(8f);
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
            }

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
                ImGui.TextColored(GradientColor.Get(ImGuiHelper.RainbowColorStart, ImGuiHelper.RainbowColorEnd, 500), "YOSHIP SAVE US!! PLEASE WE'RE BEGGING YOU!!");
            if (VIWIContext.CoreConfig != null && VIWIContext.CoreConfig.Unlocked)
            {
                ImGui.TextUnformatted($"VIWI has helped you recover from: {config.AuthsRecovered} Authentication Errors");
            }

            if (VIWIConfig.DEVMODE)
            {
#pragma warning disable CS0162 // Unreachable code detected
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
                        module?.RequestClientRestart();
                        PluginLog.Information("Testing Login Commands");
                    }
                }
                ImGuiComponents.HelpMarker("Runs the same command queue that would execute after a successful login.");
                ImGui.PopStyleColor(3);
                ImGui.PopStyleVar(2);
#pragma warning restore CS0162 // Unreachable code detected
            }
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
