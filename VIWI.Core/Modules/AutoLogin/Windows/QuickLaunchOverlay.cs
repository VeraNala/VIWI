using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using VIWI.Helpers;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.GroupPoseModule;
using static VIWI.Core.VIWIContext;

namespace VIWI.Modules.AutoLogin.Windows
{
    internal sealed unsafe class QuickLaunchOverlay : Window
    {
        private float _buttonWidth = 0f;
        private ISharedImmediateTexture? _viwiIcon;

        public QuickLaunchOverlay()
            : base("VIWI AutoLogin QuickLaunch##VIWI_AutoLoginQuickLaunch",
                  ImGuiWindowFlags.AlwaysAutoResize |
                  ImGuiWindowFlags.NoCollapse |
                  ImGuiWindowFlags.NoTitleBar |
                  ImGuiWindowFlags.NoFocusOnAppearing,
                  true)
        {
            RespectCloseHotkey = false;
            IsOpen = true;
            var imgPath = Path.Combine(PluginInterface.AssemblyLocation.DirectoryName!, "VIWI.png");
            _viwiIcon = TextureProvider.GetFromFileAbsolute(imgPath);
        }
        public override void PreDraw()
        {
            ImGui.SetNextWindowPos(new Vector2(30, 30), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowBgAlpha(0.85f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f * ImGuiHelpers.GlobalScale);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1.0f);
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 0f, 0.8f));
        }
        public override void PostDraw()
        {
            ImGui.PopStyleColor();
            ImGui.PopStyleVar(2);
        }
        public override bool DrawConditions() => IsOnTitleOrLoginScreens();

        public override void Draw()
        {
            _buttonWidth = 0f;

            var module = AutoLoginModule.Instance;
            var config = module?._configuration;
            if (module == null || config == null)
                return;

            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(0f, 0f),
                MaximumSize = new Vector2(float.MaxValue, ImGui.GetWindowViewport().Size.Y * 0.5f)
            };

            const string title = "VIWI - AutoLogin";

            float iconSize = 24f * ImGuiHelpers.GlobalScale;
            float spacing = ImGui.GetStyle().ItemSpacing.X;

            var textSize = ImGui.CalcTextSize(title);

            float totalWidth = textSize.X + spacing;
            if (_viwiIcon != null)
                totalWidth += iconSize;

            float avail = ImGui.GetContentRegionAvail().X;

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (avail - totalWidth) * 0.5f));

            if (_viwiIcon != null && _viwiIcon.TryGetWrap(out var wrap, out _))
            {
                ImGui.Image(wrap.Handle, new Vector2(iconSize, iconSize));
                ImGui.SameLine();
            }

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(title);

            ImGuiHelpers.ScaledDummy(3);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(3);

            if (module.IsAutoLoginRunning)
            {
                var snap = config.LastByRegion?
                    .GetValueOrDefault(config.CurrentRegion);

                if (snap != null && !string.IsNullOrWhiteSpace(snap.CharacterName))
                {
                    ImGui.TextDisabled($"Logging In: {snap.CharacterName}@{snap.HomeWorldName}");
                }
                else
                {
                    ImGui.TextDisabled("AutoLogin is running...");
                }

                ImGuiHelpers.ScaledDummy(3);
                float fullWidth = ImGui.GetContentRegionAvail().X;
                float height = ImGui.GetFrameHeight() * 1.25f;

                if (ImGui.Button("Stop AutoLogin", new Vector2(fullWidth, height)))
                {
                    module.StopAutoLogin();
                }

                return;
            }

            var regions = new[] { LoginRegion.NA, LoginRegion.EU, LoginRegion.OCE, LoginRegion.JP };

            config.LastByRegion ??= new();

            var entries = new List<(LoginRegion Region, LoginSnapshot Snap)>();

            foreach (var r in regions)
            {
                if (config.LastByRegion.TryGetValue(r, out var snap) &&
                    snap != null &&
                    !string.IsNullOrWhiteSpace(snap.CharacterName))
                {
                    entries.Add((r, snap));
                }
            }

            if (entries.Count == 0)
            {
                return;
            }

            foreach (var e in entries)
            {
                var label = $"{e.Snap.CharacterName}@{e.Snap.HomeWorldName}";
                var dim = ImGuiHelpers.GetButtonSize(label);

                if (dim.X > _buttonWidth)
                    _buttonWidth = dim.X;
            }

            float regionColumnWidth = ImGui.CalcTextSize("[OCE]  ").X;

            foreach (var e in entries)
            {
                var regionText = $"[{GetRegionShort(e.Region)}]";
                var label = $"{e.Snap.CharacterName}@{e.Snap.HomeWorldName}";

                var dim = ImGuiHelpers.GetButtonSize(label);

                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled(regionText);

                ImGui.SameLine(regionColumnWidth + ImGui.GetStyle().ItemSpacing.X);

                using (ImRaii.Disabled(module.IsBusyForQuickLaunch()))
                {
                    if (ImGui.Button(label, new Vector2(_buttonWidth * 1.25f, dim.Y * 1.2f)))
                    {
                        module.QuickLaunchToRegion(e.Region);
                    }
                }
            }
            bool canRestart = config.SkipAuthError && !string.IsNullOrWhiteSpace(config.ClientLaunchPath);
            bool ctrlHeld = ImGui.GetIO().KeyCtrl;
            float w = ImGui.GetContentRegionAvail().X;
            float h = ImGui.GetFrameHeight() * 1.15f;
            if (canRestart)
            {
                using (ImRaii.Disabled(!ctrlHeld))
                {
                    if (ImGui.Button("Restart Client", new Vector2(w, h)))
                    {
                        module.RequestClientRestart(config.CurrentRegion);
                    }
                }

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Hold CTRL while clicking to restart the client.");
                }
            }
        }
        private static string GetRegionShort(LoginRegion region)
        {
            return region switch
            {
                LoginRegion.NA => "NA",
                LoginRegion.EU => "EU",
                LoginRegion.JP => "JP",
                LoginRegion.OCE => "OCE",
                _ => "?"
            };
        }

        private static string BuildLabel(LoginRegion region, LoginSnapshot snap)
        {
            var baseText = $"{region}: {snap.CharacterName}@{snap.HomeWorldName}";
            if (snap.Visiting && !string.IsNullOrWhiteSpace(snap.CurrentWorldName))
                baseText += $" (→ {snap.CurrentWorldName})";
            return baseText;
        }

        private static bool IsOnTitleOrLoginScreens()
        {
            if (ClientState.IsLoggedIn) return false;
            foreach (var name in new[]
            {
                "_TitleMenu", "TitleMenu",
                "TitleDCWorldMap",
                "_CharaSelectListMenu",
                "_CharaSelectWorldServer",
                "SelectString",
            })
            {
                if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, name, out var a) &&
                    a != null &&
                    a->IsVisible &&
                    GenericHelpers.IsAddonReady(a))
                {
                    return true;
                }
            }
            return false;
        }
    }
}