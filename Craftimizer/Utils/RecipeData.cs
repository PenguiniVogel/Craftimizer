using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using Craftimizer.Solver;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ClassJob = Craftimizer.Simulator.ClassJob;

namespace Craftimizer.Utils;

public sealed record RecipeData
{
    public sealed unsafe class CSRecipeEntryPtr(RecipeNote.RecipeEntry* recipeEntry)
    {
        public ushort RecipeId { get; } = recipeEntry->RecipeId;
        public ushort Durability { get; } = recipeEntry->Durability;
        public uint Quality { get; } = recipeEntry->Quality;
        public ushort Difficulty { get; } = recipeEntry->Difficulty;
        public ushort ClassJobLevel { get; } = recipeEntry->ClassJobLevel;
        public ushort RecipeLevelTableId { get; } = recipeEntry->RecipeLevelTableId;
    } 
    
    public static CSRecipeEntryPtr? LastRecipeEntry { get; set; }
    
    public static SolverSolution? SuggestedMacro { get; set; }
    
    public ushort RecipeId { get; }

    public Recipe Recipe { get; }
    public RecipeLevelTable Table { get; }

    public ClassJob ClassJob { get; }
    
    private RecipeInfo? _backingRecipeInfo;
    public RecipeInfo RecipeInfo
    {
        get
        {
            ArgumentNullException.ThrowIfNull(_backingRecipeInfo);
            
            if (LastRecipeEntry != null && LastRecipeEntry.RecipeId == _backingRecipeInfo.RecipeId)
            {
                _backingRecipeInfo = _backingRecipeInfo with { MaxDurability = LastRecipeEntry.Durability, MaxQuality = (int)LastRecipeEntry.Quality, MaxProgress = LastRecipeEntry.Difficulty };
            }
            
            return _backingRecipeInfo;
        }
        init
        {
            _backingRecipeInfo = value;
        }
    }
    
    public bool IsCollectable => Recipe.ItemResult.ValueNullable?.AlwaysCollectable ?? false;
    public IReadOnlyList<int?>? CollectableThresholds { get; }
    public IReadOnlyList<(Item Item, int Amount)> Ingredients { get; }
    public int MaxStartingQuality { get; }
    public ushort? AdjustedJobLevel { get; }
    private int TotalHqILvls { get; }

    public RecipeData(ushort recipeId, ushort? explicitlyAdjustedJobLevel = null)
    {
        RecipeId = recipeId;

        Recipe = LuminaSheets.RecipeSheet.GetRowOrDefault(recipeId) ??
            throw new ArgumentException($"Invalid recipe id {recipeId}", nameof(recipeId));

        ClassJob = (ClassJob)Recipe.CraftType.RowId;

        if (LastRecipeEntry != null && LastRecipeEntry.RecipeId == recipeId)
        {
            Table = LuminaSheets.RecipeLevelTableSheet.GetRow(LastRecipeEntry.RecipeLevelTableId);
            
            RecipeInfo = new()
            {
                RecipeId = recipeId,
                IsExpert = Recipe.IsExpert,
                ClassJobLevel = Table.ClassJobLevel,
                ConditionsFlag = Table.ConditionsFlag,
                MaxDurability = LastRecipeEntry.Durability,
                MaxQuality = (int)LastRecipeEntry.Quality,
                MaxProgress = LastRecipeEntry.Difficulty,
                QualityModifier = Table.QualityModifier,
                QualityDivider = Table.QualityDivider,
                ProgressModifier = Table.ProgressModifier,
                ProgressDivider = Table.ProgressDivider,
            };
        }
        else
        {
            Table = LuminaSheets.RecipeLevelTableSheet.GetRow(Recipe.RecipeLevelTable.RowId);
            
            RecipeInfo = new()
            {
                RecipeId = recipeId,
                IsExpert = Recipe.IsExpert,
                ClassJobLevel = Table.ClassJobLevel,
                ConditionsFlag = Table.ConditionsFlag,
                MaxDurability = Table.Durability,
                MaxQuality = (int)Table.Quality,
                MaxProgress = Table.Difficulty,
                QualityModifier = Table.QualityModifier,
                QualityDivider = Table.QualityDivider,
                ProgressModifier = Table.ProgressModifier,
                ProgressDivider = Table.ProgressDivider,
            };
        }

        int[]? thresholds = null;
        if (Recipe.CollectableMetadata.GetValueOrDefault<CollectablesShopRefine>() is { } row)
            thresholds = [row.LowCollectability, row.MidCollectability, row.HighCollectability];
        else if (Recipe.CollectableMetadata.GetValueOrDefault<HWDCrafterSupply>() is { } row2)
        {
            foreach (var entry in row2.HWDCrafterSupplyParams)
            {
                if (entry.ItemTradeIn.RowId == Recipe.ItemResult.RowId)
                {
                    thresholds = [entry.BaseCollectableRating, entry.MidCollectableRating, entry.HighCollectableRating];
                    break;
                }
            }
        }
        else if (Recipe.CollectableMetadata.GetValueOrDefaultSubrow<SatisfactionSupply>() is { } row3)
        {
            foreach (var subrow in row3)
            {
                if (subrow.Item.RowId == Recipe.ItemResult.RowId)
                {
                    thresholds = [subrow.CollectabilityLow, subrow.CollectabilityMid, subrow.CollectabilityHigh];
                    break;
                }
            }
        }
        else if (Recipe.CollectableMetadata.GetValueOrDefault<SharlayanCraftWorksSupply>() is { } row5)
        {
            foreach (var item in row5.Item)
            {
                if (item.ItemId.RowId == Recipe.ItemResult.RowId)
                {
                    thresholds = [item.CollectabilityMid, item.CollectabilityHigh];
                    break;
                }
            }
        }
        else if (Recipe.CollectableMetadata.GetValueOrDefault<CollectablesRefine>() is { } row6)
            thresholds = [row6.CollectabilityLow, row6.CollectabilityMid, row6.CollectabilityHigh];
        else if (Recipe.CollectableMetadataKey == 7 && LuminaSheets.WKSMissionToDoEvalutionRefinSheet.TryGetRow(Recipe.CollectableMetadata.RowId, out var row7))
        {
            thresholds = [row7.Unknown0, row7.Unknown1, row7.Unknown2];
            thresholds = [.. thresholds.Select(percentage => RecipeInfo.MaxQuality * percentage / 1000)];
        }

        if (thresholds != null)
        {
            var t = thresholds.Where(t => t != 0).Cast<int?>();
            t = Enumerable.Concat(Enumerable.Repeat((int?)null, 3 - t.Count()), t);
            CollectableThresholds = t.ToArray();
        }

        Ingredients = Recipe.Ingredient.Zip(Recipe.AmountIngredient)
            .Take(6)
            .Where(i => i.First.IsValid)
            .Select(i => (i.First.Value, (int)i.Second))
            .ToList();
        
        MaxStartingQuality = (int)Math.Floor(Recipe.MaterialQualityFactor * RecipeInfo.MaxQuality / 100f);

        TotalHqILvls = (int)Ingredients.Where(i => i.Item.CanBeHq).Sum(i => i.Item.LevelItem.RowId * i.Amount);
    }

    public int CalculateItemStartingQuality(int itemIdx, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(itemIdx, Ingredients.Count);

        var ingredient = Ingredients[itemIdx];
        ArgumentOutOfRangeException.ThrowIfGreaterThan(amount, ingredient.Amount);

        if (!ingredient.Item.CanBeHq)
            return 0;

        var iLvls = ingredient.Item.LevelItem.RowId * amount;
        return (int)Math.Floor((float)iLvls / TotalHqILvls * MaxStartingQuality);
    }

    public int CalculateStartingQuality(IEnumerable<int> hqQuantities)
    {
        if (TotalHqILvls == 0)
            return 0;

        var iLvls = Ingredients.Zip(hqQuantities).Sum(i => i.First.Item.LevelItem.RowId * i.Second);

        return (int)Math.Floor((float)iLvls / TotalHqILvls * MaxStartingQuality);
    }
}
