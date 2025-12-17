namespace VIWI.Modules.Workshoppa.GameData;

internal sealed class WorkshopCraftItem
{
    public required uint ItemId { get; init; }
    public required string Name { get; init; }
    public required ushort IconId { get; init; }
    public required int SetQuantity { get; init; }
    public required int SetsRequired { get; init; }
    public int TotalQuantity => SetQuantity * SetsRequired;
}
