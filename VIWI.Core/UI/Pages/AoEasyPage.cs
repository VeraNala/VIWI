using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using VIWI.Modules.AoEasy;

namespace VIWI.UI.Pages
{
    public sealed class AoEasyPage : IDashboardPage
    {
        public string DisplayName => "AoEasy";
        public string Category => "Modules";
        public string Version => AoEasyModule.ModuleVersion;
        public bool SupportsEnableToggle => true;

        public bool IsEnabled
        {
            get => AoEasyModule.Enabled;
        }

        public void SetEnabled(bool value)
        {
            AoEasyModule.Instance?.SetEnabled(value);
        }

        public void Draw()
        {
            var module = AoEasyModule.Instance;
            var config = module?._configuration;
            if (config == null)
            {
                ImGui.TextDisabled("AoEasy is not initialized yet.");
                return;
            }
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextUnformatted($"AoEasy – Stop Running Away From Me! - V{Version}");
            ImGui.TextUnformatted($"Enabled: {config.Enabled}");
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.Separator();

            ImGuiHelpers.ScaledDummy(8f);
            ImGui.TextUnformatted("Description:");
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextWrapped(
                "STILL IN DEVELOPMENT!!!"
            );
            ImGui.Separator();

            ImGuiHelpers.ScaledDummy(8f);

        }
    }
}
