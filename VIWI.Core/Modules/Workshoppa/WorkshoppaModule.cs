using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using ECommons.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using VIWI.Core;
using VIWI.Modules.Workshoppa.External;
using VIWI.Modules.Workshoppa.GameData;
using VIWI.Modules.Workshoppa.Windows;

namespace VIWI.Modules.Workshoppa;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
internal sealed partial class WorkshoppaModule : IVIWIModule
{
    public const string ModuleName = "Workshoppa";
    public const string ModuleVersion = "1.0.0";

    public string Name => ModuleName;
    public string Version => ModuleVersion;

    internal static WorkshoppaModule? Instance { get; private set; }

    // ---- Config ----
    public static WorkshoppaConfig Config { get; private set; } = null!;
    public static bool Enabled => Config?.Enabled ?? false;

    // ---- Services ----
    private readonly IGameGui gameGui;
    private readonly IFramework framework;
    private readonly ICondition condition;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDataManager dataManager;
    private readonly ICommandManager commandManager;
    private readonly IAddonLifecycle addonLifecycle;
    private readonly IChatGui chatGui;
    private readonly ITextureProvider textureProvider;
    private readonly IPluginLog pluginLog;

    // ---- State / systems ----
    private readonly IReadOnlyList<uint> fabricationStationIds =
        new uint[] { 2005236, 2005238, 2005240, 2007821, 2011588 }.AsReadOnly();

    internal readonly IReadOnlyList<ushort> WorkshopTerritories =
        new ushort[] { 423, 424, 425, 653, 984 }.AsReadOnly();

    private ExternalPluginHandler externalPluginHandler = null!;
    private WorkshopCache workshopCache = null!;
    private GameStrings gameStrings = null!;

    private WorkshoppaWindow workshoppaWindow = null!;
    private WorkshoppaRepairKitWindow workshoppaRepairKitWindow = null!;
    private WorkshoppaCeruleumTankWindow workshoppaCeruleumTankWindow = null!;

    private Stage currentStageInternal = Stage.Stopped;
    private DateTime continueAt = DateTime.MinValue;
    private DateTime fallbackAt = DateTime.MaxValue;

    private bool isEnabled;
    private bool initialized;

    public WorkshoppaModule()
    {
        gameGui = VIWIContext.GameGui;
        framework = VIWIContext.Framework;
        condition = VIWIContext.Condition;
        clientState = VIWIContext.ClientState;
        objectTable = VIWIContext.ObjectTable;
        dataManager = VIWIContext.DataManager;
        commandManager = VIWIContext.CommandManager;
        addonLifecycle = VIWIContext.AddonLifecycle;
        chatGui = VIWIContext.ChatGui;
        textureProvider = VIWIContext.TextureProvider;
        pluginLog = VIWIContext.PluginLog;
    }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;

        Instance = this;
        LoadConfig();
        externalPluginHandler = new ExternalPluginHandler(VIWIContext.PluginInterface, pluginLog);
        workshopCache = new WorkshopCache(dataManager, pluginLog);
        gameStrings = new GameStrings(dataManager, pluginLog);

        workshoppaWindow = new WorkshoppaWindow(this, objectTable, Config, workshopCache, new IconCache(textureProvider), chatGui, new RecipeTree(dataManager, pluginLog), pluginLog);
        VIWIContext.CorePlugin.WindowSystem.AddWindow(workshoppaWindow);
        workshoppaRepairKitWindow = new WorkshoppaRepairKitWindow(pluginLog, gameGui, addonLifecycle, Config, externalPluginHandler);
        VIWIContext.CorePlugin.WindowSystem.AddWindow(workshoppaRepairKitWindow);
        workshoppaCeruleumTankWindow = new WorkshoppaCeruleumTankWindow(pluginLog, gameGui, addonLifecycle, Config, externalPluginHandler, chatGui);
        VIWIContext.CorePlugin.WindowSystem.AddWindow(workshoppaCeruleumTankWindow);

        ApplyEnabledState(Config.Enabled);
        PluginLog.Information("[Workshoppa] Module initialized.");
    }

    public void Dispose()
    {
        try
        {
            ApplyEnabledState(false);

            if (workshoppaCeruleumTankWindow != null) workshoppaCeruleumTankWindow.Dispose();
            if (workshoppaRepairKitWindow != null) workshoppaRepairKitWindow.Dispose();

            externalPluginHandler?.RestoreTextAdvance();
            externalPluginHandler?.Restore();
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

    public void ApplyEnabledState(bool enabled)
    {
        if (!initialized)
        {
            isEnabled = enabled;
            return;
        }

        if (isEnabled == enabled) return;
        isEnabled = enabled;

        if (enabled) Enable();
        else Disable();
    }

    private void Enable()
    {
        framework.Update += FrameworkUpdate;

        commandManager.AddHandler("/ws", new CommandInfo(ProcessCommand) { HelpMessage = "Open Workshoppa UI" });
        commandManager.AddHandler("/workshoppa", new CommandInfo(ProcessCommand) { ShowInHelp = false });
        commandManager.AddHandler("/buy-tanks", new CommandInfo(ProcessBuyCommand){ HelpMessage = "Buy a given number of ceruleum tank stacks."});
        commandManager.AddHandler("/fill-tanks", new CommandInfo(ProcessFillCommand){ HelpMessage = "Fill your inventory with a given number of ceruleum tank stacks."});

        workshoppaRepairKitWindow?.EnableShopListeners();
        workshoppaCeruleumTankWindow?.EnableShopListeners();
        addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoPostSetup);
        addonLifecycle.RegisterListener(AddonEvent.PostSetup, "Request", RequestPostSetup);
        addonLifecycle.RegisterListener(AddonEvent.PostRefresh, "Request", RequestPostRefresh);
        addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "ContextIconMenu", ContextIconMenuPostReceiveEvent);
    }

    private void Disable()
    {
        workshoppaRepairKitWindow?.DisableShopListeners();
        workshoppaCeruleumTankWindow?.DisableShopListeners();
        addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "ContextIconMenu", ContextIconMenuPostReceiveEvent);
        addonLifecycle.UnregisterListener(AddonEvent.PostRefresh, "Request", RequestPostRefresh);
        addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Request", RequestPostSetup);
        addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoPostSetup);

        commandManager.RemoveHandler("/fill-tanks");
        commandManager.RemoveHandler("/buy-tanks");
        commandManager.RemoveHandler("/workshoppa");
        commandManager.RemoveHandler("/ws");

        framework.Update -= FrameworkUpdate;
        if (CurrentStage != Stage.Stopped)
        {
            externalPluginHandler.Restore();
            CurrentStage = Stage.Stopped;
        }
        if (workshoppaWindow != null && workshoppaWindow.IsOpen) workshoppaWindow.IsOpen = false;
        if (workshoppaRepairKitWindow != null) workshoppaRepairKitWindow.IsOpen = false;
        if (workshoppaCeruleumTankWindow != null) workshoppaCeruleumTankWindow.IsOpen = false;
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
        Config = VIWIContext.PluginInterface.GetPluginConfig() as WorkshoppaConfig ?? new WorkshoppaConfig();
        SaveConfig();
    }

    public static void SaveConfig()
    {
        Config?.Save();
    }
    public void ToggleMainUi()
    {
        if (!isEnabled) return;
        workshoppaWindow?.Toggle(WorkshoppaWindow.EOpenReason.Command);
    }
    internal Stage CurrentStage
    {
        get => currentStageInternal;
        private set
        {
            if (currentStageInternal != value)
            {
                PluginLog.Debug($"[Workshoppa] Changing stage from {currentStageInternal} to {value}");
                currentStageInternal = value;
            }

            if (value != Stage.Stopped)
                workshoppaWindow.Flags |= ImGuiWindowFlags.NoCollapse;
            else
                workshoppaWindow.Flags &= ~ImGuiWindowFlags.NoCollapse;
        }
    }

    private void ProcessCommand(string command, string arguments) // -- Replace with page filtering eventually
    {
        if (!isEnabled) return;

        /*if (arguments is "c" or "config")
            configWindow.Toggle();
        else*/
        workshoppaWindow.Toggle(WorkshoppaWindow.EOpenReason.Command);
    }

    private void ProcessBuyCommand(string command, string arguments)
    {
        if (!isEnabled) return;

        if (workshoppaCeruleumTankWindow.TryParseBuyRequest(arguments, out int missingQuantity))
            workshoppaCeruleumTankWindow.StartPurchase(missingQuantity);
        else
            chatGui.PrintError($"Usage: {command} <stacks>");
    }

    private void ProcessFillCommand(string command, string arguments)
    {
        if (!isEnabled) return;

        if (workshoppaCeruleumTankWindow.TryParseFillRequest(arguments, out int missingQuantity))
            workshoppaCeruleumTankWindow.StartPurchase(missingQuantity);
        else
            chatGui.PrintError($"Usage: {command} <stacks>");
    }

    private void FrameworkUpdate(IFramework _)
    {
        if (!clientState.IsLoggedIn ||
            !WorkshopTerritories.Contains(clientState.TerritoryType) ||
            condition[ConditionFlag.BoundByDuty] ||
            condition[ConditionFlag.BetweenAreas] ||
            condition[ConditionFlag.BetweenAreas51] ||
            GetDistanceToEventObject(fabricationStationIds, out var fabricationStation) >= 3f)
        {
            workshoppaWindow.NearFabricationStation = false;

            if (workshoppaWindow.IsOpen &&
                workshoppaWindow.OpenReason == WorkshoppaWindow.EOpenReason.NearFabricationStation &&
                Config.CurrentlyCraftedItem == null &&
                Config.ItemQueue.Count == 0)
            {
                workshoppaWindow.IsOpen = false;
            }
        }
        else if (DateTime.Now >= continueAt)
        {
            workshoppaWindow.NearFabricationStation = true;

            if (!workshoppaWindow.IsOpen)
            {
                workshoppaWindow.IsOpen = true;
                workshoppaWindow.OpenReason = WorkshoppaWindow.EOpenReason.NearFabricationStation;
            }

            if (workshoppaWindow.State is WorkshoppaWindow.ButtonState.Pause or WorkshoppaWindow.ButtonState.Stop)
            {
                workshoppaWindow.State = WorkshoppaWindow.ButtonState.None;
                if (CurrentStage != Stage.Stopped)
                {
                    externalPluginHandler.Restore();
                    CurrentStage = Stage.Stopped;
                }

                return;
            }
            else if (workshoppaWindow.State is WorkshoppaWindow.ButtonState.Start or WorkshoppaWindow.ButtonState.Resume &&
                     CurrentStage == Stage.Stopped)
            {
                // TODO Error checking, we should ensure the player has the required job level for *all* crafting parts
                workshoppaWindow.State = WorkshoppaWindow.ButtonState.None;
                CurrentStage = Stage.TakeItemFromQueue;
            }

            if (CurrentStage != Stage.Stopped && CurrentStage != Stage.RequestStop && !externalPluginHandler.Saved)
                externalPluginHandler.Save();

            switch (CurrentStage)
            {
                case Stage.TakeItemFromQueue:
                    if (CheckContinueWithDelivery())
                        CurrentStage = Stage.ContributeMaterials;
                    else
                        TakeItemFromQueue();
                    break;

                case Stage.TargetFabricationStation:
                    if (Config.CurrentlyCraftedItem is { StartedCrafting: true })
                        CurrentStage = Stage.SelectCraftBranch;
                    else
                        CurrentStage = Stage.OpenCraftingLog;

                    InteractWithFabricationStation(fabricationStation!);

                    break;

                case Stage.OpenCraftingLog:
                    OpenCraftingLog();
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
                    externalPluginHandler.Restore();
                    CurrentStage = Stage.Stopped;
                    break;

                case Stage.SelectCraftBranch:
                    SelectCraftBranch();
                    break;

                case Stage.ContributeMaterials:
                    ContributeMaterials();
                    break;

                case Stage.OpenRequestItemWindow:
                    // see RequestPostSetup and related
                    if (DateTime.Now > fallbackAt)
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

                case Stage.Stopped:
                    break;

                default:
                    pluginLog.Warning($"Unknown stage {CurrentStage}");
                    break;
            }
        }
    }

    public void OpenWorkshoppa()
    {
        if (!isEnabled) return;
        ProcessCommand("/ws", "");
    }

    public void OpenRepairKit()
    {
        if (!isEnabled || workshoppaRepairKitWindow == null) return;
        ProcessCommand("/repairkits", "");
    }

    public void OpenTanks()
    {
        if (!isEnabled || workshoppaCeruleumTankWindow == null) return;
        ProcessCommand("/fill-tanks", "");
    }
}
