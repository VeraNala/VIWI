using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using VIWI.Helpers;

namespace VIWI.Modules.Workshoppa.GameData;

internal sealed class GameStrings
{
    public GameStrings(IDataManager dataManager, IPluginLog pluginLog)
    {
        PurchaseItemForGil = dataManager.GetRegex<Addon>(3406, addon => addon.Text, pluginLog)
                             ?? throw new ConstraintException($"Unable to resolve {nameof(PurchaseItemForGil)}");
        PurchaseItemForCompanyCredits = dataManager.GetRegex<Addon>(3473, addon => addon.Text, pluginLog)
                                        ?? throw new ConstraintException($"Unable to resolve {nameof(PurchaseItemForCompanyCredits)}");
        ViewCraftingLog =
            dataManager.GetString<WorkshopDialogue>("TEXT_CMNDEFCOMPANYMANUFACTORY_00150_MENU_CC_NOTE",
                pluginLog) ?? throw new ConstraintException($"Unable to resolve {nameof(ViewCraftingLog)}");
        TurnInHighQualityItem = dataManager.GetString<Addon>(102434, addon => addon.Text, pluginLog)
                                ?? throw new ConstraintException($"Unable to resolve {nameof(TurnInHighQualityItem)}");
        ContributeItems = dataManager.GetRegex<Addon>(6652, addon => addon.Text, pluginLog)
                          ?? throw new ConstraintException($"Unable to resolve {nameof(ContributeItems)}");
        RetrieveFinishedItem =
            dataManager.GetRegex<WorkshopDialogue>("TEXT_CMNDEFCOMPANYMANUFACTORY_00150_FINISH_CONF", pluginLog)
            ?? throw new ConstraintException($"Unable to resolve {nameof(RetrieveFinishedItem)}");
    }

    public Regex PurchaseItemForGil { get; }
    public Regex PurchaseItemForCompanyCredits { get; }
    public string ViewCraftingLog { get; }
    public string TurnInHighQualityItem { get; }
    public Regex ContributeItems { get; }
    public Regex RetrieveFinishedItem { get; }

    [Sheet("custom/001/CmnDefCompanyManufactory_00150")]
    [SuppressMessage("Performance", "CA1812")]
    private readonly struct WorkshopDialogue(ExcelPage page, uint offset, uint row)
        : IQuestDialogueText, IExcelRow<WorkshopDialogue>
    {
        public uint RowId => row;
        public ExcelPage ExcelPage => page;
        public uint RowOffset => offset;

        public ReadOnlySeString Key => page.ReadString(offset, offset);
        public ReadOnlySeString Value => page.ReadString(offset + 4, offset);

        static WorkshopDialogue IExcelRow<WorkshopDialogue>.Create(ExcelPage page, uint offset,
            uint row) =>
            new(page, offset, row);
    }
}
