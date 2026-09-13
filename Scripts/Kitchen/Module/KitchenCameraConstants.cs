public static class KitchenCameraConstants
{
    // Initial View / Fit

    // Main Camera에서 유효한 FOV를 찾지 못했을 때 사용할 기본 시야각.
    public const float DEFAULT_FOV = 20f;

    // 맵 전체가 화면에 들어오도록 카메라 거리에 곱하는 여백 비율.
    public const float CAMERA_PADDING_RATIO = 0.9f;

    // Fit 거리 탐색 안전장치.
    public const int FIT_DISTANCE_SEARCH_COUNT = 40;
    public const float MAX_FIT_DISTANCE = 1000f;

    // Movement Bounds

    // Custom Movement Bounds를 쓰지 않을 때 초기 View Bounds 바깥으로 추가할 기본 이동 여유.
    public const float DEFAULT_MOVEMENT_BOUNDS_PADDING_LEFT = 0f;
    public const float DEFAULT_MOVEMENT_BOUNDS_PADDING_TOP = 2f;
    public const float DEFAULT_MOVEMENT_BOUNDS_PADDING_BOTTOM = 0f;
    public const float DEFAULT_MOVEMENT_BOUNDS_PADDING_RIGHT = 0f;

    // Player Follow

    // 플레이어의 작은 움직임에 카메라가 바로 반응하지 않도록 하는 X/Z Dead Zone 크기.
    public const float DEFAULT_DEAD_ZONE_SIZE_X = 1.2f;
    public const float DEFAULT_DEAD_ZONE_SIZE_Z = 0.4f;

    // 카메라 위치가 목표 위치에 부드럽게 도달하는 데 사용하는 대략적인 시간.
    public const float POSITION_SMOOTH_TIME = 0.28f;

    // 카메라가 한 번에 너무 빠르게 이동하지 않도록 제한하는 최대 이동 속도.
    public const float POSITION_MAX_MOVE_SPEED = 6f;

    // 목표 위치와 충분히 가까울 때 보간을 멈추고 위치를 고정하는 거리 제곱값.
    public const float POSITION_SNAP_SQR_DISTANCE = 0.0001f;

    // Common Safety

    // 카메라와 Gameplay Plane 사이 최소 거리. 과도한 근접 배치를 막음.
    public const float MIN_DISTANCE = 1f;

    // Perspective ray가 Gameplay Plane과 거의 평행해지는 예외 상황을 막기 위한 최소값.
    public const float MIN_RAY_PLANE_DOT = 0.0001f;

    // Bounds나 View 크기가 유효하다고 볼 최소 크기.
    public const float MIN_BOUNDS_SIZE = 0.01f;
}
