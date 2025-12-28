using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation.NeoTaskManager;
using ECommons.Automation.UIInput;
using ECommons.ExcelServices;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using VIWI.Core;
using VIWI.Helpers;
using static VIWI.Core.VIWIContext;

namespace VIWI.Modules.AutoLogin
{
    internal unsafe class AutoLoginModule : VIWIModuleBase<AutoLoginConfig>
    {
        public const string ModuleName = "AutoLogin";
        public const string ModuleVersion = "1.0.3";
        public override string Name => ModuleName;
        public override string Version => ModuleVersion;
        public AutoLoginConfig _configuration => ModuleConfig;
        private VIWIConfig Core => CoreConfig;
        internal static AutoLoginModule? Instance { get; private set; }
        public static bool Enabled => Instance?._configuration.Enabled ?? false;

        protected override AutoLoginConfig CreateConfig() => new AutoLoginConfig();
        protected override AutoLoginConfig GetConfigBranch(VIWIConfig core) => core.AutoLogin;
        protected override void SetConfigBranch(VIWIConfig core, AutoLoginConfig _configuration) => core.AutoLogin = _configuration;
        protected override bool GetEnabled(AutoLoginConfig _configuration) => _configuration.Enabled;
        protected override void SetEnabledValue(AutoLoginConfig _configuration, bool enabled) => _configuration.Enabled = enabled;
        public void SaveConfig() => Core.Save();


        private readonly TaskManager taskManager = new();

        //internal IntPtr StartHandler;
        //internal IntPtr LoginHandler;
        internal IntPtr LobbyErrorHandler;
        private delegate Int64 StartHandlerDelegate(Int64 a1, Int64 a2);
        private delegate Int64 LoginHandlerDelegate(Int64 a1, Int64 a2);
        public delegate char LobbyErrorHandlerDelegate(Int64 a1, Int64 a2, Int64 a3);
        private Hook<LobbyErrorHandlerDelegate>? LobbyErrorHandlerHook;
        private bool noKillHookInitialized;

        private DateTime _lastErrorEpisode = DateTime.MinValue;
        private bool _inErrorRecovery = false;

        // ----------------------------
        // Module Base
        // ----------------------------
        public override void Initialize(VIWIConfig config)
        {
            Instance = this;
            base.Initialize(config);

            if (_configuration.Enabled)
                Enable();
        }

        public override void Enable()
        {
            NoKill();

            UpdateConfig();
            Framework.Update += OnFrameworkUpdate;
            ClientState.Login += OnLogin;
            ClientState.Logout += OnLogout;
            ClientState.TerritoryChanged += TerritoryChange;
        }

        public override void Disable()
        {
            Framework.Update -= OnFrameworkUpdate;
            ClientState.Login -= OnLogin;
            ClientState.Logout -= OnLogout;
            ClientState.TerritoryChanged -= TerritoryChange;

            taskManager.Abort();
            LobbyErrorHandlerHook?.Disable();
            LobbyErrorHandlerHook?.Dispose();
            LobbyErrorHandlerHook = null;
            noKillHookInitialized = false;
        }
        public override void Dispose()
        {
            Disable();

            try
            {
                Framework.Update -= OnFrameworkUpdate;
                ClientState.Login -= OnLogin;
                ClientState.Logout -= OnLogout;
                ClientState.TerritoryChanged -= TerritoryChange;
                taskManager.Dispose();
                if (LobbyErrorHandlerHook != null)
                {
                    if (LobbyErrorHandlerHook.IsEnabled)
                        LobbyErrorHandlerHook.Disable();

                    LobbyErrorHandlerHook.Dispose();
                    LobbyErrorHandlerHook = null;
                    noKillHookInitialized = false;

                    PluginLog.Information("[AutoLogin] LobbyErrorHandler hook disposed.");
                }
            }
            catch
            {
            }

            if (Instance == this)
                Instance = null;
            PluginLog.Information("[AutoLogin] Disposed.");
        }

        // ----------------------------
        // Core Logic
        // ----------------------------

        private void OnLogin()
        {
            PluginLog.Information("[AutoLogin] Successfully logged in, auto-login finished.");
            UpdateConfig();
            
        }
        private void OnLogout(int type, int code)
        {
            if (!_configuration.Enabled) return;

            if ((code == 90001 || code == 90002) || code == 90006 || code == 90007)
            {
                PluginLog.Information($"[AutoLogin] Disconnection Detected! Type {type}, Error Code {code}");

                if (_inErrorRecovery) return;

                _inErrorRecovery = true;
                _lastErrorEpisode = DateTime.UtcNow;

                taskManager.Abort();
                taskManager.Enqueue(ClearDisconnectErrors, "ClearDisconnectErrors");
                taskManager.Enqueue(ClearDisconnectErrors, "ClearDisconnectErrors"); //For some ungodly reason there is two windows here so we have to call this twice *only* on logouts???
                taskManager.Enqueue(() =>
                {
                    if (IsLobbyError2002Screen()) return false;
                    StartAutoLogin();
                    _inErrorRecovery = false;
                    return true;
                }, "StartAutoLogin");
            }
        }
        public void NoKill()
        {
            if (!_configuration.Enabled) return;
            if (noKillHookInitialized && LobbyErrorHandlerHook is { IsEnabled: true })
                return;

            LobbyErrorHandler = SigScanner.ScanText("40 53 48 83 EC 30 48 8B D9 49 8B C8 E8 ?? ?? ?? ?? 8B D0");
            LobbyErrorHandlerHook = HookProvider.HookFromAddress<LobbyErrorHandlerDelegate>(
                LobbyErrorHandler,
                LobbyErrorHandlerDetour);

            LobbyErrorHandlerHook.Enable();
            noKillHookInitialized = true;
        }
        private char LobbyErrorHandlerDetour(Int64 a1, Int64 a2, Int64 a3)
        {
            IntPtr p3 = new IntPtr(a3);
            var t1 = Marshal.ReadByte(p3);
            var v4 = ((t1 & 0xF) > 0) ? (uint)Marshal.ReadInt32(p3 + 8) : 0;
            UInt16 v4_16 = (UInt16)(v4);
            PluginLog.Debug($"LobbyErrorHandler a1:{a1} a2:{a2} a3:{a3} t1:{t1} v4:{v4_16}");

            if (!_configuration.Enabled)
                return LobbyErrorHandlerHook!.Original(a1, a2, a3);

            if (v4 > 0)
            {
                if (v4_16 == 0x332C && _configuration.SkipAuthError)
                {
                    PluginLog.Debug($"Skip Auth Error");
                }
                else
                {
                    Marshal.WriteInt64(p3 + 8, 0x3E80);
                    // 0x3390: maintenance
                    v4 = ((t1 & 0xF) > 0) ? (uint)Marshal.ReadInt32(p3 + 8) : 0;
                    v4_16 = (UInt16)(v4);
                }
            }
            PluginLog.Debug($"After LobbyErrorHandler a1:{a1} a2:{a2} a3:{a3} t1:{t1} v4:{v4_16}");
            return this.LobbyErrorHandlerHook!.Original(a1, a2, a3);
        }
        private void TerritoryChange(ushort obj)
        {
            UpdateConfig();
        }


        private void UpdateConfig()
        {
            var player = PlayerState;
            if (!ClientState.IsLoggedIn || player == null)
                return;

            if (player.CharacterName != _configuration.CharacterName || string.IsNullOrEmpty(_configuration.CharacterName))
            {
                _configuration.CharacterName = player.CharacterName;
            }

            if (player.HomeWorld.Value.Name.ExtractText() != _configuration.HomeWorldName || string.IsNullOrEmpty(_configuration.HomeWorldName))
            {
                _configuration.HomeWorldName = player.HomeWorld.Value.Name.ExtractText();
            }
            var worldSheet = DataManager.GetExcelSheet<World>();
            var worldRow = worldSheet?.GetRow(player.HomeWorld.RowId);
            if (worldRow != null)
            {
                _configuration.DataCenterID = (int)worldRow.Value.DataCenter.RowId;
                _configuration.DataCenterName = worldRow.Value.DataCenter.Value.Name.ExtractText();
            }


            _configuration.CurrentWorldName = player.CurrentWorld.Value.Name.ExtractText();
            _configuration.Visiting = !string.Equals(_configuration.CurrentWorldName, _configuration.HomeWorldName, StringComparison.Ordinal);            
            var cWorldRow = worldSheet?.GetRow(player.CurrentWorld.RowId);
            if (cWorldRow != null)
            {
                _configuration.vDataCenterID = (int)cWorldRow.Value.DataCenter.RowId;
                _configuration.vDataCenterName = cWorldRow.Value.DataCenter.Value.Name.ExtractText();
            }

            if (_configuration.HCMode)
            {
                _configuration.HCCurrentWorldName = _configuration.CurrentWorldName;
                _configuration.HCVisiting = _configuration.Visiting;
                _configuration.HCvDataCenterID = _configuration.vDataCenterID;
                _configuration.HCvDataCenterName = _configuration.vDataCenterName;
            }

            SaveConfig();
        }

        private void OnCommand(string command, string args)
        {
            _configuration.Enabled = !_configuration.Enabled;
            SaveConfig();
            PluginLog.Information($"[AutoLogin] Auto-login {(_configuration.Enabled ? "enabled" : "disabled")}.");
        }

        private void OnFrameworkUpdate(IFramework _)
        {
            if (!_configuration.Enabled) return;

            var errorVisible = IsLobbyError2002Screen();

            if (errorVisible && !_inErrorRecovery)
            {
                _inErrorRecovery = true;
                _lastErrorEpisode = DateTime.UtcNow;

                PluginLog.Warning("[AutoLogin] Lobby error detected (likely 2002). Switching to error clear + resume.");

                taskManager.Abort();
                taskManager.Enqueue(ClearDisconnectErrors, "ClearDisconnectErrors");
                taskManager.Enqueue(() =>
                {
                    if (IsLobbyError2002Screen()) return false;
                    StartAutoLogin();
                    _inErrorRecovery = false;
                    return true;
                }, "ResumeAutoLogin");

                return;
            }

            if (!errorVisible && _inErrorRecovery)
            {
                if ((DateTime.UtcNow - _lastErrorEpisode).TotalMilliseconds > 1000)
                    _inErrorRecovery = false;
            }

            if (taskManager.IsBusy) return;
        }

        public void StartAutoLogin()
        {
            if (!_configuration.Enabled) return;
            var hWorld = _configuration.HCMode ? _configuration.HCHomeWorldName : _configuration.HomeWorldName;
            var dc = _configuration.HCMode ? _configuration.HCDataCenterID : _configuration.DataCenterID;
            var chara = _configuration.HCMode ? _configuration.HCCharacterName : _configuration.CharacterName;
            var cWorld = _configuration.HCMode ? _configuration.HCCurrentWorldName : _configuration.CurrentWorldName;
            var visit = _configuration.HCMode ? _configuration.HCVisiting : _configuration.Visiting;

            taskManager.Enqueue(() => SelectDataCenterMenu(), "SelectDataCenterMenu");
            taskManager.Enqueue(() => SelectDataCenter(dc, cWorld), "SelectDataCenter");
            //taskManager.Enqueue(() => SelectWorldServer(hWorld), "SelectWorldServer");
            taskManager.Enqueue(() => SelectCharacter(chara, hWorld, cWorld, dc), "SelectCharacter");
            taskManager.Enqueue(() => ConfirmLogin(), "ConfirmLogin");
        }
        private bool HasLobbyErrorDialogue()
        {
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "Dialogue", out var d) && d->IsVisible) return true;

            return false;
        }
        private bool ClearDisconnectErrors()
        {
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "Dialogue", out var dialogue) && GenericHelpers.IsAddonReady(dialogue) && dialogue->IsVisible)
            {
                if (EzThrottler.Throttle("AutoLogin.Clear.DialogueOk", 800))
                {
                    var btn = dialogue->GetComponentButtonById(4);
                    if (btn != null)
                    {
                        btn->ClickAddonButton(dialogue);
                        PluginLog.Information("[AutoLogin] Clicking Dialogue OK");
                        return true;
                    }
                }

                return false;
            }

            return !HasLobbyErrorDialogue();
        }
        private bool SelectDataCenterMenu()  // Title Screen -> Selecting Data Center Menu
        {
            if (GuardAgainstErrors()) return false;
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "TitleDCWorldMap", out var dcMenu) && dcMenu->IsVisible)
            {
                PluginLog.Information("[AutoLogin] DC Selection Menu Visible");
                return true;
            }

            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "_TitleMenu", out var titleMenuAddon) && GenericHelpers.IsAddonReady(titleMenuAddon))
            {
                var menu = new AddonMaster._TitleMenu((void*)titleMenuAddon);

                if (menu.IsReady && EzThrottler.Throttle("TitleMenuThrottle", 100))
                {
                    PluginLog.Information("[AutoLogin] Title Screen => Starting Login Process");
                    menu.DataCenter();
                    return false;
                }
            }

            return false;
        }

        private bool SelectDataCenter(int dc, string currWorld)
        {
            if (GuardAgainstErrors()) return false;
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "_CharaSelectListMenu", out var charaMenu) && charaMenu->IsVisible)
            {
                PluginLog.Information("[AutoLogin] Character Selection Menu Visible");
                return true;
            }

            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "TitleDCWorldMap", out var dcMenuAddon) && GenericHelpers.IsAddonReady(dcMenuAddon))
            {
                var targetDc = dc;
                var worldSheet = DataManager.GetExcelSheet<World>();
                var visitingWorldRow = worldSheet?
                    .FirstOrDefault(row => row.Name.ExtractText()
                        .Equals(currWorld, StringComparison.Ordinal));

                if (visitingWorldRow != null)
                {
                    var visitingDc = (int)visitingWorldRow.Value.DataCenter.RowId;

                    if (visitingDc != 0 && visitingDc != dc)
                    {
                        PluginLog.Information(
                            $"[AutoLogin] World {currWorld} is on a different DC {visitingDc}. " +
                            $"Overriding home DC {dc}.");
                        targetDc = visitingDc;
                    }
                    else
                    {
                        PluginLog.Information(
                            $"[AutoLogin] World {currWorld} is on the same DC ({visitingDc}) " +
                            $"as home DC ({dc}); using home DC.");
                    }
                }

                var m = new AddonMaster.TitleDCWorldMap((void*)dcMenuAddon);
                if (EzThrottler.Throttle("SelectDCThrottle", 100))
                {
                    PluginLog.Information($"[AutoLogin] Selecting Data Center index {targetDc}");
                    m.Select(targetDc);
                    return false;
                }
            }

            return false;
        }
        /*private bool SelectWorldServer(string homeWorld)
        {
            if (TryGetAddonByName<AtkUnitBase>("_CharaSelectWorldServer", out var worldMenuAddon) && GenericHelpers.IsAddonReady(worldMenuAddon))
            {
                var mw = new AddonMaster._CharaSelectWorldServer((void*)worldMenuAddon);
                var targetWorld = mw.Worlds.FirstOrDefault(w => w.Name == homeWorld);
                if (targetWorld == null)
                    return false;

                if (EzThrottler.Throttle("SelectWorld", 150))
                {
                    PluginLog.Information($"[AutoLogin] Selecting world {targetWorld.Name}");
                    targetWorld.Select();
                    return true;
                }
            }
            return false;
        }*/

        private bool? SelectCharacter(string name, string homeWorld, string currWorld, int dc)
        {
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "SelectYesno", out _) || AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "SelectOk", out _))
                return true;

            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "_CharaSelectListMenu", out var charMenuAddon) && GenericHelpers.IsAddonReady(charMenuAddon))
            {
                var menu = new AddonMaster._CharaSelectListMenu((void*)charMenuAddon);
                var worldSheet = DataManager.GetExcelSheet<World>();
                var currWorldRow = worldSheet?
                    .FirstOrDefault(row =>
                        row.Name.ExtractText().Equals(currWorld, StringComparison.Ordinal));

                int currDc = currWorldRow != null ? (int)currWorldRow.Value.DataCenter.RowId : dc; 
                bool sameDc = currDc == dc;
                string targetWorld = sameDc ? homeWorld : currWorld;
                foreach (var c in menu.Characters)
                {
                    if (!string.Equals(c.Name, name, StringComparison.Ordinal))
                        continue;

                    var cHome = ExcelWorldHelper.GetName(c.HomeWorld);
                    var cCurr = ExcelWorldHelper.GetName(c.CurrentWorld);

                    if (!string.Equals(targetWorld, cHome, StringComparison.Ordinal) && !string.Equals(targetWorld, cCurr, StringComparison.Ordinal))
                        continue;
                    if (EzThrottler.Throttle("SelectChara", 150))
                    {
                        PluginLog.Information($"[AutoLogin] Logging in to world {ExcelWorldHelper.GetName(c.CurrentWorld)} as {c.Name}@{ExcelWorldHelper.GetName(c.HomeWorld)}");
                        c.Login();
                    }
                    return false;
                }
            }
        return false;
        }

        private bool? ConfirmLogin()
        {
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "SelectOk", out _)) return true;
            if (ClientState.IsLoggedIn) return true;

            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "SelectYesno", out var yesnoPtr) && GenericHelpers.IsAddonReady(yesnoPtr))
            {
                var m = new AddonMaster.SelectYesno((void*)yesnoPtr);
                if (m.Text.Contains("Log in", StringComparison.OrdinalIgnoreCase) || m.Text.Contains("Logging in", StringComparison.OrdinalIgnoreCase) || m.Text.Contains("last logged out", StringComparison.Ordinal))
                {
                    if (EzThrottler.Throttle("ConfirmLogin", 150))
                    {
                        PluginLog.Debug("[AutoLogin] Confirming login...");
                        m.Yes();
                    }
                }
                return false;
            }

            return false;
        }
        private bool GuardAgainstErrors()
        {
            if (IsLobbyError2002Screen())
            {
                PluginLog.Warning("[AutoLogin] Error dialog detected mid-login; returning to error clearing.");
                return true;
            }
            return false;
        }
        private unsafe bool IsLobbyError2002Screen()
        {
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, "Dialogue", out var dialogue) &&
                GenericHelpers.IsAddonReady(dialogue) && dialogue->IsVisible)
            {
                return true;
            }
            foreach (var name in new[] { "_TitleError", "TitleError", "TitleServerError", "TitleNetworkError" })
            {
                if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(GameGui, name, out var a) &&
                    GenericHelpers.IsAddonReady(a) && a->IsVisible)
                    return true;
            }

            return false;
        }
    }
}
