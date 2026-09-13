using UnityEngine;

public sealed class KitchenStageCamera : MonoBehaviour
{
    enum ViewCorner
    {
        BottomLeft,
        TopLeft,
        BottomRight,
        TopRight
    }

    const int VIEW_CORNER_COUNT = (int)ViewCorner.TopRight + 1;
    const float GIZMO_HEIGHT = 0.05f;

    public static float DefaultFieldOfView => KitchenCameraConstants.DEFAULT_FOV;

    [SerializeField] Camera _gameCamera;
    [SerializeField] Transform _cameraTransform;
    [SerializeField] Transform _followTarget;
    [SerializeField] Transform _mapBoundsRoot;
    [SerializeField] Renderer[] _mapBoundsRenderers = System.Array.Empty<Renderer>();
    [SerializeField] bool _useCustomMovementBounds;
    [SerializeField] Vector2 _movementBoundsCenter;
    [SerializeField] Vector2 _movementBoundsSize = new(20f, 20f);
    [SerializeField] float _gameplayPlaneY;
    [SerializeField] bool _drawGizmos = true;

    readonly Vector3[] _viewportRayDirections = new Vector3[VIEW_CORNER_COUNT];
    readonly Vector3[] _viewCorners = new Vector3[VIEW_CORNER_COUNT];

    Bounds _mapBounds;
    Bounds _initialViewBounds;
    Bounds _movementBounds;
    Bounds _currentViewBounds;
    Vector3 _cameraOffsetFromTarget;
    Vector3 _initialTargetPosition;
    Vector3 _initialFollowTargetPosition;
    Vector3 _currentTargetPosition;
    Vector3 _cameraMoveVelocity;
    Quaternion _fixedRotation;
    float _fixedFieldOfView;
    int _cachedPixelWidth = -1;
    int _cachedPixelHeight = -1;
    bool _isInitialized;
    bool _isFollowEnabled;

    public void Initialize(Camera gameCamera, Transform cameraTransform, Transform followTarget, Transform mapBoundsRoot)
    {
        if (gameCamera != null)
        {
            _gameCamera = gameCamera;
        }

        if (cameraTransform != null)
        {
            _cameraTransform = cameraTransform;
        }

        if (followTarget != null)
        {
            _followTarget = followTarget;
        }

        if (mapBoundsRoot != null)
        {
            _mapBoundsRoot = mapBoundsRoot;
        }

        Initialize();
    }

    public void Initialize()
    {
        _isInitialized = false;
        _isFollowEnabled = false;
        _cameraMoveVelocity = Vector3.zero;

        ResolveMissingReferences();
        if (_gameCamera == null || _cameraTransform == null)
        {
            Debug.LogWarning("[KitchenStageCamera] Camera reference is missing.");
            return;
        }

        if (_gameCamera.orthographic)
        {
            Debug.LogWarning("[KitchenStageCamera] Orthographic camera is not supported. Camera projection was changed to Perspective.");
            _gameCamera.orthographic = false;
        }

        _fixedRotation = _cameraTransform.rotation;
        _fixedFieldOfView = _gameCamera.fieldOfView > 0f ? _gameCamera.fieldOfView : KitchenCameraConstants.DEFAULT_FOV;
        _gameCamera.fieldOfView = _fixedFieldOfView;

        if (!CacheViewportRays() || !CalculateMapBounds())
        {
            return;
        }

        FitCamera();
        ValidateMovementBounds();
        _isInitialized = true;
    }

    public void SetFollowEnabled(bool isFollowEnabled)
    {
        _isFollowEnabled = isFollowEnabled;
        if (!isFollowEnabled)
        {
            _cameraMoveVelocity = Vector3.zero;
        }
    }

    public static bool TryCalculateRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
        {
            return false;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        return TryCalculateRendererBounds(renderers, out bounds);
    }

    public static bool TryCalculateFitCameraPosition(
        Camera gameCamera,
        Transform cameraTransform,
        float fieldOfView,
        Bounds mapBounds,
        float gameplayPlaneY,
        out Vector3 cameraPosition)
    {
        cameraPosition = default;
        if (gameCamera == null || cameraTransform == null)
        {
            return false;
        }

        var previousFieldOfView = gameCamera.fieldOfView;
        if (fieldOfView > 0f)
        {
            gameCamera.fieldOfView = fieldOfView;
        }

        try
        {
            var rayDirections = new Vector3[VIEW_CORNER_COUNT];
            var viewCorners = new Vector3[VIEW_CORNER_COUNT];
            if (!CacheViewportRays(gameCamera, rayDirections))
            {
                return false;
            }

            var target = mapBounds.center;
            target.y = gameplayPlaneY;
            var cameraRotation = cameraTransform.rotation;
            var forward = cameraRotation * Vector3.forward;
            if (Mathf.Abs(forward.y) < KitchenCameraConstants.MIN_RAY_PLANE_DOT)
            {
                return false;
            }

            var distance = CalculateFitDistance(
                rayDirections,
                viewCorners,
                cameraTransform.position,
                mapBounds,
                target,
                forward,
                cameraRotation,
                gameplayPlaneY);

            var initialCameraPosition = target - forward * distance;
            if (CalculateViewBounds(rayDirections, viewCorners, initialCameraPosition, cameraRotation, gameplayPlaneY, out var initialViewBounds))
            {
                var movementBounds = CreateAutoMovementBounds(initialViewBounds, mapBounds, gameplayPlaneY);
                distance = CalculateFixedDistanceForMovement(
                    rayDirections,
                    viewCorners,
                    mapBounds,
                    initialViewBounds,
                    movementBounds,
                    target,
                    forward,
                    cameraRotation,
                    gameplayPlaneY,
                    distance);
            }

            cameraPosition = target - forward * distance;
            return true;
        }
        finally
        {
            gameCamera.fieldOfView = previousFieldOfView;
        }
    }

    public bool CalculateMapBounds()
    {
        if (_mapBoundsRenderers != null &&
            _mapBoundsRenderers.Length > 0 &&
            TryCalculateRendererBounds(_mapBoundsRenderers, out _mapBounds))
        {
            return true;
        }

        if (_mapBoundsRoot == null)
        {
            Debug.LogWarning("[KitchenStageCamera] Map bounds root is not assigned.");
            return false;
        }

        var renderers = _mapBoundsRoot.GetComponentsInChildren<Renderer>(true);
        if (TryCalculateRendererBounds(renderers, out _mapBounds))
        {
            return true;
        }

        Debug.LogWarning($"[KitchenStageCamera] Map bounds is empty. root: {_mapBoundsRoot.name}");
        return false;
    }

    public void FitCamera()
    {
        CacheViewportRays();

        var target = _mapBounds.center;
        target.y = _gameplayPlaneY;
        var forward = _fixedRotation * Vector3.forward;
        if (Mathf.Abs(forward.y) < KitchenCameraConstants.MIN_RAY_PLANE_DOT)
        {
            Debug.LogWarning("[KitchenStageCamera] Camera forward is almost parallel to Gameplay Plane.");
            return;
        }

        var distance = CalculateFitDistance(target, forward);
        var position = target - forward * distance;
        _initialTargetPosition = target;
        _initialFollowTargetPosition = _followTarget != null ? _followTarget.position : target;
        _initialFollowTargetPosition.y = _gameplayPlaneY;
        _currentTargetPosition = target;
        _cameraMoveVelocity = Vector3.zero;

        SetCameraPosition(position);
        CalculateViewBounds(position, _fixedRotation, out _currentViewBounds);
        _initialViewBounds = _currentViewBounds;
        _movementBounds = _useCustomMovementBounds
            ? CreateMovementBounds()
            : CreateAutoMovementBounds(_initialViewBounds, _mapBounds, _gameplayPlaneY);

        distance = CalculateFixedDistanceForMovement(forward, distance);
        _cameraOffsetFromTarget = -forward * distance;
        position = target + _cameraOffsetFromTarget;

        SetCameraPosition(position);
        CalculateViewBounds(position, _fixedRotation, out _currentViewBounds);
        _initialViewBounds = _currentViewBounds;
        _movementBounds = _useCustomMovementBounds
            ? CreateMovementBounds()
            : CreateAutoMovementBounds(_initialViewBounds, _mapBounds, _gameplayPlaneY);
    }

    public void ValidateMovementBounds()
    {
        var mapMin = _mapBounds.min;
        var mapMax = _mapBounds.max;
        var moveMin = _movementBounds.min;
        var moveMax = _movementBounds.max;

        if (moveMin.x > mapMin.x || moveMax.x < mapMax.x || moveMin.z > mapMin.z || moveMax.z < mapMax.z)
        {
            Debug.LogWarning(
                "[KitchenStageCamera] Movement Bounds must contain Map Bounds on X/Z. " +
                $"Map Min: ({mapMin.x:F2}, {mapMin.z:F2}), Map Max: ({mapMax.x:F2}, {mapMax.z:F2}), " +
                $"Movement Min: ({moveMin.x:F2}, {moveMin.z:F2}), Movement Max: ({moveMax.x:F2}, {moveMax.z:F2})");
        }

        if (_currentViewBounds.size.x > _movementBounds.size.x + KitchenCameraConstants.MIN_BOUNDS_SIZE ||
            _currentViewBounds.size.z > _movementBounds.size.z + KitchenCameraConstants.MIN_BOUNDS_SIZE)
        {
            Debug.LogWarning("[KitchenStageCamera] Current Camera View is larger than Movement Bounds.");
        }
    }

    public Vector3 CalculateDesiredTarget()
    {
        if (_followTarget == null)
        {
            return _currentTargetPosition;
        }

        var followTargetPosition = _followTarget.position;
        followTargetPosition.y = _gameplayPlaneY;

        var target = _initialTargetPosition + followTargetPosition - _initialFollowTargetPosition;
        target.x = ClampDesiredTargetAxis(
            target.x,
            _initialTargetPosition.x,
            _initialViewBounds.min.x,
            _initialViewBounds.max.x,
            _movementBounds.min.x,
            _movementBounds.max.x);
        target.z = ClampDesiredTargetAxis(
            target.z,
            _initialTargetPosition.z,
            _initialViewBounds.min.z,
            _initialViewBounds.max.z,
            _movementBounds.min.z,
            _movementBounds.max.z);
        target.y = _gameplayPlaneY;
        return target;
    }

    public Vector3 ApplyDeadZone(Vector3 desiredTarget)
    {
        var target = _currentTargetPosition;
        target.x = ApplyDeadZoneAxis(
            desiredTarget.x,
            _currentTargetPosition.x,
            KitchenCameraConstants.DEFAULT_DEAD_ZONE_SIZE_X);
        target.z = ApplyDeadZoneAxis(
            desiredTarget.z,
            _currentTargetPosition.z,
            KitchenCameraConstants.DEFAULT_DEAD_ZONE_SIZE_Z);
        target.y = _gameplayPlaneY;
        return target;
    }

    public Vector3 ClampToMovementBounds(Vector3 cameraPosition)
    {
        if (!CalculateViewBounds(cameraPosition, _fixedRotation, out var viewBounds))
        {
            return cameraPosition;
        }

        var moveMin = _movementBounds.min;
        var moveMax = _movementBounds.max;
        var correctionX = CalculateClampCorrection(viewBounds.min.x, viewBounds.max.x, viewBounds.center.x, moveMin.x, moveMax.x, _movementBounds.center.x);
        var correctionZ = CalculateClampCorrection(viewBounds.min.z, viewBounds.max.z, viewBounds.center.z, moveMin.z, moveMax.z, _movementBounds.center.z);

        cameraPosition.x += correctionX;
        cameraPosition.z += correctionZ;
        viewBounds.center += new Vector3(correctionX, 0f, correctionZ);
        _currentViewBounds = viewBounds;
        return cameraPosition;
    }

    public void SmoothFollow(Vector3 finalCameraPosition)
    {
        SmoothFollow(finalCameraPosition, Time.deltaTime);
    }

    void LateUpdate()
    {
        if (!_isInitialized || !_isFollowEnabled || _cameraTransform == null)
        {
            return;
        }

        if (HasCameraProjectionChanged())
        {
            FitCamera();
            ValidateMovementBounds();
        }

        var desiredTarget = CalculateDesiredTarget();
        var deadZoneTarget = ApplyDeadZone(desiredTarget);
        var clampedPosition = ClampToMovementBounds(deadZoneTarget + _cameraOffsetFromTarget);
        _currentTargetPosition = clampedPosition - _cameraOffsetFromTarget;
        _currentTargetPosition.y = _gameplayPlaneY;
        SmoothFollow(clampedPosition, Time.deltaTime);
    }

    void SmoothFollow(Vector3 finalCameraPosition, float deltaTime)
    {
        var currentPosition = _cameraTransform.position;
        if ((currentPosition - finalCameraPosition).sqrMagnitude <= KitchenCameraConstants.POSITION_SNAP_SQR_DISTANCE)
        {
            _cameraMoveVelocity = Vector3.zero;
            SetCameraPosition(finalCameraPosition);
            CalculateViewBounds(finalCameraPosition, _fixedRotation, out _currentViewBounds);
            return;
        }

        var position = Vector3.SmoothDamp(
            currentPosition,
            finalCameraPosition,
            ref _cameraMoveVelocity,
            KitchenCameraConstants.POSITION_SMOOTH_TIME,
            KitchenCameraConstants.POSITION_MAX_MOVE_SPEED,
            deltaTime);

        SetCameraPosition(position);
        CalculateViewBounds(position, _fixedRotation, out _currentViewBounds);
    }

    void ResolveMissingReferences()
    {
        if (_gameCamera == null)
        {
            _gameCamera = Camera.main;
        }

        if (_cameraTransform == null && _gameCamera != null)
        {
            _cameraTransform = _gameCamera.transform;
        }
    }

    bool CacheViewportRays()
    {
        if (!CacheViewportRays(_gameCamera, _viewportRayDirections))
        {
            return false;
        }

        _cachedPixelWidth = _gameCamera.pixelWidth;
        _cachedPixelHeight = _gameCamera.pixelHeight;
        return true;
    }

    static bool CacheViewportRays(Camera gameCamera, Vector3[] rayDirections)
    {
        if (gameCamera == null || rayDirections == null || rayDirections.Length < VIEW_CORNER_COUNT)
        {
            return false;
        }

        var inverseRotation = Quaternion.Inverse(gameCamera.transform.rotation);
        rayDirections[(int)ViewCorner.BottomLeft] = inverseRotation * gameCamera.ViewportPointToRay(new Vector3(0f, 0f, 0f)).direction;
        rayDirections[(int)ViewCorner.TopLeft] = inverseRotation * gameCamera.ViewportPointToRay(new Vector3(0f, 1f, 0f)).direction;
        rayDirections[(int)ViewCorner.BottomRight] = inverseRotation * gameCamera.ViewportPointToRay(new Vector3(1f, 0f, 0f)).direction;
        rayDirections[(int)ViewCorner.TopRight] = inverseRotation * gameCamera.ViewportPointToRay(new Vector3(1f, 1f, 0f)).direction;
        return true;
    }

    bool HasCameraProjectionChanged()
    {
        if (_gameCamera == null)
        {
            return false;
        }

        if (!Mathf.Approximately(_gameCamera.fieldOfView, _fixedFieldOfView))
        {
            _gameCamera.fieldOfView = _fixedFieldOfView;
            return true;
        }

        return _gameCamera.pixelWidth != _cachedPixelWidth || _gameCamera.pixelHeight != _cachedPixelHeight;
    }

    float CalculateFitDistance(Vector3 target, Vector3 forward)
    {
        return CalculateFitDistance(
            _viewportRayDirections,
            _viewCorners,
            _cameraTransform.position,
            _mapBounds,
            target,
            forward,
            _fixedRotation,
            _gameplayPlaneY);
    }

    float CalculateFixedDistanceForMovement(Vector3 forward, float baseDistance)
    {
        return CalculateFixedDistanceForMovement(
            _viewportRayDirections,
            _viewCorners,
            _mapBounds,
            _initialViewBounds,
            _movementBounds,
            _initialTargetPosition,
            forward,
            _fixedRotation,
            _gameplayPlaneY,
            baseDistance);
    }

    static float CalculateFitDistance(
        Vector3[] rayDirections,
        Vector3[] viewCorners,
        Vector3 currentCameraPosition,
        Bounds mapBounds,
        Vector3 target,
        Vector3 forward,
        Quaternion cameraRotation,
        float gameplayPlaneY)
    {
        var max = Mathf.Max(KitchenCameraConstants.MIN_DISTANCE, Vector3.Dot(currentCameraPosition - target, -forward));
        while (max < KitchenCameraConstants.MAX_FIT_DISTANCE &&
               !ContainsMapBounds(rayDirections, viewCorners, target - forward * max, cameraRotation, mapBounds, gameplayPlaneY))
        {
            max *= 2f;
        }

        var low = KitchenCameraConstants.MIN_DISTANCE;
        var high = Mathf.Min(max, KitchenCameraConstants.MAX_FIT_DISTANCE);
        for (var i = 0; i < KitchenCameraConstants.FIT_DISTANCE_SEARCH_COUNT; i++)
        {
            var mid = (low + high) * 0.5f;
            if (ContainsMapBounds(rayDirections, viewCorners, target - forward * mid, cameraRotation, mapBounds, gameplayPlaneY))
            {
                high = mid;
            }
            else
            {
                low = mid;
            }
        }

        return Mathf.Max(KitchenCameraConstants.MIN_DISTANCE, high * KitchenCameraConstants.CAMERA_PADDING_RATIO);
    }

    static float CalculateFixedDistanceForMovement(
        Vector3[] rayDirections,
        Vector3[] viewCorners,
        Bounds mapBounds,
        Bounds initialViewBounds,
        Bounds movementBounds,
        Vector3 initialTarget,
        Vector3 forward,
        Quaternion cameraRotation,
        float gameplayPlaneY,
        float baseDistance)
    {
        var distance = baseDistance;
        TryCalculateTargetRangeAxis(
            initialTarget.x,
            initialViewBounds.min.x,
            initialViewBounds.max.x,
            movementBounds.min.x,
            movementBounds.max.x,
            out var minTargetX,
            out var maxTargetX);
        TryCalculateTargetRangeAxis(
            initialTarget.z,
            initialViewBounds.min.z,
            initialViewBounds.max.z,
            movementBounds.min.z,
            movementBounds.max.z,
            out var minTargetZ,
            out var maxTargetZ);

        distance = Mathf.Max(distance, CalculateFitDistanceAtTarget(rayDirections, viewCorners, mapBounds, minTargetX, minTargetZ, forward, cameraRotation, gameplayPlaneY, distance));
        distance = Mathf.Max(distance, CalculateFitDistanceAtTarget(rayDirections, viewCorners, mapBounds, minTargetX, maxTargetZ, forward, cameraRotation, gameplayPlaneY, distance));
        distance = Mathf.Max(distance, CalculateFitDistanceAtTarget(rayDirections, viewCorners, mapBounds, maxTargetX, minTargetZ, forward, cameraRotation, gameplayPlaneY, distance));
        distance = Mathf.Max(distance, CalculateFitDistanceAtTarget(rayDirections, viewCorners, mapBounds, maxTargetX, maxTargetZ, forward, cameraRotation, gameplayPlaneY, distance));

        return Mathf.Max(KitchenCameraConstants.MIN_DISTANCE, distance);
    }

    static float CalculateFitDistanceAtTarget(
        Vector3[] rayDirections,
        Vector3[] viewCorners,
        Bounds mapBounds,
        float targetX,
        float targetZ,
        Vector3 forward,
        Quaternion cameraRotation,
        float gameplayPlaneY,
        float currentDistance)
    {
        var target = new Vector3(targetX, gameplayPlaneY, targetZ);
        return CalculateFitDistance(
            rayDirections,
            viewCorners,
            target - forward * currentDistance,
            mapBounds,
            target,
            forward,
            cameraRotation,
            gameplayPlaneY);
    }

    static bool ContainsMapBounds(
        Vector3[] rayDirections,
        Vector3[] viewCorners,
        Vector3 cameraPosition,
        Quaternion cameraRotation,
        Bounds mapBounds,
        float gameplayPlaneY)
    {
        if (!CalculateViewCorners(rayDirections, viewCorners, cameraPosition, cameraRotation, gameplayPlaneY))
        {
            return false;
        }

        var min = mapBounds.min;
        var max = mapBounds.max;
        return IsInsideView(viewCorners, min.x, min.z, gameplayPlaneY) &&
               IsInsideView(viewCorners, min.x, max.z, gameplayPlaneY) &&
               IsInsideView(viewCorners, max.x, min.z, gameplayPlaneY) &&
               IsInsideView(viewCorners, max.x, max.z, gameplayPlaneY);
    }

    bool CalculateViewBounds(Vector3 cameraPosition, Quaternion cameraRotation, out Bounds viewBounds)
    {
        viewBounds = default;
        if (!CalculateViewCorners(cameraPosition, cameraRotation))
        {
            return false;
        }

        viewBounds = new Bounds(_viewCorners[0], Vector3.zero);
        for (var i = 1; i < _viewCorners.Length; i++)
        {
            viewBounds.Encapsulate(_viewCorners[i]);
        }

        return true;
    }

    static bool CalculateViewBounds(
        Vector3[] rayDirections,
        Vector3[] viewCorners,
        Vector3 cameraPosition,
        Quaternion cameraRotation,
        float gameplayPlaneY,
        out Bounds viewBounds)
    {
        viewBounds = default;
        if (!CalculateViewCorners(rayDirections, viewCorners, cameraPosition, cameraRotation, gameplayPlaneY))
        {
            return false;
        }

        viewBounds = new Bounds(viewCorners[0], Vector3.zero);
        for (var i = 1; i < viewCorners.Length; i++)
        {
            viewBounds.Encapsulate(viewCorners[i]);
        }

        return true;
    }

    bool CalculateViewCorners(Vector3 cameraPosition, Quaternion cameraRotation)
    {
        return CalculateViewCorners(_viewportRayDirections, _viewCorners, cameraPosition, cameraRotation, _gameplayPlaneY);
    }

    static bool CalculateViewCorners(
        Vector3[] rayDirections,
        Vector3[] viewCorners,
        Vector3 cameraPosition,
        Quaternion cameraRotation,
        float gameplayPlaneY)
    {
        if (rayDirections == null || viewCorners == null)
        {
            return false;
        }

        for (var i = 0; i < rayDirections.Length && i < viewCorners.Length; i++)
        {
            var direction = cameraRotation * rayDirections[i];
            if (Mathf.Abs(direction.y) < KitchenCameraConstants.MIN_RAY_PLANE_DOT)
            {
                return false;
            }

            var distance = (gameplayPlaneY - cameraPosition.y) / direction.y;
            if (distance <= 0f)
            {
                return false;
            }

            viewCorners[i] = cameraPosition + direction * distance;
            viewCorners[i].y = gameplayPlaneY;
        }

        return true;
    }

    static bool IsInsideView(Vector3[] viewCorners, float x, float z, float gameplayPlaneY)
    {
        var point = new Vector3(x, gameplayPlaneY, z);
        return IsInsideTriangle(
                   point,
                   viewCorners[(int)ViewCorner.BottomLeft],
                   viewCorners[(int)ViewCorner.BottomRight],
                   viewCorners[(int)ViewCorner.TopRight]) ||
               IsInsideTriangle(
                   point,
                   viewCorners[(int)ViewCorner.BottomLeft],
                   viewCorners[(int)ViewCorner.TopRight],
                   viewCorners[(int)ViewCorner.TopLeft]);
    }

    static bool IsInsideTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        var s1 = CrossXZ(point, a, b);
        var s2 = CrossXZ(point, b, c);
        var s3 = CrossXZ(point, c, a);
        return !((s1 < 0f || s2 < 0f || s3 < 0f) && (s1 > 0f || s2 > 0f || s3 > 0f));
    }

    static float CrossXZ(Vector3 point, Vector3 a, Vector3 b)
    {
        return (point.x - b.x) * (a.z - b.z) - (a.x - b.x) * (point.z - b.z);
    }

    static float ClampDesiredTargetAxis(
        float desiredTarget,
        float initialTarget,
        float initialViewMin,
        float initialViewMax,
        float movementMin,
        float movementMax)
    {
        if (!TryCalculateTargetRangeAxis(
                initialTarget,
                initialViewMin,
                initialViewMax,
                movementMin,
                movementMax,
                out var minTarget,
                out var maxTarget))
        {
            return initialTarget;
        }

        return Mathf.Clamp(desiredTarget, minTarget, maxTarget);
    }

    static bool TryCalculateTargetRangeAxis(
        float initialTarget,
        float initialViewMin,
        float initialViewMax,
        float movementMin,
        float movementMax,
        out float minTarget,
        out float maxTarget)
    {
        minTarget = initialTarget + movementMin - initialViewMin;
        maxTarget = initialTarget + movementMax - initialViewMax;
        if (minTarget <= maxTarget)
        {
            return true;
        }

        minTarget = initialTarget;
        maxTarget = initialTarget;
        return false;
    }

    static float ApplyDeadZoneAxis(float desiredTarget, float currentTarget, float deadZoneSize)
    {
        var halfSize = Mathf.Max(0f, deadZoneSize) * 0.5f;
        var delta = desiredTarget - currentTarget;
        if (Mathf.Abs(delta) <= halfSize)
        {
            return currentTarget;
        }

        return desiredTarget - Mathf.Sign(delta) * halfSize;
    }

    static float CalculateClampCorrection(float viewMin, float viewMax, float viewCenter, float boundsMin, float boundsMax, float boundsCenter)
    {
        if (viewMax - viewMin > boundsMax - boundsMin)
        {
            return boundsCenter - viewCenter;
        }

        if (viewMin < boundsMin)
        {
            return boundsMin - viewMin;
        }

        return viewMax > boundsMax ? boundsMax - viewMax : 0f;
    }

    static bool TryCalculateRendererBounds(Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        var hasBounds = false;
        if (renderers == null)
        {
            return false;
        }

        for (var i = 0; i < renderers.Length; i++)
        {
            var targetRenderer = renderers[i];
            if (targetRenderer == null || !targetRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(targetRenderer.bounds);
            }
        }

        return hasBounds;
    }

    Bounds CreateMovementBounds()
    {
        var size = new Vector3(
            Mathf.Max(KitchenCameraConstants.MIN_BOUNDS_SIZE, Mathf.Abs(_movementBoundsSize.x)),
            GIZMO_HEIGHT,
            Mathf.Max(KitchenCameraConstants.MIN_BOUNDS_SIZE, Mathf.Abs(_movementBoundsSize.y)));
        return new Bounds(new Vector3(_movementBoundsCenter.x, _gameplayPlaneY, _movementBoundsCenter.y), size);
    }

    static Bounds CreateAutoMovementBounds(Bounds initialViewBounds, Bounds mapBounds, float gameplayPlaneY)
    {
        var bounds = initialViewBounds;
        EncapsulateMapBoundsOnGameplayPlane(ref bounds, mapBounds, gameplayPlaneY);
        ExpandAutoMovementBounds(ref bounds);
        return bounds;
    }

    static void EncapsulateMapBoundsOnGameplayPlane(ref Bounds bounds, Bounds mapBounds, float gameplayPlaneY)
    {
        var min = mapBounds.min;
        var max = mapBounds.max;
        bounds.Encapsulate(new Vector3(min.x, gameplayPlaneY, min.z));
        bounds.Encapsulate(new Vector3(min.x, gameplayPlaneY, max.z));
        bounds.Encapsulate(new Vector3(max.x, gameplayPlaneY, min.z));
        bounds.Encapsulate(new Vector3(max.x, gameplayPlaneY, max.z));
    }

    static void ExpandAutoMovementBounds(ref Bounds bounds)
    {
        var min = bounds.min;
        var max = bounds.max;

        min.x -= Mathf.Max(0f, KitchenCameraConstants.DEFAULT_MOVEMENT_BOUNDS_PADDING_LEFT);
        max.x += Mathf.Max(0f, KitchenCameraConstants.DEFAULT_MOVEMENT_BOUNDS_PADDING_RIGHT);
        min.z -= Mathf.Max(0f, KitchenCameraConstants.DEFAULT_MOVEMENT_BOUNDS_PADDING_BOTTOM);
        max.z += Mathf.Max(0f, KitchenCameraConstants.DEFAULT_MOVEMENT_BOUNDS_PADDING_TOP);

        bounds.SetMinMax(min, max);
    }

    void SetCameraPosition(Vector3 position)
    {
        _cameraTransform.SetPositionAndRotation(position, _fixedRotation);
    }

    void Reset()
    {
        _gameCamera = GetComponent<Camera>();
        _cameraTransform = transform;
    }

    void OnValidate()
    {
        _movementBoundsSize.x = Mathf.Max(KitchenCameraConstants.MIN_BOUNDS_SIZE, Mathf.Abs(_movementBoundsSize.x));
        _movementBoundsSize.y = Mathf.Max(KitchenCameraConstants.MIN_BOUNDS_SIZE, Mathf.Abs(_movementBoundsSize.y));
    }

    void OnDrawGizmos()
    {
        if (!_drawGizmos)
        {
            return;
        }

        if (_isInitialized)
        {
            DrawBounds(_mapBounds, Color.green);
            DrawBounds(_movementBounds, Color.yellow);
            DrawView();
            return;
        }

        if (_useCustomMovementBounds)
        {
            DrawBounds(CreateMovementBounds(), Color.yellow);
        }
    }

    void DrawBounds(Bounds bounds, Color color)
    {
        var previousColor = Gizmos.color;
        Gizmos.color = color;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
        Gizmos.color = previousColor;
    }

    void DrawView()
    {
        var previousColor = Gizmos.color;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(_viewCorners[(int)ViewCorner.BottomLeft], _viewCorners[(int)ViewCorner.BottomRight]);
        Gizmos.DrawLine(_viewCorners[(int)ViewCorner.BottomRight], _viewCorners[(int)ViewCorner.TopRight]);
        Gizmos.DrawLine(_viewCorners[(int)ViewCorner.TopRight], _viewCorners[(int)ViewCorner.TopLeft]);
        Gizmos.DrawLine(_viewCorners[(int)ViewCorner.TopLeft], _viewCorners[(int)ViewCorner.BottomLeft]);
        Gizmos.color = previousColor;
    }
}
