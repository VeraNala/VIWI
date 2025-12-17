using System.Collections.Generic;

namespace VIWI.Modules.Workshoppa.GameData;

internal sealed class WorkshopCraft
{
    public required uint WorkshopItemId { get; init; }
    public required uint ResultItem { get; init; }
    public required string Name { get; init; }
    public required ushort IconId { get; init; }
    public required WorkshopCraftCategory Category { get; init; }
    public required uint Type { get; init; }
    public required IReadOnlyList<WorkshopCraftPhase> Phases { get; init; }
}
