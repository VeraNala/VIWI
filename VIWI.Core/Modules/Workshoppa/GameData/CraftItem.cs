namespace VIWI.Modules.Workshoppa.GameData;

public class CraftItem
{
    public uint ItemId { get; set; }
    public uint IconId { get; set; }
    public string? ItemName { get; set; }
    public int CrafterIconId { get; set; }
    public uint ItemCountPerStep { get; set; }
    public uint ItemCountNQ { get; set; }
    public uint ItemCountHQ { get; set; }
    public uint Experience { get; set; }
    public uint StepsComplete { get; set; }
    public uint StepsTotal { get; set; }
    public bool Finished { get; set; }
    public uint CrafterMinimumLevel { get; set; }

    public uint QuantityComplete => StepsComplete * ItemCountPerStep;
}
