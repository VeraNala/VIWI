using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
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

        public bool debugEnabled = false;
        public bool SupportsEnableToggle => true;

        public bool IsEnabled
        {
            get => AutoLoginModule.Enabled;
        }

        public void SetEnabled(bool value)
        {
            AutoLoginModule.Instance?.SetEnabled(value);
        }

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
            ImGui.TextColored(GradientColor.Get(ImGuiHelper.RainbowColorStart, ImGuiHelper.RainbowColorEnd, 500), "DDoS Begone!");
            ImGui.TextUnformatted($"Enabled: {config.Enabled}");
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
                //ImGui.TextUnformatted($"Service Account Index: {config.AccountIndex}");
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
                AutoLoginModule.Instance?.SaveConfig();
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

            ImGuiHelpers.ScaledDummy(8f);
            ImGui.Separator();

            bool skipAuth = config.SkipAuthError;

            if (ImGui.Checkbox("Skip Auth", ref skipAuth))
            {
                config.SkipAuthError = skipAuth;
                AutoLoginModule.Instance?.SaveConfig();
            }
            ImGuiComponents.HelpMarker("Experimental: Will attempt to bypass Auth Errors.... I think?");


            if (VIWIPlugin.DEVMODE)
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

                    if (AutoLoginModule.Instance != null && debugEnabled == true)
                    {
                        AutoLoginModule.Instance.StartAutoLogin();
                    }
                    else
                    {
                        ImGuiHelpers.ScaledDummy(4f);
                        ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f),
                            "AutoLogin module is not initialized (Instance is null).");
                    }
                }
                ImGui.PopStyleColor(3);
                ImGui.PopStyleVar(2);
#pragma warning restore CS0162 // Unreachable code detected
            }
        }
    }
}
