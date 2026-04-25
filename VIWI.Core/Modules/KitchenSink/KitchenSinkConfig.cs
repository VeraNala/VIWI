using System;
using System.Collections.Generic;

namespace VIWI.Modules.KitchenSink;

[Serializable]
public class KitchenSinkConfig
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = false;

    public sealed class CharacterData
    {
        public ulong LocalContentId { get; set; }

        public bool IsGlamourDresserInitialized { get; set; }

        public HashSet<uint> GlamourDresserItems { get; set; } = new HashSet<uint>();

        public bool WarnAboutLeves { get; set; }
    }

    public List<CharacterData> Characters { get; set; } = new List<CharacterData>();
    public bool ShowOnlyMissingGlamourSets { get; set; } = true;

    public bool WeaponIconsEnabled { get; set; } = false;
    public bool WeaponIconsRequireCtrl { get; set; } = false;
    public bool WeaponIconsMiniMode { get; set; } = true;
}
