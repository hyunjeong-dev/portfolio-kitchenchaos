using System;
using UnityEngine;

[Serializable]
public sealed class KitchenMapCounterEntry
{
    [SerializeField] KitchenMapCounterType _counterType = KitchenMapCounterType.Clear;
    [SerializeField] KitchenMapDirection _direction = KitchenMapDirection.Front;
    [SerializeField] Vector2Int _gridPosition;
    [SerializeField] string _containerIngredientCode;

    public KitchenMapCounterEntry()
    {
    }

    public KitchenMapCounterEntry(
        KitchenMapCounterType counterType,
        KitchenMapDirection direction,
        Vector2Int gridPosition,
        string containerIngredientCode = null)
    {
        _counterType = counterType;
        _direction = direction;
        _gridPosition = gridPosition;
        _containerIngredientCode = containerIngredientCode;
    }

    public KitchenMapCounterType CounterType => _counterType;
    public KitchenMapDirection Direction => _direction;
    public Vector2Int GridPosition => _gridPosition;
    public string ContainerIngredientCode => _containerIngredientCode;
}
