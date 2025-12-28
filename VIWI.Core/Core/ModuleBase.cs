using ECommons.Logging;
using System;

namespace VIWI.Core
{
    public abstract class VIWIModuleBase<TModuleConfig> : IVIWIModule
        where TModuleConfig : class
    {
        protected VIWIConfig CoreConfig { get; private set; } = null!;
        protected TModuleConfig ModuleConfig { get; private set; } = null!;

        public abstract string Name { get; }
        public abstract string Version { get; }

        protected abstract TModuleConfig CreateConfig();
        protected abstract TModuleConfig GetConfigBranch(VIWIConfig core);

        protected abstract void SetConfigBranch(VIWIConfig core, TModuleConfig config);
        protected abstract bool GetEnabled(TModuleConfig config);
        protected abstract void SetEnabledValue(TModuleConfig config, bool enabled);

        public virtual void Initialize(VIWIConfig config)
        {
            CoreConfig = config ?? throw new ArgumentNullException(nameof(config));

            var branch = GetConfigBranch(config);
            if (branch == null)
            {
                PluginLog.Information($"[{Name}] No config branch detected, creating new config.");
                branch = CreateConfig();
                SetConfigBranch(config, branch);
            }

            ModuleConfig = branch;

            PluginLog.Information($"[{Name}] Module initialized.");
            PluginLog.Information($"[{Name}] Module Config Loaded. Enabled={GetEnabled(ModuleConfig)}");

            /*if (GetEnabled(ModuleConfig))
                Enable();*/
        }

        public void SetEnabled(bool enable)
        {
            if (CoreConfig == null || ModuleConfig == null)
                return;

            var wasEnabled = GetEnabled(ModuleConfig);
            if (wasEnabled == enable)
                return;

            SetEnabledValue(ModuleConfig, enable);
            CoreConfig.Save();

            if (enable) Enable();
            else Disable();
        }

        public abstract void Enable();
        public abstract void Disable();
        public virtual void Dispose() { }
    }
    public interface IVIWIModule : IDisposable
    {
        string Name { get; }
        string Version { get; }
        void Initialize(VIWIConfig config);
        void SetEnabled(bool enable);
        void Enable();
        void Disable();
    }
}
