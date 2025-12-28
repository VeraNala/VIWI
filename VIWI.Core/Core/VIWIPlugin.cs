using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using VIWI.UI.Windows;

namespace VIWI.Core;

public sealed class VIWIPlugin : IDalamudPlugin
{
    public static string Name => "VIWI Core";
    public const bool DEVMODE = false;
    public static VIWIPlugin Instance { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider HookProvider { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;

    internal readonly WindowSystem WindowSystem = new("VIWI");
    internal MainDashboardWindow? MainWindow;

    public VIWIPlugin(
            IDalamudPluginInterface pluginInterface,
            IPluginLog pluginLog,
            IClientState clientState,
            IPlayerState playerState,
            IObjectTable objectTable,
            IDataManager dataManager,
            ITextureProvider textureProvider,
            IGameGui gameGui,
            IFramework framework,
            ICommandManager commandManager,
            ISigScanner sigScanner,
            IGameInteropProvider hookProvider,
            IAddonLifecycle addonLifecycle,
            IChatGui chatGui,
            ICondition condition)
    {
        Instance = this;
        PluginInterface = pluginInterface;

        VIWIContext.CorePlugin = this;
        VIWIContext.PluginInterface = pluginInterface;
        VIWIContext.PluginLog = pluginLog;
        VIWIContext.ClientState = clientState;
        VIWIContext.PlayerState = playerState;
        VIWIContext.ObjectTable = objectTable;
        VIWIContext.DataManager = dataManager;
        VIWIContext.TextureProvider = textureProvider;
        VIWIContext.GameGui = gameGui;
        VIWIContext.Framework = framework;
        VIWIContext.CommandManager = commandManager;
        VIWIContext.SigScanner = sigScanner;
        VIWIContext.HookProvider = hookProvider;
        VIWIContext.AddonLifecycle = addonLifecycle;
        VIWIContext.ChatGui = chatGui;
        VIWIContext.Condition = condition;

        ECommonsMain.Init(pluginInterface, this);
        PluginLog.Information("Core + ECommons initialized.");

        MainWindow = new MainDashboardWindow();
        WindowSystem.AddWindow(MainWindow);
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUI;
        commandManager.AddHandler("/viwi", new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the VIWI dashboard."
        });

        var raw = PluginInterface.GetPluginConfig();
        if (raw != null && raw is not VIWIConfig)
            PluginLog.Warning($"[VIWI] CONFIG mismatch: {raw.GetType().FullName} (expected {typeof(VIWIConfig).FullName}). Recreating defaults.");

        var config = raw as VIWIConfig ?? new VIWIConfig();
        config.Initialize(PluginInterface);
        ModuleManager.Initialize(config);
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler("/viwi");
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUI;

        if (MainWindow is not null)
        {
            WindowSystem.RemoveWindow(MainWindow);
            MainWindow.Dispose();
        }

        ModuleManager.Dispose();
        ECommonsMain.Dispose();
        PluginLog.Information("Core + ECommons unloaded.");
    }

    private void ToggleMainUI() => MainWindow?.Toggle();

    private void OnCommand(string command, string args) => ToggleMainUI();
}