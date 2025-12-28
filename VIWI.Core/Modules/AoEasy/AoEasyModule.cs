using VIWI.Core;
using static VIWI.Core.VIWIContext;

namespace VIWI.Modules.AoEasy
{
    public sealed class AoEasyModule : VIWIModuleBase<AoEasyConfig>
    {
        public const string ModuleName = "AoEasy";
        public const string ModuleVersion = "0.0.1";
        public override string Name => ModuleName;
        public override string Version => ModuleVersion;
        public AoEasyConfig _configuration => ModuleConfig;
        private VIWIConfig Core => CoreConfig;
        internal static AoEasyModule? Instance { get; private set; }
        public static bool Enabled => Instance?._configuration.Enabled ?? false;

        protected override AoEasyConfig CreateConfig() => new AoEasyConfig();
        protected override AoEasyConfig GetConfigBranch(VIWIConfig core) => core.AoEasy;
        protected override void SetConfigBranch(VIWIConfig core, AoEasyConfig _configuration) => core.AoEasy = _configuration;
        protected override bool GetEnabled(AoEasyConfig _configuration) => _configuration.Enabled;
        protected override void SetEnabledValue(AoEasyConfig _configuration, bool enabled) => _configuration.Enabled = enabled;
        public void SaveConfig() => Core.Save();

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
        }
        public override void Disable()
        {
        }
        public override void Dispose()
        {
            Disable();

            if (Instance == this)
                Instance = null;
            PluginLog.Information("[AoEasy] Disposed.");
        }



        public void Update()
        {
            var player = VIWIContext.PlayerState;
            if (player != null && JobData.TryGet(player.ClassJob.RowId, out var jobInfo))
            {
                PluginLog.Information($"Current job: {jobInfo.Name} ({jobInfo.Abbreviation}), Role={jobInfo.Role}");
            }
        }
    }
}
