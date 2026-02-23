using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using ECommons.ImGuiMethods;
using Lumina.Excel.Sheets;
using System;
using System.Numerics;
using VIWI.Core;
using VIWI.Helpers;
using VIWI.Modules.Workshoppa;

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
            ImGui.SameLine();
            ImGui.TextColored(GradientColor.Get(ImGuiHelper.RainbowColorStart, ImGuiHelper.RainbowColorEnd, 500), "Workshop Project Automation");
            ImGui.TextUnformatted("Enabled:");
            ImGui.SameLine();
            ImGui.TextColored(
                config.Enabled ? new Vector4(0.3f, 1f, 0.3f, 1f) : new Vector4(1f, 0.3f, 0.3f, 1f),
                config.Enabled ? "Yes" : "No - Click the OFF button to Enable Workshoppa!!"
            );
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

            bool enableMudstone = config.EnableGrindstoneShopCalculator;
            if (ImGui.Checkbox("Enable Mudstone Calculator", ref enableMudstone))
            {
                config.EnableGrindstoneShopCalculator = enableMudstone;
                WorkshoppaModule.Instance?.SaveConfig();
            }
            ImGuiComponents.HelpMarker("Feature to Automatically purchase Mudstone from *(RESIDENTIAL DISTRICT) MATERIAL SUPPLIER* vendors. \n" +
                "Just input how many stacks of mudstones you would like to buy in the respective window.");

            ImGuiHelpers.ScaledDummy(8f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            ImGui.TextUnformatted("Grindstone Level Targets");
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("Set your Target level and ensure you have enough materials to reach it!!");

            var dm = VIWIContext.DataManager;
            var ps = VIWIContext.PlayerState;

            var localPlayer = VIWIContext.ObjectTable.LocalPlayer;
            bool hasPreferredWorldBonus = localPlayer != null && WorkshoppaHelpers.HasStatus(localPlayer, WorkshoppaHelpers.PreferredWorldBonusStatusId);

            var crp = WorkshoppaHelpers.GetJobByAbbrev(dm, "CRP");
            var min = WorkshoppaHelpers.GetJobByAbbrev(dm, "MIN");
            var btn = WorkshoppaHelpers.GetJobByAbbrev(dm, "BTN");

            // Elm Lumber: 747 EXP each, turn-in size 55 => always multiple of 55
            var (crpQtyText, crpEligible, crpStatusText) = WorkshoppaHelpers.ComputeRow(dm, ps, crp, config.CrpTargetLevel, hasPreferredWorldBonus, minRequiredLevel: 16, expPerMaterial: 747, materialsPerTurnin: 55);
            // Mudstone: 498 EXP each, turn-in size 55 => always multiple of 55
            var (minQtyText, minEligible, minStatusText) = WorkshoppaHelpers.ComputeRow(dm, ps, min, config.MinTargetLevel, hasPreferredWorldBonus, minRequiredLevel: 20, expPerMaterial: 498, materialsPerTurnin: 55);
            // Spruce Log: 2334 EXP each, turn-in size 55 => always multiple of 55
            var (btnQtyText, btnEligible, btnStatusText) = WorkshoppaHelpers.ComputeRow(dm, ps, btn, config.BtnTargetLevel, hasPreferredWorldBonus, minRequiredLevel: 50, expPerMaterial: 2334, materialsPerTurnin: 55);

            if (ImGui.BeginTable("WorkshoppaLevelTargets", 6, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Enable", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Current Lvl", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Target Lvl", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Required Material", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Quantity", ImGuiTableColumnFlags.WidthStretch);
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("This takes Road-To-90 status into consideration! Buy whatever number you see!");
                ImGui.TableHeadersRow();

                void JobRow(ref bool active, string label, ClassJob? job, ref int targetValue, string reqMat, int qtyText, bool eligible, string statusText)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    using (var disabledEnable = ImRaii.Disabled(!eligible))
                    {
                        bool a = active;
                        if (ImGui.Checkbox($"##use_{label}", ref a))
                        {
                            active = a;
                            WorkshoppaModule.Instance?.SaveConfig();
                        }
                    }

                    if (!eligible && active)
                    {
                        active = false;
                        WorkshoppaModule.Instance?.SaveConfig();
                    }

                    if (!eligible)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "⚠");
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted(statusText);
                            ImGui.EndTooltip();
                        }
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(label);

                    ImGui.TableNextColumn();
                    if (job == null)
                    {
                        ImGui.TextDisabled("?");
                    }
                    else
                    {
                        var current = ps.GetClassJobLevel(job.Value);
                        ImGui.TextUnformatted(current > 0 ? current.ToString() : "-");
                    }

                    ImGui.TableNextColumn();
                    using (var disabledTarget = ImRaii.Disabled(!eligible || !active))
                    {
                        int tmp = targetValue;
                        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
                        if (ImGui.InputInt($"##target_{label}", ref tmp))
                        {
                            tmp = WorkshoppaHelpers.ClampTargetLevel(tmp);
                            if (tmp != targetValue)
                            {
                                targetValue = tmp;
                                WorkshoppaModule.Instance?.SaveConfig();
                            }
                        }
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(reqMat);

                    ImGui.TableNextColumn();
                    if (eligible)
                        ImGui.TextUnformatted($"{qtyText}");
                    else
                        ImGui.TextDisabled($"{qtyText}");
                }

                JobRow(ref config.CrpTargetActive, "CRP", crp, ref config.CrpTargetLevel, "Elm Lumber", crpQtyText, crpEligible, crpStatusText);
                JobRow(ref config.MinTargetActive, "MIN", min, ref config.MinTargetLevel, "Mudstone", minQtyText, minEligible, minStatusText);
                JobRow(ref config.BtnTargetActive, "BTN", btn, ref config.BtnTargetLevel, "Spruce Log", btnQtyText, btnEligible, btnStatusText);

                ImGui.EndTable();
            }

            // ---------------------------
            // Commands
            // ---------------------------
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);
            ImGui.TextUnformatted("Commands:");
            ImGuiHelpers.ScaledDummy(4f);

            if (ImGui.BeginTable("WorkshoppaCommands", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);

                void Row(string cmd, string desc)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(cmd);
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped(desc);
                }

                Row("/ws", "Open Workshoppa UI.");
                Row("/workshoppa", "Open Workshoppa UI.");
                Row("/buy-tanks", "Buy a given number of ceruleum tank stacks.");
                Row("/fill-tanks", "Fill your inventory with a given number of ceruleum tank stacks.");
                Row("/buy-stone", "Buy a given number of mudstone stacks.");
                Row("/fill-stone", "Fill your inventory with a given number of mudstone stacks.");
                Row("/grindstone", "Starts the experimental leveling feature.");
                Row("/g6sm", "Buy Grade6DarkMatter at a 5:1 ratio for RepairKits.");

                ImGui.EndTable();
            }

            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            if (ImGui.Button("Open Workshoppa"))
                WorkshoppaModule.Instance?.OpenWorkshoppa();
        }
    }
}