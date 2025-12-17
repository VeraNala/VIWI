using System.Collections.Generic;

namespace VIWI.Modules.Workshoppa.GameData;

internal sealed class WorkshopCraftPhase
{
    public required string Name { get; init; }
    public required IReadOnlyList<WorkshopCraftItem> Items { get; init; }
}
