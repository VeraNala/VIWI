using Dalamud.Plugin.Services;
using ECommons.Automation;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using TerraFX.Interop.Windows;
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
        DiscontinueItem = dataManager.GetRegex<WorkshopDialogue>("TEXT_CMNDEFCOMPANYMANUFACTORY_00150_SUBMENU_CC_BREAK_ALL_CONF", pluginLog)
            ?? throw new ConstraintException($"Unable to resolve {nameof(DiscontinueItem)}");
    }

    public Regex PurchaseItemForGil { get; }
    public Regex PurchaseItemForCompanyCredits { get; }
    public string ViewCraftingLog { get; }
    public string TurnInHighQualityItem { get; }
    public Regex ContributeItems { get; }
    public Regex RetrieveFinishedItem { get; }
    public Regex DiscontinueItem { get; }

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
    /*
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_MENU_TITLE => What would you like to do?
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_MENU_CC_NOTE => View company crafting log.
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_MENU_CC_SUPPLY => Contribute materials. (Quality: /100)
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_MENU_CC_BREAK => Discontinue project.
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_MENU_CC_GET_ITEM => Collect finished product.
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_MENU_CC_PROGRESS => Advance to the next phase of production. (Quality: /100)
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_MENU_CC_LAST_PROGRESS => Complete the construction of. (Quality: /100)
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_MENU_EXIT => Nothing.
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_SUBMENU_CC_BREAK_ALL_CONF => Are you certain you wish to discontinue the construction of ?
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_SUBMENU_CC_BREAK_ALL_CHECK => Discontinue project.
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_SUBMENU_CC_BREAK_YES => Yes
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_SUBMENU_CC_BREAK_NO => No
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_FINISH_CONF => Retrieve from the company workshop?
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_FINISH_YES => Yes
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_FINISH_NO => No
        TEXT_CMNDEFCOMPANYMANUFACTORY_00150_NULL => */
}

