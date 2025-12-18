using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation.NeoTaskManager;
using ECommons.Automation.UIInput;
using ECommons.ExcelServices;
using ECommons.Logging;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using VIWI.Core;
using VIWI.Helpers;

namespace VIWI.Modules.AutoLogin
{
    internal unsafe class AutoLoginModule : IVIWIModule
    {
        public const string ModuleName = "AutoLogin";
        public const string ModuleVersion = "1.0.2";

        public string Name => ModuleName;
        public string Version => ModuleVersion;

        internal static AutoLoginModule? Instance { get; private set; }
        public static AutoLoginConfig Config { get; private set; } = null!;
        public static bool Enabled => Config?.Enabled ?? false;

        private readonly ICommandManager commandManager;
        private readonly IDataManager dataManager;
        private readonly IFramework framework;
        private readonly IClientState clientState;
        private readonly IPlayerState playerState;
        private readonly IGameGui gameGui;
        private readonly ISigScanner sigScanner;
        private readonly IGameInteropProvider hookProvider;

        private static readonly TaskManager TaskManager = new();

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
        // Construction / init
        // ----------------------------

        public AutoLoginModule()
        {
            commandManager = VIWIContext.CommandManager;
            dataManager = VIWIContext.DataManager;
            framework = VIWIContext.Framework;
            clientState = VIWIContext.ClientState;
            playerState = VIWIContext.PlayerState;
            gameGui = VIWIContext.GameGui;
            sigScanner = VIWIContext.SigScanner;
            hookProvider = VIWIContext.HookProvider;
        }
        // ----------------------------
        // Skeleton Loop
        // ----------------------------

        public void Initialize()
        {
            Instance = this;
            LoadConfig();

            if (Config.Enabled)
                Enable();

            PluginLog.Information("[AutoLogin] Module initialized.");
        }
        private void Enable()
        {
            NoKill();

            UpdateConfig();
            framework.Update += OnFrameworkUpdate;
            clientState.Login += OnLogin;
            clientState.Logout += OnLogout;
            clientState.TerritoryChanged += TerritoryChange;
        }

        private void Disable()
        {
            framework.Update -= OnFrameworkUpdate;
            clientState.Login -= OnLogin;
            clientState.Logout -= OnLogout;
            clientState.TerritoryChanged -= TerritoryChange;

            TaskManager.Dispose();
        }

        public static void SetEnabled(bool value)
        {
            if (Config == null) return;

            Config.Enabled = value;
            SaveConfig();

            if (value)
                Instance?.Enable();
            else
                Instance?.Disable();
        }

        // ----------------------------
        // Config Handlers
        // ----------------------------


        public static void LoadConfig()
        {
            Config = VIWIContext.PluginInterface.GetPluginConfig() as AutoLoginConfig
                     ?? new AutoLoginConfig();

            SaveConfig();
        }

        public static void SaveConfig()
        {
            Config?.Save();
        }

        // ----------------------------
        // Commands & Events
        // ----------------------------

        private void OnLogin()
        {
            PluginLog.Information("[AutoLogin] Successfully logged in, auto-login finished.");
            UpdateConfig();
            
        }
        private void OnLogout(int type, int code)
        {
            if (!Config.Enabled) return;

            if ((code == 90001 || code == 90002) || code == 90006 || code == 90007)
            {
                PluginLog.Information($"[AutoLogin] Disconnection Detected! Type {type}, Error Code {code}");

                if (_inErrorRecovery) return;

                _inErrorRecovery = true;
                _lastErrorEpisode = DateTime.UtcNow;

                TaskManager.Abort();
                TaskManager.Enqueue(ClearDisconnectErrors, "ClearDisconnectErrors");
                TaskManager.Enqueue(ClearDisconnectErrors, "ClearDisconnectErrors"); //For some ungodly reason there is two windows here so we have to call this twice *only* on logouts???
                TaskManager.Enqueue(() =>
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
            if (!Config.Enabled) return;
            if (noKillHookInitialized && LobbyErrorHandlerHook is { IsEnabled: true })
                return;

            LobbyErrorHandler = sigScanner.ScanText("40 53 48 83 EC 30 48 8B D9 49 8B C8 E8 ?? ?? ?? ?? 8B D0");
            LobbyErrorHandlerHook = hookProvider.HookFromAddress<LobbyErrorHandlerDelegate>(
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

            if (!Config.Enabled)
                return LobbyErrorHandlerHook!.Original(a1, a2, a3);

            if (v4 > 0)
            {
                if (v4_16 == 0x332C && Config.SkipAuthError)
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
            return this.LobbyErrorHandlerHook.Original(a1, a2, a3);
        }
        private void TerritoryChange(ushort obj)
        {
            UpdateConfig();
        }


        private void UpdateConfig()
        {
            var player = playerState;
            if (!clientState.IsLoggedIn || player == null)
                return;

            if (player.CharacterName != Config.CharacterName || string.IsNullOrEmpty(Config.CharacterName))
            {
                Config.CharacterName = player.CharacterName;
            }

            if (player.HomeWorld.Value.Name.ExtractText() != Config.HomeWorldName || string.IsNullOrEmpty(Config.HomeWorldName))
            {
                Config.HomeWorldName = player.HomeWorld.Value.Name.ExtractText();
            }
            var worldSheet = dataManager.GetExcelSheet<World>();
            var worldRow = worldSheet?.GetRow(player.HomeWorld.RowId);
            if (worldRow != null)
            {
                Config.DataCenterID = (int)worldRow.Value.DataCenter.RowId;
                Config.DataCenterName = worldRow.Value.DataCenter.Value.Name.ExtractText();
            }


            Config.CurrentWorldName = player.CurrentWorld.Value.Name.ExtractText();
            Config.Visiting = !string.Equals(Config.CurrentWorldName, Config.HomeWorldName, StringComparison.Ordinal);            
            var cWorldRow = worldSheet?.GetRow(player.CurrentWorld.RowId);
            if (cWorldRow != null)
            {
                Config.vDataCenterID = (int)cWorldRow.Value.DataCenter.RowId;
                Config.vDataCenterName = cWorldRow.Value.DataCenter.Value.Name.ExtractText();
            }

            if (Config.HCMode)
            {
                Config.HCCurrentWorldName = Config.CurrentWorldName;
                Config.HCVisiting = Config.Visiting;
                Config.HCvDataCenterID = Config.vDataCenterID;
                Config.HCvDataCenterName = Config.vDataCenterName;
            }

            SaveConfig();
        }

        private void OnCommand(string command, string args)
        {
            Config.Enabled = !Config.Enabled;
            SaveConfig();
            PluginLog.Information($"[AutoLogin] Auto-login {(Config.Enabled ? "enabled" : "disabled")}.");
        }

        private void OnFrameworkUpdate(IFramework _)
        {
            if (!Config.Enabled) return;

            var errorVisible = IsLobbyError2002Screen();

            if (errorVisible && !_inErrorRecovery)
            {
                _inErrorRecovery = true;
                _lastErrorEpisode = DateTime.UtcNow;

                PluginLog.Warning("[AutoLogin] Lobby error detected (likely 2002). Switching to error clear + resume.");

                TaskManager.Abort();
                TaskManager.Enqueue(ClearDisconnectErrors, "ClearDisconnectErrors");
                TaskManager.Enqueue(() =>
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

            if (TaskManager.IsBusy) return;
        }

        // ----------------------------
        // Core logic
        // ----------------------------

        public void StartAutoLogin()
        {
            if (!Config.Enabled) return;
            var hWorld = Config.HCMode ? Config.HCHomeWorldName : Config.HomeWorldName;
            var dc = Config.HCMode ? Config.HCDataCenterID : Config.DataCenterID;
            var chara = Config.HCMode ? Config.HCCharacterName : Config.CharacterName;
            var cWorld = Config.HCMode ? Config.HCCurrentWorldName : Config.CurrentWorldName;
            var visit = Config.HCMode ? Config.HCVisiting : Config.Visiting;

            TaskManager.Enqueue(() => SelectDataCenterMenu(), "SelectDataCenterMenu");
            TaskManager.Enqueue(() => SelectDataCenter(dc, cWorld), "SelectDataCenter");
            //TaskManager.Enqueue(() => SelectWorldServer(hWorld), "SelectWorldServer");
            TaskManager.Enqueue(() => SelectCharacter(chara, hWorld, cWorld, dc), "SelectCharacter");
            TaskManager.Enqueue(() => ConfirmLogin(), "ConfirmLogin");
        }
        private bool HasLobbyErrorDialogue()
        {
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(gameGui, "Dialogue", out var d) && d->IsVisible) return true;

            return false;
        }
        private bool ClearDisconnectErrors()
        {
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(gameGui, "Dialogue", out var dialogue) && GenericHelpers.IsAddonReady(dialogue) && dialogue->IsVisible)
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
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(gameGui, "TitleDCWorldMap", out var dcMenu) && dcMenu->IsVisible)
            {
                PluginLog.Information("[AutoLogin] DC Selection Menu Visible");
                return true;
            }

            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(gameGui, "_TitleMenu", out var titleMenuAddon) && GenericHelpers.IsAddonReady(titleMenuAddon))
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
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(gameGui, "_CharaSelectListMenu", out var charaMenu) && charaMenu->IsVisible)
            {
                PluginLog.Information("[AutoLogin] Character Selection Menu Visible");
                return true;
            }

            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(gameGui, "TitleDCWorldMap", out var dcMenuAddon) && GenericHelpers.IsAddonReady(dcMenuAddon))
            {
                var targetDc = dc;
                var worldSheet = dataManager.GetExcelSheet<World>();
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
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(gameGui, "SelectYesno", out _) || AddonHelpers.TryGetAddonByName<AtkUnitBase>(gameGui, "SelectOk", out _))
                return true;

            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(gameGui, "_CharaSelectListMenu", out var charMenuAddon) && GenericHelpers.IsAddonReady(charMenuAddon))
            {
                var menu = new AddonMaster._CharaSelectListMenu((void*)charMenuAddon);
                var worldSheet = dataManager.GetExcelSheet<World>();
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
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(gameGui, "SelectOk", out _)) return true;
            if (clientState.IsLoggedIn) return true;

            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(gameGui, "SelectYesno", out var yesnoPtr) && GenericHelpers.IsAddonReady(yesnoPtr))
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
            if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(gameGui, "Dialogue", out var dialogue) &&
                GenericHelpers.IsAddonReady(dialogue) && dialogue->IsVisible)
            {
                return true;
            }
            foreach (var name in new[] { "_TitleError", "TitleError", "TitleServerError", "TitleNetworkError" })
            {
                if (AddonHelpers.TryGetAddonByName<AtkUnitBase>(gameGui, name, out var a) &&
                    GenericHelpers.IsAddonReady(a) && a->IsVisible)
                    return true;
            }

            return false;
        }

        // ----------------------------
        // Utilities
        // ----------------------------

        public void Dispose()
        {
            Disable();

            try
            {
                framework.Update -= OnFrameworkUpdate;
                clientState.Login -= OnLogin;
                clientState.Logout -= OnLogout;
                clientState.TerritoryChanged -= TerritoryChange;
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
    }
}
