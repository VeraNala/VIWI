using System;

namespace VIWI.Modules.AoEasy
{
    [Serializable]
    public class AoEasyConfig
    {
        public int Version { get; set; } = 1;

        public bool Enabled { get; set; } = false;
    }
}
