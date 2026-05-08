using System;

namespace VIWI.Modules.SideCheck
{
    [Serializable]
    public class SideCheckConfig
    {
        public int Version { get; set; } = 1;

        public bool Enabled { get; set; } = false;
        public bool DebugLogging { get; set; } = true;
        public bool PrintOnChange { get; set; } = true;
    }
}
