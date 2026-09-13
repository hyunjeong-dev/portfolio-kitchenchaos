using UnityEngine;

public readonly struct KitchenNavigationDestination
{
    public readonly Vector2Int Cell;
    public readonly BaseCounter TargetCounter;

    public bool HasTargetCounter => TargetCounter != null;

    KitchenNavigationDestination(Vector2Int cell, BaseCounter targetCounter)
    {
        Cell = cell;
        TargetCounter = targetCounter;
    }

    public static KitchenNavigationDestination MoveTo(Vector2Int cell)
    {
        return new(cell, null);
    }

    public static KitchenNavigationDestination InteractWith(Vector2Int cell, BaseCounter targetCounter)
    {
        return new(cell, targetCounter);
    }
}
