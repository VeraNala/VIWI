using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using VIWI.Core;
using VIWI.UI.Pages;

namespace VIWI.UI.Windows
{
    public sealed class MainDashboardWindow : Window
    {
        private IDashboardPage? activePage;
        private readonly VIWIConfig _config;
        private const string DonationUrl = "https://ko-fi.com/veralynnala";
        private ISharedImmediateTexture? _sidebarImage;

        public MainDashboardWindow(VIWIConfig config)
            : base("VIWI - Vera's Integrated World Improvements##VIWI Dashboard",
                  ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            _config = config;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(600, 600),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
            };
            var imgPath = Path.Combine(VIWIContext.PluginInterface.AssemblyLocation.DirectoryName!, "VIWI.png");
            _sidebarImage = VIWIContext.TextureProvider.GetFromFileAbsolute(imgPath);

            DashboardRegistry.Register(new OverviewDashboardPage());
            //DashboardRegistry.Register(new AoEasyPage());
            DashboardRegistry.Register(new AutoLoginPage());
            //DashboardRegistry.Register(new ViwiwiPage());
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
            if (_sidebarImage != null)
            {
                if (_sidebarImage.TryGetWrap(out IDalamudTextureWrap? wrap, out Exception? ex) && wrap != null)
                {
                    float availableWidth = ImGui.GetContentRegionAvail().X;
                    float maxHeight = 100f * ImGuiHelpers.GlobalScale;

                    float aspect = (float)wrap.Width / wrap.Height;
                    float height = Math.Min(maxHeight, wrap.Height);
                    float width = height * aspect;
                    float cursorX = ImGui.GetCursorPosX();
                    float centeredX = cursorX + (availableWidth - width) * 0.5f;
                    ImGui.SetCursorPosX(centeredX);

                    ImGui.Image(wrap.Handle, new Vector2(width, height));

                    ImGuiHelpers.ScaledDummy(8f);
                    ImGui.Separator();
                    ImGuiHelpers.ScaledDummy(6f);
                }
            }

            float footerHeight = 48f * ImGuiHelpers.GlobalScale;
            float sidebarHeight = ImGui.GetContentRegionAvail().Y;

            using (ImRaii.Child("##viwi_sidebar_content", new Vector2(0, sidebarHeight - footerHeight), false))
            {
                var overview = DashboardRegistry.Pages.FirstOrDefault(p => p.DisplayName == "Overview");
                if (overview != null)
                {
                    DrawPageEntry(overview);
                    DrawDonateButton();

                    ImGuiHelpers.ScaledDummy(6f);
                    ImGui.Separator();
                    ImGuiHelpers.ScaledDummy(6f);
                }

                var grouped = DashboardRegistry.Pages
                    .Where(p => !ReferenceEquals(p, overview))
                    .GroupBy(p => p.Category)
                    .OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    ImGui.TextDisabled(group.Key);
                    ImGuiHelpers.ScaledDummy(1f);

                    foreach (var page in group.OrderBy(p => p.DisplayName))
                        DrawPageEntry(page);

                    ImGuiHelpers.ScaledDummy(6f);
                    ImGui.Separator();
                    ImGuiHelpers.ScaledDummy(6f);
                }
            }

            ImGui.Separator();
            using (ImRaii.Child("##viwi_sidebar_footer", new Vector2(0, 0), false))
            {
                DrawPasskeyField();
            }
        }
        private void DrawPageEntry(IDashboardPage page)
        {
            bool isActive = ReferenceEquals(page, activePage);

            ImGui.PushID(page.DisplayName);

            float totalWidth = ImGui.GetContentRegionAvail().X;
            bool hasToggle = page.SupportsEnableToggle;
            float toggleWidth = hasToggle ? 60f * ImGuiHelpers.GlobalScale : 0f;
            float spacing = hasToggle ? 4f * ImGuiHelpers.GlobalScale : 0f;
            float epsilon = 2f * ImGuiHelpers.GlobalScale;
            float mainButtonWidth = hasToggle ? MathF.Max(10f * ImGuiHelpers.GlobalScale, totalWidth - toggleWidth - spacing - epsilon) : totalWidth;

            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 14f * ImGuiHelpers.GlobalScale);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(14f, 7f) * ImGuiHelpers.GlobalScale);
            using (ImRaii.PushColor(ImGuiCol.Button, isActive ? 0xFFF5652Du : 0xFFAC5F41u))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, isActive ? 0xFFFFA26Du : 0xFFD0896Du))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, isActive ? 0xFFFFA26Du : 0xFFD0896Du))
            {
                if (ImGui.Button(page.DisplayName, new Vector2(mainButtonWidth, 0)))
                { 
                activePage = page;
                }
            }
            ImGui.PopStyleVar(2);

            if (hasToggle)
            {
                float rightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
                float toggleX = rightEdge - toggleWidth;

                ImGui.SameLine(0, spacing);
                ImGui.SetCursorPosX(toggleX);

                bool enabled = page.IsEnabled;
                string label = enabled ? "ON" : "OFF";

                uint colBase = enabled ? 0xFF27AE60u : 0xFF7F8C8Du;
                uint colHovered = enabled ? 0xFF2ECC71u : 0xFF95A5A6u;
                uint colActive = enabled ? 0xFF1E8449u : 0xFF7F8C8Du;

                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 14f * ImGuiHelpers.GlobalScale);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(14f, 7f) * ImGuiHelpers.GlobalScale);
                using (ImRaii.PushColor(ImGuiCol.Button, colBase))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, colHovered))
                using (ImRaii.PushColor(ImGuiCol.ButtonActive, colActive))

                if (ImGui.Button(label, new Vector2(toggleWidth, 0)))
                {
                    page.SetEnabled(!enabled);
                }
                
                ImGui.PopStyleVar(2);
            }

            ImGui.PopID();
            ImGuiHelpers.ScaledDummy(2f);
        }
        private void DrawDonateButton()
        {
            float totalWidth = ImGui.GetContentRegionAvail().X;

            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 14f * ImGuiHelpers.GlobalScale);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(14f, 7f) * ImGuiHelpers.GlobalScale);

            using (ImRaii.PushColor(ImGuiCol.Button, 0xFFE8AFC9u))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, 0xFFDD94B8u))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, 0xFFD07FAAu))
            {
                if (ImGui.Button("♥ Donate ♥", new Vector2(totalWidth, 0)))
                    Util.OpenLink(DonationUrl);
            }
            ImGui.PopStyleVar(2);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Support Vera and the development of VIWI ♥\nOpens Kofi in your browser.");
        }

        private string _passkeyInput = string.Empty;
        private string _passkeyStatus = string.Empty;
        private const string ExpectedPasskeyHash = "0d2ca3afc299c9adec3c2e3bb52b229c768a52051f20f87f2ad81c1b329fda0a";
        private Vector4 _passkeyStatusColor = Vector4.One;
        private void DrawPasskeyField()
        {
            ImGuiHelpers.ScaledDummy(4f);

            var hint = string.IsNullOrEmpty(_passkeyStatus) ? "Passkey" : _passkeyStatus;

            if (!string.IsNullOrEmpty(_passkeyStatus))
                ImGui.PushStyleColor(ImGuiCol.TextDisabled, _passkeyStatusColor);

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            bool submitted = ImGui.InputTextWithHint("##viwi_passkey", hint, ref _passkeyInput, 128, ImGuiInputTextFlags.EnterReturnsTrue);

            if (!string.IsNullOrEmpty(_passkeyStatus))
                ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Press Enter to submit.");
            if (!string.IsNullOrEmpty(_passkeyStatus) && ImGui.IsItemActive() && ImGui.IsItemEdited())
            {
                _passkeyStatus = string.Empty;
                _passkeyStatusColor = Vector4.One;
            }
            if (submitted)
            {
                var attempt = _passkeyInput;
                _passkeyInput = string.Empty;

                if (IsPasskeyValid(attempt))
                {
                    _passkeyStatus = "Unlocked!";
                    _passkeyStatusColor = new Vector4(0.3f, 1f, 0.3f, 1f);
                    _config.Unlocked = true;
                    _config.Save();
                }
                else
                {
                    _passkeyStatus = "Incorrect passkey.";
                    _passkeyStatusColor = new Vector4(1f, 0.3f, 0.3f, 1f);
                }
            }
            //For Testing
            /*if (_config.FeaturesUnlocked && ImGui.Button("Lock"))
            {
                _config.FeaturesUnlocked = false;
                _config.Save();

                _passkeyStatus = "Locked.";
                _passkeyStatusColor = new Vector4(0.7f, 0.7f, 0.7f, 1f);
            }*/

            ImGuiHelpers.ScaledDummy(4f);
        }
        private static bool IsPasskeyValid(string attempt)
        {
            var normalized = attempt.Trim().ToLowerInvariant();
            var hash = ComputeSha256(normalized);
            return hash == ExpectedPasskeyHash;
        }
        private static string ComputeSha256(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}