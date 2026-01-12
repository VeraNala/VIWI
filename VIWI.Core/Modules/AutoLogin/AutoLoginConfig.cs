using System;
using System.Collections.Generic;

namespace VIWI.Modules.AutoLogin
{
    [Serializable]
    public class AutoLoginConfig
    {
        public int Version { get; set; } = 1;
        public bool Enabled { get; set; } = false;
        public bool HCMode { get; set; } = false;
        public bool SkipAuthError { get; set; } = false;
        public int ServiceAccountIndex { get; set; } = 0;


        public string CharacterName { get; set; } = "";
        public string HomeWorldName { get; set; } = "";
        public int DataCenterID { get; set; } = 0;
        public string DataCenterName { get; set; } = "";

        public bool Visiting { get; set; } = false;
        public string CurrentWorldName { get; set; } = "";
        public int vDataCenterID { get; set; } = 0;
        public string vDataCenterName { get; set; } = "";


        public string HCCharacterName { get; set; } = "";
        public string HCHomeWorldName { get; set; } = "";
        public int HCDataCenterID { get; set; } = 0;
        public string HCDataCenterName { get; set; } = "";


        public bool HCVisiting { get; set; } = false;
        public string HCCurrentWorldName { get; set; } = "";
        public int HCvDataCenterID { get; set; } = 0;
        public string HCvDataCenterName { get; set; } = "";

        public int DCsRecovered { get; set; } = 0;
        public int AuthsRecovered { get; set; } = 0;

        public bool RunLoginCommands = false;
        public bool ARActiveSkipLoginCommands = true;
        public List<string> LoginCommands = [];

        public string ClientLaunchPath { get; set; } = string.Empty;
        public string ClientLaunchArgs { get; set; } = string.Empty;
        public bool RestartingClient { get; set; } = false;

    }
}
