using System.Collections.Generic;
using UnityEngine;
using static Generated.GameData;

public sealed class KitchenMenuData
{
    readonly List<int> _ingredientIds = new();

    public int RecipeId { get; }
    public RecipeInfoData RecipeInfoData { get; }
    public IReadOnlyList<int> IngredientIds => _ingredientIds;
    public KitchenPlateCombinationKey CombinationKey { get; }
    public int Score { get; }
    public int ExpiredPenaltyScore { get; }
    public float Duration { get; }
    public float RemainingTime { get; private set; }
    public float RemainingTimeNormalized => Duration > 0f ? Mathf.Clamp01(RemainingTime / Duration) : 0f;
    
    public KitchenMenuData(RecipeInfoData recipeInfoData)
    {
        RecipeInfoData = recipeInfoData;
        RecipeId = recipeInfoData.RecipeId;
        Score = recipeInfoData.Score;
        ExpiredPenaltyScore = recipeInfoData.ExpiredPenaltyScore;
        Duration = recipeInfoData.DurationSeconds;
        RemainingTime = Duration;

        AddIngredients(recipeInfoData.IngredientIds);
        CombinationKey = recipeInfoData.GetPlateCombinationKey();
    }

    public bool Tick(float deltaTime)
    {
        if (deltaTime <= 0f || RemainingTime <= 0f)
        {
            return false;
        }

        RemainingTime = Mathf.Max(0f, RemainingTime - deltaTime);
        return RemainingTime <= 0f;
    }

    void AddIngredients(int[] ingredientIds)
    {
        if (ingredientIds == null)
        {
            return;
        }

        for (var i = 0; i < ingredientIds.Length; i++)
        {
            AddIngredient(ingredientIds[i]);
        }
    }

    void AddIngredient(int ingredientId)
    {
        _ingredientIds.Add(ingredientId);
    }
}
