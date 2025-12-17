using System;

namespace VIWI.Modules.Workshoppa.Windows.Shop;

internal readonly record struct ShopItemForSale
{
    public int Position { get; init; }
    public string ItemName { get; init; }
    public uint Price { get; init; }
    public uint OwnedItems { get; init; }
    public uint ItemId { get; init; }
}

internal sealed class ShopPurchaseState
{
    public int DesiredItems { get; }
    public int OwnedItems { get; set; }

    public DateTime NextStep { get; set; } = DateTime.MinValue;
    public bool IsAwaitingYesNo { get; set; }

    public ShopPurchaseState(int desiredItems, int ownedItems)
    {
        DesiredItems = desiredItems;
        OwnedItems = ownedItems;
    }

    public int ItemsLeftToBuy => Math.Max(0, DesiredItems - OwnedItems);
    public bool IsComplete => OwnedItems >= DesiredItems;
}
