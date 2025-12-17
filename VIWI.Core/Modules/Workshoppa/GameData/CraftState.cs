using System.Collections.Generic;
using System.Linq;

namespace VIWI.Modules.Workshoppa.GameData;

public sealed class CraftState
{
    public required uint ResultItem { get; init; }
    public required uint StepsComplete { get; init; }
    public required uint StepsTotal { get; init; }
    public required IReadOnlyList<CraftItem> Items { get; init; }

    public bool IsPhaseComplete() => Items.All(x => x.Finished || x.StepsComplete == x.StepsTotal);

    public bool IsCraftComplete() => StepsComplete == StepsTotal - 1 && IsPhaseComplete();
}
