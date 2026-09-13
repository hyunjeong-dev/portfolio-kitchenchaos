using System.Collections.Generic;
using UnityEngine;

public sealed class KitchenGridPathFinder
{
    readonly struct PathNode
    {
        public readonly Vector2Int Cell;
        public readonly int Score;
        public readonly int Heuristic;

        public PathNode(Vector2Int cell, int score, int heuristic)
        {
            Cell = cell;
            Score = score;
            Heuristic = heuristic;
        }
    }

    readonly Dictionary<Vector2Int, Vector2Int> _cameFrom = new();
    readonly Dictionary<Vector2Int, int> _costByCell = new();
    readonly Dictionary<Vector2Int, int> _openIndexByCell = new();
    readonly List<PathNode> _openHeap = new();

    public bool TryFindPath(
        KitchenNavigationMap navigationMap,
        Vector2Int startCell,
        Vector2Int targetCell,
        List<Vector2Int> results)
    {
        results.Clear();

        if (!navigationMap.IsPathTargetWalkable(startCell, startCell) ||
            !navigationMap.IsPathTargetWalkable(targetCell, startCell))
        {
            return false;
        }

        if (startCell == targetCell)
        {
            results.Add(targetCell);
            return true;
        }

        ResetSearch();

        _costByCell[startCell] = 0;
        PushOrUpdateOpenCell(startCell, targetCell);

        while (_openHeap.Count > 0)
        {
            var current = PopBestOpenCell();
            if (current == targetCell)
            {
                BuildResultPath(startCell, targetCell, results);
                return results.Count > 0;
            }

            var currentCost = _costByCell[current];
            for (var i = 0; i < KitchenGridDirection.COUNT; i++)
            {
                var next = current + KitchenGridDirection.Get(i);
                if (!navigationMap.IsPathTargetWalkable(next, startCell))
                {
                    continue;
                }

                var nextCost = currentCost + 1;
                if (_costByCell.TryGetValue(next, out var previousCost) && previousCost <= nextCost)
                {
                    continue;
                }

                _costByCell[next] = nextCost;
                _cameFrom[next] = current;
                PushOrUpdateOpenCell(next, targetCell);
            }
        }

        return false;
    }

    void ResetSearch()
    {
        _openHeap.Clear();
        _openIndexByCell.Clear();
        _cameFrom.Clear();
        _costByCell.Clear();
    }

    Vector2Int PopBestOpenCell()
    {
        var bestCell = _openHeap[0].Cell;
        var lastIndex = _openHeap.Count - 1;
        var lastNode = _openHeap[lastIndex];

        _openHeap.RemoveAt(lastIndex);
        _openIndexByCell.Remove(bestCell);

        if (_openHeap.Count > 0)
        {
            _openHeap[0] = lastNode;
            _openIndexByCell[lastNode.Cell] = 0;
            SiftDownOpenCell(0);
        }

        return bestCell;
    }

    void PushOrUpdateOpenCell(Vector2Int cell, Vector2Int targetCell)
    {
        var node = new PathNode(
            cell,
            GetEstimatedCost(cell, targetCell),
            GetHeuristicCost(cell, targetCell));

        if (_openIndexByCell.TryGetValue(cell, out var index))
        {
            _openHeap[index] = node;
            SiftUpOpenCell(index);
            SiftDownOpenCell(index);
            return;
        }

        _openHeap.Add(node);
        var newIndex = _openHeap.Count - 1;
        _openIndexByCell.Add(cell, newIndex);
        SiftUpOpenCell(newIndex);
    }

    void SiftUpOpenCell(int index)
    {
        while (index > 0)
        {
            var parentIndex = (index - 1) / 2;
            if (CompareOpenNode(_openHeap[parentIndex], _openHeap[index]) <= 0)
            {
                return;
            }

            SwapOpenNodes(parentIndex, index);
            index = parentIndex;
        }
    }

    void SiftDownOpenCell(int index)
    {
        while (true)
        {
            var leftIndex = index * 2 + 1;
            var rightIndex = leftIndex + 1;
            var bestIndex = index;

            if (leftIndex < _openHeap.Count &&
                CompareOpenNode(_openHeap[leftIndex], _openHeap[bestIndex]) < 0)
            {
                bestIndex = leftIndex;
            }

            if (rightIndex < _openHeap.Count &&
                CompareOpenNode(_openHeap[rightIndex], _openHeap[bestIndex]) < 0)
            {
                bestIndex = rightIndex;
            }

            if (bestIndex == index)
            {
                return;
            }

            SwapOpenNodes(index, bestIndex);
            index = bestIndex;
        }
    }

    void SwapOpenNodes(int leftIndex, int rightIndex)
    {
        var left = _openHeap[leftIndex];
        var right = _openHeap[rightIndex];

        _openHeap[leftIndex] = right;
        _openHeap[rightIndex] = left;
        _openIndexByCell[right.Cell] = leftIndex;
        _openIndexByCell[left.Cell] = rightIndex;
    }

    int CompareOpenNode(PathNode left, PathNode right)
    {
        var scoreCompare = left.Score.CompareTo(right.Score);
        return scoreCompare != 0
            ? scoreCompare
            : left.Heuristic.CompareTo(right.Heuristic);
    }

    void BuildResultPath(Vector2Int startCell,
        Vector2Int targetCell,
        List<Vector2Int> results)
    {
        var current = targetCell;
        results.Add(current);

        while (current != startCell)
        {
            if (!_cameFrom.TryGetValue(current, out current))
            {
                results.Clear();
                return;
            }

            if (current != startCell)
            {
                results.Add(current);
            }
        }

        results.Reverse();
    }

    int GetEstimatedCost(Vector2Int cell, Vector2Int targetCell)
    {
        var pathCost = _costByCell.TryGetValue(cell, out var cost) ? cost : 0;
        return pathCost + GetHeuristicCost(cell, targetCell);
    }

    int GetHeuristicCost(Vector2Int cell, Vector2Int targetCell)
    {
        return Mathf.Abs(cell.x - targetCell.x) + Mathf.Abs(cell.y - targetCell.y);
    }
}
