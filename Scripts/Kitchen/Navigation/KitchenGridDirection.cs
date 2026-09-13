using UnityEngine;

public static class KitchenGridDirection
{
    public const int COUNT = 4;

    public static Vector2Int Get(int index)
    {
        switch (index)
        {
            case 0:
                return new Vector2Int(1, 0);
            case 1:
                return new Vector2Int(-1, 0);
            case 2:
                return new Vector2Int(0, 1);
            default:
                return new Vector2Int(0, -1);
        }
    }
}
