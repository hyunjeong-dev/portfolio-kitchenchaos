using System.Collections.Generic;
using System.IO;
using Cinemachine;
using Game.Common.GameData;
using Generated;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Generated.GameData;

public static class KitchenMapSceneBuilder
{
    const string TEMPLATE_SCENE_PATH = "Assets/Scenes/GameScene/GameSceneTemplate.unity";
    const string OUTPUT_SCENE_ROOT = "Assets/Scenes/GameScene";
    const string GAME_DATA_BINARY_PATH = "Assets/Resources/GameData/GameData.bytes";
    const string GAME_SCENE_PREFIX = "GameScene";
    const string COUNTERS_ROOT_NAME = KitchenMapConstants.COUNTERS_ROOT_NAME;
    const string DIM_ROOT_NAME = "DimRoot";
    const string PLAYER_ROOT_NAME = "PlayerRoot";
    const string PLAYER_OBJECT_NAME = "Player";
    const string CONTEXT_VIRTUAL_CAMERA_PROPERTY_NAME = "_virtualCamera";
    const string CONTEXT_CAMERA_TARGET_ROOT_PROPERTY_NAME = "_cameraTargetRoot";
    const string CONTEXT_PLAYER_ROOT_PROPERTY_NAME = "_playerRoot";
    const string COUNTER_PREFAB_ROOT = "Assets/Prefabs/Counters/";
    const string ADDRESSABLE_ASSET_ROOT = "Assets/Addressables/";
    const string WALL_MATERIAL_PATH = "Assets/_Assets/Materials/Wall.mat";
    const string BLACK_MATERIAL_PATH = "Assets/_Assets/Materials/Black.mat";
    const string RIGHT_WALL_NAME = "Wall";
    const string LEFT_WALL_NAME = "Wall (1)";
    const string BACK_WALL_NAME = "Wall (2)";
    const string RIGHT_BLACK_BLOCK_NAME = "Cube";
    const string LEFT_BLACK_BLOCK_NAME = "Cube (1)";
    const string BACK_BLACK_BLOCK_NAME = "Cube (2)";
    const float WALL_Y = 1.3730497f;
    const float WALL_THICKNESS = 0.25f;
    const float WALL_HEIGHT = 3f;
    const float WALL_GAP_FROM_GRID = 0.03f;
    const float WALL_FRONT_EXTENSION = 2.1f;
    const float WALL_BACK_EXTENSION = 0.07f;
    const float WALL_HORIZONTAL_SIDE_OVERLAP = 0.25f;
    const float BLACK_BLOCK_Y = 1.2659f;
    const float BLACK_BLOCK_HEIGHT = 3.1676338f;
    const float BLACK_BLOCK_THICKNESS = 7.6868587f;
    const float BLACK_BLOCK_GAP_FROM_GRID = 0.25f;
    const float BLACK_BLOCK_FRONT_EXTENSION = 2.17f;
    const float BLACK_BLOCK_BACK_EXTENSION = 0.28f;
    const float BLACK_BLOCK_BACK_SIDE_EXTENSION = 8.25f;

    static readonly string[] _spriteAssetExtensions =
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".tga",
        ".psd",
        ".asset",
    };

    public static Transform CounterRoot => FindOrCreateRoot(COUNTERS_ROOT_NAME);

    public static bool Build(
        int stageLevel,
        int width,
        int height,
        Vector3 origin,
        float cellSize,
        IReadOnlyList<KitchenMapCounterEntry> counters,
        bool hasPlayerRootLocalPosition,
        Vector3 playerRootLocalPosition)
    {
        if (stageLevel <= 0)
        {
            Debug.LogError($"[MapEditor] StageLevel is invalid. stageLevel: {stageLevel}");
            return false;
        }

        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        cellSize = Mathf.Max(0.01f, cellSize);

        var outputPath = GetOutputScenePath(GetSceneName(stageLevel));
        if (!TryPrepareOutputScene(outputPath))
        {
            return false;
        }

        var scene = EditorSceneManager.OpenScene(outputPath);
        var countersRoot = CounterRoot;
        ClearCounters(countersRoot);
        CreateCounters(width, height, origin, cellSize, counters, countersRoot, scene);
        UpdateBoundaryBlocks(width, height, origin, cellSize);
        UpdateOpenSceneCamera(countersRoot, out _);
        ApplyStageLevel(stageLevel);
        ApplyPlayerRootLocalPosition(hasPlayerRootLocalPosition, playerRootLocalPosition);

        Undo.FlushUndoRecordObjects();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError($"[MapEditor] Scene save failed. path: {outputPath}");
            return false;
        }

        Debug.Log($"[MapEditor] Scene built. path: {outputPath}");
        return true;
    }

    public static bool TryGetOpenScenePlayerRootLocalPosition(out Vector3 localPosition)
    {
        localPosition = default;

        var scene = EditorSceneManager.GetActiveScene();
        var context = FindOpenSceneContext(scene);
        var playerRoot = ResolvePlayerRoot(scene, context);
        if (playerRoot == null)
        {
            return false;
        }

        localPosition = playerRoot.localPosition;
        return true;
    }

    public static string GetSceneName(int stageLevel)
    {
        return GAME_SCENE_PREFIX + stageLevel;
    }

    static bool TryPrepareOutputScene(string outputPath)
    {
        if (!File.Exists(TEMPLATE_SCENE_PATH))
        {
            Debug.LogError($"[MapEditor] Template scene does not exist. path: {TEMPLATE_SCENE_PATH}");
            return false;
        }

        if (TEMPLATE_SCENE_PATH == outputPath)
        {
            Debug.LogError($"[MapEditor] Output scene cannot overwrite template scene. path: {outputPath}");
            return false;
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        if (File.Exists(outputPath))
        {
            var overwrite = EditorUtility.DisplayDialog(
                "Scene 덮어쓰기",
                $"이미 존재하는 씬입니다.\n\n{outputPath}\n\n기존 씬을 덮어씌우시겠습니까?",
                "덮어쓰기",
                "취소");

            if (!overwrite)
            {
                return false;
            }

            AssetDatabase.DeleteAsset(outputPath);
        }

        var copied = AssetDatabase.CopyAsset(TEMPLATE_SCENE_PATH, outputPath);
        AssetDatabase.Refresh();

        if (!copied)
        {
            Debug.LogError($"[MapEditor] Failed to copy scene. from: {TEMPLATE_SCENE_PATH}, to: {outputPath}");
            return false;
        }

        return true;
    }

    static void CreateCounters(
        int width,
        int height,
        Vector3 origin,
        float cellSize,
        IReadOnlyList<KitchenMapCounterEntry> counters,
        Transform countersRoot,
        UnityEngine.SceneManagement.Scene scene)
    {
        if (counters == null)
        {
            return;
        }

        for (var i = 0; i < counters.Count; i++)
        {
            var entry = counters[i];
            if (!ContainsGridPosition(entry.GridPosition, width, height))
            {
                Debug.LogWarning(
                    $"[MapEditor] Counter skipped because it is out of map bounds. index: {i}, position: {entry.GridPosition}, size: {width}x{height}");
                continue;
            }

            var prefab = ResolveCounterPrefab(entry);
            if (prefab == null)
            {
                Debug.LogError($"[MapEditor] Counter prefab is missing. index: {i}, type: {entry.CounterType}");
                continue;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                Debug.LogError($"[MapEditor] Counter instantiate failed. index: {i}, type: {entry.CounterType}");
                continue;
            }

            instance.transform.SetParent(countersRoot, false);
            instance.transform.localPosition = GetLocalPosition(origin, cellSize, entry.GridPosition);
            SetLocalEulerAngles(instance.transform, GetLocalEulerAngles(entry.Direction));
            instance.name = prefab.name;
            Undo.RegisterCreatedObjectUndo(instance, "Create Map Counter");
            ApplyCounterPostProcess(instance, entry);
        }
    }

    static GameObject ResolveCounterPrefab(KitchenMapCounterEntry entry)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(GetCounterPrefabPath(entry.CounterType));
    }

    public static GameObject InstantiateCounterInOpenScene(
        KitchenMapCounterType counterType,
        KitchenMapDirection direction,
        string containerIngredientCode,
        Vector2Int gridPosition,
        Vector3 origin,
        float cellSize)
    {
        var prefab = ResolveCounterPrefab(counterType);
        if (prefab == null)
        {
            Debug.LogError($"[MapEditor] Counter prefab is missing. type: {counterType}");
            return null;
        }

        RemoveOpenSceneCounterAtGrid(gridPosition, origin, cellSize);

        var scene = EditorSceneManager.GetActiveScene();
        var countersRoot = CounterRoot;
        var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            Debug.LogError($"[MapEditor] Counter instantiate failed. type: {counterType}");
            return null;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Create Map Counter");
        instance.transform.SetParent(countersRoot, false);
        instance.transform.localPosition = GetLocalPosition(origin, cellSize, gridPosition);
        SetLocalEulerAngles(instance.transform, GetLocalEulerAngles(direction));
        instance.name = prefab.name;
        ApplyCounterPostProcess(instance, new KitchenMapCounterEntry(
            counterType,
            direction,
            gridPosition,
            containerIngredientCode));
        EditorSceneManager.MarkSceneDirty(scene);
        return instance;
    }

    static void ApplyCounterPostProcess(GameObject instance, KitchenMapCounterEntry entry)
    {
        ApplyContainerIngredient(instance, entry.ContainerIngredientCode);
        ApplyBurnerCounterAction(instance, entry.CounterType);
        ApplyDeliveryLookAtCamera(instance);
    }

    static void ApplyBurnerCounterAction(GameObject instance, KitchenMapCounterType counterType)
    {
        if (instance == null || !TryGetCookingActionType(counterType, out var actionType))
        {
            return;
        }

        var burnerCounter = instance.GetComponent<BurnerCounter>();
        if (burnerCounter == null)
        {
            return;
        }

        Undo.RecordObject(burnerCounter, "Update Burner Counter Action");
        burnerCounter.SetActionType(actionType);
        PrefabUtility.RecordPrefabInstancePropertyModifications(burnerCounter);
        EditorUtility.SetDirty(burnerCounter);
    }

    public static void ApplyContainerIngredient(GameObject instance, string ingredientCode)
    {
        ApplyContainerIngredient(instance, ingredientCode, "Update Container Ingredient");
    }

    public static void ApplyContainerIngredient(GameObject instance, string ingredientCode, string undoName)
    {
        if (string.IsNullOrWhiteSpace(ingredientCode) ||
            instance == null ||
            !instance.TryGetComponent<ContainerCounter>(out var containerCounter))
        {
            return;
        }

        var topSpriteRenderer = ResolveTopSpriteRenderer(containerCounter);
        Undo.RecordObject(containerCounter, undoName);
        if (topSpriteRenderer != null)
        {
            Undo.RecordObject(topSpriteRenderer, undoName);
        }

        var spriteIcon = LoadContainerIngredientIcon(ingredientCode);
        containerCounter.SetIngredient(ingredientCode, spriteIcon);
        PrefabUtility.RecordPrefabInstancePropertyModifications(containerCounter);
        EditorUtility.SetDirty(containerCounter);

        if (topSpriteRenderer != null)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(topSpriteRenderer);
            EditorUtility.SetDirty(topSpriteRenderer);
        }
    }

    public static Sprite LoadContainerIngredientIcon(string ingredientCode)
    {
        if (string.IsNullOrWhiteSpace(ingredientCode) ||
            !TryLoadEditorGameData(out var gameData))
        {
            return null;
        }

        var ingredientData = gameData.GetIngredientInfoDataByCode(ingredientCode);
        if (ingredientData == null)
        {
            return null;
        }

        var spriteIconPath = GetIngredientSpriteIconPath(ingredientData);
        var sprite = LoadEditorSprite(spriteIconPath);
        if (sprite == null)
        {
            Debug.LogWarning(
                $"[MapEditor] Container ingredient sprite load failed. ingredientCode: {ingredientCode}, addressOrPath: {spriteIconPath}");
        }

        return sprite;
    }

    public static Sprite LoadEditorSprite(string addressOrPath)
    {
        if (string.IsNullOrEmpty(addressOrPath))
        {
            return null;
        }

        var sprite = LoadSpriteAtAssetPath(addressOrPath);
        if (sprite != null)
        {
            return sprite;
        }

        if (TryGetAddressableAssetPath(addressOrPath, out var assetPath) ||
            TryGetAddressableAssetPathByConvention(addressOrPath, out assetPath))
        {
            return LoadSpriteAtAssetPath(assetPath);
        }

        return null;
    }

    static SpriteRenderer ResolveTopSpriteRenderer(ContainerCounter containerCounter)
    {
        var serializedObject = new SerializedObject(containerCounter);
        var property = serializedObject.FindProperty("_topSpriteRenderer");
        return property != null ? property.objectReferenceValue as SpriteRenderer : null;
    }

    static Sprite LoadSpriteAtAssetPath(string assetPath)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
        {
            return sprite;
        }

        var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (var i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite foundSprite)
            {
                return foundSprite;
            }
        }

        return null;
    }

    static bool TryGetAddressableAssetPath(string address, out string assetPath)
    {
        assetPath = string.Empty;

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            return false;
        }

        for (var groupIndex = 0; groupIndex < settings.groups.Count; groupIndex++)
        {
            var group = settings.groups[groupIndex];
            if (group == null)
            {
                continue;
            }

            foreach (var entry in group.entries)
            {
                if (entry == null || entry.address != address)
                {
                    continue;
                }

                assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                return !string.IsNullOrEmpty(assetPath);
            }
        }

        return false;
    }

    static bool TryGetAddressableAssetPathByConvention(string address, out string assetPath)
    {
        assetPath = string.Empty;
        if (address.StartsWith("Assets/", System.StringComparison.Ordinal))
        {
            return false;
        }

        var pathWithoutExtension = ADDRESSABLE_ASSET_ROOT + address;
        for (var i = 0; i < _spriteAssetExtensions.Length; i++)
        {
            var candidatePath = pathWithoutExtension + _spriteAssetExtensions[i];
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            assetPath = candidatePath;
            return true;
        }

        return false;
    }

    static string GetIngredientSpriteIconPath(IngredientInfoData ingredientData)
    {
        if (ingredientData == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrEmpty(ingredientData.SpritePath)
            ? ingredientData.SpritePath
            : ingredientData.IconPath;
    }

    static bool TryLoadEditorGameData(out GameData gameData)
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
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[MapEditor] GameData load failed. reason: {exception.Message}");
            return false;
        }
    }

    static void ApplyDeliveryLookAtCamera(GameObject instance)
    {
        if (instance == null || instance.GetComponent<DeliveryCounter>() == null)
        {
            return;
        }

        var behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
        for (var i = 0; i < behaviours.Length; i++)
        {
            var behaviour = behaviours[i];
            if (behaviour == null || behaviour.GetType().Name != "LookAtCamera")
            {
                continue;
            }

            Undo.RecordObject(behaviour.transform, "Update Delivery LookAtCamera");
            behaviour.SendMessage("SetMode", SendMessageOptions.DontRequireReceiver);
        }
    }

    static void RemoveOpenSceneCounterAtGrid(Vector2Int gridPosition, Vector3 origin, float cellSize)
    {
        var counters = Object.FindObjectsByType<BaseCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = counters.Length - 1; i >= 0; i--)
        {
            var counter = counters[i];
            if (counter == null)
            {
                continue;
            }

            var currentGridPosition = ResolveGridPosition(origin, cellSize, counter.transform.localPosition);
            if (currentGridPosition != gridPosition)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(counter.gameObject);
        }
    }

    public static void DeleteCounterInOpenScene(Vector2Int gridPosition, Vector3 origin, float cellSize)
    {
        RemoveOpenSceneCounterAtGrid(gridPosition, origin, cellSize);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    public static void RebuildOpenSceneCounters(
        int width,
        int height,
        Vector3 origin,
        float cellSize,
        IReadOnlyList<KitchenMapCounterEntry> counters)
    {
        var scene = EditorSceneManager.GetActiveScene();
        var countersRoot = CounterRoot;
        ClearCounters(countersRoot);
        CreateCounters(width, height, origin, cellSize, counters, countersRoot, scene);
        UpdateBoundaryBlocks(width, height, origin, cellSize);
        UpdateOpenSceneCamera(countersRoot, out _);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    public static bool UpdateOpenSceneCamera(out Transform cameraTransform)
    {
        return UpdateOpenSceneCamera(CounterRoot, out cameraTransform);
    }

    static GameObject ResolveCounterPrefab(KitchenMapCounterType counterType)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(GetCounterPrefabPath(counterType));
    }

    public static KitchenMapCounterType ResolveCounterType(BaseCounter counter)
    {
        if (counter is ContainerCounter)
        {
            return KitchenMapCounterType.Container;
        }

        if (counter is CuttingCounter)
        {
            return KitchenMapCounterType.Cutting;
        }

        if (counter is BurnerCounter burnerCounter)
        {
            return burnerCounter.ActionType == ActionType.Boil
                ? KitchenMapCounterType.Pot
                : KitchenMapCounterType.FryingPan;
        }

        if (counter is PlatesCounter)
        {
            return KitchenMapCounterType.Plates;
        }

        if (counter is DeliveryCounter)
        {
            return KitchenMapCounterType.Delivery;
        }

        if (counter is TrashCounter)
        {
            return KitchenMapCounterType.Trash;
        }

        return KitchenMapCounterType.Clear;
    }

    public static KitchenMapDirection ResolveDirection(Transform transform)
    {
        var y = Mathf.Repeat(transform.eulerAngles.y, 360f);
        if (IsNear(y, 180f))
        {
            return KitchenMapDirection.Front;
        }

        if (IsNear(y, 90f))
        {
            return KitchenMapDirection.Right;
        }

        if (IsNear(y, 270f))
        {
            return KitchenMapDirection.Left;
        }

        return KitchenMapDirection.Back;
    }

    public static Vector2Int ResolveGridPosition(Vector3 origin, float cellSize, Vector3 localPosition)
    {
        cellSize = Mathf.Max(0.01f, cellSize);
        return new(
            Mathf.RoundToInt((localPosition.x - origin.x) / cellSize),
            Mathf.RoundToInt((localPosition.z - origin.z) / cellSize));
    }

    public static bool ContainsGridPosition(Vector2Int gridPosition, int width, int height)
    {
        return gridPosition.x >= 0 &&
               gridPosition.y >= 0 &&
               gridPosition.x < width &&
               gridPosition.y < height;
    }

    public static Vector3 GetLocalPosition(Vector3 origin, float cellSize, Vector2Int gridPosition)
    {
        cellSize = Mathf.Max(0.01f, cellSize);
        return new(
            origin.x + gridPosition.x * cellSize,
            origin.y,
            origin.z + gridPosition.y * cellSize);
    }

    public static Vector2Int ResolveGridPositionFromWorld(
        Transform root,
        Vector3 origin,
        float cellSize,
        Vector3 worldPosition)
    {
        var localPosition = root != null
            ? root.InverseTransformPoint(worldPosition)
            : worldPosition;
        return ResolveGridPosition(origin, cellSize, localPosition);
    }

    public static Transform ResolveGridRoot()
    {
        var countersRoot = GameObject.Find(COUNTERS_ROOT_NAME);
        if (countersRoot != null)
        {
            return countersRoot.transform;
        }

        var context = Object.FindFirstObjectByType<KitchenGameContext>(FindObjectsInactive.Include);
        if (context != null)
        {
            return context.transform;
        }

        return null;
    }

    public static Transform EnsureOpenScenePlayerRoot(bool activateRoot)
    {
        var scene = EditorSceneManager.GetActiveScene();
        var context = FindOpenSceneContext(scene);
        var playerRoot = ResolvePlayerRoot(scene, context);
        var player = FindScenePlayer(scene, context);

        if (playerRoot == null)
        {
            playerRoot = CreatePlayerRoot(context, player);
        }

        if (playerRoot == null)
        {
            return null;
        }

        if (activateRoot && !playerRoot.gameObject.activeSelf)
        {
            Undo.RecordObject(playerRoot.gameObject, "Activate Player Root");
            playerRoot.gameObject.SetActive(true);
        }

        if (player != null && !player.transform.IsChildOf(playerRoot))
        {
            MovePlayerUnderRoot(player.transform, playerRoot);
        }

        AssignContextPlayerRoot(context, playerRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        return playerRoot;
    }

    public static Transform MoveOpenScenePlayerRootToGrid(Vector2Int gridPosition, Vector3 origin, float cellSize)
    {
        var scene = EditorSceneManager.GetActiveScene();
        var playerRoot = EnsureOpenScenePlayerRoot(true);
        if (playerRoot == null)
        {
            return null;
        }

        var gridRoot = ResolveGridRoot();
        var gridLocalPosition = GetLocalPosition(origin, cellSize, gridPosition);
        var worldPosition = gridRoot != null
            ? gridRoot.TransformPoint(gridLocalPosition)
            : gridLocalPosition;
        var parent = playerRoot.parent;
        var localPosition = parent != null
            ? parent.InverseTransformPoint(worldPosition)
            : worldPosition;

        Undo.RecordObject(playerRoot, "Move Player Root");
        playerRoot.localPosition = localPosition;
        EditorUtility.SetDirty(playerRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        return playerRoot;
    }

    static void ApplyPlayerRootLocalPosition(bool hasLocalPosition, Vector3 localPosition)
    {
        var playerRoot = EnsureOpenScenePlayerRoot(true);
        if (!hasLocalPosition || playerRoot == null)
        {
            return;
        }

        Undo.RecordObject(playerRoot, "Update Player Root");
        playerRoot.localPosition = localPosition;
        EditorUtility.SetDirty(playerRoot);
        EditorSceneManager.MarkSceneDirty(playerRoot.gameObject.scene);
    }

    public static bool SetOpenScenePlayerRootActive(bool active)
    {
        var scene = EditorSceneManager.GetActiveScene();
        var context = FindOpenSceneContext(scene);
        var playerRoot = ResolvePlayerRoot(scene, context);
        if (playerRoot == null)
        {
            return false;
        }

        if (playerRoot.gameObject.activeSelf == active)
        {
            return true;
        }

        Undo.RecordObject(playerRoot.gameObject, active ? "Activate Player Root" : "Deactivate Player Root");
        playerRoot.gameObject.SetActive(active);
        EditorUtility.SetDirty(playerRoot.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    static Vector3 GetLocalEulerAngles(KitchenMapDirection direction)
    {
        switch (direction)
        {
            case KitchenMapDirection.Front:
                return new(0f, 180f, 0f);
            case KitchenMapDirection.Back:
                return Vector3.zero;
            case KitchenMapDirection.Right:
                return new(0f, 90f, 0f);
            case KitchenMapDirection.Left:
                return new(0f, 270f, 0f);
            default:
                return Vector3.zero;
        }
    }

    static void SetLocalEulerAngles(Transform target, Vector3 localEulerAngles)
    {
        localEulerAngles = SnapEulerAngles(localEulerAngles);
        target.localRotation = Quaternion.Euler(localEulerAngles);

        var serializedObject = new SerializedObject(target);
        var eulerHintProperty = serializedObject.FindProperty("m_LocalEulerAnglesHint");
        if (eulerHintProperty == null)
        {
            return;
        }

        eulerHintProperty.vector3Value = localEulerAngles;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static Vector3 SnapEulerAngles(Vector3 eulerAngles)
    {
        return new(
            SnapEulerAngle(eulerAngles.x),
            SnapEulerAngle(eulerAngles.y),
            SnapEulerAngle(eulerAngles.z));
    }

    static float SnapEulerAngle(float angle)
    {
        if (IsNear(angle, 360f) || IsNear(angle, -360f))
        {
            return 0f;
        }

        var snappedAngle = Mathf.Round(angle / 90f) * 90f;
        return Mathf.Abs(angle - snappedAngle) <= 0.01f ? snappedAngle : angle;
    }

    static string GetCounterPrefabPath(KitchenMapCounterType counterType)
    {
        switch (counterType)
        {
            case KitchenMapCounterType.Container:
                return COUNTER_PREFAB_ROOT + "ContainerCounter.prefab";
            case KitchenMapCounterType.Cutting:
                return COUNTER_PREFAB_ROOT + "CuttingCounter.prefab";
            case KitchenMapCounterType.FryingPan:
            case KitchenMapCounterType.Pot:
                return COUNTER_PREFAB_ROOT + "BurnerCounter.prefab";
            case KitchenMapCounterType.Plates:
                return COUNTER_PREFAB_ROOT + "PlatesCounter.prefab";
            case KitchenMapCounterType.Delivery:
                return COUNTER_PREFAB_ROOT + "DeliveryCounter.prefab";
            case KitchenMapCounterType.Trash:
                return COUNTER_PREFAB_ROOT + "TrashCounter.prefab";
            default:
                return COUNTER_PREFAB_ROOT + "ClearCounter.prefab";
        }
    }

    static bool TryGetCookingActionType(KitchenMapCounterType counterType, out ActionType actionType)
    {
        switch (counterType)
        {
            case KitchenMapCounterType.FryingPan:
                actionType = ActionType.PanFry;
                return true;
            case KitchenMapCounterType.Pot:
                actionType = ActionType.Boil;
                return true;
            default:
                actionType = ActionType.None;
                return false;
        }
    }

    static Transform FindOrCreateRoot(string rootName)
    {
        var root = GameObject.Find(rootName);
        if (root != null)
        {
            return root.transform;
        }

        var rootTransform = new GameObject(rootName).transform;
        var context = Object.FindFirstObjectByType<KitchenGameContext>(FindObjectsInactive.Include);
        if (context != null)
        {
            rootTransform.SetParent(context.transform, false);
        }

        return rootTransform;
    }

    static Transform ResolvePlayerRoot(UnityEngine.SceneManagement.Scene scene, KitchenGameContext context)
    {
        if (context != null)
        {
            var serializedObject = new SerializedObject(context);
            var playerRoot = serializedObject.FindProperty(CONTEXT_PLAYER_ROOT_PROPERTY_NAME)?.objectReferenceValue as Transform;
            if (playerRoot != null && playerRoot.gameObject.scene == scene)
            {
                return playerRoot;
            }

            var childRoot = context.transform.Find(PLAYER_ROOT_NAME);
            if (childRoot != null)
            {
                return childRoot;
            }
        }

        return FindSceneTransformByName(scene, PLAYER_ROOT_NAME);
    }

    static Transform CreatePlayerRoot(KitchenGameContext context, PlayerBehaviour player)
    {
        var playerRoot = new GameObject(PLAYER_ROOT_NAME).transform;
        Undo.RegisterCreatedObjectUndo(playerRoot.gameObject, "Create Player Root");

        var parent = context != null ? context.transform : null;
        playerRoot.SetParent(parent, false);
        if (player != null)
        {
            playerRoot.position = player.transform.position;
            playerRoot.rotation = player.transform.rotation;
        }

        return playerRoot;
    }

    static PlayerBehaviour FindScenePlayer(UnityEngine.SceneManagement.Scene scene, KitchenGameContext context)
    {
        PlayerBehaviour fallbackPlayer = null;
        var players = Object.FindObjectsByType<PlayerBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < players.Length; i++)
        {
            var player = players[i];
            if (player == null || player.gameObject.scene != scene)
            {
                continue;
            }

            if (context != null && player.transform.IsChildOf(context.transform))
            {
                return player;
            }

            fallbackPlayer = player;
        }

        if (fallbackPlayer != null)
        {
            return fallbackPlayer;
        }

        var playerTransform = FindSceneTransformByName(scene, PLAYER_OBJECT_NAME);
        return playerTransform != null ? playerTransform.GetComponent<PlayerBehaviour>() : null;
    }

    static void MovePlayerUnderRoot(Transform player, Transform playerRoot)
    {
        Undo.RecordObject(player, "Move Player Under Root");
        player.SetParent(playerRoot, true);
        player.localPosition = Vector3.zero;
        SetLocalEulerAngles(player, Vector3.zero);
        player.localScale = Vector3.one;
    }

    static void AssignContextPlayerRoot(KitchenGameContext context, Transform playerRoot)
    {
        if (context == null || playerRoot == null)
        {
            return;
        }

        var serializedObject = new SerializedObject(context);
        var playerRootProperty = serializedObject.FindProperty(CONTEXT_PLAYER_ROOT_PROPERTY_NAME);
        if (playerRootProperty == null || playerRootProperty.objectReferenceValue == playerRoot)
        {
            return;
        }

        Undo.RecordObject(context, "Assign Player Root");
        playerRootProperty.objectReferenceValue = playerRoot;
        serializedObject.ApplyModifiedProperties();
    }

    static void UpdateBoundaryBlocks(int width, int height, Vector3 origin, float cellSize)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        cellSize = Mathf.Max(0.01f, cellSize);

        var halfCellSize = cellSize * 0.5f;
        var gridMinX = origin.x - halfCellSize;
        var gridMaxX = origin.x + (width - 1) * cellSize + halfCellSize;
        var gridMinZ = origin.z - halfCellSize;
        var gridMaxZ = origin.z + (height - 1) * cellSize + halfCellSize;
        var gridCenterX = (gridMinX + gridMaxX) * 0.5f;
        var gridWidth = gridMaxX - gridMinX;

        var parent = ResolveBoundaryParent();
        var wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(WALL_MATERIAL_PATH);
        var blackMaterial = AssetDatabase.LoadAssetAtPath<Material>(BLACK_MATERIAL_PATH);
        var wallHalfThickness = WALL_THICKNESS * 0.5f;
        var blackBlockHalfThickness = BLACK_BLOCK_THICKNESS * 0.5f;
        var verticalWallMinZ = gridMinZ - WALL_FRONT_EXTENSION;
        var verticalWallMaxZ = gridMaxZ + WALL_BACK_EXTENSION;
        var verticalWallCenterZ = (verticalWallMinZ + verticalWallMaxZ) * 0.5f;
        var verticalWallDepth = verticalWallMaxZ - verticalWallMinZ;
        var sideBlackBlockMinZ = gridMinZ - BLACK_BLOCK_FRONT_EXTENSION;
        var sideBlackBlockMaxZ = gridMaxZ + BLACK_BLOCK_BACK_EXTENSION;
        var sideBlackBlockCenterZ = (sideBlackBlockMinZ + sideBlackBlockMaxZ) * 0.5f;
        var sideBlackBlockDepth = sideBlackBlockMaxZ - sideBlackBlockMinZ;

        CreateOrUpdateBlock(
            RIGHT_WALL_NAME,
            parent,
            new Vector3(gridMaxX + WALL_GAP_FROM_GRID + wallHalfThickness, WALL_Y, verticalWallCenterZ),
            Vector3.zero,
            new Vector3(WALL_THICKNESS, WALL_HEIGHT, verticalWallDepth),
            wallMaterial);

        CreateOrUpdateBlock(
            LEFT_WALL_NAME,
            parent,
            new Vector3(gridMinX - WALL_GAP_FROM_GRID - wallHalfThickness, WALL_Y, verticalWallCenterZ),
            Vector3.zero,
            new Vector3(WALL_THICKNESS, WALL_HEIGHT, verticalWallDepth),
            wallMaterial);

        CreateOrUpdateBlock(
            BACK_WALL_NAME,
            parent,
            new Vector3(gridCenterX, WALL_Y, gridMaxZ + WALL_GAP_FROM_GRID + wallHalfThickness),
            new Vector3(0f, -90f, 0f),
            new Vector3(WALL_THICKNESS, WALL_HEIGHT, gridWidth + WALL_HORIZONTAL_SIDE_OVERLAP * 2f),
            wallMaterial);

        CreateOrUpdateBlock(
            RIGHT_BLACK_BLOCK_NAME,
            parent,
            new Vector3(gridMaxX + BLACK_BLOCK_GAP_FROM_GRID + blackBlockHalfThickness, BLACK_BLOCK_Y, sideBlackBlockCenterZ),
            Vector3.zero,
            new Vector3(BLACK_BLOCK_THICKNESS, BLACK_BLOCK_HEIGHT, sideBlackBlockDepth),
            blackMaterial);

        CreateOrUpdateBlock(
            LEFT_BLACK_BLOCK_NAME,
            parent,
            new Vector3(gridMinX - BLACK_BLOCK_GAP_FROM_GRID - blackBlockHalfThickness, BLACK_BLOCK_Y, sideBlackBlockCenterZ),
            Vector3.zero,
            new Vector3(BLACK_BLOCK_THICKNESS, BLACK_BLOCK_HEIGHT, sideBlackBlockDepth),
            blackMaterial);

        CreateOrUpdateBlock(
            BACK_BLACK_BLOCK_NAME,
            parent,
            new Vector3(gridCenterX, BLACK_BLOCK_Y, gridMaxZ + BLACK_BLOCK_GAP_FROM_GRID + blackBlockHalfThickness),
            new Vector3(0f, -90f, 0f),
            new Vector3(BLACK_BLOCK_THICKNESS, BLACK_BLOCK_HEIGHT, gridWidth + BLACK_BLOCK_BACK_SIDE_EXTENSION * 2f),
            blackMaterial);
    }

    static Transform ResolveBoundaryParent()
    {
        var context = Object.FindFirstObjectByType<KitchenGameContext>(FindObjectsInactive.Include);
        if (context != null)
        {
            return FindOrCreateChildRoot(context.transform, DIM_ROOT_NAME);
        }

        return FindOrCreateRoot(DIM_ROOT_NAME);
    }

    static Transform FindOrCreateChildRoot(Transform parent, string rootName)
    {
        if (parent != null)
        {
            var child = parent.Find(rootName);
            if (child != null)
            {
                return child;
            }
        }

        var rootTransform = new GameObject(rootName).transform;
        Undo.RegisterCreatedObjectUndo(rootTransform.gameObject, $"Create {rootName}");
        rootTransform.SetParent(parent, false);
        return rootTransform;
    }

    static void CreateOrUpdateBlock(
        string objectName,
        Transform parent,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale,
        Material material)
    {
        var target = FindBoundaryBlock(objectName, parent);
        if (target == null)
        {
            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = objectName;
            Undo.RegisterCreatedObjectUndo(target, "Create Map Boundary Block");
        }

        if (target.transform.parent != parent)
        {
            target.transform.SetParent(parent, false);
        }

        Undo.RecordObject(target.transform, "Update Map Boundary Block");
        target.transform.localPosition = localPosition;
        SetLocalEulerAngles(target.transform, localEulerAngles);
        target.transform.localScale = localScale;

        var renderer = target.GetComponent<MeshRenderer>();
        if (renderer != null && material != null)
        {
            Undo.RecordObject(renderer, "Update Map Boundary Material");
            renderer.sharedMaterial = material;
        }
    }

    static GameObject FindBoundaryBlock(string objectName, Transform parent)
    {
        if (parent != null)
        {
            var child = parent.Find(objectName);
            if (child != null)
            {
                return child.gameObject;
            }
        }

        return GameObject.Find(objectName);
    }

    static void ClearCounters(Transform countersRoot)
    {
        var scene = EditorSceneManager.GetActiveScene();
        var counters = Object.FindObjectsByType<BaseCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = countersRoot.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(countersRoot.GetChild(i).gameObject);
        }

        for (var i = counters.Length - 1; i >= 0; i--)
        {
            var counter = counters[i];
            if (counter == null ||
                counter.gameObject.scene != scene ||
                counter.transform.IsChildOf(countersRoot))
            {
                continue;
            }

            Undo.DestroyObjectImmediate(counter.gameObject);
        }
    }

    static void ApplyStageLevel(int stageLevel)
    {
        var context = Object.FindFirstObjectByType<KitchenGameContext>(FindObjectsInactive.Include);
        if (context == null)
        {
            Debug.LogWarning("[MapEditor] KitchenGameContext is not found. StageLevel was not applied.");
            return;
        }

        var serializedObject = new SerializedObject(context);
        var stageLevelProperty = serializedObject.FindProperty("_stageLevel");
        if (stageLevelProperty == null)
        {
            Debug.LogWarning("[MapEditor] KitchenGameContext._stageLevel is not found.");
            return;
        }

        stageLevelProperty.intValue = stageLevel;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static bool UpdateOpenSceneCamera(Transform fallbackTargetRoot, out Transform cameraTransform)
    {
        cameraTransform = null;

        var scene = EditorSceneManager.GetActiveScene();
        ResolveOpenSceneCameraReferences(
            scene,
            fallbackTargetRoot,
            out var gameCamera,
            out var virtualCamera,
            out var targetRoot);

        if (targetRoot == null)
        {
            Debug.LogWarning("[MapEditor] Camera target root is not found.");
            return false;
        }

        if (!KitchenStageCamera.TryCalculateRendererBounds(targetRoot, out var targetBounds))
        {
            Debug.LogWarning($"[MapEditor] Camera target bounds is empty. root: {targetRoot.name}");
            return false;
        }

        cameraTransform = virtualCamera != null ? virtualCamera.transform : gameCamera != null ? gameCamera.transform : null;
        if (gameCamera == null || cameraTransform == null)
        {
            Debug.LogWarning("[MapEditor] Game camera is not found.");
            return false;
        }

        var fieldOfView = ResolveCameraFieldOfView(gameCamera, virtualCamera);
        if (!KitchenStageCamera.TryCalculateFitCameraPosition(
                gameCamera,
                cameraTransform,
                fieldOfView,
                targetBounds,
                0f,
                out var cameraPosition))
        {
            Debug.LogWarning("[MapEditor] Camera position calculation failed.");
            return false;
        }

        Undo.RecordObject(cameraTransform, "Update Map Camera");
        cameraTransform.position = cameraPosition;
        EditorUtility.SetDirty(cameraTransform);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    static float ResolveCameraFieldOfView(Camera gameCamera, CinemachineVirtualCamera virtualCamera)
    {
        if (virtualCamera != null && virtualCamera.m_Lens.FieldOfView > 0f)
        {
            return virtualCamera.m_Lens.FieldOfView;
        }

        if (gameCamera != null && gameCamera.fieldOfView > 0f)
        {
            return gameCamera.fieldOfView;
        }

        return KitchenStageCamera.DefaultFieldOfView;
    }

    static void ResolveOpenSceneCameraReferences(
        UnityEngine.SceneManagement.Scene scene,
        Transform fallbackTargetRoot,
        out Camera gameCamera,
        out CinemachineVirtualCamera virtualCamera,
        out Transform targetRoot)
    {
        gameCamera = null;
        virtualCamera = null;
        targetRoot = fallbackTargetRoot;

        var context = FindOpenSceneContext(scene);
        if (context != null)
        {
            var serializedObject = new SerializedObject(context);
            virtualCamera = serializedObject.FindProperty(CONTEXT_VIRTUAL_CAMERA_PROPERTY_NAME)?.objectReferenceValue as CinemachineVirtualCamera;
            targetRoot = serializedObject.FindProperty(CONTEXT_CAMERA_TARGET_ROOT_PROPERTY_NAME)?.objectReferenceValue as Transform ?? targetRoot;
        }

        gameCamera = FindSceneMainCamera(scene);
        virtualCamera = virtualCamera != null ? virtualCamera : FindSceneVirtualCamera(scene);
        targetRoot = targetRoot != null ? targetRoot : FindSceneTransformByName(scene, COUNTERS_ROOT_NAME);
    }

    static KitchenGameContext FindOpenSceneContext(UnityEngine.SceneManagement.Scene scene)
    {
        var contexts = Object.FindObjectsByType<KitchenGameContext>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < contexts.Length; i++)
        {
            var context = contexts[i];
            if (context != null && context.gameObject.scene == scene)
            {
                return context;
            }
        }

        return null;
    }

    static Camera FindSceneMainCamera(UnityEngine.SceneManagement.Scene scene)
    {
        var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Camera fallbackCamera = null;

        for (var i = 0; i < cameras.Length; i++)
        {
            var camera = cameras[i];
            if (camera == null || camera.gameObject.scene != scene)
            {
                continue;
            }

            if (camera.CompareTag("MainCamera"))
            {
                return camera;
            }

            if (fallbackCamera == null)
            {
                fallbackCamera = camera;
            }
        }

        return fallbackCamera;
    }

    static CinemachineVirtualCamera FindSceneVirtualCamera(UnityEngine.SceneManagement.Scene scene)
    {
        var cameras = Object.FindObjectsByType<CinemachineVirtualCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        CinemachineVirtualCamera result = null;

        for (var i = 0; i < cameras.Length; i++)
        {
            var camera = cameras[i];
            if (camera == null || camera.gameObject.scene != scene)
            {
                continue;
            }

            if (result == null || camera.Priority > result.Priority)
            {
                result = camera;
            }
        }

        return result;
    }

    static Transform FindSceneTransformByName(UnityEngine.SceneManagement.Scene scene, string objectName)
    {
        var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < transforms.Length; i++)
        {
            var target = transforms[i];
            if (target != null && target.gameObject.scene == scene && target.name == objectName)
            {
                return target;
            }
        }

        return null;
    }

    static string GetOutputScenePath(string sceneName)
    {
        var safeSceneName = string.Concat(sceneName.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrEmpty(safeSceneName))
        {
            safeSceneName = "GameScene";
        }

        return Path.Combine(OUTPUT_SCENE_ROOT, safeSceneName + ".unity");
    }

    static bool IsNear(float value, float target)
    {
        return Mathf.Abs(Mathf.DeltaAngle(value, target)) <= 1f;
    }
}
