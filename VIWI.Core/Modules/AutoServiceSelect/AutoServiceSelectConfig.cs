using System;
using System.Collections.Generic;

namespace VIWI.Modules.AutoServiceSelect
{
    [Serializable]
    public class AutoServiceSelectConfig
    {
        public int Version { get; set; } = 1;
        public bool Enabled { get; set; } = false;
        public int ServiceAccountIndex { get; set; } = 1;
    }
}