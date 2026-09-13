using UnityEngine;

public static class KitchenMapConstants
{
    public const string COUNTERS_ROOT_NAME = "Counters";
    public const int DEFAULT_WIDTH = 10;
    public const int DEFAULT_HEIGHT = 7;
    public const float DEFAULT_CELL_SIZE = 1.5f;
    public const float DEFAULT_OFFSET_X = -6f;
    public const float DEFAULT_OFFSET_Y = 0f;
    public const float DEFAULT_OFFSET_Z = -5.5f;

    public static Vector3 DefaultOffset => new(DEFAULT_OFFSET_X, DEFAULT_OFFSET_Y, DEFAULT_OFFSET_Z);
}
