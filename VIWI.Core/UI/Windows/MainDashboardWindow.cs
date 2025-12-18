using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using VIWI.UI.Pages;

namespace VIWI.UI.Windows
{
    public sealed class MainDashboardWindow : Window
    {
        private IDashboardPage? activePage;

        public MainDashboardWindow()
            : base("VIWI - Vera's Integrated World Improvements##VIWI Dashboard",
                  ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(600, 400),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
            };

            DashboardRegistry.Register(new OverviewDashboardPage());
            DashboardRegistry.Register(new AoEasyPage());
            DashboardRegistry.Register(new AutoLoginPage());
            DashboardRegistry.Register(new WorkshoppaPage());

            activePage = DashboardRegistry.Pages.FirstOrDefault();
        }

        public override void Draw()
        {
            var sidebarWidth = 180f * ImGuiHelpers.GlobalScale;

            using (ImRaii.Child("##viwi_sidebar", new Vector2(sidebarWidth, 0), true))
            {
                DrawSidebar();
            }

            ImGui.SameLine();

            using (ImRaii.Child("##viwi_content", Vector2.Zero, false))
            {
                if (activePage != null)
                    activePage.Draw();
                else
                    ImGui.TextUnformatted("No page selected.");
            }
        }
        public void Dispose()
        {

        }

        private void DrawSidebar()
        {
            var grouped = DashboardRegistry.Pages
                                           .GroupBy(p => p.Category)
                                           .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                ImGui.TextDisabled(group.Key);
                ImGuiHelpers.ScaledDummy(2f);

                foreach (var page in group.OrderBy(p => p.DisplayName))
                {
                    bool isActive = ReferenceEquals(page, activePage);

                    ImGui.PushID(page.DisplayName);

                    float totalWidth = ImGui.GetContentRegionAvail().X;
                    float toggleWidth = page.SupportsEnableToggle ? 60f * ImGuiHelpers.GlobalScale : 0f;
                    float spacing = page.SupportsEnableToggle ? 4f * ImGuiHelpers.GlobalScale : 0f;
                    float buttonWidth = totalWidth - toggleWidth - spacing;

                    using (ImRaii.PushColor(ImGuiCol.Button, isActive ? 0xFFF5652Du : 0xFFAC5F41u))
                    using (ImRaii.PushColor(ImGuiCol.ButtonHovered, isActive ? 0xFFFFA26Du : 0xFFD0896Du))
                    using (ImRaii.PushColor(ImGuiCol.ButtonActive, isActive ? 0xFFFFA26Du : 0xFFD0896Du))
                    {
                        if (ImGui.Button(page.DisplayName, new Vector2(buttonWidth, 0)))
                        {
                            activePage = page;
                        }
                    }

                    if (page.SupportsEnableToggle)
                    {
                        ImGui.SameLine();

                        bool enabled = page.IsEnabled;
                        string label = enabled ? "ON" : "OFF";

                        uint colBase = enabled ? 0xFF27AE60u : 0xFF7F8C8Du;
                        uint colHovered = enabled ? 0xFF2ECC71u : 0xFF95A5A6u;
                        uint colActive = enabled ? 0xFF1E8449u : 0xFF7F8C8Du;

                        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 999f);
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10f, 3f) * ImGuiHelpers.GlobalScale);
                        ImGui.PushStyleColor(ImGuiCol.Button, colBase);
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colHovered);
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, colActive);

                        if (ImGui.Button(label, new Vector2(toggleWidth, 0)))
                        {
                            page.SetEnabled(!enabled);
                        }

                        ImGui.PopStyleColor(3);
                        ImGui.PopStyleVar(2);
                    }

                    ImGui.PopID();
                    ImGuiHelpers.ScaledDummy(2f);
                }

                ImGuiHelpers.ScaledDummy(6f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(6f);
            }
        }
    }
}
