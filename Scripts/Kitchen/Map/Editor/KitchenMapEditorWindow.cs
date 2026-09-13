using System.Collections.Generic;
using Game.Common.GameData;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed partial class KitchenMapEditorWindow : EditorWindow
{
    const string TEMPLATE_SCENE_PATH = "Assets/Scenes/GameScene/GameSceneTemplate.unity";
    const string GAME_SCENE_FOLDER_PATH = "Assets/Scenes/GameScene";
    const string GAME_DATA_BINARY_PATH = "Assets/Resources/GameData/GameData.bytes";
    const string GAME_DATA_ID_MAP_PATH = "GameData/GameDataIdMap.csv";
    const string GAME_SCENE_PREFIX = "GameScene";
    public const int DEFAULT_WIDTH = KitchenMapConstants.DEFAULT_WIDTH;
    public const int DEFAULT_HEIGHT = KitchenMapConstants.DEFAULT_HEIGHT;
    public const float DEFAULT_CELL_SIZE = KitchenMapConstants.DEFAULT_CELL_SIZE;
    const string CLEAR_COUNTER_PREFAB_PATH = "Assets/Prefabs/Counters/ClearCounter.prefab";
    const string COUNTER_TOP_POINT_NAME = "CounterTopPoint";
    const float GRID_PREVIEW_SURFACE_PADDING = 0.02f;
    const float FALLBACK_GRID_PREVIEW_Y_OFFSET = 0.04f;
    const float GRID_LINE_WIDTH = 2f;
    const float GRID_HOVER_LINE_WIDTH = 4f;
    const float GRID_ERROR_LINE_WIDTH = 5f;
    const float MIN_WINDOW_WIDTH = 280f;
    const float MIN_WINDOW_HEIGHT = 360f;

    SerializedObject _editorSerializedObject;
    SerializedProperty _openSceneAssetProperty;
    SerializedProperty _stageLevelProperty;
    SerializedProperty _widthProperty;
    SerializedProperty _heightProperty;
    readonly Color _gridLineColor = new(0.1f, 0.45f, 1f, 0.55f);
    readonly Color _gridHoverLineColor = new(0.1f, 0.7f, 1f, 1f);
    readonly Color _gridErrorLineColor = new(1f, 0.05f, 0.02f, 1f);
    readonly Vector3[] _gridCellCorners = new Vector3[5];
    readonly Vector2[] _gridGuiCorners = new Vector2[4];
    readonly List<RequiredCounterStatus> _requiredCounterStatuses = new();
    readonly List<int> _ingredientOptionIds = new();
    readonly List<Vector2Int> _duplicateGridPositions = new();
    readonly List<Vector2Int> _outOfBoundsGridPositions = new();
    static bool _hasCachedGridPreviewYOffset;
    static float _cachedGridPreviewYOffset;
    Generated.GameData _cachedEditorGameData;
    string _cachedEditorGameDataError;
    GameDataIdMap _cachedEditorIdMap;
    string _cachedEditorIdMapError;
    string[] _ingredientOptionLabels = new string[0];
    bool _hasIngredientOptions;
    bool _hasSelectedGridPosition;
    Vector2Int _selectedGridPosition;
    BaseCounter _lastSyncedSelectedCounter;
    KitchenMapCounterType _counterType = KitchenMapCounterType.Clear;
    KitchenMapDirection _direction = KitchenMapDirection.Front;
    string _containerIngredientCode;
    bool _hasLastPaintGridPosition;
    Vector2Int _lastPaintGridPosition;
    bool _hasRequiredCounterStatuses;
    bool _isPlayerRootPlacementActive;
    bool _isBuildSceneQueued;
    string _activeRequiredCounterLabel;
    string _requiredCounterStatusMessage;
    Vector2 _scrollPosition;
    bool _isBuildingScene;
    GUIStyle _requiredCounterPlacedStyle;
    GUIStyle _requiredCounterMissingStyle;

    public static Vector3 DefaultOffset => KitchenMapConstants.DefaultOffset;
    static float GridPreviewYOffset => ResolveGridPreviewYOffset();

    [SerializeField] int _stageLevel = 1;
    [SerializeField] int _width = DEFAULT_WIDTH;
    [SerializeField] int _height = DEFAULT_HEIGHT;
    [SerializeField] SceneAsset _openSceneAsset;

    sealed class RequiredCounterStatus
    {
        public string Label;
        public bool IsPlaced;
        public int PlacedCount;
        public KitchenMapCounterType CounterType;
        public string ContainerIngredientCode;
    }

    [MenuItem("Tools/MapEditor")]
    public static void ShowKitchenMapEditorWindow()
    {
        try
        {
            var windows = Resources.FindObjectsOfTypeAll<KitchenMapEditorWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                if (windows[i] == null)
                {
                    continue;
                }

                windows[i].Close();
            }

            var window = CreateWindow<KitchenMapEditorWindow>("MapEditor");
            InitializeWindow(window);
            window.Show();
            window.Focus();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            var window = GetWindowWithRect<KitchenMapEditorWindow>(
                new Rect(100f, 100f, 420f, 720f),
                false,
                "MapEditor");
            InitializeWindow(window);
            window.Show();
            window.Focus();
        }
    }

    static void InitializeWindow(KitchenMapEditorWindow window)
    {
        window.titleContent = new("MapEditor");
        window.minSize = new(MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
        window.position = new(100f, 100f, 420f, 720f);
    }

    void OnEnable()
    {
        minSize = new(MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
        BindSerializedProperties();
        SceneView.duringSceneGui += OnSceneGUI;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        EditorApplication.delayCall += SyncSettingsFromOpenSceneDelayed;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        EditorApplication.delayCall -= SyncSettingsFromOpenSceneDelayed;
        EditorApplication.delayCall -= BuildSceneDelayed;
    }

    void SyncSettingsFromOpenSceneDelayed()
    {
        if (this == null)
        {
            return;
        }

        try
        {
            SyncSettingsFromOpenScene();
            RefreshRequiredCounterStatusesIfVisible();
            RepaintSceneAndWindow();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    void OnGUI()
    {
        try
        {
            using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scrollView.scrollPosition;
                DrawSceneHeader();
                DrawPlayerRootPlacementToggle();
                DrawLayoutSettings();
                DrawCounterGeneratorFields();
                DrawSelectedGridInfo();
                DrawSceneBuildFields();
            }
        }
        catch (System.Exception exception)
        {
            EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            if (GUILayout.Button("Log Exception"))
            {
                Debug.LogException(exception);
            }
        }
    }

    void DrawSceneHeader()
    {
        DrawOpenTemplateSceneButton();
        DrawOpenGameSceneFolderButton();

        DrawOpenSceneAssetField();

        EditorGUILayout.HelpBox(
            "Scene View에서 Ctrl + 좌클릭 드래그로 생성/삭제/교체합니다. Required Counter의 배치 버튼이 활성화된 상태에서는 grid 클릭으로 같은 동작을 실행합니다.",
            MessageType.None);
    }

    void DrawOpenTemplateSceneButton()
    {
        if (GUILayout.Button("Template Scene 열기"))
        {
            OpenScene(TEMPLATE_SCENE_PATH);
        }
    }

    static void DrawOpenGameSceneFolderButton()
    {
        if (GUILayout.Button("GameScene Folder 열기"))
        {
            EditorUtility.RevealInFinder(GAME_SCENE_FOLDER_PATH);
        }
    }

    void DrawOpenSceneAssetField()
    {
        if (_editorSerializedObject == null)
        {
            return;
        }

        _editorSerializedObject.Update();
        EditorGUILayout.PropertyField(_openSceneAssetProperty, new GUIContent("Open Scene"));
        var selectedSceneAsset = _openSceneAssetProperty.objectReferenceValue as SceneAsset;
        using (new EditorGUI.DisabledScope(selectedSceneAsset == null))
        {
            if (GUILayout.Button("선택 Scene 열기"))
            {
                OpenSelectedScene(selectedSceneAsset);
            }
        }

        _editorSerializedObject.ApplyModifiedProperties();
    }

    void DrawPlayerRootPlacementToggle()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Player Root", EditorStyles.boldLabel);

        var nextActive = GUILayout.Toggle(_isPlayerRootPlacementActive, "Player Root 배치", GUI.skin.button);
        if (nextActive == _isPlayerRootPlacementActive)
        {
            return;
        }

        if (nextActive)
        {
            ActivatePlayerRootPlacement();
        }
        else
        {
            DeactivatePlayerRootPlacement();
        }
    }

    void ActivatePlayerRootPlacement()
    {
        _isPlayerRootPlacementActive = true;
        ClearRequiredCounterPlacement();
        SelectPlayerRoot();
    }

    void DeactivatePlayerRootPlacement()
    {
        _isPlayerRootPlacementActive = false;
        KitchenMapSceneBuilder.SetOpenScenePlayerRootActive(false);
        RepaintSceneAndWindow();
    }

    void SelectPlayerRoot()
    {
        var undoGroup = BeginUndoGroup("Select Player Root");
        var playerRoot = KitchenMapSceneBuilder.EnsureOpenScenePlayerRoot(true);
        EndUndoGroup(undoGroup);

        if (playerRoot == null)
        {
            EditorUtility.DisplayDialog("Player Root 선택 실패", "열려있는 Scene에서 PlayerRoot를 찾거나 생성하지 못했습니다.", "확인");
            return;
        }

        Selection.activeGameObject = playerRoot.gameObject;
        EditorGUIUtility.PingObject(playerRoot.gameObject);

        RepaintSceneAndWindow();
    }

    void OpenSelectedScene(SceneAsset sceneAsset)
    {
        if (sceneAsset == null)
        {
            return;
        }

        OpenScene(AssetDatabase.GetAssetPath(sceneAsset));
    }

    void OpenScene(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath) ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            EditorUtility.DisplayDialog("Scene 열기 실패", $"Scene 파일을 찾을 수 없습니다.\n\n{scenePath}", "확인");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(scenePath);
        SyncSettingsFromOpenScene();
        RefreshRequiredCounterStatusesIfVisible();
        RepaintSceneAndWindow();
    }

    void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        if (_isBuildingScene)
        {
            return;
        }

        SyncSettingsFromOpenScene();
        RefreshRequiredCounterStatusesIfVisible();
        RepaintSceneAndWindow();
    }

    void SyncSettingsFromOpenScene()
    {
        var changed = false;
        if (TryResolveStageLevelFromOpenScene(out var stageLevel) &&
            _stageLevel != stageLevel)
        {
            _stageLevel = stageLevel;
            changed = true;
        }

        if (TryResolveGridSizeFromOpenScene(out var width, out var height))
        {
            if (_width != width)
            {
                _width = width;
                changed = true;
            }

            if (_height != height)
            {
                _height = height;
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        BindSerializedProperties();
    }

    static bool TryResolveStageLevelFromOpenScene(out int stageLevel)
    {
        var context = UnityEngine.Object.FindFirstObjectByType<KitchenGameContext>(FindObjectsInactive.Include);
        if (context != null && context.StageLevel > 0)
        {
            stageLevel = context.StageLevel;
            return true;
        }

        var sceneName = EditorSceneManager.GetActiveScene().name;
        return TryResolveStageLevelFromSceneName(sceneName, out stageLevel);
    }

    static bool TryResolveStageLevelFromSceneName(string sceneName, out int stageLevel)
    {
        stageLevel = 0;
        if (string.IsNullOrEmpty(sceneName))
        {
            return false;
        }

        if (!sceneName.StartsWith(GAME_SCENE_PREFIX, System.StringComparison.Ordinal))
        {
            return false;
        }

        var levelText = sceneName.Substring(GAME_SCENE_PREFIX.Length);
        return int.TryParse(levelText, out stageLevel) && stageLevel > 0;
    }

    static bool TryResolveGridSizeFromOpenScene(out int width, out int height)
    {
        width = 0;
        height = 0;

        var counters = FindObjectsByType<BaseCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (counters.Length == 0)
        {
            return false;
        }

        var root = KitchenMapSceneBuilder.ResolveGridRoot();
        var maxX = -1;
        var maxY = -1;
        for (var i = 0; i < counters.Length; i++)
        {
            var counter = counters[i];
            if (counter == null)
            {
                continue;
            }

            var gridPosition = ResolveCounterGridPosition(root, counter);
            if (gridPosition.x < 0 || gridPosition.y < 0)
            {
                continue;
            }

            maxX = Mathf.Max(maxX, gridPosition.x);
            maxY = Mathf.Max(maxY, gridPosition.y);
        }

        if (maxX < 0 || maxY < 0)
        {
            return false;
        }

        width = maxX + 1;
        height = maxY + 1;
        return true;
    }

    void DrawLayoutSettings()
    {
        if (_editorSerializedObject == null)
        {
            return;
        }

        UpdateSelectedCounterGridInfo();

        DrawSetGridSettings();
        DrawStageSettings();
        DrawRequiredCounterStatuses();
    }

    void DrawSetGridSettings()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Set Grid", EditorStyles.boldLabel);

        _editorSerializedObject.Update();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_widthProperty, new GUIContent("Width"));
        EditorGUILayout.PropertyField(_heightProperty, new GUIContent("Height"));
        var applyGridClicked = GUILayout.Button("W x H 적용");
        var previewCameraClicked = GUILayout.Button("카메라 미리보기");

        _widthProperty.intValue = Mathf.Max(1, _widthProperty.intValue);
        _heightProperty.intValue = Mathf.Max(1, _heightProperty.intValue);
        var gridChanged = EditorGUI.EndChangeCheck();
        _editorSerializedObject.ApplyModifiedProperties();

        if (gridChanged)
        {
            RefreshRequiredCounterStatusesIfVisible();
            RepaintSceneAndWindow();
        }

        if (applyGridClicked)
        {
            ApplyClearCounterGridFrame();
        }

        if (previewCameraClicked)
        {
            PreviewCamera();
        }
    }

    void DrawStageSettings()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Stage", EditorStyles.boldLabel);

        _editorSerializedObject.Update();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_stageLevelProperty, new GUIContent("Stage Level"));
        var validClicked = GUILayout.Button("Valid");
        var stageChanged = EditorGUI.EndChangeCheck();
        _editorSerializedObject.ApplyModifiedProperties();

        if (stageChanged || validClicked)
        {
            RefreshRequiredCounterStatuses();
        }
    }

    void DrawRequiredCounterStatuses()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Require Counters", EditorStyles.boldLabel);
        EnsureRequiredCounterStyles();

        if (!_hasRequiredCounterStatuses)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_requiredCounterStatusMessage))
        {
            EditorGUILayout.HelpBox(_requiredCounterStatusMessage, MessageType.Warning);
            return;
        }

        for (var i = 0; i < _requiredCounterStatuses.Count; i++)
        {
            DrawRequiredCounterStatusRow(_requiredCounterStatuses[i]);
        }
    }

    void DrawRequiredCounterStatusRow(RequiredCounterStatus status)
    {
        var style = status.IsPlaced ? _requiredCounterPlacedStyle : _requiredCounterMissingStyle;
        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button(GetRequiredCounterStatusText(status), style))
            {
                PingRequiredCounter(status);
            }

            DrawRequiredCounterPlacementToggle(status);
        }
    }

    void EnsureRequiredCounterStyles()
    {
        if (_requiredCounterPlacedStyle != null &&
            _requiredCounterMissingStyle != null)
        {
            return;
        }

        _requiredCounterPlacedStyle = new(EditorStyles.label)
        {
            wordWrap = true,
        };
        _requiredCounterPlacedStyle.normal.textColor = Color.green;

        _requiredCounterMissingStyle = new(EditorStyles.label)
        {
            wordWrap = true,
        };
        _requiredCounterMissingStyle.normal.textColor = Color.red;
    }

    string GetRequiredCounterStatusText(RequiredCounterStatus status)
    {
        var prefix = status.IsPlaced
            ? $"[배치됨 {status.PlacedCount}개] "
            : $"[미배치 {status.PlacedCount}개] ";
        return prefix + status.Label;
    }

    void DrawRequiredCounterPlacementToggle(RequiredCounterStatus status)
    {
        var isActive = IsRequiredCounterPlacementActive(status);
        if (GUILayout.Toggle(isActive, "배치", GUI.skin.button, GUILayout.Width(64f)) != isActive)
        {
            SetRequiredCounterAsGenerator(status);
        }
    }

    void PingRequiredCounter(RequiredCounterStatus status)
    {
        if (status == null || !TryFindRequiredCounter(status, out var counter))
        {
            return;
        }

        Selection.activeGameObject = counter.gameObject;
        EditorGUIUtility.PingObject(counter.gameObject);
        SyncEditorFieldsFromCounter(counter);
        UpdateSelectedGridPosition(ResolveCounterGridPosition(ResolveCounterGridRoot(counter.transform), counter));
    }

    void DrawCounterGeneratorFields()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Counter Generator", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        var nextCounterType = (KitchenMapCounterType)EditorGUILayout.EnumPopup("Counter Type", _counterType);
        var counterTypeChanged = EditorGUI.EndChangeCheck();
        if (counterTypeChanged)
        {
            _counterType = nextCounterType;
            ClearRequiredCounterPlacement();
        }

        _direction = (KitchenMapDirection)EditorGUILayout.EnumPopup("Direction", _direction);

        if (_counterType == KitchenMapCounterType.Container)
        {
            DrawContainerIngredientPopup(null);
        }
    }

    void DrawSelectedGridInfo()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Selected Grid", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Vector2IntField(
                "Grid",
                _hasSelectedGridPosition ? _selectedGridPosition : new Vector2Int(-1, -1));
        }

        if (!_hasSelectedGridPosition || !TryGetCounterAtGrid(_selectedGridPosition, out var counter))
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Counter", null, typeof(GameObject), true);
                EditorGUILayout.TextField("Counter Type", "None");
            }

            return;
        }

        var counterType = KitchenMapSceneBuilder.ResolveCounterType(counter);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Counter", counter.gameObject, typeof(GameObject), true);
            EditorGUILayout.TextField("Counter Type", counterType.ToString());
            EditorGUILayout.TextField("Direction", KitchenMapSceneBuilder.ResolveDirection(counter.transform).ToString());
        }

        if (counter is ContainerCounter containerCounter)
        {
            DrawSelectedContainerIngredientField(containerCounter);
        }
    }

    void DrawSelectedContainerIngredientField(ContainerCounter selectedCounter)
    {
        var ingredientLabel = ResolveSelectedContainerIngredientLabel(selectedCounter);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Ingredient", ingredientLabel);
        }
    }

    string ResolveSelectedContainerIngredientLabel(ContainerCounter selectedCounter)
    {
        if (selectedCounter == null)
        {
            return "None";
        }

        var ingredientCode = ResolveContainerIngredientCode(selectedCounter);
        if (string.IsNullOrWhiteSpace(ingredientCode))
        {
            return "None";
        }

        if (!TryGetEditorGameData(out var gameData, out _))
        {
            return ingredientCode;
        }

        var ingredientData = gameData.GetIngredientInfoDataByCode(ingredientCode);
        if (ingredientData == null)
        {
            return ingredientCode;
        }

        return $"{ingredientData.Code} ({FormatGameDataId(ingredientData.IngredientId)})";
    }

    void DrawContainerIngredientPopup(ContainerCounter selectedCounter)
    {
        if (!TryGetEditorGameData(out var gameData, out _))
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Ingredient", "GameData Load Failed");
            }

            return;
        }

        EnsureIngredientOptions(gameData);
        if (_ingredientOptionIds.Count == 0)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Ingredient", "Empty");
            }

            return;
        }

        var selectedCode = selectedCounter != null
            ? ResolveContainerIngredientCode(selectedCounter)
            : _containerIngredientCode;
        var selectedIndex = ResolveIngredientIndex(gameData, _ingredientOptionIds, selectedCode);
        EditorGUI.BeginChangeCheck();
        var nextIndex = EditorGUILayout.Popup("Ingredient", selectedIndex, _ingredientOptionLabels);
        var changed = EditorGUI.EndChangeCheck();
        if (nextIndex < 0 || nextIndex >= _ingredientOptionIds.Count)
        {
            return;
        }

        var nextIngredient = gameData.GetIngredientInfoData(_ingredientOptionIds[nextIndex]);
        if (nextIngredient == null)
        {
            return;
        }

        if (selectedCounter == null && string.IsNullOrWhiteSpace(_containerIngredientCode))
        {
            _containerIngredientCode = nextIngredient.Code;
        }

        if (!changed || selectedCode == nextIngredient.Code)
        {
            return;
        }

        _containerIngredientCode = nextIngredient.Code;
        if (selectedCounter == null)
        {
            return;
        }

        KitchenMapSceneBuilder.ApplyContainerIngredient(
            selectedCounter.gameObject,
            _containerIngredientCode,
            "Change Container Ingredient");
        EditorSceneManager.MarkSceneDirty(selectedCounter.gameObject.scene);
        RefreshRequiredCounterStatusesIfVisible();
    }

    void DrawSceneBuildFields()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Scene Create", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Scene Name", KitchenMapSceneBuilder.GetSceneName(_stageLevel));
        }

        using (new EditorGUI.DisabledScope(_isBuildSceneQueued || _isBuildingScene))
        {
            if (GUILayout.Button("Build Scene"))
            {
                EnqueueBuildScene();
            }
        }
    }

    void EnqueueBuildScene()
    {
        _isBuildSceneQueued = true;
        EditorApplication.delayCall -= BuildSceneDelayed;
        EditorApplication.delayCall += BuildSceneDelayed;
        Repaint();
    }

    void BuildSceneDelayed()
    {
        EditorApplication.delayCall -= BuildSceneDelayed;
        if (this == null)
        {
            return;
        }

        _isBuildSceneQueued = false;
        BuildScene();
    }

    void BindSerializedProperties()
    {
        _editorSerializedObject = new(this);
        _openSceneAssetProperty = _editorSerializedObject.FindProperty(nameof(_openSceneAsset));
        _stageLevelProperty = _editorSerializedObject.FindProperty(nameof(_stageLevel));
        _widthProperty = _editorSerializedObject.FindProperty(nameof(_width));
        _heightProperty = _editorSerializedObject.FindProperty(nameof(_height));
    }
}
