namespace VIWI.Modules.Workshoppa;

public enum Stage
{
    TakeItemFromQueue,
    TargetFabricationStation,

    OpenCraftingLog,
    SelectCraftCategory,
    WaitCraftLogRefresh,
    SelectCraft,
    ConfirmCraft,

    SelectCraftBranch,
    ContributeMaterials,
    MergeStacks,
    OpenRequestItemWindow,
    OpenRequestItemSelect,
    ConfirmRequestItemWindow,
    ConfirmMaterialDelivery,

    ConfirmCollectProduct,
    CloseDeliveryMenu,
    DiscontinueProject,

    RequestStop,
    Stopped,  
}
