using System.Collections.Generic;
using Generated;
using UnityEngine;
using static Generated.GameData;

public sealed class KitchenPlateModule : KitchenGameModule
{
    [ModuleRef] KitchenResourceModule _resourceModule;

    readonly List<int> _recipeGroupIds = new();
    readonly List<RecipeInfoData> _recipeInfoDataList = new();

    readonly Dictionary<KitchenPlateCombinationKey, RecipeInfoData> _recipeInfoDataByKey =
        new();

    public override void OnRegister()
    {
        base.OnRegister();

        _recipeGroupIds.Clear();
        _recipeInfoDataList.Clear();
        _recipeInfoDataByKey.Clear();

        GameData.Instance.CollectStageRecipeGroupIds(Context.CurrentStageData, _recipeGroupIds);
        BuildRecipeCache();
    }

    public override void OnUnregister()
    {
        _recipeGroupIds.Clear();
        _recipeInfoDataList.Clear();
        _recipeInfoDataByKey.Clear();

        base.OnUnregister();
    }

    public void SpawnPlateObject(PlateObject plateTemplate, IHolder holder)
    {
        var plateObject = Object.Instantiate(plateTemplate);
        if (!plateObject.gameObject.activeSelf)
        {
            plateObject.gameObject.SetActive(true);
        }

        plateObject.Initialize(Context);
        HoldableTransferLogic.TryMoveTo(plateObject, holder);
    }

    public bool TryAddIngredient(PlateObject plateObject, IngredientObject ingredientObject)
    {
        return TryAddIngredient(plateObject, ingredientObject, out _);
    }

    public bool TryAddIngredient(
        PlateObject plateObject,
        IngredientObject ingredientObject,
        out RecipeInfoData completedRecipeInfoData)
    {
        if (plateObject == null || ingredientObject == null)
        {
            completedRecipeInfoData = null;
            return false;
        }

        completedRecipeInfoData = null;
        var ingredientId = ingredientObject.IngredientId;
        if (ingredientId <= 0 || plateObject.ContainsIngredient(ingredientId))
        {
            return false;
        }

        if (!TryGetPlateActionInfo(ingredientObject, out var plateActionInfoData))
        {
            return false;
        }

        using var _ = ListPool<int>.GetList(out var candidateIngredientIds);
        KitchenCollectionLogic.CopyIngredientIds(plateObject.IngredientIds, candidateIngredientIds);
        candidateIngredientIds.Add(ingredientId);

        var candidateKey = new KitchenPlateCombinationKey(candidateIngredientIds);
        if (!candidateKey.IsValid || !HasRecipeCandidate(candidateIngredientIds))
        {
            return false;
        }

        TryResolveRecipe(candidateKey, out completedRecipeInfoData);

        if (!plateObject.AddIngredient(ingredientId))
        {
            return false;
        }

        ingredientObject.CompleteAction(plateActionInfoData);
        TryApplyPlateVisual(plateObject, candidateKey);
        return true;
    }

    public bool TryResolveRecipe(
        PlateObject plateObject,
        out RecipeInfoData recipeInfoData)
    {
        recipeInfoData = null;
        return plateObject != null && TryResolveRecipe(plateObject.CombinationKey, out recipeInfoData);
    }

    public bool TryResolveRecipe(
        KitchenPlateCombinationKey combinationKey,
        out RecipeInfoData recipeInfoData)
    {
        recipeInfoData = null;
        return combinationKey.IsValid &&
               _recipeInfoDataByKey.TryGetValue(combinationKey, out recipeInfoData);
    }

    void BuildRecipeCache()
    {
        var stageData = Context.CurrentStageData;
        if (stageData == null || stageData.RecipeIds == null)
        {
            return;
        }

        for (var i = 0; i < stageData.RecipeIds.Length; i++)
        {
            var recipeId = stageData.RecipeIds[i];
            var recipeInfoData = GameData.Instance.GetRecipeInfoData(recipeId);
            if (recipeInfoData == null ||
                recipeInfoData.IngredientIds == null ||
                recipeInfoData.IngredientIds.Length == 0)
            {
                continue;
            }

            _recipeInfoDataList.Add(recipeInfoData);

            var recipeKey = recipeInfoData.GetPlateCombinationKey();
            if (!recipeKey.IsValid || _recipeInfoDataByKey.ContainsKey(recipeKey))
            {
                continue;
            }

            _recipeInfoDataByKey.Add(recipeKey, recipeInfoData);
        }
    }

    bool TryGetPlateActionInfo(
        IngredientObject ingredientObject,
        out IngredientActionInfoData actionInfoData)
    {
        actionInfoData = null;
        if (ingredientObject.IsBurned)
        {
            return false;
        }

        return ingredientObject.CanExecuteAction(
            ActionType.Plate,
            _recipeGroupIds,
            out actionInfoData);
    }

    void TryApplyPlateVisual(PlateObject plateObject, KitchenPlateCombinationKey combinationKey)
    {
        if (_resourceModule == null)
        {
            Debug.LogWarning(
                "[KitchenPlateModule] KitchenResourceModule is missing. " +
                "Plate combination committed without visual change.");
            return;
        }

        if (!_resourceModule.TryGetPlateVisualPrefabAddress(combinationKey, out var plateVisualPrefabAddress))
        {
            Debug.LogWarning(
                $"[KitchenPlateModule] Plate visual prefab path not found. ingredientCount: {combinationKey.Count}. " +
                "Plate combination committed without visual change.");
            return;
        }

        var plateVisualPrefab = _resourceModule.LoadPlateVisualPrefab(plateVisualPrefabAddress);
        if (plateVisualPrefab == null)
        {
            Debug.LogWarning(
                $"[KitchenPlateModule] Plate visual prefab load failed. address: {plateVisualPrefabAddress}. " +
                "Plate combination committed without visual change.");
            return;
        }

        plateObject.ApplyPlateVisual(plateVisualPrefab);
    }

    bool HasRecipeCandidate(IReadOnlyList<int> candidateIngredientIds)
    {
        if (candidateIngredientIds == null || candidateIngredientIds.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < _recipeInfoDataList.Count; i++)
        {
            var recipeInfoData = _recipeInfoDataList[i];
            if (KitchenPlateLogic.IsSubsetOfRecipe(candidateIngredientIds, recipeInfoData.IngredientIds))
            {
                return true;
            }
        }

        return false;
    }
}
