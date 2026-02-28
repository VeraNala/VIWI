using Dalamud.Interface.Windowing;
using VIWI.Core;
using VIWI.IPC;
using VIWI.Modules.KitchenSink.Commands;
using static VIWI.Core.VIWIContext;

namespace VIWI.Modules.KitchenSink
{
    public sealed class KitchenSinkModule : VIWIModuleBase<KitchenSinkConfig>
    {
        public const string ModuleName = "KitchenSink";
        public const string ModuleVersion = "1.0.2";
        public override string Name => ModuleName;
        public override string Version => ModuleVersion;
        public KitchenSinkConfig _configuration => ModuleConfig;
        private VIWIConfig Core => CoreConfig;
        internal static KitchenSinkModule? Instance { get; private set; }
        public static bool Enabled => Instance?._configuration.Enabled ?? false;

        protected override KitchenSinkConfig CreateConfig() => new KitchenSinkConfig();
        protected override KitchenSinkConfig GetConfigBranch(VIWIConfig core) => core.KitchenSink;
        protected override void SetConfigBranch(VIWIConfig core, KitchenSinkConfig _configuration) => core.KitchenSink = _configuration;
        protected override bool GetEnabled(KitchenSinkConfig _configuration) => _configuration.Enabled;
        protected override void SetEnabledValue(KitchenSinkConfig _configuration, bool enabled) => _configuration.Enabled = enabled;
        public void SaveConfig() => Core.Save();

        private readonly WindowSystem _windowSystem = new($"{nameof(VIWI)}.{ModuleName}");
        private AutoRetainerIPC _autoRetainer = null!;
        private CharacterSwitch? _characterSwitch;
        private DropboxQueue? _dropboxQueue;
        private GlamourSetter? _glamourSetter;
        private WeaponIcons? _weaponIcons;
        private WeatherForecast? _weatherForecast;
        private BunnyBlessed? _bunnyBlessed;
        private Leves? _leves;
        public AutoRetainerIPC? GetAutoRetainer() => _autoRetainer;

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
            _autoRetainer ??= new AutoRetainerIPC();

            _characterSwitch ??= new CharacterSwitch(
                _autoRetainer,
                CommandManager,
                ClientState,
                ChatGui,
                NotificationManager,
                DtrBar,
                Condition,
                PluginLog,
                Framework);

            _dropboxQueue ??= new DropboxQueue(
                PluginInterface,
                CommandManager,
                ChatGui,
                PluginLog);

            _glamourSetter ??= new GlamourSetter(
                PluginInterface,
                DataManager,
                ClientState,
                ChatGui,
                CommandManager,
                AddonLifecycle,
                _configuration,
                SaveConfig);

            _weaponIcons ??= new WeaponIcons(GameGui, KeyState, DataManager, TextureProvider, PluginLog, _configuration);

            _weatherForecast ??= new WeatherForecast(
                ClientState,
                DataManager,
                CommandManager,
                ChatGui,
                TextureProvider);

            _bunnyBlessed ??= new BunnyBlessed(
                PluginInterface,
                ClientState,
                Framework,
                CommandManager,
                ChatGui,
                ObjectTable);

            _leves ??= new Leves(
                Framework,
                ClientState,
                ChatGui,
                _configuration);
            if (_configuration.WeaponIconsEnabled)
            {
                //PluginInterface.UiBuilder.Draw += _weaponIcons.Draw;
            }
            PluginInterface.UiBuilder.Draw += _windowSystem.Draw;

            if (_glamourSetter != null)
                _windowSystem.AddWindow(_glamourSetter);

            if (_weatherForecast != null)
                _windowSystem.AddWindow(_weatherForecast);

            PluginLog.Information("[KitchenSink] Enabled.");
        }
        public override void Disable()
        {
            PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;

            if (_glamourSetter != null)
                _windowSystem.RemoveWindow(_glamourSetter);

            if (_weatherForecast != null)
                _windowSystem.RemoveWindow(_weatherForecast);
            if (_weaponIcons != null)
            {
                PluginInterface.UiBuilder.Draw -= _weaponIcons.Draw;
                _weaponIcons.Dispose();
                _weaponIcons = null;
            }

            _leves?.Dispose();
            _bunnyBlessed?.Dispose();
            _weatherForecast?.Dispose();
            _glamourSetter?.Dispose();
            _dropboxQueue?.Dispose();
            _characterSwitch?.Dispose();

            PluginLog.Information("[KitchenSink] Disabled.");
        }
        public override void Dispose()
        {
            Disable();
            if (Instance == this)
                Instance = null;
            PluginLog.Information("[KitchenSink] Disposed.");
        }
    }
}