using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using ECommons.ImGuiMethods;
using VIWI.Helpers;
using VIWI.Modules.Workshoppa;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonActionCross;

namespace VIWI.UI.Pages
{
    public sealed class WorkshoppaPage : IDashboardPage
    {
        public string DisplayName => "Workshoppa";
        public string Category => "Modules";
        public string Version => WorkshoppaModule.ModuleVersion;
        public bool SupportsEnableToggle => true;

        public bool IsEnabled
        {
            get => WorkshoppaModule.Enabled;
        }

        public void SetEnabled(bool value)
        {
            WorkshoppaModule.Instance?.SetEnabled(value);
        }

        public void Draw()
        {
            var module = WorkshoppaModule.Instance;
            var config = module?._configuration;
            if (config == null)
            {
                ImGui.TextDisabled("Workshoppa is not initialized yet.");
                return;
            }
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
            ImGui.TextUnformatted("Settings");
            ImGuiHelpers.ScaledDummy(4f);

            bool enableRepair = config.EnableRepairKitCalculator;
            if (ImGui.Checkbox("Enable Repair Kit Calculator", ref enableRepair))
            {
                config.EnableRepairKitCalculator = enableRepair;
                WorkshoppaModule.Instance?.SaveConfig();
            }
            ImGuiComponents.HelpMarker("Feature to Automatically purchase Grade6DarkMatter from *JUNKMONGER* vendors. \n" +
                "This is based off the CURRENT number of DarkMatterCLUSTERS you have in your inventory at a 5:1 ratio for crafting RepairKits.");

            bool enableTanks = config.EnableCeruleumTankCalculator;
            if (ImGui.Checkbox("Enable Ceruleum Tank Calculator", ref enableTanks))
            {
                config.EnableCeruleumTankCalculator = enableTanks;
                WorkshoppaModule.Instance?.SaveConfig();
            }
            ImGuiComponents.HelpMarker("Feature to Automatically purchase CerueleumFuelTanks from *FC MAMMET & RESIDENT CARETAKER* vendors. \n" +
                "Just input how many stacks of fuel you would like to buy in the respective window.");

            bool enableMudstone = config.EnableMudstoneCalculator;
            if (ImGui.Checkbox("Enable Mudstone Calculator", ref enableMudstone))
            {
                config.EnableMudstoneCalculator = enableMudstone;
                WorkshoppaModule.Instance?.SaveConfig();
            }
            ImGuiComponents.HelpMarker("Feature to Automatically purchase Mudstone from *(RESIDENTIAL DISTRICT) MATERIAL SUPPLIER* vendors. \n" +
                "Just input how many stacks of mudstones you would like to buy in the respective window.");

            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);
            ImGui.TextUnformatted("Commands:"); //I could have made a table but I'm tired.
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.Text("/ws                     = Open Workshoppa UI.");
            ImGui.Text("/workshoppa   = Open Workshoppa UI.");
            ImGui.Text("/buy-tanks        = Buy a given number of ceruleum tank stacks.");
            ImGui.Text("/fill-tanks          = Fill your inventory with a given number of ceruleum tank stacks.");
            ImGui.Text("/buy-stone        = Buy a given number of mudstone stacks.");
            ImGui.Text("/fill-stone          = Fill your inventory with a given number of mudstone stacks.");
            ImGui.Text("/grindstone       = Starts the experimental leveling feature.");
            ImGui.Text("/g6dm                = Buy Grade6DarkMatter at a 5:1 ratio for RepairKits.");

            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            if (ImGui.Button("Open Workshoppa"))
                WorkshoppaModule.Instance?.OpenWorkshoppa();

            /* -- TODO: Automate travel to these vendors??
            ImGui.SameLine();
            if (ImGui.Button("Open Repair Kit"))
                WorkshoppaModule.Instance?.OpenRepairKit();

            ImGui.SameLine();

            if (ImGui.Button("Open Ceruleum Tanks"))
                WorkshoppaModule.Instance?.OpenTanks();
            */
        }
    }
}