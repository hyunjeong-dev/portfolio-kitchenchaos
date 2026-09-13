using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed partial class KitchenMapEditorWindow
{
    void CreateSelectedCounterAtGrid(Vector2Int gridPosition, KitchenMapDirection direction)
    {
        if (!ContainsGridPosition(gridPosition))
        {
            Debug.LogWarning(
                $"[MapEditor] Grid position is out of map bounds. position: {gridPosition}, size: {_width}x{_height}");
            return;
        }

        var undoGroup = BeginUndoGroup("Create Map Counter");
        var instance = KitchenMapSceneBuilder.InstantiateCounterInOpenScene(
            _counterType,
            direction,
            _counterType == KitchenMapCounterType.Container ? _containerIngredientCode : null,
            gridPosition,
            DefaultOffset,
            DEFAULT_CELL_SIZE);
        EndUndoGroup(undoGroup);
        if (instance == null)
        {
            return;
        }

        Selection.activeGameObject = instance;
        SyncEditorFieldsFromCounter(instance.GetComponent<BaseCounter>());
        RefreshAfterCounterChanged(gridPosition);
    }

    void DeleteCounterAtGrid(Vector2Int gridPosition)
    {
        if (!ContainsGridPosition(gridPosition))
        {
            Debug.LogWarning(
                $"[MapEditor] Grid position is out of map bounds. position: {gridPosition}, size: {_width}x{_height}");
            return;
        }

        var undoGroup = BeginUndoGroup("Delete Map Counter");
        KitchenMapSceneBuilder.DeleteCounterInOpenScene(gridPosition, DefaultOffset, DEFAULT_CELL_SIZE);
        EndUndoGroup(undoGroup);
        RefreshAfterCounterChanged(gridPosition);
    }

    void RefreshAfterCounterChanged(Vector2Int gridPosition)
    {
        UpdateSelectedGridPosition(gridPosition);
        RefreshRequiredCounterStatusesIfVisible();
        RepaintSceneAndWindow();
    }

    void RepaintSceneAndWindow()
    {
        SceneView.RepaintAll();
        Repaint();
    }

    static int BeginUndoGroup(string undoName)
    {
        var undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(undoName);
        return undoGroup;
    }

    static void EndUndoGroup(int undoGroup)
    {
        Undo.CollapseUndoOperations(undoGroup);
    }

    void ApplyClearCounterGridFrame()
    {
        var confirm = EditorUtility.DisplayDialog(
            "W x H 적용",
            $"현재 카운터 목록을 지우고 {_width} x {_height} 외곽 ClearCounter 기본틀을 생성합니다.",
            "확인",
            "취소");

        if (!confirm)
        {
            return;
        }

        var counters = new List<KitchenMapCounterEntry>();
        var createdCount = 0;
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                if (!IsFrameGridPosition(x, y))
                {
                    continue;
                }

                counters.Add(new KitchenMapCounterEntry(
                    KitchenMapCounterType.Clear,
                    GetAutoDirectionForGridPosition(new Vector2Int(x, y)),
                    new Vector2Int(x, y)));
                createdCount++;
            }
        }

        var undoGroup = BeginUndoGroup("Apply Map Grid Frame");
        KitchenMapSceneBuilder.RebuildOpenSceneCounters(_width, _height, DefaultOffset, DEFAULT_CELL_SIZE, counters);
        EndUndoGroup(undoGroup);
        RefreshRequiredCounterStatusesIfVisible();
        RepaintSceneAndWindow();
        Debug.Log($"[MapEditor] ClearCounter grid frame created. size: {_width}x{_height}, count: {createdCount}");
    }

    void PreviewCamera()
    {
        var undoGroup = BeginUndoGroup("Preview Map Camera");
        var updated = KitchenMapSceneBuilder.UpdateOpenSceneCamera(out var cameraTransform);
        EndUndoGroup(undoGroup);

        if (!updated)
        {
            return;
        }

        AlignSceneViewToCamera(cameraTransform);
        RepaintSceneAndWindow();
    }

    static void AlignSceneViewToCamera(Transform cameraTransform)
    {
        if (cameraTransform == null || SceneView.lastActiveSceneView == null)
        {
            return;
        }

        SceneView.lastActiveSceneView.AlignViewToObject(cameraTransform);
    }

    void OnSceneGUI(SceneView sceneView)
    {
        var currentEvent = Event.current;
        CaptureSceneMouseControl(currentEvent);
        DrawGridPreview(currentEvent.mousePosition);

        if (TryPaintSelectedCounter(currentEvent))
        {
            return;
        }

        if (TryPlaceRequiredCounter(currentEvent))
        {
            return;
        }

        if (TryMovePlayerRootToGrid(currentEvent))
        {
            return;
        }

        SelectCounterFromSceneGrid(currentEvent);
    }

    void CaptureSceneMouseControl(Event currentEvent)
    {
        if (currentEvent.type != EventType.Layout ||
            !ShouldCaptureSceneMouseControl(currentEvent))
        {
            return;
        }

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }

    bool ShouldCaptureSceneMouseControl(Event currentEvent)
    {
        return !string.IsNullOrEmpty(_activeRequiredCounterLabel) ||
               _isPlayerRootPlacementActive ||
               currentEvent.control;
    }

    bool TryPaintSelectedCounter(Event currentEvent)
    {
        if (currentEvent.type == EventType.MouseUp ||
            !currentEvent.control)
        {
            _hasLastPaintGridPosition = false;
            return false;
        }

        if (!IsPaintMouseEvent(currentEvent))
        {
            return false;
        }

        if (!TryGetValidGridPositionFromEvent(currentEvent, out var gridPosition))
        {
            return false;
        }

        if (_hasLastPaintGridPosition && _lastPaintGridPosition == gridPosition)
        {
            currentEvent.Use();
            return true;
        }

        PaintCounterAtGrid(gridPosition);

        _hasLastPaintGridPosition = true;
        _lastPaintGridPosition = gridPosition;
        currentEvent.Use();
        return true;
    }

    bool TryPlaceRequiredCounter(Event currentEvent)
    {
        if (string.IsNullOrEmpty(_activeRequiredCounterLabel) ||
            !IsRequiredPlacementMouseEvent(currentEvent))
        {
            return false;
        }

        if (!TryGetValidGridPositionFromEvent(currentEvent, out var gridPosition))
        {
            return false;
        }

        PaintCounterAtGrid(gridPosition);
        currentEvent.Use();
        return true;
    }

    static bool IsPaintMouseEvent(Event currentEvent)
    {
        return (currentEvent.type == EventType.MouseDown || currentEvent.type == EventType.MouseDrag) &&
               currentEvent.button == 0 &&
               !currentEvent.alt;
    }

    static bool IsRequiredPlacementMouseEvent(Event currentEvent)
    {
        return currentEvent.type == EventType.MouseDown &&
               currentEvent.button == 0 &&
               !currentEvent.alt &&
               !currentEvent.control;
    }

    bool TryMovePlayerRootToGrid(Event currentEvent)
    {
        if (!_isPlayerRootPlacementActive ||
            !IsPlayerRootPlacementMouseEvent(currentEvent))
        {
            return false;
        }

        if (!TryGetValidGridPositionFromEvent(currentEvent, out var gridPosition))
        {
            return false;
        }

        var playerRoot = KitchenMapSceneBuilder.MoveOpenScenePlayerRootToGrid(
            gridPosition,
            DefaultOffset,
            DEFAULT_CELL_SIZE);
        if (playerRoot == null)
        {
            return false;
        }

        Selection.activeGameObject = playerRoot.gameObject;
        EditorGUIUtility.PingObject(playerRoot.gameObject);
        UpdateSelectedGridPosition(gridPosition);
        RepaintSceneAndWindow();
        currentEvent.Use();
        return true;
    }

    static bool IsPlayerRootPlacementMouseEvent(Event currentEvent)
    {
        return currentEvent.type == EventType.MouseDown &&
               currentEvent.button == 0 &&
               !currentEvent.alt &&
               !currentEvent.control;
    }

    bool TryGetValidGridPositionFromEvent(Event currentEvent, out Vector2Int gridPosition)
    {
        if (TryGetGridPositionFromSceneMouse(currentEvent.mousePosition, out gridPosition) &&
            ContainsGridPosition(gridPosition))
        {
            return true;
        }

        gridPosition = default;
        return false;
    }

    void PaintCounterAtGrid(Vector2Int gridPosition)
    {
        if (TryGetCounterAtGrid(gridPosition, out var counter) && IsSameSelectedCounter(counter))
        {
            DeleteCounterAtGrid(gridPosition);
        }
        else
        {
            var direction = counter != null
                ? KitchenMapSceneBuilder.ResolveDirection(counter.transform)
                : GetAutoDirectionForGridPosition(gridPosition);
            _direction = direction;
            CreateSelectedCounterAtGrid(gridPosition, direction);
        }
    }

    bool IsSameSelectedCounter(BaseCounter counter)
    {
        if (counter == null)
        {
            return false;
        }

        if (KitchenMapSceneBuilder.ResolveCounterType(counter) != _counterType)
        {
            return false;
        }

        if (_counterType != KitchenMapCounterType.Container)
        {
            return true;
        }

        return counter is ContainerCounter containerCounter &&
               ResolveContainerIngredientCode(containerCounter) == _containerIngredientCode;
    }

    void SelectCounterFromSceneGrid(Event currentEvent)
    {
        if (currentEvent.type != EventType.MouseDown ||
            currentEvent.button != 0 ||
            currentEvent.alt)
        {
            return;
        }

        if (!TryGetGridPositionFromSceneMouse(currentEvent.mousePosition, out var gridPosition) ||
            !ContainsGridPosition(gridPosition))
        {
            return;
        }

        if (!TryGetCounterAtGrid(gridPosition, out var counter))
        {
            return;
        }

        Selection.activeGameObject = counter.gameObject;
        EditorGUIUtility.PingObject(counter.gameObject);
        SyncEditorFieldsFromCounter(counter);
        UpdateSelectedGridPosition(gridPosition);
        currentEvent.Use();
    }

    bool TryGetCounterAtGrid(Vector2Int gridPosition, out BaseCounter foundCounter)
    {
        var root = KitchenMapSceneBuilder.ResolveGridRoot();
        var counters = FindObjectsByType<BaseCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < counters.Length; i++)
        {
            var counter = counters[i];
            if (counter == null)
            {
                continue;
            }

            var currentGridPosition = ResolveCounterGridPosition(root, counter);
            if (currentGridPosition != gridPosition)
            {
                continue;
            }

            foundCounter = counter;
            return true;
        }

        foundCounter = null;
        return false;
    }

    void DrawGridPreview(Vector2 mousePosition)
    {
        if (_width <= 0 || _height <= 0)
        {
            return;
        }

        if (!TryGetGridPreviewRoot(out var counterRoot))
        {
            return;
        }

        TryGetGridPositionFromSceneMouse(mousePosition, counterRoot, out var hoverGridPosition);
        var hasHoverGrid = ContainsGridPosition(hoverGridPosition);
        if (hasHoverGrid)
        {
            TryUpdateHoverGridInfo(hoverGridPosition);
        }

        using (new Handles.DrawingScope(_gridLineColor))
        {
            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    DrawGridCellOutline(counterRoot, new Vector2Int(x, y), GRID_LINE_WIDTH);
                }
            }
        }

        DrawGridIssuePreview(counterRoot);

        if (hasHoverGrid)
        {
            using (new Handles.DrawingScope(_gridHoverLineColor))
            {
                DrawGridCellOutline(counterRoot, hoverGridPosition, GRID_HOVER_LINE_WIDTH);
            }
        }

        if (Event.current.type == EventType.MouseMove)
        {
            HandleUtility.Repaint();
        }
    }

    void DrawGridIssuePreview(Transform counterRoot)
    {
        RefreshGridIssuePositions(counterRoot);
        if (_duplicateGridPositions.Count == 0 && _outOfBoundsGridPositions.Count == 0)
        {
            return;
        }

        using (new Handles.DrawingScope(_gridErrorLineColor))
        {
            for (var i = 0; i < _duplicateGridPositions.Count; i++)
            {
                DrawGridCellOutline(counterRoot, _duplicateGridPositions[i], GRID_ERROR_LINE_WIDTH);
            }

            for (var i = 0; i < _outOfBoundsGridPositions.Count; i++)
            {
                DrawGridCellOutline(counterRoot, _outOfBoundsGridPositions[i], GRID_ERROR_LINE_WIDTH);
            }
        }
    }

    void RefreshGridIssuePositions(Transform counterRoot)
    {
        _duplicateGridPositions.Clear();
        _outOfBoundsGridPositions.Clear();

        var counters = FindObjectsByType<BaseCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < counters.Length; i++)
        {
            var counter = counters[i];
            if (counter == null)
            {
                continue;
            }

            var gridPosition = ResolveCounterGridPosition(counterRoot, counter);
            if (!ContainsGridPosition(gridPosition))
            {
                AddUnique(_outOfBoundsGridPositions, gridPosition);
                continue;
            }

            for (var j = i + 1; j < counters.Length; j++)
            {
                var otherCounter = counters[j];
                if (otherCounter == null)
                {
                    continue;
                }

                if (ResolveCounterGridPosition(counterRoot, otherCounter) == gridPosition)
                {
                    AddUnique(_duplicateGridPositions, gridPosition);
                    break;
                }
            }
        }
    }

    void TryUpdateHoverGridInfo(Vector2Int gridPosition)
    {
        if (TryGetSelectedCounter(out _))
        {
            return;
        }

        UpdateSelectedGridPosition(gridPosition);
    }

    void UpdateSelectedCounterGridInfo()
    {
        if (!TryGetSelectedCounter(out var counter))
        {
            _lastSyncedSelectedCounter = null;
            return;
        }

        var root = ResolveCounterGridRoot(counter.transform);
        var gridPosition = ResolveCounterGridPosition(root, counter);
        if (!ContainsGridPosition(gridPosition))
        {
            return;
        }

        UpdateSelectedGridPosition(gridPosition);
        if (_lastSyncedSelectedCounter != counter)
        {
            SyncEditorFieldsFromCounter(counter);
            _lastSyncedSelectedCounter = counter;
        }
    }

    bool TryGetSelectedCounter(out BaseCounter counter)
    {
        counter = null;
        if (Selection.activeGameObject == null)
        {
            return false;
        }

        counter = Selection.activeGameObject.GetComponentInParent<BaseCounter>();
        return counter != null;
    }

    void UpdateSelectedGridPosition(Vector2Int gridPosition)
    {
        if (_hasSelectedGridPosition && _selectedGridPosition == gridPosition)
        {
            return;
        }

        _hasSelectedGridPosition = true;
        _selectedGridPosition = gridPosition;
        Repaint();
    }

    void SyncEditorFieldsFromCounter(BaseCounter counter)
    {
        if (counter == null)
        {
            return;
        }

        var counterType = KitchenMapSceneBuilder.ResolveCounterType(counter);
        var direction = KitchenMapSceneBuilder.ResolveDirection(counter.transform);
        var containerIngredientCode = counter is ContainerCounter containerCounter
            ? ResolveContainerIngredientCode(containerCounter)
            : _containerIngredientCode;
        if (_counterType == counterType &&
            _direction == direction &&
            _containerIngredientCode == containerIngredientCode)
        {
            return;
        }

        _counterType = counterType;
        _direction = direction;
        _containerIngredientCode = containerIngredientCode;
        Repaint();
    }

    void DrawGridCellOutline(Transform counterRoot, Vector2Int gridPosition, float lineWidth)
    {
        FillGridCellCorners(counterRoot, gridPosition, GridPreviewYOffset);
        Handles.DrawAAPolyLine(lineWidth, _gridCellCorners);
    }

    void FillGridCellCorners(Transform counterRoot, Vector2Int gridPosition, float yOffset)
    {
        var center = KitchenMapSceneBuilder.GetLocalPosition(DefaultOffset, DEFAULT_CELL_SIZE, gridPosition);
        var halfSize = DEFAULT_CELL_SIZE * 0.5f;
        var y = center.y + yOffset;

        _gridCellCorners[0] = counterRoot.TransformPoint(new Vector3(center.x - halfSize, y, center.z - halfSize));
        _gridCellCorners[1] = counterRoot.TransformPoint(new Vector3(center.x - halfSize, y, center.z + halfSize));
        _gridCellCorners[2] = counterRoot.TransformPoint(new Vector3(center.x + halfSize, y, center.z + halfSize));
        _gridCellCorners[3] = counterRoot.TransformPoint(new Vector3(center.x + halfSize, y, center.z - halfSize));
        _gridCellCorners[4] = counterRoot.TransformPoint(new Vector3(center.x - halfSize, y, center.z - halfSize));
    }

    bool TryGetGridPositionFromSceneMouse(Vector2 mousePosition, out Vector2Int gridPosition)
    {
        var counterRoot = KitchenMapSceneBuilder.ResolveGridRoot();
        if (counterRoot == null)
        {
            counterRoot = KitchenMapSceneBuilder.CounterRoot;
        }

        return TryGetGridPositionFromSceneMouse(mousePosition, counterRoot, out gridPosition);
    }

    bool TryGetGridPositionFromSceneMouse(Vector2 mousePosition, Transform counterRoot, out Vector2Int gridPosition)
    {
        if (TryGetGridPositionFromSceneGrid(mousePosition, counterRoot, out gridPosition))
        {
            return true;
        }

        var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        var worldOrigin = counterRoot.TransformPoint(DefaultOffset);
        var plane = new Plane(counterRoot.up, worldOrigin);
        if (!plane.Raycast(ray, out var distance))
        {
            gridPosition = default;
            return false;
        }

        var worldPosition = ray.GetPoint(distance);
        var localPosition = counterRoot.InverseTransformPoint(worldPosition);
        gridPosition = KitchenMapSceneBuilder.ResolveGridPosition(DefaultOffset, DEFAULT_CELL_SIZE, localPosition);
        return true;
    }

    bool TryGetGridPositionFromSceneGrid(Vector2 mousePosition, Transform counterRoot, out Vector2Int gridPosition)
    {
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var currentGridPosition = new Vector2Int(x, y);
                FillGridCellCorners(counterRoot, currentGridPosition, GridPreviewYOffset);
                for (var i = 0; i < _gridGuiCorners.Length; i++)
                {
                    _gridGuiCorners[i] = HandleUtility.WorldToGUIPoint(_gridCellCorners[i]);
                }

                if (!ContainsPointInQuad(mousePosition, _gridGuiCorners))
                {
                    continue;
                }

                gridPosition = currentGridPosition;
                return true;
            }
        }

        gridPosition = default;
        return false;
    }

    static bool ContainsPointInQuad(Vector2 point, Vector2[] quad)
    {
        return ContainsPointInTriangle(point, quad[0], quad[1], quad[2]) ||
               ContainsPointInTriangle(point, quad[0], quad[2], quad[3]);
    }

    static bool ContainsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        var signA = Sign(point, a, b);
        var signB = Sign(point, b, c);
        var signC = Sign(point, c, a);
        var hasNegative = signA < 0f || signB < 0f || signC < 0f;
        var hasPositive = signA > 0f || signB > 0f || signC > 0f;
        return !(hasNegative && hasPositive);
    }

    static float Sign(Vector2 point, Vector2 a, Vector2 b)
    {
        return (point.x - b.x) * (a.y - b.y) - (a.x - b.x) * (point.y - b.y);
    }

    static float ResolveGridPreviewYOffset()
    {
        if (_hasCachedGridPreviewYOffset)
        {
            return _cachedGridPreviewYOffset;
        }

        var clearCounterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CLEAR_COUNTER_PREFAB_PATH);
        if (clearCounterPrefab == null)
        {
            _cachedGridPreviewYOffset = FALLBACK_GRID_PREVIEW_Y_OFFSET;
            _hasCachedGridPreviewYOffset = true;
            return _cachedGridPreviewYOffset;
        }

        var transforms = clearCounterPrefab.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < transforms.Length; i++)
        {
            var target = transforms[i];
            if (target.name != COUNTER_TOP_POINT_NAME)
            {
                continue;
            }

            _cachedGridPreviewYOffset = target.localPosition.y + GRID_PREVIEW_SURFACE_PADDING;
            _hasCachedGridPreviewYOffset = true;
            return _cachedGridPreviewYOffset;
        }

        _cachedGridPreviewYOffset = FALLBACK_GRID_PREVIEW_Y_OFFSET;
        _hasCachedGridPreviewYOffset = true;
        return _cachedGridPreviewYOffset;
    }

    bool TryGetGridPreviewRoot(out Transform root)
    {
        root = KitchenMapSceneBuilder.ResolveGridRoot();
        return root != null;
    }

    Transform ResolveCounterGridRoot(Transform counterTransform)
    {
        var gridRoot = KitchenMapSceneBuilder.ResolveGridRoot();
        return gridRoot != null ? gridRoot : counterTransform.parent;
    }

}
