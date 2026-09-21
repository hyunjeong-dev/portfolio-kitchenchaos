using System.Collections.Generic;
using UnityEngine;

public sealed class KitchenTouchNavigationModule : KitchenGameModule
{
    // 웨이포인트 근처에서 다음 지점으로 넘겨 미세 이동 떨림을 막는다.
    const float TOUCH_WAYPOINT_ARRIVAL_DISTANCE = 0.05f;

    enum TouchNavigationStatus
    {
        None,
        PendingCommand,
        Moving,
        ActionReady
    }

    [ModuleRef] KitchenInputModule _inputModule;

    readonly KitchenNavigationMap _navigationMap = new();
    readonly KitchenTouchDestinationBuilder _destinationBuilder = new();
    readonly KitchenNavigationPathBuilder _pathBuilder = new();
    readonly List<Vector3> _touchPath = new();

    KitchenTouchCommand _pendingCommand;
    BaseCounter _pendingTouchCounter;
    Camera _mainCamera;
    int _touchPathIndex;
    TouchNavigationStatus _status;

    public override void OnBegin()
    {
        base.OnBegin();

        _mainCamera = Camera.main;

        _inputModule.Events.OnTouchCommand += OnInputTouchCommand;
    }

    public override void OnPostProcess()
    {
        _navigationMap.Rebuild(Context);
    }

    public override void OnEnd()
    {
        _inputModule.Events.OnTouchCommand -= OnInputTouchCommand;

        Cancel();
        _mainCamera = null;

        base.OnEnd();
    }

    public Vector2 GetMoveInput(Vector3 playerPosition, float maxMoveDistance)
    {
        BuildPendingPathIfNeeded(playerPosition);
        AdvanceReachedWaypoints(playerPosition);
        if (_status != TouchNavigationStatus.Moving || maxMoveDistance <= 0f)
        {
            return Vector2.zero;
        }

        var moveDir = _touchPath[_touchPathIndex] - playerPosition;
        var input = new Vector2(moveDir.x, moveDir.z);
        return Vector2.ClampMagnitude(input / maxMoveDistance, 1f);
    }

    public bool TryConsumeAction(
        Vector3 playerPosition,
        out BaseCounter counter,
        out KitchenTouchActionType actionType)
    {
        counter = null;
        actionType = KitchenTouchActionType.Move;

        // 이동 후 도착 확인에서는 다음 프레임의 이동 입력을 생성하지 않는다.
        AdvanceReachedWaypoints(playerPosition);
        if (_status != TouchNavigationStatus.ActionReady)
        {
            return false;
        }

        counter = _pendingTouchCounter;
        actionType = KitchenInputLogic.ResolveTouchActionType(counter);
        Cancel();

        return counter != null && _navigationMap.IsCounterReachable(playerPosition, counter);
    }

    public void Cancel()
    {
        _touchPath.Clear();
        _touchPathIndex = 0;
        _pendingCommand = default;
        _pendingTouchCounter = null;
        _status = TouchNavigationStatus.None;
    }

    void OnInputTouchCommand(KitchenTouchCommand command)
    {
        if (!Context.IsPlayable)
        {
            return;
        }

        _pendingCommand = command;
        _status = TouchNavigationStatus.PendingCommand;
    }

    void BuildPendingPathIfNeeded(Vector3 playerPosition)
    {
        if (_status != TouchNavigationStatus.PendingCommand)
        {
            return;
        }

        _status = TouchNavigationStatus.None;

        var camera = ResolveMainCamera();
        if (camera == null)
        {
            Debug.LogWarning("[KitchenTouchNavigationModule] Main camera is not found.");
            Cancel();
            return;
        }

        if (!_destinationBuilder.TryBuild(
                camera,
                _pendingCommand,
                _navigationMap,
                out var destination) ||
            !_pathBuilder.TryBuildPath(
                _navigationMap,
                destination,
                playerPosition,
                _touchPath))
        {
            Cancel();
            return;
        }

        _pendingTouchCounter = destination.TargetCounter;
        _touchPathIndex = 0;
        _status = TouchNavigationStatus.Moving;
    }

    void AdvanceReachedWaypoints(Vector3 playerPosition)
    {
        if (_status != TouchNavigationStatus.Moving)
        {
            return;
        }

        while (_touchPathIndex < _touchPath.Count)
        {
            var waypoint = _touchPath[_touchPathIndex];
            var moveDir = waypoint - playerPosition;
            moveDir.y = 0f;

            if (moveDir.sqrMagnitude > TOUCH_WAYPOINT_ARRIVAL_DISTANCE * TOUCH_WAYPOINT_ARRIVAL_DISTANCE)
            {
                return;
            }

            _touchPathIndex++;
        }

        _status = _pendingTouchCounter != null
            ? TouchNavigationStatus.ActionReady
            : TouchNavigationStatus.None;
    }

    Camera ResolveMainCamera()
    {
        if (_mainCamera != null)
        {
            return _mainCamera;
        }

        _mainCamera = Camera.main;
        return _mainCamera;
    }
}
