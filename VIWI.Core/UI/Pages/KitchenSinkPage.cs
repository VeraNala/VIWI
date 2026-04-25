using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using VIWI.Core;
using VIWI.Helpers;
using VIWI.Modules.KitchenSink;

namespace VIWI.UI.Pages
{
    public sealed class KitchenSinkPage : IDashboardPage
    {
        public string DisplayName => "KitchenSink";
        public string Category => "Modules";
        public string Version => KitchenSinkModule.ModuleVersion;
        public bool SupportsEnableToggle => true;
        public bool IsEnabled => KitchenSinkModule.Enabled;
        public void SetEnabled(bool value) => KitchenSinkModule.Instance?.SetEnabled(value);

        private bool _buffersInitialized;
        private string _charSearch = string.Empty;
        private readonly Dictionary<ulong, (string Name, string World)> _nameByCid = new();
        private DateTime _nextRefreshAt = DateTime.MinValue;

        public void Draw()
        {
            var module = KitchenSinkModule.Instance;
            var config = module?._configuration;

            if (config == null)
            {
                ImGui.TextDisabled("KitchenSink is not initialized yet.");
                return;
            }

            if (!_buffersInitialized)
            {
                _buffersInitialized = true;
                _charSearch = string.Empty;
                _nameByCid.Clear();
                _nextRefreshAt = DateTime.MinValue;
            }

            ImGuiHelpers.ScaledDummy(4f);

            ImGui.TextUnformatted($"KitchenSink - V{Version}");
            ImGui.SameLine();
            ImGui.TextColored(GradientColor.Get(ImGuiHelper.RainbowColorStart, ImGuiHelper.RainbowColorEnd, 500), "Yes, Everything Is Included!");

            ImGui.TextUnformatted("Enabled:");
            ImGui.SameLine();
            ImGui.TextColored(
                config.Enabled ? new Vector4(0.3f, 1f, 0.3f, 1f) : new Vector4(1f, 0.3f, 0.3f, 1f),
                config.Enabled ? "Yes" : "No - Click the OFF button to Enable KitchenSink!!"
            );

            ImGuiHelpers.ScaledDummy(4f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            ImGui.TextUnformatted("Description:");
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextWrapped(
                "KitchenSink is a collection of small utility tools, overlays, and QoL commands originally put together by Liza.\n" +
                "Character Switching helpers, Dropbox helpers, GlamourSet Tracking, OC Carrot Markers, and more!\n" +
                "Some features require specific plugins (e.g. AutoRetainer, Dropbox) to be installed."
            );

            ImGuiHelpers.ScaledDummy(8f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            DrawQuickStatus(module!);
            ImGuiHelpers.ScaledDummy(2f);

            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(4f);
            DrawWeaponIconsSection(config, module!);
            ImGuiHelpers.ScaledDummy(2f);
            DrawPerCharacterSection(config, module!);
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.Separator();

            DrawCommandsCheatsheet();
        }

        private void DrawQuickStatus(KitchenSinkModule module)
        {
            var loggedIn = VIWIContext.ClientState?.IsLoggedIn ?? false;

            ImGui.TextUnformatted("Status:");
            ImGui.SameLine();
            ImGui.TextColored(loggedIn ? new Vector4(0.3f, 1f, 0.3f, 1f) : new Vector4(1f, 0.75f, 0.3f, 1f), loggedIn ? "Logged in" : "Not logged in");

            var ar = module.GetAutoRetainer();
            bool arLoaded = IPCHelper.IsPluginLoaded("AutoRetainer");
            bool arReady = false;

            if (arLoaded)
            {
                try { arReady = ar!.Ready; } catch { arReady = false; }
            }

            ImGui.TextUnformatted("AutoRetainer:");
            ImGui.SameLine();
            ImGui.TextColored(arReady ? new Vector4(0.3f, 1f, 0.3f, 1f) : arLoaded ? new Vector4(1f, 0.75f, 0.3f, 1f) : new Vector4(1f, 0.3f, 0.3f, 1f), arReady ? "Ready" : arLoaded ? "Loaded (not ready)" : "Not loaded");
            ImGuiComponents.HelpMarker("AutoRetainer enables some character-aware features in KitchenSink:\n\n" +
                                        "• Character switching commands (/k+, /k-, /ks)\n" +
                                        "• DTR bar character index display\n" +
                                        "• Character storing for leve count indicators\n");
            bool dbLoaded = IPCHelper.IsPluginLoaded("Dropbox");
            bool dbReady = dbLoaded;

            ImGui.TextUnformatted("Dropbox:");
            ImGui.SameLine();
            ImGui.TextColored(dbReady ? new Vector4(0.3f, 1f, 0.3f, 1f) : dbLoaded ? new Vector4(1f, 0.75f, 0.3f, 1f) : new Vector4(1f, 0.3f, 0.3f, 1f), dbReady ? "Ready" : dbLoaded ? "Loaded (not ready)" : "Not loaded");
            ImGuiComponents.HelpMarker("Dropbox enables inventory and trade helpers via /dbq commands.");
        }

        private void DrawPerCharacterSection(KitchenSinkConfig config, KitchenSinkModule module)
        {
            if (!ImGui.CollapsingHeader("Per-character settings", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            ImGuiComponents.HelpMarker(
                "KitchenSink stores some options per character (by LocalContentId).\n" +
                "If you don't see your character here, log in once so KitchenSink can capture it."
            );

            ImGuiHelpers.ScaledDummy(2f);

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##ks_char_search", "Filter characters (name/world)", ref _charSearch, 128);

            var chars = config.Characters ?? new List<KitchenSinkConfig.CharacterData>();
            if (chars.Count == 0)
            {
                ImGui.TextDisabled("No character entries yet.");
                return;
            }

            RefreshNameCacheIfNeeded(chars, module);

            var filter = _charSearch?.Trim();
            var rows = new List<(KitchenSinkConfig.CharacterData Data, string Name, string World)>(chars.Count);

            foreach (var c in chars)
            {
                var cid = c.LocalContentId;

                if (!_nameByCid.TryGetValue(cid, out var nw))
                    continue;

                if (string.IsNullOrWhiteSpace(nw.Name) || string.IsNullOrWhiteSpace(nw.World))
                    continue;

                string name = nw.Name.Trim();
                string world = nw.World.Trim();

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    var blob = $"{name}@{world}";
                    if (!blob.Contains(filter!, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                rows.Add((c, name, world));
            }

            if (rows.Count == 0)
            {
                ImGui.TextDisabled("No matches.");
                return;
            }

            rows = rows
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.World, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ImGuiHelpers.ScaledDummy(2f);

            const ImGuiTableFlags flags =
                ImGuiTableFlags.RowBg |
                ImGuiTableFlags.BordersInnerH |
                ImGuiTableFlags.BordersInnerV |
                ImGuiTableFlags.SizingStretchProp |
                ImGuiTableFlags.NoSavedSettings;

            if (!ImGui.BeginTable("##ks_perchar_table", 3, flags))
                return;

            // Columns:
            // 0: Character
            // 1: Leves
            // 2: Delete
            ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##leves", ImGuiTableColumnFlags.WidthFixed, 60f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("##delete", ImGuiTableColumnFlags.WidthFixed, 36f * ImGuiHelpers.GlobalScale);
            ImGui.TableHeadersRow();

            ImGui.TableSetColumnIndex(1);

            float columnWidth = ImGui.GetColumnWidth();
            float iconWidth = ImGui.CalcTextSize($"{(char)FontAwesomeIcon.ClipboardList}").X;

            float offset = (columnWidth - iconWidth) * 0.5f;

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);

            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.TextUnformatted($"{(char)FontAwesomeIcon.ClipboardList}");
            ImGui.PopFont();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Warn character about Leve Allowances on Login");

            for (int i = 0; i < rows.Count; i++)
            {
                var (c, name, world) = rows[i];
                var cid = c.LocalContentId;

                ImGui.PushID((int)(cid % int.MaxValue));

                ImGui.TableNextRow();

                // --- Character column ---
                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(name);
                ImGui.SameLine();
                ImGui.TextDisabled($"@{world}");

                // --- Leves column ---
                ImGui.TableSetColumnIndex(1);
                bool warnLeves = c.WarnAboutLeves;

                var cellMin = ImGui.GetCursorScreenPos();
                float cellW = ImGui.GetContentRegionAvail().X;
                float cb = ImGui.GetFrameHeight();
                ImGui.SetCursorScreenPos(new Vector2(cellMin.X + (cellW - cb) * 0.5f, cellMin.Y));

                if (ImGui.Checkbox("##warn_leves", ref warnLeves))
                {
                    c.WarnAboutLeves = warnLeves;
                    module.SaveConfig();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Warn character about Leve Allowances on Login");

                // --- Delete column ---
                ImGui.TableSetColumnIndex(2);

                var dMin = ImGui.GetCursorScreenPos();
                float dW = ImGui.GetContentRegionAvail().X;
                float iconSize = ImGui.GetFrameHeight();
                ImGui.SetCursorScreenPos(new Vector2(dMin.X + (dW - iconSize) * 0.5f, dMin.Y));

                ImGui.PushStyleColor(ImGuiCol.Button, 0x66000000);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0x88FFFFFF);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xAAFFFFFF);

                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                {
                    config.Characters?.RemoveAll(x => x.LocalContentId == cid);
                    _nameByCid.Remove(cid);
                    module.SaveConfig();

                    ImGui.PopStyleColor(3);
                    ImGui.PopID();

                    break;
                }

                ImGui.PopStyleColor(3);

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Delete this character entry from KitchenSink");

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        private void RefreshNameCacheIfNeeded(List<KitchenSinkConfig.CharacterData> chars, KitchenSinkModule module)
        {
            var now = DateTime.UtcNow;
            if (now < _nextRefreshAt)
                return;

            _nextRefreshAt = now.AddSeconds(2);

            var ar = module.GetAutoRetainer();
            if (ar == null || !ar.IsLoaded)
                return;

            bool ready;
            try { ready = ar.Ready; }
            catch { return; }

            if (!ready)
                return;
            foreach (var ch in chars)
            {
                var cid = ch.LocalContentId;
                if (_nameByCid.ContainsKey(cid))
                    continue;

                var info = ar.GetOfflineCharacterInfo(cid);
                if (info != null && !string.IsNullOrWhiteSpace(info.Name) && !string.IsNullOrWhiteSpace(info.World))
                {
                    _nameByCid[cid] = (info.Name.Trim(), info.World.Trim());
                }
            }
        }
        private void DrawWeaponIconsSection(KitchenSinkConfig config, KitchenSinkModule module)
        {
            if (!ImGui.CollapsingHeader("Weapon Icons (Armoury Board Overlay)", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            ImGui.PushID("WeaponIconsSettings");

            bool enabled = config.WeaponIconsEnabled;
            if (ImGui.Checkbox("Enable Weapon Icons overlay", ref enabled))
            {
                config.WeaponIconsEnabled = enabled;
                module.SaveConfig();
            }
            ImGuiComponents.HelpMarker("Draws job/role icons over Armoury Board item slots.");

            bool mini = config.WeaponIconsMiniMode;
            if (ImGui.Checkbox("Mini mode (bottom-left icons)", ref mini))
            {
                config.WeaponIconsMiniMode = mini;
                module.SaveConfig();
            }
            ImGuiComponents.HelpMarker("Draws smaller icons anchored to the bottom-left of each Armoury slot.");

            bool requireCtrl = config.WeaponIconsRequireCtrl;
            if (ImGui.Checkbox("Require Ctrl key", ref requireCtrl))
            {
                config.WeaponIconsRequireCtrl = requireCtrl;
                module.SaveConfig();
            }
            ImGuiComponents.HelpMarker("When enabled, overlay only appears while holding Ctrl.");

        }
        private static void DrawCommandsCheatsheet()
        {
            ImGui.TextUnformatted("Commands:");
            ImGuiHelpers.ScaledDummy(4f);

            if (ImGui.BeginTable("KitchenSinkCommands", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);

                void SectionHeader(string text)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, 0x33000000);
                    ImGui.TextUnformatted(text);
                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted("");
                }

                void Row(string cmd, string desc)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(cmd);
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped(desc);
                }

                SectionHeader("Character Switcher");
                Row("/k+", "Switch to the next AR-enabled character.");
                Row("/k-", "Switch to the previous AR-enabled character.");
                Row("/ks [partialCharacterName]", "Switch to the first character with a matching name.");
                Row("/ks [world] [index]", "Switch to the Nth character on the specified world.");

                SectionHeader("Dropbox Queue");
                Row("/dbq item1:qty1 item2:qty2 …", "Queue items for the next trade (* = all).");
                Row("/dbq clear", "Remove all items from the queue.");
                Row("/dbq request …", "Generate a command to fill your inventory.");
                Row("/dbq [shards/crystals/shards+crystals]", "Generate a command to fill shards/crystals to 9999.");

                SectionHeader("Utilities");
                Row("/whatweather [n]", "Toggle weather overlay or set forecast length.");
                Row("/glamoursets", "Show the glamour set tracker.");
                Row("/bunbun", "Toggle OC Bunny overlay.");

                ImGui.EndTable();
            }
        }
    }
}
