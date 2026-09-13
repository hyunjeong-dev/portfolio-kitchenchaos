using System.Collections.Generic;
using UnityEngine;

public sealed class KitchenNavigationPathBuilder
{
    readonly KitchenGridPathFinder _pathFinder = new();
    readonly List<Vector2Int> _candidateCells = new(4);
    readonly List<Vector2Int> _cellPath = new();
    readonly List<Vector2Int> _bestCellPath = new();

    public bool TryBuildPath(
        KitchenNavigationMap navigationMap,
        KitchenNavigationDestination destination,
        Vector3 startWorldPosition,
        List<Vector3> results)
    {
        results.Clear();
        if (!navigationMap.IsReady)
        {
            return false;
        }

        var startCell = navigationMap.WorldToCell(startWorldPosition);
        return destination.HasTargetCounter
            ? TryBuildCounterPath(navigationMap, startCell, destination.Cell, results)
            : TryBuildMovePath(navigationMap, startCell, destination.Cell, results);
    }

    bool TryBuildMovePath(
        KitchenNavigationMap navigationMap,
        Vector2Int startCell,
        Vector2Int targetCell,
        List<Vector3> results)
    {
        if (!_pathFinder.TryFindPath(navigationMap, startCell, targetCell, _cellPath))
        {
            return false;
        }

        BuildWorldPath(navigationMap, _cellPath, results);
        return results.Count > 0;
    }

    bool TryBuildCounterPath(
        KitchenNavigationMap navigationMap,
        Vector2Int startCell,
        Vector2Int counterCell,
        List<Vector3> results)
    {
        navigationMap.CollectCounterApproachCells(counterCell, startCell, _candidateCells);
        if (_candidateCells.Count == 0)
        {
            return false;
        }

        var bestPathCount = int.MaxValue;
        _bestCellPath.Clear();

        for (var i = 0; i < _candidateCells.Count; i++)
        {
            if (!_pathFinder.TryFindPath(navigationMap, startCell, _candidateCells[i], _cellPath))
            {
                continue;
            }

            if (_cellPath.Count >= bestPathCount)
            {
                continue;
            }

            bestPathCount = _cellPath.Count;
            _bestCellPath.Clear();
            _bestCellPath.AddRange(_cellPath);
        }

        if (_bestCellPath.Count == 0)
        {
            return false;
        }

        BuildWorldPath(navigationMap, _bestCellPath, results);
        return results.Count > 0;
    }

    void BuildWorldPath(
        KitchenNavigationMap navigationMap,
        IReadOnlyList<Vector2Int> cellPath,
        List<Vector3> results)
    {
        results.Clear();
        for (var i = 0; i < cellPath.Count; i++)
        {
            results.Add(navigationMap.CellToWorld(cellPath[i]));
        }
    }
}
