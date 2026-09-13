using System.Collections.Generic;
using UnityEngine;

public sealed class KitchenNavigationMap
{
    const byte CELL_WALKABLE = 0;
    const byte CELL_BLOCKED = 1;

    readonly List<BaseCounter> _sceneCounters = new();

    byte[] _occupancyByCell;
    BaseCounter[] _counterByCell;
    Transform _gridRoot;
    Vector3 _origin = KitchenMapConstants.DefaultOffset;
    float _cellSize = KitchenMapConstants.DEFAULT_CELL_SIZE;
    RectInt _bounds;
    bool _isReady;

    public bool IsReady => _isReady;

    public void Rebuild(KitchenGameContext context)
    {
        Clear();
        _gridRoot = ResolveGridRoot(context);
        ResetBounds();

        if (context == null)
        {
            return;
        }

        CollectSceneCounters(context);
        for (var i = 0; i < _sceneCounters.Count; i++)
        {
            var counter = _sceneCounters[i];
            if (counter != null && counter.gameObject.activeInHierarchy)
            {
                EncapsulateBounds(WorldToCell(counter.transform.position));
            }
        }

        AllocateCells();
        _isReady = true;

        for (var i = 0; i < _sceneCounters.Count; i++)
        {
            var counter = _sceneCounters[i];
            if (counter == null || !counter.gameObject.activeInHierarchy)
            {
                continue;
            }

            var cell = WorldToCell(counter.transform.position);
            if (!IsWithinBounds(cell))
            {
                continue;
            }

            var index = GetCellIndex(cell);
            _occupancyByCell[index] = CELL_BLOCKED;
            _counterByCell[index] = counter;
        }
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        var localPosition = new Vector3(
            _origin.x + cell.x * _cellSize,
            _origin.y,
            _origin.z + cell.y * _cellSize);
        return _gridRoot != null ? _gridRoot.TransformPoint(localPosition) : localPosition;
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        var localPosition = _gridRoot != null ? _gridRoot.InverseTransformPoint(worldPosition) : worldPosition;
        return new(
            Mathf.RoundToInt((localPosition.x - _origin.x) / _cellSize),
            Mathf.RoundToInt((localPosition.z - _origin.z) / _cellSize));
    }

    public bool IsWalkable(Vector2Int cell)
    {
        return IsWithinBounds(cell) && _occupancyByCell[GetCellIndex(cell)] == CELL_WALKABLE;
    }

    public bool IsPathTargetWalkable(Vector2Int cell, Vector2Int startCell)
    {
        return IsWithinBounds(cell) && (IsWalkable(cell) || cell == startCell);
    }

    public bool IsWithinBounds(Vector2Int cell)
    {
        return _isReady && _bounds.Contains(cell);
    }

    public bool TryGetCounter(Vector2Int cell, out BaseCounter counter)
    {
        counter = null;
        if (!IsWithinBounds(cell))
        {
            return false;
        }

        counter = _counterByCell[GetCellIndex(cell)];
        return counter != null;
    }

    public bool IsCounterReachable(Vector3 playerPosition, BaseCounter counter)
    {
        if (counter == null)
        {
            return false;
        }

        var playerCell = WorldToCell(playerPosition);
        var counterCell = WorldToCell(counter.transform.position);
        for (var i = 0; i < KitchenGridDirection.COUNT; i++)
        {
            if (counterCell + KitchenGridDirection.Get(i) == playerCell)
            {
                return true;
            }
        }

        return false;
    }

    public void CollectCounterApproachCells(
        Vector2Int counterCell,
        Vector2Int startCell,
        List<Vector2Int> results)
    {
        results.Clear();
        for (var i = 0; i < KitchenGridDirection.COUNT; i++)
        {
            var cell = counterCell + KitchenGridDirection.Get(i);
            if (IsWalkable(cell) || cell == startCell)
            {
                results.Add(cell);
            }
        }
    }

    void Clear()
    {
        _sceneCounters.Clear();
        _occupancyByCell = null;
        _counterByCell = null;
        _gridRoot = null;
        _isReady = false;
    }

    void CollectSceneCounters(KitchenGameContext context)
    {
        if (_gridRoot != null)
        {
            _gridRoot.GetComponentsInChildren(true, _sceneCounters);
            return;
        }

        context.GetComponentsInChildren(true, _sceneCounters);
    }

    void AllocateCells()
    {
        var cellCount = _bounds.width * _bounds.height;
        _occupancyByCell = new byte[cellCount];
        _counterByCell = new BaseCounter[cellCount];
    }

    void EncapsulateBounds(Vector2Int cell)
    {
        var min = Vector2Int.Min(_bounds.min, cell);
        var max = Vector2Int.Max(_bounds.max - Vector2Int.one, cell);
        _bounds.SetMinMax(min, max + Vector2Int.one);
    }

    void ResetBounds()
    {
        _bounds = new RectInt(
            0,
            0,
            KitchenMapConstants.DEFAULT_WIDTH,
            KitchenMapConstants.DEFAULT_HEIGHT);
    }

    int GetCellIndex(Vector2Int cell)
    {
        return (cell.y - _bounds.yMin) * _bounds.width + cell.x - _bounds.xMin;
    }

    static Transform ResolveGridRoot(KitchenGameContext context)
    {
        var root = context != null ? context.transform.Find(KitchenMapConstants.COUNTERS_ROOT_NAME) : null;
        if (root != null)
        {
            return root;
        }

        var rootObject = GameObject.Find(KitchenMapConstants.COUNTERS_ROOT_NAME);
        return rootObject != null ? rootObject.transform : null;
    }
}
