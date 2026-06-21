using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.Automation.UIInput;
using ECommons.ExcelServices;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using VIWI.Core;
using VIWI.Helpers;
using VIWI.IPC;
using static VIWI.Core.VIWIContext;

namespace VIWI.Modules.AutoServiceSelect
{
    internal unsafe class AutoServiceSelectModule : VIWIModuleBase<AutoServiceSelectConfig>
    {
        public const string ModuleName = "AutoServiceSelect";
        public const string ModuleVersion = "1.0.0";
        public override string Name => ModuleName;
        public override string Version => ModuleVersion;
        public AutoServiceSelectConfig _configuration => ModuleConfig;
        private VIWIConfig Core => CoreConfig;

        internal static AutoServiceSelectModule? Instance { get; private set; }
        public static bool Enabled => Instance?._configuration.Enabled ?? false;

        protected override AutoServiceSelectConfig CreateConfig() => new AutoServiceSelectConfig();
        protected override AutoServiceSelectConfig GetConfigBranch(VIWIConfig core) => core.AutoServiceSelect;
        protected override void SetConfigBranch(VIWIConfig core, AutoServiceSelectConfig _configuration) => core.AutoServiceSelect = _configuration;
        protected override bool GetEnabled(AutoServiceSelectConfig _configuration) => _configuration.Enabled;
        protected override void SetEnabledValue(AutoServiceSelectConfig _configuration, bool enabled) => _configuration.Enabled = enabled;
        public void SaveConfig() => Core.Save();

        public override void Initialize(VIWIConfig config)
        {
            Instance = this;
            base.Initialize(config);

            if (_configuration.Enabled)
                Enable();
        }

        public override void Enable()
        {
            AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "SelectString", ServiceAccountSelectStringPostReceiveEvent);
        }

        public override void Disable()
        {
            AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, "SelectString", ServiceAccountSelectStringPostReceiveEvent);
        }

        private unsafe bool SelectServiceAccountIndex()
        {
            var idx = _configuration.ServiceAccountIndex-1;
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "TitleDCWorldMap", out var dc) && dc->IsVisible)
                return true;
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "_CharaSelectListMenu", out var chara) && chara->IsVisible)
                return true;

            if (!TryGetServiceAccountSelectString(out var sel, out var entryCount))
                return true;

            if (idx < 0 || idx >= entryCount)
            {
                PluginLog.Warning($"[AutoServiceSelect] Saved ServiceAccountIndex={idx} out of range (entries={entryCount}). Please manually select to refresh.");
                return false;
            }

            if (EzThrottler.Throttle("AutoServiceSelect.SelectServiceAccount", 100))
            {
                PluginLog.Information($"[AutoServiceSelect] Auto-selecting service account index {idx}");
                sel->AtkUnitBase.FireCallbackInt(idx);
            }
            return false;
        }
        private unsafe bool TryGetServiceAccountSelectString(out AddonSelectString* sel, out int entryCount)
        {
            sel = null;
            entryCount = 0;

            if (!AddonHelpers.TryGetAddonByName<AddonSelectString>(GameGui, "SelectString", out var s))
                return false;
            PluginLog.Debug("Passed Getting Addon Name");
            if (!AddonState.IsAddonReady(&s->AtkUnitBase) || !s->AtkUnitBase.IsVisible)
                return false;
            PluginLog.Debug("Passed Addon Ready");
            var m = new AddonMaster.SelectString((void*)s);
            if (!IsServiceAccountPromptText(m.Text))
                return false;

            var popup = s->PopupMenu.PopupMenu;
            entryCount = popup.EntryCount;
            if (entryCount <= 0)
                return false;

            sel = s;
            return true;
        }
        private bool IsServiceAccountPromptText(string text)
        {
            var compareTo = DataManager.GetExcelSheet<Lobby>()?.GetRow(11).Text.ExtractText();
            return !string.IsNullOrEmpty(compareTo) && string.Equals(text, compareTo, StringComparison.Ordinal);
        }
        private void ServiceAccountSelectStringPostReceiveEvent(AddonEvent type, AddonArgs args)
        {    
            try
            {
                if (!_configuration.Enabled) return;
                if (!TryGetServiceAccountSelectString(out _, out var entryCount))
                    return;

                SelectServiceAccountIndex();
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "[AutoServiceSelect] ServiceAccountSelectStringPostReceiveEvent failed.");
            }
        }
    }
}