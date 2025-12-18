using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace VIWI.Core;

public static class VIWIContext
{
    public static VIWIPlugin CorePlugin { get; internal set; } = null!;
    public static IDalamudPluginInterface PluginInterface { get; internal set; } = null!;
    public static IClientState ClientState { get; internal set; } = null!;
    public static IPlayerState PlayerState { get; internal set; } = null!;
    public static IObjectTable ObjectTable { get; internal set; } = null!;
    public static ITargetManager TargetManager { get; internal set; } = null!;
    public static IPlayerCharacter PlayerCharacter { get; internal set; } = null!;
    public static IDataManager DataManager { get; internal set; } = null!;
    public static ITextureProvider TextureProvider { get; internal set; } = null!;
    public static IGameGui GameGui { get; internal set; } = null!;
    public static IGameInteropProvider HookProvider { get; internal set; } = null!;
    public static ISigScanner SigScanner { get; internal set; } = null!;
    public static IFramework Framework { get; internal set; } = null!;
    public static ICommandManager CommandManager { get; internal set; } = null!;
    public static IChatGui ChatGui { get; internal set; } = null!;
    public static IAddonLifecycle AddonLifecycle { get; internal set; } = null!;
    public static ICondition Condition { get; internal set; } = null!;
    public static IPluginLog PluginLog { get; internal set; } = null!;
}
