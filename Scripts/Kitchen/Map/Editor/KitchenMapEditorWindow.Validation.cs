using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Common.GameData;
using Generated;
using static Generated.GameData;
using UnityEditor;
using UnityEngine;

public sealed partial class KitchenMapEditorWindow
{
    bool TryValidateBuild(out List<KitchenMapCounterEntry> entries)
    {
        entries = CreateCounterEntriesFromOpenScene();

        var errors = new List<string>();
        ValidatePlacedCounters(entries, errors);

        if (TryGetEditorGameData(out var gameData, out var gameDataError))
        {
            ValidateStageCounters(gameData, entries, errors);
        }
        else
        {
            errors.Add(gameDataError);
        }

        if (errors.Count == 0)
        {
            return true;
        }

        ShowBuildValidationPopup(errors);
        return false;
    }

    void ValidatePlacedCounters(IReadOnlyList<KitchenMapCounterEntry> entries, List<string> errors)
    {
        var occupiedGrids = new List<Vector2Int>();
        var root = KitchenMapSceneBuilder.ResolveGridRoot();
        var counters = FindObjectsByType<BaseCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < counters.Length; i++)
        {
            var counter = counters[i];
            if (counter == null)
            {
                continue;
            }

            var gridPosition = ResolveCounterGridPosition(root, counter);
            if (ContainsGridPosition(gridPosition))
            {
                if (ContainsGrid(occupiedGrids, gridPosition))
                {
                    errors.Add($"같은 grid에 Counter가 중복 배치되었습니다. grid: {gridPosition}");
                    continue;
                }

                occupiedGrids.Add(gridPosition);
                continue;
            }

            errors.Add(
                $"배치된 Counter가 현재 Width/Height 범위를 벗어났습니다. name: {counter.name}, grid: {gridPosition}, size: {_width}x{_height}");
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (ContainsGridPosition(entry.GridPosition))
            {
                continue;
            }

            errors.Add(
                $"빌드 대상 Counter grid가 현재 Width/Height와 일치하지 않습니다. type: {entry.CounterType}, grid: {entry.GridPosition}, size: {_width}x{_height}");
        }

        ValidateFrameGridMatchesLayout(occupiedGrids, errors);
    }

    void ValidateFrameGridMatchesLayout(IReadOnlyList<Vector2Int> occupiedGrids, List<string> errors)
    {
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                if (!IsFrameGridPosition(x, y))
                {
                    continue;
                }

                var gridPosition = new Vector2Int(x, y);
                if (ContainsGrid(occupiedGrids, gridPosition))
                {
                    continue;
                }

                errors.Add(
                    $"입력된 Width/Height와 배치된 외곽 그리드가 일치하지 않습니다. 비어있는 외곽 grid: {gridPosition}, size: {_width}x{_height}");
            }
        }
    }

    void ValidateStageCounters(
        Generated.GameData gameData,
        IReadOnlyList<KitchenMapCounterEntry> entries,
        List<string> errors)
    {
        var stageData = gameData.GetStageInfoDataByLevel(_stageLevel);
        if (stageData == null)
        {
            errors.Add($"StageLevel에 해당하는 StageInfo가 없습니다. stageLevel: {_stageLevel}");
            return;
        }

        var hasDeliveryCounter = HasCounterType(entries, KitchenMapCounterType.Delivery);
        var hasTrashCounter = HasCounterType(entries, KitchenMapCounterType.Trash);
        var hasPlatesCounter = HasCounterType(entries, KitchenMapCounterType.Plates);
        if (!hasDeliveryCounter)
        {
            errors.Add("필수 Counter가 없습니다. type: Delivery");
        }

        if (!hasTrashCounter)
        {
            errors.Add("필수 Counter가 없습니다. type: Trash");
        }

        if (!hasPlatesCounter)
        {
            errors.Add("필수 Counter가 없습니다. type: Plates");
        }

        var ingredientIds = new List<int>();
        for (var i = 0; i < stageData.RecipeIds.Length; i++)
        {
            var recipeId = stageData.RecipeIds[i];
            var recipeData = gameData.GetRecipeInfoData(recipeId);
            if (recipeData == null)
            {
                errors.Add($"Stage RecipeId에 해당하는 RecipeInfo가 없습니다. stageLevel: {_stageLevel}, recipeId: {recipeId}");
                continue;
            }

            AddIngredientIds(ingredientIds, recipeData.IngredientIds);
        }

        ValidateIngredientContainerCounters(gameData, ingredientIds, errors);
        ValidateProcessCounters(gameData, stageData, ingredientIds, entries, errors);
    }

    void ValidateIngredientContainerCounters(
        Generated.GameData gameData,
        IReadOnlyList<int> ingredientIds,
        List<string> errors)
    {
        var containerIngredientIds = ResolveContainerIngredientIds(gameData);
        for (var i = 0; i < ingredientIds.Count; i++)
        {
            var ingredientId = ingredientIds[i];
            var ingredientData = gameData.GetIngredientInfoData(ingredientId);
            if (ingredientData == null)
            {
                errors.Add($"Recipe 재료에 해당하는 IngredientInfo가 없습니다. ingredientId: {FormatGameDataId(ingredientId)}");
                continue;
            }

            if (containerIngredientIds.Contains(ingredientId))
            {
                continue;
            }

            errors.Add(
                $"Recipe 재료를 생성할 ContainerCounter가 없습니다. ingredientId: {FormatGameDataId(ingredientId)}, code: {ingredientData.Code}");
        }
    }

    void ValidateProcessCounters(
        Generated.GameData gameData,
        StageInfoData stageData,
        IReadOnlyList<int> ingredientIds,
        IReadOnlyList<KitchenMapCounterEntry> entries,
        List<string> errors)
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

        if (requiresCuttingCounter && !HasCounterType(entries, KitchenMapCounterType.Cutting))
        {
            errors.Add("Recipe 재료 가공에 필요한 CuttingCounter가 없습니다.");
        }

        if (requiresFryingPanCounter && !HasCounterType(entries, KitchenMapCounterType.FryingPan))
        {
            errors.Add("Recipe 재료 조리에 필요한 FryingPan용 BurnerCounter가 없습니다.");
        }

        if (requiresPotCounter && !HasCounterType(entries, KitchenMapCounterType.Pot))
        {
            errors.Add("Recipe 재료 조리에 필요한 Pot용 BurnerCounter가 없습니다.");
        }
    }

    List<int> ResolveContainerIngredientIds(Generated.GameData gameData)
    {
        var ingredientIds = new List<int>();
        var counters = FindObjectsByType<ContainerCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < counters.Length; i++)
        {
            var counter = counters[i];
            if (counter == null)
            {
                continue;
            }

            var code = ResolveContainerIngredientCode(counter);
            var ingredientData = gameData.GetIngredientInfoDataByCode(code);
            if (ingredientData == null)
            {
                continue;
            }

            AddUnique(ingredientIds, ingredientData.IngredientId);
        }

        return ingredientIds;
    }

    static string ResolveContainerIngredientCode(ContainerCounter counter)
    {
        var serializedObject = new SerializedObject(counter);
        var ingredientCodeProperty = serializedObject.FindProperty("ingredientCode");
        if (ingredientCodeProperty != null && !string.IsNullOrWhiteSpace(ingredientCodeProperty.stringValue))
        {
            return ingredientCodeProperty.stringValue;
        }

        return GetIngredientCodeFromContainerName(counter.gameObject.name);
    }

    static string GetIngredientCodeFromContainerName(string objectName)
    {
        var separatorIndex = objectName.LastIndexOf('_');
        var value = separatorIndex >= 0 && separatorIndex < objectName.Length - 1
            ? objectName.Substring(separatorIndex + 1)
            : objectName;

        return TryGetIngredientCodeByResourceSuffix(value, out var code)
            ? code
            : ToCamelCase(value);
    }

    static bool TryGetIngredientCodeByResourceSuffix(string suffix, out string code)
    {
        code = null;
        if (string.IsNullOrEmpty(suffix) ||
            !TryLoadRuntimeEditorGameData(out var gameData))
        {
            return false;
        }

        var ingredientData = gameData.GetIngredientInfoDataByResourceSuffix(suffix) ??
                             gameData.GetIngredientInfoDataByCode(suffix);
        code = ingredientData != null ? ingredientData.Code : null;
        return ingredientData != null;
    }

    static bool TryLoadRuntimeEditorGameData(out GameData gameData)
    {
        gameData = null;

        var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(GAME_DATA_BINARY_PATH);
        if (textAsset == null)
        {
            return false;
        }

        try
        {
            var database = GameDataBinarySerializer.DeserializeDatabase(textAsset.bytes);
            gameData = GameData.Instance;
            gameData.Load(database);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static string ToCamelCase(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    bool TryGetEditorGameData(out Generated.GameData gameData, out string error)
    {
        if (_cachedEditorGameData != null)
        {
            gameData = _cachedEditorGameData;
            error = null;
            return true;
        }

        if (!string.IsNullOrEmpty(_cachedEditorGameDataError))
        {
            gameData = null;
            error = _cachedEditorGameDataError;
            return false;
        }

        if (LoadEditorGameData(out gameData, out error))
        {
            _cachedEditorGameData = gameData;
            _cachedEditorGameDataError = null;
            _hasIngredientOptions = false;
            return true;
        }

        _cachedEditorGameDataError = error;
        return false;
    }

    static bool LoadEditorGameData(out Generated.GameData gameData, out string error)
    {
        gameData = null;
        error = null;

        var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(GAME_DATA_BINARY_PATH);
        if (textAsset == null)
        {
            error = $"GameData binary를 찾을 수 없습니다. path: {GAME_DATA_BINARY_PATH}";
            return false;
        }

        try
        {
            var database = GameDataBinarySerializer.DeserializeDatabase(textAsset.bytes);
            gameData = new();
            gameData.Load(database);
            return true;
        }
        catch (System.Exception exception)
        {
            error = $"GameData 로드에 실패했습니다. reason: {exception.Message}";
            return false;
        }
    }

    string FormatGameDataId(int intId)
    {
        if (!TryGetEditorIdMap(out var idMap) ||
            !idMap.TryGet(intId, out var entries) ||
            entries.Count == 0)
        {
            return intId.ToString();
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry != null && entry.Status == GameDataIdStatus.Live)
            {
                return $"[{entry.StringId}]";
            }
        }

        return entries[0] != null
            ? $"[{entries[0].StringId}]"
            : intId.ToString();
    }

    bool TryGetEditorIdMap(out GameDataIdMap idMap)
    {
        if (_cachedEditorIdMap != null)
        {
            idMap = _cachedEditorIdMap;
            return true;
        }

        if (!string.IsNullOrEmpty(_cachedEditorIdMapError))
        {
            idMap = null;
            return false;
        }

        if (LoadEditorIdMap(out idMap, out var error))
        {
            _cachedEditorIdMap = idMap;
            _cachedEditorIdMapError = null;
            return true;
        }

        _cachedEditorIdMapError = error;
        return false;
    }

    static bool LoadEditorIdMap(out GameDataIdMap idMap, out string error)
    {
        idMap = null;
        error = null;

        if (!File.Exists(GAME_DATA_ID_MAP_PATH))
        {
            error = $"GameData IdMap을 찾을 수 없습니다. path: {GAME_DATA_ID_MAP_PATH}";
            return false;
        }

        try
        {
            var csvText = File.ReadAllText(GAME_DATA_ID_MAP_PATH, Encoding.UTF8);
            idMap = GameDataIdMap.FromCsv(csvText);
            return true;
        }
        catch (System.Exception exception)
        {
            error = $"GameData IdMap 로드에 실패했습니다. reason: {exception.Message}";
            return false;
        }
    }

    static int CountCounterType(IReadOnlyList<KitchenMapCounterEntry> entries, KitchenMapCounterType counterType)
    {
        var count = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].CounterType == counterType)
            {
                count++;
            }
        }

        return count;
    }

    static int CountContainerIngredient(IReadOnlyList<KitchenMapCounterEntry> entries, string ingredientCode)
    {
        if (string.IsNullOrWhiteSpace(ingredientCode))
        {
            return 0;
        }

        var count = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.CounterType == KitchenMapCounterType.Container &&
                entry.ContainerIngredientCode == ingredientCode)
            {
                count++;
            }
        }

        return count;
    }

    static bool HasCounterType(IReadOnlyList<KitchenMapCounterEntry> entries, KitchenMapCounterType counterType)
    {
        return CountCounterType(entries, counterType) > 0;
    }

    static void AddIngredientIds(List<int> results, int[] ingredientIds)
    {
        if (ingredientIds == null)
        {
            return;
        }

        for (var i = 0; i < ingredientIds.Length; i++)
        {
            AddUnique(results, ingredientIds[i]);
        }
    }

    static void AddUnique(List<int> results, int value)
    {
        if (results.Contains(value))
        {
            return;
        }

        results.Add(value);
    }

    static void AddUnique(List<Vector2Int> results, Vector2Int value)
    {
        if (ContainsGrid(results, value))
        {
            return;
        }

        results.Add(value);
    }

    static bool ContainsGrid(IReadOnlyList<Vector2Int> results, Vector2Int value)
    {
        for (var i = 0; i < results.Count; i++)
        {
            if (results[i] == value)
            {
                return true;
            }
        }

        return false;
    }

    static void ShowBuildValidationPopup(IReadOnlyList<string> errors)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Scene Build를 진행할 수 없습니다.");
        builder.AppendLine();
        for (var i = 0; i < errors.Count; i++)
        {
            builder.Append("- ");
            builder.AppendLine(errors[i]);
        }

        EditorUtility.DisplayDialog("MapEditor Build 검증 실패", builder.ToString(), "확인");
    }

    bool ContainsGridPosition(Vector2Int gridPosition)
    {
        return KitchenMapSceneBuilder.ContainsGridPosition(gridPosition, _width, _height);
    }

    static Vector2Int ResolveCounterGridPosition(Transform root, BaseCounter counter)
    {
        return KitchenMapSceneBuilder.ResolveGridPositionFromWorld(
            root,
            DefaultOffset,
            DEFAULT_CELL_SIZE,
            counter.transform.position);
    }

    bool IsFrameGridPosition(int x, int y)
    {
        return x == 0 ||
               y == 0 ||
               x == _width - 1 ||
               y == _height - 1;
    }

    KitchenMapDirection GetAutoDirectionForGridPosition(Vector2Int gridPosition)
    {
        if (_height > 1 && gridPosition.y == _height - 1)
        {
            return KitchenMapDirection.Front;
        }

        if (_height > 1 && gridPosition.y == 0)
        {
            return KitchenMapDirection.Back;
        }

        if (_width > 1 && gridPosition.x == 0)
        {
            return KitchenMapDirection.Right;
        }

        if (_width > 1 && gridPosition.x == _width - 1)
        {
            return KitchenMapDirection.Left;
        }

        return _direction;
    }

}
