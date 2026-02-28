using System;
using System.Collections.Generic;

namespace VIWI.Modules.AutoLogin
{
    public enum LoginRegion
    {
        Unknown = 0,
        NA,
        EU,
        OCE,
        JP,
    }

    [Serializable]
    public sealed class LoginSnapshot
    {
        public string CharacterName { get; set; } = "";
        public string HomeWorldName { get; set; } = "";

        public int DataCenterID { get; set; } = 0;
        public string DataCenterName { get; set; } = "";

        public bool Visiting { get; set; } = false;
        public string CurrentWorldName { get; set; } = "";

        public int vDataCenterID { get; set; } = 0;
        public string vDataCenterName { get; set; } = "";

        public bool HasMinimumIdentity =>
            !string.IsNullOrWhiteSpace(CharacterName) &&
            !string.IsNullOrWhiteSpace(HomeWorldName) &&
            DataCenterID != 0;
    }

    [Serializable]
    public sealed class AutoLoginConfig
    {
        public int Version { get; set; } = 2;

        public bool Enabled { get; set; } = false;
        public bool SkipAuthError { get; set; } = false;
        public int ServiceAccountIndex { get; set; } = 0;
        public LoginSnapshot Current { get; set; } = new();
        public Dictionary<LoginRegion, LoginSnapshot> LastByRegion { get; set; } = new();

        public bool RestartingClient { get; set; } = false;
        public LoginRegion PendingRestartRegion { get; set; } = LoginRegion.Unknown;

        public LoginRegion CurrentRegion { get; set; } = LoginRegion.Unknown;
        public int DCsRecovered { get; set; } = 0;
        public int AuthsRecovered { get; set; } = 0;

        public bool RunLoginCommands = false;
        public bool ARActiveSkipLoginCommands = true;
        public List<string> LoginCommands = [];

        public string ClientLaunchPath { get; set; } = string.Empty;
        public string ClientLaunchArgs { get; set; } = string.Empty;
    }
}