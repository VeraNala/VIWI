using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using VIWI.Core;
using VIWI.Modules.Workshoppa.External;
using VIWI.Modules.Workshoppa.GameData;
using VIWI.Modules.Workshoppa.Windows;
using static VIWI.Core.VIWIContext;
using static VIWI.Modules.Workshoppa.WorkshoppaConfig;

namespace VIWI.Modules.Workshoppa;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
internal sealed partial class WorkshoppaModule : VIWIModuleBase<WorkshoppaConfig>
{
    public const string ModuleName = "Workshoppa";
    public const string ModuleVersion = "1.1.0";
    public override string Name => ModuleName;
    public override string Version => ModuleVersion;
    public WorkshoppaConfig _configuration => ModuleConfig;
    private VIWIConfig Core => CoreConfig;
    internal static WorkshoppaModule? Instance { get; private set; }
    public static bool Enabled => Instance?._configuration?.Enabled ?? false;

    protected override WorkshoppaConfig CreateConfig() => new WorkshoppaConfig();
    protected override WorkshoppaConfig GetConfigBranch(VIWIConfig core) => core.Workshoppa;
    protected override void SetConfigBranch(VIWIConfig core, WorkshoppaConfig _configuration) => core.Workshoppa = _configuration;
    protected override bool GetEnabled(WorkshoppaConfig _configuration) => _configuration.Enabled;
    protected override void SetEnabledValue(WorkshoppaConfig _configuration, bool enabled) => _configuration.Enabled = enabled;
    public void SaveConfig() => Core.Save();

    // ---- State / systems ----
    private readonly IReadOnlyList<uint> _fabricationStationIds =
        new uint[] { 2005236, 2005238, 2005240, 2007821, 2011588 }.AsReadOnly();

    internal readonly IReadOnlyList<ushort> WorkshopTerritories =
        new ushort[] { 423, 424, 425, 653, 984 }.AsReadOnly();

    private ExternalPluginHandler _externalPluginHandler = null!;
    private WorkshopCache _workshopCache = null!;
    private GameStrings _gameStrings = null!;

    private WorkshoppaWindow _mainWindow = null!;
    private WorkshoppaRepairKitWindow _repairKitWindow = null!;
    private WorkshoppaCeruleumTankWindow _ceruleumTankWindow = null!;
    private WorkshoppaGrindstoneShopWindow _grindstoneShopWindow = null!;

    private Stage _currentStageInternal = Stage.Stopped;
    private DateTime _continueAt = DateTime.MinValue;
    private DateTime _fallbackAt = DateTime.MaxValue;

    private StackMergeRunner? _mergeRunner;
    private bool _mergePending;
    private uint _mergeItemId;
    private uint _mergeRequired;
    private string _mergeItemName = "";
    private int _mergeAttempts;
    private const int MaxMergeAttempts = 2;
    private bool _mergePassInFlight;

    private int _stallTicks;
    private const int MaxStallTicks = 2000;
    private Stage _lastStage;
    private DateTime _lastProgressAt = DateTime.MinValue;
    private static readonly TimeSpan MaxNoProgress = TimeSpan.FromSeconds(15);

    public override void Initialize(VIWIConfig config)
    {
        Instance = this;
        base.Initialize(config);
        _externalPluginHandler = new ExternalPluginHandler(PluginInterface, PluginLog);
        _workshopCache = new WorkshopCache(DataManager, PluginLog);
        _gameStrings = new(DataManager, PluginLog);

        _mainWindow = new WorkshoppaWindow(this, ClientState, _configuration, _workshopCache, new IconCache(TextureProvider), ChatGui, new RecipeTree(DataManager, PluginLog), PluginLog);
        CorePlugin.WindowSystem.AddWindow(_mainWindow);
        _repairKitWindow = new(PluginLog, GameGui, AddonLifecycle, _configuration, _externalPluginHandler);
        CorePlugin.WindowSystem.AddWindow(_repairKitWindow);
        _ceruleumTankWindow = new(PluginLog, GameGui, AddonLifecycle, _configuration, _externalPluginHandler, ChatGui);
        CorePlugin.WindowSystem.AddWindow(_ceruleumTankWindow);
        _grindstoneShopWindow = new(PluginLog, GameGui, AddonLifecycle, _configuration, _externalPluginHandler, ChatGui);
        CorePlugin.WindowSystem.AddWindow(_grindstoneShopWindow);

        if (_configuration.Enabled) 
            Enable();
    }
    public override void Enable()
    {
        Framework.Update += OnFrameworkUpdate;

        CommandManager.AddHandler("/ws", new CommandInfo(ProcessCommand) { ShowInHelp = false });
        CommandManager.AddHandler("/workshoppa", new CommandInfo(ProcessCommand) { HelpMessage = "Open Workshoppa UI" });
        CommandManager.AddHandler("/buy-tanks", new CommandInfo(ProcessFuelBuyCommand) { ShowInHelp = false });
        CommandManager.AddHandler("/fill-tanks", new CommandInfo(ProcessFuelFillCommand) { ShowInHelp = false });
        CommandManager.AddHandler("/buy-stone", new CommandInfo(ProcessStoneBuyCommand) { ShowInHelp = false });
        CommandManager.AddHandler("/fill-stone", new CommandInfo(ProcessStoneFillCommand) { ShowInHelp = false });
        CommandManager.AddHandler("/grindstone", new CommandInfo(ProcessLevelingCommand) { ShowInHelp = false });
        CommandManager.AddHandler("/g6dm", new CommandInfo(ProcessDarkMatterCommand) { ShowInHelp = false });

        _repairKitWindow?.EnableShopListeners();
        _ceruleumTankWindow?.EnableShopListeners();
        _grindstoneShopWindow?.EnableShopListeners();
        _mergeRunner = new StackMergeRunner(Svc.Framework, canOperate: () => Svc.ClientState.IsLoggedIn, moveDelayMs: 175);

        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoPostSetup);
        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Request", RequestPostSetup);
        AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "Request", RequestPostRefresh);
        AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "ContextIconMenu", ContextIconMenuPostReceiveEvent);
    }

    public override void Disable()
    {
        _repairKitWindow?.DisableShopListeners();
        _ceruleumTankWindow?.DisableShopListeners();
        _grindstoneShopWindow?.DisableShopListeners();
        _mergeRunner?.Dispose();
        AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "ContextIconMenu", ContextIconMenuPostReceiveEvent);
        AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, "Request", RequestPostRefresh);
        AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Request", RequestPostSetup);
        AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoPostSetup);

        CommandManager.RemoveHandler("/g6dm");
        CommandManager.RemoveHandler("/grindstone");
        CommandManager.RemoveHandler("/fill-stone");
        CommandManager.RemoveHandler("/buy-stone");
        CommandManager.RemoveHandler("/fill-tanks");
        CommandManager.RemoveHandler("/buy-tanks");
        CommandManager.RemoveHandler("/workshoppa");
        CommandManager.RemoveHandler("/ws");

        Framework.Update -= OnFrameworkUpdate;

        if (CurrentStage != Stage.Stopped)
        {
            _externalPluginHandler?.RestoreTextAdvance();
            _externalPluginHandler?.Restore();
            CurrentStage = Stage.Stopped;
        }

        if (_mainWindow != null && _mainWindow.IsOpen) _mainWindow.IsOpen = false;
        if (_repairKitWindow != null) _repairKitWindow.IsOpen = false;
        if (_ceruleumTankWindow != null) _ceruleumTankWindow.IsOpen = false;
        if (_grindstoneShopWindow != null) _grindstoneShopWindow.IsOpen = false;
    }

    public override void Dispose()
    {
        Disable();

        try
        {
            if (_ceruleumTankWindow != null) _ceruleumTankWindow.Dispose();
            if (_repairKitWindow != null) _repairKitWindow.Dispose();
            if (_grindstoneShopWindow != null) _grindstoneShopWindow.Dispose();
        }
        catch (Exception)
        {
            PluginLog.Error("[Workshoppa] Dispose failed.");
        }
        finally
        {
            if (Instance == this) Instance = null;
            PluginLog.Information("[Workshoppa] Disposed.");
        }
    }

    // ----------------------------
    // Config Handlers
    // ----------------------------

    public void ToggleWorkshoppaUi()
    {
        if (!_configuration.Enabled) return;
        _mainWindow?.Toggle(WorkshoppaWindow.EOpenReason.Command);
    }
    internal Stage CurrentStage
    {
        get => _currentStageInternal;
        private set
        {
            if (_currentStageInternal != value)
            {
                PluginLog.Debug($"Changing stage from {_currentStageInternal} to {value}");
                _currentStageInternal = value;
            }

            if (value != Stage.Stopped)
                _mainWindow.Flags |= ImGuiWindowFlags.NoCollapse;
            else
                _mainWindow.Flags &= ~ImGuiWindowFlags.NoCollapse;
        }
    }
    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!ClientState.IsLoggedIn ||
            !WorkshopTerritories.Contains(ClientState.TerritoryType) ||
            Condition[ConditionFlag.BoundByDuty] ||
            Condition[ConditionFlag.BetweenAreas] ||
            Condition[ConditionFlag.BetweenAreas51] ||
            GetDistanceToEventObject(_fabricationStationIds, out var fabricationStation) >= 3f)
        {
            _mainWindow.NearFabricationStation = false;

            if (_mainWindow.IsOpen &&
                _mainWindow.OpenReason == WorkshoppaWindow.EOpenReason.NearFabricationStation &&
                _configuration.CurrentlyCraftedItem == null &&
                _configuration.ItemQueue.Count == 0)
            {
                _mainWindow.IsOpen = false;
            }
        }
        else if (DateTime.Now >= _continueAt)
        {
            _mainWindow.NearFabricationStation = true;

            if (!_mainWindow.IsOpen)
            {
                _mainWindow.IsOpen = true;
                _mainWindow.OpenReason = WorkshoppaWindow.EOpenReason.NearFabricationStation;
            }

            if (_mainWindow.State is WorkshoppaWindow.ButtonState.Pause or WorkshoppaWindow.ButtonState.Stop)
            {
                _mainWindow.State = WorkshoppaWindow.ButtonState.None;
                if (CurrentStage != Stage.Stopped)
                {
                    _externalPluginHandler.Restore();
                    CurrentStage = Stage.Stopped;
                    ResetLevelingRuntimeState();
                    _configuration.Mode = TurnInMode.Normal;
                    SaveConfig();
                }

                return;
            }
            else if (_mainWindow.State is WorkshoppaWindow.ButtonState.Start or WorkshoppaWindow.ButtonState.Resume &&
                     CurrentStage == Stage.Stopped)
            {
                // TODO Error checking, we should ensure the player has the required job level for *all* crafting parts
                _mainWindow.State = WorkshoppaWindow.ButtonState.None;
                CurrentStage = Stage.TakeItemFromQueue;
            }

            if (CurrentStage != Stage.Stopped && CurrentStage != Stage.RequestStop && !_externalPluginHandler.Saved)
                _externalPluginHandler.Save();
            if (CurrentStage != Stage.Stopped && CurrentStage == _lastStage)
            {
                _stallTicks++;
                if (_stallTicks >= MaxStallTicks || (_lastProgressAt != DateTime.MinValue && DateTime.Now - _lastProgressAt > MaxNoProgress))
                {
                    if (CurrentStage is Stage.MergeStacks)
                    {
                        CurrentStage = Stage.TargetFabricationStation;
                        return;
                    }
                    else
                    {
                        ChatGui.PrintError("[Workshoppa] Stall detected, Bailing out.");
                        CurrentStage = Stage.RequestStop;
                    }
                    return;
                }
            }
            else
            {
                _stallTicks = 0;
                _lastStage = CurrentStage;
            }
            switch (CurrentStage)
            {
                case Stage.TakeItemFromQueue:
                    if (CheckContinueWithDelivery())
                        CurrentStage = Stage.ContributeMaterials;
                    else
                    { 
                        if (_configuration.Mode == TurnInMode.Leveling && _configuration.ItemQueue.Count == 0)
                        {
                            EnqueueLevelingProject(1002, quantity: 1);
                        }
                        TakeItemFromQueue();
                    }
                    break;

                case Stage.TargetFabricationStation:
                    if (_configuration.CurrentlyCraftedItem is { StartedCrafting: true } || _mergeAttempts > 0)
                        CurrentStage = Stage.SelectCraftBranch;
                    else
                        CurrentStage = Stage.OpenCraftingLog;
                    MarkProgress();
                    InteractWithFabricationStation(fabricationStation!);

                    break;

                case Stage.OpenCraftingLog:
                    OpenCraftingLog();
                    MarkProgress();
                    InteractWithFabricationStation(fabricationStation!); // Safety Check
                    break;

                case Stage.SelectCraftCategory:
                    SelectCraftCategory();
                    break;

                case Stage.SelectCraft:
                    SelectCraft();
                    break;

                case Stage.ConfirmCraft:
                    ConfirmCraft();
                    break;

                case Stage.RequestStop:
                    _externalPluginHandler.Restore();
                    _externalPluginHandler.RestoreTextAdvance();
                    ResetLevelingRuntimeState();
                    _configuration.Mode = TurnInMode.Normal;
                    CurrentStage = Stage.Stopped;
                    break;

                case Stage.SelectCraftBranch:
                    SelectCraftBranch();
                    break;

                case Stage.ContributeMaterials:
                    if (_configuration.Mode == TurnInMode.Leveling)
                        ContributeSpecificMaterial();
                    else
                        ContributeMaterials();
                    MarkProgress();
                    break;

                case Stage.MergeStacks:
                    if (_mergeRunner.IsRunning)
                        break;
                    if (HasItemInSingleSlot(_mergeItemId, _mergeRequired))
                    {
                        PluginLog.Information($"[Workshoppa] Merge satisfied for {_mergeRequired}x {_mergeItemName}, resuming.");
                        ClearMergeState();
                        CurrentStage = Stage.TargetFabricationStation;
                        MarkProgress();
                        break;
                    }

                    if (!_mergeRunner.TryStartMergeForItem(_mergeItemId))
                    {
                        PluginLog.Warning($"[Workshoppa] Merge pass could not start for {_mergeItemName} (plan empty or blocked).");
                        _mergeAttempts++;
                        break;
                    }
                    PluginLog.Information($"[Workshoppa] Merge pass {_mergeAttempts}/{MaxMergeAttempts} started for {_mergeItemName}...");
                    CurrentStage = Stage.TargetFabricationStation;
                    _continueAt = DateTime.Now.AddSeconds(5);
                    break;

                case Stage.OpenRequestItemWindow:
                // see RequestPostSetup and related
                if (DateTime.Now > _fallbackAt)
                    goto case Stage.ContributeMaterials;
                break;

                case Stage.OpenRequestItemSelect:
                case Stage.ConfirmRequestItemWindow:
                    // see RequestPostSetup and related
                    break;


                case Stage.ConfirmMaterialDelivery:
                    // see SelectYesNoPostSetup
                    break;

                case Stage.ConfirmCollectProduct:
                    // see SelectYesNoPostSetup
                    break;
                case Stage.CloseDeliveryMenu:
                    CloseMaterialDelivery();
                    break;
                case Stage.DiscontinueProject:
                    break;

                case Stage.Stopped:
                    break;

                default:
                    PluginLog.Warning($"Unknown stage {CurrentStage}");
                    break;
            }
        }
    }
    private bool TryGetCurrentCraft(out WorkshopCraft craft)
    {
        craft = default;

        var id = _configuration.CurrentlyCraftedItem?.WorkshopItemId;
        if (id == null)
            return false;

        craft = _workshopCache.Crafts.FirstOrDefault(x => x.WorkshopItemId == id.Value);
        return craft.WorkshopItemId != 0;
    }
    private void ClearMergeState()
    {
        _mergePending = false;
        _mergeAttempts = 0;
        _mergeItemId = 0;
        _mergeRequired = 0;
        _mergeItemName = string.Empty;
    }

    private void ProcessCommand(string command, string arguments)
    {
        /*if (arguments is "c" or "config")
            _configWindow.Toggle();
        else*/
            _mainWindow.Toggle(WorkshoppaWindow.EOpenReason.Command);
    }

    private void ProcessFuelBuyCommand(string command, string arguments)
    {
        if (_ceruleumTankWindow.TryParseBuyRequest(arguments, out int missingQuantity))
            _ceruleumTankWindow.StartPurchase(missingQuantity);
        else
            ChatGui.PrintError($"Usage: {command} <stacks>");
    }

    private void ProcessFuelFillCommand(string command, string arguments)
    {
        if (_ceruleumTankWindow.TryParseFillRequest(arguments, out int missingQuantity))
            _ceruleumTankWindow.StartPurchase(missingQuantity);
        else
            ChatGui.PrintError($"Usage: {command} <stacks>");
    }
    private void ProcessStoneBuyCommand(string command, string arguments)
    {
        if (_grindstoneShopWindow.TryParseBuyRequest(arguments, out int missingQuantity))
            _grindstoneShopWindow.StartPurchase(missingQuantity);
        else
            ChatGui.PrintError($"Usage: {command} <stacks>");
    }

    private void ProcessStoneFillCommand(string command, string arguments) //TODO: add classjob
    {
        /*if (_grindstoneShopWindow.TryParseFillRequest(arguments, out int missingQuantity))
            _grindstoneShopWindow.StartPurchase(missingQuantity);
        else
            ChatGui.PrintError($"Usage: {command} <stacks>");*/
        ChatGui.PrintError($"Disabled =[");
    }
    private void ProcessDarkMatterCommand(string command, string arguments)
    {
        _repairKitWindow.ShopLogic();
    }
    private void ProcessLevelingCommand(string command, string arguments)
    {
        _mainWindow.StartLevelingConditions();
    }
    public void OpenWorkshoppa()
    {
        if (!_configuration.Enabled) return;
        ProcessCommand("/ws", "");
    }

    /*public void OpenRepairKit()
    {
        if (!isEnabled || _repairKitWindow == null) return;
        ProcessCommand("/repairkits", "");
    }

    public void OpenTanks()
    {
        if (!isEnabled || _ceruleumTankWindow == null) return;
        ProcessCommand("/fill-tanks", "");
    }*/
}
