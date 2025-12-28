using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using System.Linq;
using System.Numerics;
using VIWI.Core;

namespace VIWI.UI.Pages
{
    public sealed class OverviewDashboardPage : IDashboardPage
    {
        public string DisplayName => "Overview";
        public string Category => "Core";
        public string Version => $"{VIWIContext.PluginInterface.Manifest.AssemblyVersion}";

        public bool SupportsEnableToggle => false;
        public bool IsEnabled => true;
        public void SetEnabled(bool value) { }

        public void Draw()
        {
            var io = ImGui.GetIO();

            // ---------------------------
            // Header
            // ---------------------------
            ImGui.TextUnformatted("VIWI – Vera's Integrated World Improvements");
            ImGui.TextUnformatted($"VIWI Version: {VIWIContext.PluginInterface.Manifest.AssemblyVersion}");
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            // ---------------------------
            // Character Details
            // ---------------------------
            ImGui.TextUnformatted("Character Details:");
            ImGuiHelpers.ScaledDummy(4f);
 
            if (VIWIContext.ObjectTable.LocalPlayer != null)
            {
                var localPlayer = VIWIContext.ObjectTable.LocalPlayer;
                var playerState = VIWIContext.PlayerState;
                var currentLevel = localPlayer.Level;
                var targetLevel = 60;

                var expRemaining = ExpCalc.GetExpRemainingToLevel(
                    VIWIContext.DataManager,
                    VIWIContext.PlayerState,
                    currentLevel,
                    targetLevel);

                ImGui.BulletText($"Name: {localPlayer.Name} @ {localPlayer.HomeWorld.Value.Name}");
                ImGui.BulletText($"Current World: {localPlayer.CurrentWorld.Value.Name}");
                ImGui.BulletText($"Job: ({localPlayer.ClassJob.RowId}) \"{localPlayer.ClassJob.Value.Abbreviation}\"  Level: {localPlayer.Level}");
                ImGui.BulletText($"ClassJobLevel: {playerState.GetClassJobLevel(localPlayer.ClassJob.Value)}, Exp: {playerState.GetClassJobExperience(localPlayer.ClassJob.Value)}");
                //ImGui.TextUnformatted($"EXP needed to reach {targetLevel}: {expRemaining:N0}");

                var territoryId = VIWIContext.ClientState.TerritoryType;
                if (VIWIContext.DataManager.GetExcelSheet<TerritoryType>()
                    .TryGetRow(territoryId, out var territoryRow))
                {
                    ImGui.BulletText($"Location: ({territoryId}) \"{territoryRow.PlaceName.Value.Name}\"");
                }
                else
                {
                    ImGui.BulletText("Location: Unknown / invalid territory.");
                }

                var target = localPlayer.TargetObject;
                if (target != null)
                {
                    ImGui.BulletText(
                        $"Target: {target.Name}, BaseID: {target.BaseId}, ObjID: {target.GameObjectId}");

                    Vector3 pos = localPlayer.Position;
                    Vector3 tpos = target.Position;
                    var distanceToTarget = Vector3.Distance(pos, tpos);
                    var adjustedDistance = distanceToTarget - target.HitboxRadius - localPlayer.HitboxRadius;
                    if (adjustedDistance < 0)
                    { 
                        adjustedDistance = 0;
                    }

                    ImGui.BulletText($"DistanceToTarget: {adjustedDistance:0.000}");
                }
                else
                {
                    ImGui.BulletText("Target: None");
                    ImGui.BulletText("DistanceToTarget: 0");
                }
            }
            else
            {
                ImGui.TextDisabled("Busy / Not Loaded");
            }

                ImGuiHelpers.ScaledDummy(10f);

            // ---------------------------
            // System Info
            // ---------------------------
            ImGui.TextUnformatted("System Info");
            ImGuiHelpers.ScaledDummy(4f);

            var time = System.DateTime.Now;
            var timeZone = System.TimeZoneInfo.Local;
            ImGui.BulletText($"Time: {time}");
            ImGui.BulletText($"Time Zone: {timeZone}");

            var fps = io.Framerate;
            if (fps > 0.1f)
            {
                var ms = 1000f / fps;
                ImGui.BulletText($"FPS: {fps:0.0} ({ms:0.0} ms/frame)");
            }
            else
            {
                ImGui.BulletText("FPS: Unknown");
            }

            ImGui.BulletText($"Logged in: {VIWIContext.ClientState.IsLoggedIn}");

            ImGuiHelpers.ScaledDummy(10f);

            // ---------------------------
            // Loaded Modules
            // ---------------------------
            ImGui.TextUnformatted("Loaded Modules");
            ImGuiHelpers.ScaledDummy(4f);

            var remaining = ImGui.GetContentRegionAvail().Y;
            if (remaining < 80f * ImGuiHelpers.GlobalScale)
                remaining = 80f * ImGuiHelpers.GlobalScale;

            using (ImRaii.Child("##loaded_modules", new Vector2(0, remaining), true))
            {
                var orderedModules = DashboardRegistry.Pages
                    .Where(p => p.Category == "Modules")
                    .OrderBy(p => p.DisplayName);

                foreach (var page in orderedModules)
                {
                    var status = page.SupportsEnableToggle ? (page.IsEnabled ? "Enabled" : "Disabled") : "N/A";

                    ImGui.BulletText($"{page.DisplayName} (V{page.Version}) - {status}");
                }
            }
        }
    }
}
