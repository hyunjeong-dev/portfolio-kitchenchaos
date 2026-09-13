using System.Collections.Generic;
using Generated;
using static Generated.GameData;
using UnityEditor;
using UnityEngine;

public sealed partial class KitchenMapEditorWindow
{
    void RefreshRequiredCounterStatuses()
    {
        _hasRequiredCounterStatuses = true;
        _requiredCounterStatusMessage = null;
        _requiredCounterStatuses.Clear();

        if (!TryGetEditorGameData(out var gameData, out var gameDataError))
        {
            _requiredCounterStatusMessage = gameDataError;
            Repaint();
            return;
        }

        var stageData = gameData.GetStageInfoDataByLevel(_stageLevel);
        if (stageData == null)
        {
            _requiredCounterStatusMessage = $"StageLevel에 해당하는 StageInfo가 없습니다. stageLevel: {_stageLevel}";
            Repaint();
            return;
        }

        var entries = CreateCounterEntriesFromOpenScene();
        var ingredientIds = new List<int>();
        for (var i = 0; i < stageData.RecipeIds.Length; i++)
        {
            var recipeId = stageData.RecipeIds[i];
            var recipeData = gameData.GetRecipeInfoData(recipeId);
            if (recipeData == null)
            {
                _requiredCounterStatusMessage = $"Stage RecipeId에 해당하는 RecipeInfo가 없습니다. recipeId: {recipeId}";
                Repaint();
                return;
            }

            AddIngredientIds(ingredientIds, recipeData.IngredientIds);
        }

        AddRequiredCounterStatus(
            "DeliveryCounter",
            CountCounterType(entries, KitchenMapCounterType.Delivery),
            KitchenMapCounterType.Delivery);
        AddRequiredCounterStatus(
            "TrashCounter",
            CountCounterType(entries, KitchenMapCounterType.Trash),
            KitchenMapCounterType.Trash);
        AddRequiredCounterStatus(
            "PlatesCounter",
            CountCounterType(entries, KitchenMapCounterType.Plates),
            KitchenMapCounterType.Plates);
        AddRequiredIngredientContainerStatuses(gameData, ingredientIds, entries);
        AddRequiredProcessCounterStatuses(gameData, stageData, ingredientIds, entries);
        Repaint();
    }

    void AddRequiredIngredientContainerStatuses(
        Generated.GameData gameData,
        IReadOnlyList<int> ingredientIds,
        IReadOnlyList<KitchenMapCounterEntry> entries)
    {
        for (var i = 0; i < ingredientIds.Count; i++)
        {
            var ingredientId = ingredientIds[i];
            var ingredientData = gameData.GetIngredientInfoData(ingredientId);
            if (ingredientData == null)
            {
                AddRequiredCounterStatus(
                    $"ContainerCounter ingredientId:{FormatGameDataId(ingredientId)}",
                    0,
                    KitchenMapCounterType.Container);
                continue;
            }

            var placedCount = CountContainerIngredient(entries, ingredientData.Code);
            AddRequiredCounterStatus(
                $"ContainerCounter_{ingredientData.Code}",
                placedCount,
                KitchenMapCounterType.Container,
                ingredientData.Code);
        }
    }

    void AddRequiredProcessCounterStatuses(
        Generated.GameData gameData,
        StageInfoData stageData,
        IReadOnlyList<int> ingredientIds,
        IReadOnlyList<KitchenMapCounterEntry> entries)
    {
        var requiresCuttingCounter = false;
        var requiresFryingPanCounter = false;
        var requiresPotCounter = false;
        var recipeGroupIds = new List<int>();
        gameData.CollectStageRecipeGroupIds(stageData, recipeGroupIds);
        for (var i = 0; i < ingredientIds.Count; i++)
        {
            var ingredientId = ingredientIds[i];
            requiresCuttingCounter |= gameData.HasIngredientAction(ingredientId, recipeGroupIds, ActionType.Cut);
            requiresFryingPanCounter |= gameData.HasIngredientAction(ingredientId, recipeGroupIds, ActionType.PanFry);
            requiresPotCounter |= gameData.HasIngredientAction(ingredientId, recipeGroupIds, ActionType.Boil);
        }

        if (requiresCuttingCounter)
        {
            AddRequiredCounterStatus(
                "CuttingCounter",
                CountCounterType(entries, KitchenMapCounterType.Cutting),
                KitchenMapCounterType.Cutting);
        }

        if (requiresFryingPanCounter)
        {
            AddRequiredCounterStatus(
                "BurnerCounter(FryingPan)",
                CountCounterType(entries, KitchenMapCounterType.FryingPan),
                KitchenMapCounterType.FryingPan);
        }

        if (requiresPotCounter)
        {
            AddRequiredCounterStatus(
                "BurnerCounter(Pot)",
                CountCounterType(entries, KitchenMapCounterType.Pot),
                KitchenMapCounterType.Pot);
        }
    }

    void AddRequiredCounterStatus(
        string label,
        int placedCount,
        KitchenMapCounterType counterType,
        string containerIngredientCode = null)
    {
        for (var i = 0; i < _requiredCounterStatuses.Count; i++)
        {
            if (_requiredCounterStatuses[i].Label == label)
            {
                _requiredCounterStatuses[i].PlacedCount += placedCount;
                _requiredCounterStatuses[i].IsPlaced = _requiredCounterStatuses[i].PlacedCount > 0;
                if (!string.IsNullOrWhiteSpace(containerIngredientCode))
                {
                    _requiredCounterStatuses[i].ContainerIngredientCode = containerIngredientCode;
                }

                return;
            }
        }

        _requiredCounterStatuses.Add(new RequiredCounterStatus
        {
            Label = label,
            IsPlaced = placedCount > 0,
            PlacedCount = placedCount,
            CounterType = counterType,
            ContainerIngredientCode = containerIngredientCode,
        });
    }

    void SetRequiredCounterAsGenerator(RequiredCounterStatus status)
    {
        if (status == null)
        {
            return;
        }

        if (IsRequiredCounterPlacementActive(status))
        {
            ClearRequiredCounterPlacement();
            return;
        }

        ActivateRequiredCounterPlacement(status);
    }

    bool IsRequiredCounterPlacementActive(RequiredCounterStatus status)
    {
        return status != null &&
               !string.IsNullOrEmpty(_activeRequiredCounterLabel) &&
               _activeRequiredCounterLabel == status.Label;
    }

    bool TryFindRequiredCounter(RequiredCounterStatus status, out BaseCounter foundCounter)
    {
        foundCounter = null;
        var counters = FindObjectsByType<BaseCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < counters.Length; i++)
        {
            var counter = counters[i];
            if (counter == null || !IsRequiredCounterMatch(status, counter))
            {
                continue;
            }

            foundCounter = counter;
            return true;
        }

        return false;
    }

    bool IsRequiredCounterMatch(RequiredCounterStatus status, BaseCounter counter)
    {
        if (KitchenMapSceneBuilder.ResolveCounterType(counter) != status.CounterType)
        {
            return false;
        }

        if (status.CounterType != KitchenMapCounterType.Container)
        {
            return true;
        }

        return counter is ContainerCounter containerCounter &&
               ResolveContainerIngredientCode(containerCounter) == status.ContainerIngredientCode;
    }

    void ActivateRequiredCounterPlacement(RequiredCounterStatus status)
    {
        _counterType = status.CounterType;
        _activeRequiredCounterLabel = status.Label;
        if (_isPlayerRootPlacementActive)
        {
            DeactivatePlayerRootPlacement();
        }

        if (_counterType == KitchenMapCounterType.Container &&
            !string.IsNullOrWhiteSpace(status.ContainerIngredientCode))
        {
            _containerIngredientCode = status.ContainerIngredientCode;
        }

        Selection.activeGameObject = null;
        _hasSelectedGridPosition = false;
        _hasLastPaintGridPosition = false;
        RepaintSceneAndWindow();
    }

    void ClearRequiredCounterPlacement()
    {
        _activeRequiredCounterLabel = null;
        RepaintSceneAndWindow();
    }

    void RefreshRequiredCounterStatusesIfVisible()
    {
        if (!_hasRequiredCounterStatuses)
        {
            return;
        }

        RefreshRequiredCounterStatuses();
    }

    void OnUndoRedoPerformed()
    {
        RefreshRequiredCounterStatusesIfVisible();
        RepaintSceneAndWindow();
    }

    static int ResolveIngredientIndex(
        Generated.GameData gameData,
        IReadOnlyList<int> ingredientIds,
        string ingredientCode)
    {
        for (var i = 0; i < ingredientIds.Count; i++)
        {
            var ingredientData = gameData.GetIngredientInfoData(ingredientIds[i]);
            if (ingredientData != null && ingredientData.Code == ingredientCode)
            {
                return i;
            }
        }

        return 0;
    }

    void EnsureIngredientOptions(Generated.GameData gameData)
    {
        if (_hasIngredientOptions)
        {
            return;
        }

        gameData.CollectIngredientInfoDataIds(_ingredientOptionIds);

        _ingredientOptionIds.Sort();
        _ingredientOptionLabels = CreateIngredientLabels(gameData, _ingredientOptionIds);
        _hasIngredientOptions = true;
    }

    string[] CreateIngredientLabels(Generated.GameData gameData, IReadOnlyList<int> ingredientIds)
    {
        var labels = new string[ingredientIds.Count];
        for (var i = 0; i < ingredientIds.Count; i++)
        {
            var ingredientData = gameData.GetIngredientInfoData(ingredientIds[i]);
            labels[i] = ingredientData != null
                ? $"{ingredientData.Code} ({FormatGameDataId(ingredientData.IngredientId)})"
                : $"Unknown ({FormatGameDataId(ingredientIds[i])})";
        }

        return labels;
    }

}
