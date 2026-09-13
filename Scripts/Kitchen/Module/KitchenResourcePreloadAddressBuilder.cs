using System.Collections.Generic;
using Generated;
using static Generated.GameData;

public sealed class KitchenResourcePreloadAddressBuilder
{
    public void Build(
        StageInfoData stageData,
        List<string> prefabAddresses,
        List<string> iconAddresses,
        string deliveryResultUIPrefabAddress)
    {
        if (prefabAddresses == null || iconAddresses == null)
        {
            return;
        }

        prefabAddresses.Clear();
        iconAddresses.Clear();

        using var _ = ListPool<int>.GetList(out var ingredientIds);
        CollectStageIngredientIds(stageData, ingredientIds);

        for (var i = 0; i < ingredientIds.Count; i++)
        {
            var ingredientInfoData = GameData.Instance.GetIngredientInfoData(ingredientIds[i]);
            AddIngredientAddresses(ingredientInfoData, prefabAddresses, iconAddresses);
        }

        AddRecipeIconAddresses(stageData, iconAddresses);
        KitchenCollectionLogic.AddUniqueAddress(prefabAddresses, deliveryResultUIPrefabAddress);
    }

    void CollectStageIngredientIds(StageInfoData stageData, List<int> results)
    {
        if (stageData == null || stageData.RecipeIds == null)
        {
            return;
        }

        for (var i = 0; i < stageData.RecipeIds.Length; i++)
        {
            var recipeData = GameData.Instance.GetRecipeInfoData(stageData.RecipeIds[i]);
            if (recipeData == null)
            {
                continue;
            }

            KitchenCollectionLogic.AddUniqueIngredientIds(results, recipeData.IngredientIds);
        }
    }

    void AddIngredientAddresses(
        IngredientInfoData ingredientInfoData,
        List<string> prefabAddresses,
        List<string> iconAddresses)
    {
        if (ingredientInfoData == null)
        {
            return;
        }

        var gameData = GameData.Instance;
        KitchenCollectionLogic.AddUniqueAddress(prefabAddresses, ingredientInfoData.PrefabPath);
        KitchenCollectionLogic.AddUniqueAddress(iconAddresses, ingredientInfoData.IconPath);
        KitchenCollectionLogic.AddUniqueAddress(iconAddresses, GetIngredientSpriteIconAddress(ingredientInfoData));

        using var _ = ListPool<IngredientActionInfoData>.GetList(out var actionInfoDataList);
        gameData.CollectIngredientActionInfoData(ingredientInfoData.IngredientId, actionInfoDataList);
        for (var i = 0; i < actionInfoDataList.Count; i++)
        {
            var actionInfoData = actionInfoDataList[i];
            if (actionInfoData == null)
            {
                continue;
            }

            var actionData = actionInfoData.ActionInfo;
            if (actionData != null)
            {
                KitchenCollectionLogic.AddUniqueAddress(iconAddresses, actionData.IconPath);
            }

            KitchenCollectionLogic.AddUniqueAddress(prefabAddresses, actionInfoData.PrefabPath);
        }
    }

    void AddRecipeIconAddresses(StageInfoData stageData, List<string> iconAddresses)
    {
        if (stageData == null || stageData.RecipeIds == null)
        {
            return;
        }

        for (var i = 0; i < stageData.RecipeIds.Length; i++)
        {
            var recipeData = GameData.Instance.GetRecipeInfoData(stageData.RecipeIds[i]);
            if (recipeData != null)
            {
                KitchenCollectionLogic.AddUniqueAddress(iconAddresses, recipeData.IconPath);
            }
        }
    }

    string GetIngredientSpriteIconAddress(IngredientInfoData ingredientInfoData)
    {
        return ingredientInfoData != null ? ingredientInfoData.SpritePath : string.Empty;
    }
}
