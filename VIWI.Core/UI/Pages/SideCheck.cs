using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using System.Numerics;
using VIWI.Helpers;
using VIWI.Modules.SideCheck;

namespace VIWI.UI.Pages
{
    public sealed class SideCheckPage : IDashboardPage
    {
        public string DisplayName => "SideCheck";
        public string Category => "Modules";
        public bool RequiresUnlock => true;
        public string Version => SideCheckModule.ModuleVersion;
        public bool SupportsEnableToggle => true;
        public bool IsEnabled => SideCheckModule.Enabled;
        public void SetEnabled(bool value) => SideCheckModule.Instance?.SetEnabled(value);

        public void Draw()
        {
            var module = SideCheckModule.Instance;
            var config = module?._configuration;
            if (config == null)
            {
                ImGui.TextDisabled("SideCheck is not initialized yet.");
                return;
            }
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextUnformatted($"SideCheck - V{Version}");
            ImGui.SameLine();
            ImGui.TextColored(GradientColor.Get(ImGuiHelper.RainbowColorStart, ImGuiHelper.RainbowColorEnd, 500), "It's always better from behind!");
            ImGui.TextUnformatted("Enabled:");
            ImGui.SameLine();
            ImGui.TextColored(
                config.Enabled ? new Vector4(0.3f, 1f, 0.3f, 1f) : new Vector4(1f, 0.3f, 0.3f, 1f),
                config.Enabled ? "Yes" : "No - Click the OFF button to Enable SideCheck!!"
            );
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
