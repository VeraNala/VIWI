using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace VIWI.Modules.Workshoppa.GameData;

internal sealed class RecipeTree
{
    private readonly IDataManager _dataManager;
    private readonly IPluginLog _pluginLog;
    private readonly IReadOnlyList<uint> _shopItemsOnly;

    public RecipeTree(IDataManager dataManager, IPluginLog pluginLog)
    {
        _dataManager = dataManager;
        _pluginLog = pluginLog;

        // probably incomplete, e.g. different housing districts have different shop types
        var shopVendorIds = new uint[]
        {
            262461, // Purchase Items (Lumber, Metal, Stone, Bone, Leather)
            262462, // Purchase Items (Cloth, Reagents)
            262463, // Purchase Items (Gardening, Dyes)
            262471, // Purchase Items (Catalysts)
            262472, // Purchase (Cooking Ingredients)

            262692, // Amalj'aa
            262422, // Housing District Merchant
            262211, // Z'ranmaia, upper decks
        };

        _shopItemsOnly = _dataManager.GetSubrowExcelSheet<GilShopItem>()
            .Flatten()
            .Where(x => shopVendorIds.Contains(x.RowId))
            .Select(x => x.Item.RowId)
            .Where(x => x > 0)
            .Distinct()
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<Ingredient> ResolveRecipes(IReadOnlyList<Ingredient> materials)
    {
        // look up recipes recursively
        int limit = 10;
        List<RecipeInfo> nextStep = ExtendWithAmountCrafted(materials);
        List<RecipeInfo> completeList = new(nextStep);
        while (--limit > 0 && nextStep.Any(x => x.Type == Ingredient.EType.Craftable))
        {
            nextStep = GetIngredients(nextStep);
            completeList.AddRange(nextStep);
        }

        // sum up all recipes
        completeList = completeList.GroupBy(x => x.ItemId)
            .Select(x => new RecipeInfo
            {
                ItemId = x.Key,
                Name = x.First().Name,
                TotalQuantity = x.Sum(y => y.TotalQuantity),
                Type = x.First().Type,
                DependsOn = x.First().DependsOn,
                AmountCrafted = x.First().AmountCrafted,
            })
            .ToList();
        _pluginLog.Verbose("Complete craft list:");
        foreach (var item in completeList)
            _pluginLog.Verbose($"  {item.TotalQuantity}x {item.Name}");

        // if a recipe has a specific amount crafted, divide the gathered amount by it
        foreach (var ingredient in completeList.Where(x => x is { AmountCrafted: > 1 }))
        {
            //_pluginLog.Information($"Fudging {ingredient.Name}");
            foreach (var part in completeList.Where(x => ingredient.DependsOn.Contains(x.ItemId)))
            {
                //_pluginLog.Information($"   → {part.Name}");

                int unmodifiedQuantity = part.TotalQuantity;
                int roundedQuantity =
                    (int)((unmodifiedQuantity + ingredient.AmountCrafted - 1) / ingredient.AmountCrafted);
                part.TotalQuantity = part.TotalQuantity - unmodifiedQuantity + roundedQuantity;
            }
        }

        // figure out the correct order for items to be crafted
        foreach (var item in completeList.Where(x => x.Type == Ingredient.EType.ShopItem))
            item.DependsOn.Clear();
        List<RecipeInfo> sortedList = new List<RecipeInfo>();
        while (sortedList.Count < completeList.Count)
        {
            _pluginLog.Verbose("Sort round");
            var canBeCrafted = completeList.Where(x =>
                    !sortedList.Contains(x) && x.DependsOn.All(y => sortedList.Any(z => y == z.ItemId)))
                .ToList();
            foreach (var item in canBeCrafted)
                _pluginLog.Verbose($"  can craft: {item.TotalQuantity}x {item.Name}");
            if (canBeCrafted.Count == 0)
            {
                foreach (var item in completeList.Where(x => !sortedList.Contains(x)))
                    _pluginLog.Warning($"  can't craft: {item.TotalQuantity}x {item.Name} → ({string.Join(", ", item.DependsOn.Where(y => sortedList.All(z => y != z.ItemId)))})");
                throw new InvalidOperationException("Unable to sort items");
            }

            sortedList.AddRange(canBeCrafted.OrderBy(x => x.Name));
        }

        return sortedList.Cast<Ingredient>().ToList();
    }

    private List<RecipeInfo> GetIngredients(List<RecipeInfo> materials)
    {
        List<RecipeInfo> ingredients = new();
        foreach (var material in materials.Where(x => x.Type == Ingredient.EType.Craftable))
        {
            //_pluginLog.Information($"Looking up recipe for {material.Name}");

            var recipe = GetFirstRecipeForItem(material.ItemId);
            if (recipe == null)
                continue;

            for (int i = 0; i < 8; ++ i)
            {
                var ingredient = recipe.Value.Ingredient[i];
                if (!ingredient.IsValid || ingredient.RowId == 0)
                    continue;

                Item item = ingredient.Value;
                if (!IsValidItem(item.RowId))
                    continue;

                Recipe? ingredientRecipe = GetFirstRecipeForItem(ingredient.RowId);

                //_pluginLog.Information($"Adding {item.Name}");
                ingredients.Add(new RecipeInfo
                {
                    ItemId = ingredient.RowId,
                    Name = item.Name.ToString(),
                    TotalQuantity = material.TotalQuantity * recipe.Value.AmountIngredient[i],
                    Type =
                        _shopItemsOnly.Contains(ingredient.RowId) ? Ingredient.EType.ShopItem :
                        ingredientRecipe != null ? Ingredient.EType.Craftable :
                        GetGatheringItem(ingredient.RowId) != null ? Ingredient.EType.Gatherable :
                        GetVentureItem(ingredient.RowId) != null ? Ingredient.EType.Gatherable :
                        Ingredient.EType.Other,

                    AmountCrafted = ingredientRecipe?.AmountResult ?? 1,
                    DependsOn = ingredientRecipe?.Ingredient.Where(x => x.IsValid && IsValidItem(x.RowId))
                                    .Select(x => x.RowId)
                                    .ToList()
                                ?? new(),
                });
            }
        }

        return ingredients;
    }

    private List<RecipeInfo> ExtendWithAmountCrafted(IEnumerable<Ingredient> materials)
    {
        return materials.Select(x => new
            {
                Ingredient = x,
                Recipe = GetFirstRecipeForItem(x.ItemId)
            })
            .Where(x => x.Recipe != null)
            .Select(x => new RecipeInfo
            {
                ItemId = x.Ingredient.ItemId,
                Name = x.Ingredient.Name,
                TotalQuantity = x.Ingredient.TotalQuantity,
                Type = _shopItemsOnly.Contains(x.Ingredient.ItemId) ? Ingredient.EType.ShopItem : x.Ingredient.Type,
                AmountCrafted = x.Recipe!.Value.AmountResult,
                DependsOn = x.Recipe.Value.Ingredient.Where(y => y.IsValid && IsValidItem(y.RowId))
                    .Select(y => y.RowId)
                    .ToList(),
            })
            .ToList();
    }

    private Recipe? GetFirstRecipeForItem(uint itemId)
    {
        return _dataManager.GetExcelSheet<Recipe>().FirstOrDefault(x => x.RowId > 0 && x.ItemResult.RowId == itemId);
    }

    private GatheringItem? GetGatheringItem(uint itemId)
    {
        return _dataManager.GetExcelSheet<GatheringItem>().FirstOrDefault(x => x.RowId > 0 && x.Item.RowId == itemId);
    }

    private RetainerTaskNormal? GetVentureItem(uint itemId)
    {
        return _dataManager.GetExcelSheet<RetainerTaskNormal>()
            .FirstOrDefault(x => x.RowId > 0 && x.Item.RowId == itemId);
    }

    private static bool IsValidItem(uint itemId)
    {
        return itemId > 19 && itemId != uint.MaxValue;
    }

    private sealed class RecipeInfo : Ingredient
    {
        public required uint AmountCrafted { get; init; }
        public required List<uint> DependsOn { get; init; }
    }
}
