using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace VIWI.Modules.Workshoppa.GameData;

internal sealed class WorkshopCache
{
    public WorkshopCache(IDataManager dataManager, IPluginLog pluginLog)
    {
        Task.Run(() =>
        {
            try
            {
                Dictionary<uint, Item> itemMapping = dataManager.GetExcelSheet<CompanyCraftSupplyItem>()
                    .Where(x => x.RowId > 0)
                    .ToDictionary(x => x.RowId, x => x.Item.Value);

                Crafts = dataManager.GetExcelSheet<CompanyCraftSequence>()
                    .Where(x => x.RowId > 0)
                    .Select(x => new WorkshopCraft
                    {
                        WorkshopItemId = x.RowId,
                        ResultItem = x.ResultItem.RowId,
                        Name = x.ResultItem.Value.Name.ToString(),
                        IconId = x.ResultItem.Value.Icon,
                        Category = (WorkshopCraftCategory)x.CompanyCraftDraftCategory.RowId,
                        Type = x.CompanyCraftType.RowId,
                        Phases = x.CompanyCraftPart.Where(part => part.RowId != 0)
                            .SelectMany(part =>
                                part.Value.CompanyCraftProcess
                                    .Select(y => new WorkshopCraftPhase
                                    {
                                        Name = part.Value.CompanyCraftType.Value.Name.ToString(),
                                        Items = Enumerable.Range(0, y.Value.SupplyItem.Count)
                                            .Select(i => new
                                            {
                                                SupplyItem = y.Value.SupplyItem[i],
                                                SetsRequired = y.Value.SetsRequired[i],
                                                SetQuantity = y.Value.SetQuantity[i],
                                            })
                                            .Where(item => item.SupplyItem.RowId > 0)
                                            .Select(item => new WorkshopCraftItem
                                            {
                                                ItemId = itemMapping[item.SupplyItem.RowId].RowId,
                                                Name = itemMapping[item.SupplyItem.RowId].Name.ToString(),
                                                IconId = itemMapping[item.SupplyItem.RowId].Icon,
                                                SetQuantity = item.SetQuantity,
                                                SetsRequired = item.SetsRequired,
                                            })
                                            .ToList()
                                            .AsReadOnly(),
                                    }))
                            .ToList()
                            .AsReadOnly(),
                    })
                    .ToList()
                    .AsReadOnly();
            }
            catch (Exception e)
            {
                pluginLog.Error(e, "Unable to load cached items");
            }
        });
    }

    public IReadOnlyList<WorkshopCraft> Crafts { get; private set; } = new List<WorkshopCraft>();
}
