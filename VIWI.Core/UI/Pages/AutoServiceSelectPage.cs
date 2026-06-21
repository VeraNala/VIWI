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
using VIWI.Modules.AutoServiceSelect;

namespace VIWI.UI.Pages
{
    public sealed class AutoServiceSelectPage : IDashboardPage
    {
        public string DisplayName => "AutoServiceSelect";
        public string Category => "Modules";
        public string Version => AutoServiceSelectModule.ModuleVersion;
        public bool SupportsEnableToggle => true;
        public bool IsEnabled => AutoServiceSelectModule.Enabled;
        public void SetEnabled(bool value) => AutoServiceSelectModule.Instance?.SetEnabled(value);

        public bool debugEnabled = false;
        private string _newLoginCmd = string.Empty;

        public void Draw()
        {
            var module = AutoServiceSelectModule.Instance;
            var config = module?._configuration;
            if (config == null)
            {
                ImGui.TextDisabled("AutoServiceSelect is not initialized yet.");
                return;
            }
            ImGui.TextUnformatted($"AutoServiceSelect - V{Version}");    
            ImGui.TextUnformatted("Enabled:");
            ImGui.SameLine();
            ImGui.TextColored(
                config.Enabled ? new Vector4(0.3f, 1f, 0.3f, 1f) : new Vector4(1f, 0.3f, 0.3f, 1f),
                config.Enabled ? "Yes" : "No - Click the OFF button to Enable AutoServiceSelect!!"
            );
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            int ServiceAccountIndex = config.ServiceAccountIndex;

            if (ImGui.SliderInt("Service Account", ref ServiceAccountIndex, 1, 10))
            {
                config.ServiceAccountIndex = ServiceAccountIndex;
            }
        }
    }
}