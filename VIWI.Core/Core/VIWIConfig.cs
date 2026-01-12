using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using VIWI.Modules.AoEasy;
using VIWI.Modules.AutoLogin;
//using VIWI.Modules.Viwiwi;
using VIWI.Modules.Workshoppa;

namespace VIWI.Core
{
    [Serializable]
    public sealed class VIWIConfig : IPluginConfiguration
    {
        public int Version { get; set; } = 1;
        public IPluginLog? pluginLog;
        public const bool DEVMODE = false;
        public bool Unlocked { get; set; } = false;

        public AoEasyConfig AoEasy { get; set; } = new();
        public AutoLoginConfig AutoLogin { get; set; } = new();
        //public ViwiwiConfig Viwiwi { get; set; } = new();
        public WorkshoppaConfig Workshoppa { get; set; } = new();



        [NonSerialized]
        private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pi)
        {
            pluginInterface = pi;
        }

        public void Save()
        {
            if (pluginInterface == null)
            {
                return;
            }

            try
            {
                pluginInterface.SavePluginConfig(this);
                pluginLog?.Information("[VIWI] Saved CONFIG.");
            }
            catch (Exception ex)
            {
                pluginLog?.Error(ex, "[VIWI] Failed to save CONFIG.");
            }
        }
    }
}
