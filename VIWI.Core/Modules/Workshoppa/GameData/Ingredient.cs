namespace VIWI.Modules.Workshoppa.GameData;

public class Ingredient
{
    public required uint ItemId { get; init; }
    public uint IconId { get; init; }
    public required string Name { get; init; }
    public required int TotalQuantity { get; set; }
    public required EType Type { get; init; }

    public enum EType
    {
        Craftable,
        Gatherable,
        Other,
        ShopItem,
    }
}
