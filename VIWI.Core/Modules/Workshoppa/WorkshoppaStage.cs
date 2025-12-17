namespace VIWI.Modules.Workshoppa;

public enum Stage
{
    TakeItemFromQueue,
    TargetFabricationStation,

    OpenCraftingLog,
    SelectCraftCategory,
    SelectCraft,
    ConfirmCraft,

    SelectCraftBranch,
    ContributeMaterials,
    OpenRequestItemWindow,
    OpenRequestItemSelect,
    ConfirmRequestItemWindow,
    ConfirmMaterialDelivery,

    ConfirmCollectProduct,

    RequestStop,
    Stopped,
}
