using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using VIWI.Helpers;
using VIWI.Modules.Workshoppa;

namespace VIWI.UI.Pages
{
    public sealed class WorkshoppaPage : IDashboardPage
    {
        public string DisplayName => "Workshoppa";
        public string Category => "Modules";
        public bool SupportsEnableToggle => true;
        public string Version => WorkshoppaModule.ModuleVersion;

        public bool IsEnabled
        {
            get => WorkshoppaModule.Enabled;
        }

        public void SetEnabled(bool value)
        {
            WorkshoppaModule.Config.Enabled = value;
            WorkshoppaModule.SaveConfig();

            WorkshoppaModule.Instance?.ApplyEnabledState(value);
        }

        public void Draw()
        {
            var config = WorkshoppaModule.Config;
            ImGuiHelpers.ScaledDummy(4f);

            ImGui.TextUnformatted($"Workshoppa - V{Version}");
            ImGui.TextColored(GradientColor.Get(ImGuiHelper.RainbowColorStart, ImGuiHelper.RainbowColorEnd, 500), "Workshop Project Automation");
            ImGui.TextUnformatted($"Enabled: {config.Enabled}");
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);
            ImGui.TextUnformatted("Description:");
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextWrapped(
                "Workshoppa is a workshop project manager and automator originally created by Liza,\n" +
                "now completely rewritten using ECommons and fully integrated into VIWI.\n" +
                "Workshoppa allows you to queue multiple company projects and automatically turn in the required materials\n" +
                "to progress each project to its next stage.\n" +
                "Additionally, Workshoppa includes features for automatically purchasing Grade 6 Dark Matter for repair kits,\n" +
                "as well as recursively purchasing ceruleum fuel tanks from the FC mammet."
            );
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            ImGuiHelpers.ScaledDummy(8f);
            ImGui.TextUnformatted("Settings");
            ImGuiHelpers.ScaledDummy(4f);

            bool enableRepair = config.EnableRepairKitCalculator;
            if (ImGui.Checkbox("Enable Repair Kit Calculator", ref enableRepair))
            {
                config.EnableRepairKitCalculator = enableRepair;
                WorkshoppaModule.SaveConfig();
            }

            bool enableTanks = config.EnableCeruleumTankCalculator;
            if (ImGui.Checkbox("Enable Ceruleum Tank Calculator", ref enableTanks))
            {
                config.EnableCeruleumTankCalculator = enableTanks;
                WorkshoppaModule.SaveConfig();
            }

            ImGuiHelpers.ScaledDummy(8f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            if (ImGui.Button("Open Workshoppa"))
                WorkshoppaModule.Instance?.OpenWorkshoppa();

            ImGui.SameLine();

            if (ImGui.Button("Open Repair Kit"))
                WorkshoppaModule.Instance?.OpenRepairKit();

            ImGui.SameLine();

            if (ImGui.Button("Open Ceruleum Tanks"))
                WorkshoppaModule.Instance?.OpenTanks();
        }
    }
}