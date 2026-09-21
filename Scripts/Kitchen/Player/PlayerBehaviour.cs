using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PlayerBehaviour : MonoBehaviour, IHolder
{
    const float INTERACT_DISTANCE = 2f;
    const float PLAYER_RADIUS = .6f;
    const float PLAYER_HEIGHT = 2f;
    const float ROTATE_SPEED = 10f;
    const float ROTATION_SNAP_ANGLE = 0.1f;
    const int MOVE_CAST_HIT_CAPACITY = 16;
    const int FOOTSTEP_SOUND_INTERVAL_MILLISECONDS = 100;
    const string FLOOR_COLLIDER_NAME = "Floor";

    public static PlayerBehaviour Instance { get; private set; }

    public BehaviourFsm<PlayerBehaviour> Fsm { get; private set; }
    public float MoveSpeed => moveSpeed;
    public float DashSpeed => dashSpeed;
    public float DashDuration => dashDuration;
    public Vector3 DashDirection => dashDirection;
    public bool HasMoveInput => _moveInput != Vector2.zero;
    public bool CanDash => Fsm != null && dashCooldownTimer <= 0f && !Fsm.IsActivateState<PlayerDashState>();
    public bool IsWalking => isWalking;
    public BaseCounter SelectedCounter => _selectorModule.CurrentCounter;
    public Vector3 Forward => transform.forward;
    public Vector3 MoveDirection => new(_moveInput.x, 0f, _moveInput.y);

    [SerializeField] float moveSpeed = 7f;
    [SerializeField] float dashSpeed = 18f;
    [SerializeField] float dashDuration = .15f;
    [SerializeField] float dashCooldown = .5f;
    [SerializeField] LayerMask countersLayerMask;
    [SerializeField] Transform holdableObjectPoint;

    bool isWalking;
    readonly RaycastHit[] _moveCastHits = new RaycastHit[MOVE_CAST_HIT_CAPACITY];
    Vector3 _targetFacingDirection;
    Vector3 dashDirection;
    float dashCooldownTimer;
    IHoldable _currentHoldable;
    Vector2 _moveInput;
    SelectorModule _selectorModule;
    CancellationTokenSource _soundCancellationTokenSource;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("There is more than one PlayerBehaviour instance");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        StopSoundLoop();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Initialize(KitchenGameContext context)
    {
        _selectorModule = context.GetModule<SelectorModule>();
        _targetFacingDirection = Forward;

        InitFsm();
        Fsm.ActivateState<PlayerIdleState>();
        StartSoundLoop();
    }

    public void Uninitialize()
    {
        StopSoundLoop();

        Fsm?.DestroyAllState();
        Fsm = null;

        _moveInput = Vector2.zero;
        isWalking = false;
        dashDirection = Vector3.zero;
        dashCooldownTimer = 0f;

        _selectorModule.ReleaseCounter();
        _selectorModule = null;
    }

    public void InteractAlternate()
    {
        var selectedCounter = SelectedCounter;
        if (selectedCounter != null)
        {
            selectedCounter.InteractAlternate(this);
        }
    }

    public void Interact()
    {
        var selectedCounter = SelectedCounter;
        if (selectedCounter != null)
        {
            selectedCounter.Interact(this);
        }
    }

    public void OnUpdate(float deltaTime, Vector2 moveInput, bool requestDash)
    {
        _moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        TickDashCooldown(deltaTime);
        if (requestDash)
        {
            RequestDash();
        }

        Fsm?.Update(deltaTime);
        UpdateFacing(deltaTime);
    }

    public void FocusCounter(BaseCounter counter)
    {
        if (counter == null)
        {
            return;
        }

        var interactDir = counter.transform.position - transform.position;
        interactDir.y = 0f;

        if (interactDir.sqrMagnitude > 0.0001f)
        {
            _targetFacingDirection = interactDir.normalized;
        }

        CompleteFacing();
    }

    public void CompleteFacing()
    {
        transform.rotation = Quaternion.LookRotation(_targetFacingDirection, Vector3.up);
    }

    void UpdateFacing(float deltaTime)
    {
        var targetRotation = Quaternion.LookRotation(_targetFacingDirection, Vector3.up);
        // Touch의 보간감을 유지하면서 프레임률과 무관하게 yaw 회전을 완료한다.
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-ROTATE_SPEED * deltaTime));
        if (Quaternion.Angle(transform.rotation, targetRotation) <= ROTATION_SNAP_ANGLE)
        {
            transform.rotation = targetRotation;
        }
    }

    public void SetWalking(bool isWalking)
    {
        this.isWalking = isWalking;
    }

    public void Stop()
    {
        isWalking = false;
        _moveInput = Vector2.zero;
        _targetFacingDirection = Forward;
        dashDirection = Vector3.zero;
        _selectorModule.ReleaseCounter();

        if (Fsm != null && !Fsm.IsActivateState<PlayerIdleState>())
        {
            Fsm.ActivateState<PlayerIdleState>();
        }
    }

    public void Move(Vector3 moveDir, float deltaTime)
    {
        Move(moveDir, moveSpeed, deltaTime);
    }

    public void Move(Vector3 moveDir, float speed, float deltaTime)
    {
        if (moveDir == Vector3.zero)
        {
            isWalking = false;
            return;
        }

        var requestedDirection = moveDir.normalized;
        var inputMagnitude = moveDir.magnitude;
        var moveDistance = speed * deltaTime;
        var canMove = CanMove(moveDir, moveDistance);

        if (!canMove)
        {
            Vector3 moveDirX = new(moveDir.x, 0, 0);
            moveDirX = moveDirX.normalized * inputMagnitude;
            if (Mathf.Abs(requestedDirection.x) > .5f)
            {
                canMove = CanMove(moveDirX, moveDistance);
            }
            else
            {
                canMove = false;
            }

            if (canMove)
            {
                moveDir = moveDirX;
            }
            else
            {
                Vector3 moveDirZ = new(0, 0, moveDir.z);
                moveDirZ = moveDirZ.normalized * inputMagnitude;
                if (Mathf.Abs(requestedDirection.z) > .5f)
                {
                    canMove = CanMove(moveDirZ, moveDistance);
                }
                else
                {
                    canMove = false;
                }

                if (canMove)
                {
                    moveDir = moveDirZ;
                }
            }
        }

        if (canMove)
        {
            transform.position += moveDir * moveDistance;
        }

        isWalking = canMove && moveDistance > 0f;
        // 막혀도 입력 방향으로 제자리 회전하며, 미끄러지면 실제 이동 방향을 따른다.
        _targetFacingDirection = canMove ? moveDir.normalized : requestedDirection;
    }

    bool CanMove(Vector3 moveDir, float moveDistance)
    {
        var moveDirMagnitude = moveDir.magnitude;
        if (moveDirMagnitude <= Mathf.Epsilon || moveDistance <= 0f)
        {
            return true;
        }

        var hitCount = Physics.CapsuleCastNonAlloc(
            transform.position,
            transform.position + Vector3.up * PLAYER_HEIGHT,
            PLAYER_RADIUS,
            moveDir / moveDirMagnitude,
            _moveCastHits,
            moveDistance * moveDirMagnitude,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        for (var i = 0; i < hitCount; i++)
        {
            if (IsMoveBlockingHit(_moveCastHits[i]))
            {
                return false;
            }
        }

        return true;
    }

    bool IsMoveBlockingHit(RaycastHit hit)
    {
        var hitCollider = hit.collider;
        if (hitCollider == null)
        {
            return false;
        }

        if (hitCollider.name == FLOOR_COLLIDER_NAME || hitCollider.transform.IsChildOf(transform))
        {
            return false;
        }

        return hitCollider.GetComponentInParent<PlateObject>() == null &&
               hitCollider.GetComponentInParent<IngredientObject>() == null &&
               hitCollider.GetComponentInParent<ToolObject>() == null;
    }

    public void RequestDash()
    {
        if (!CanDash)
        {
            return;
        }

        var moveDir = MoveDirection;
        if (moveDir == Vector3.zero)
        {
            moveDir = Forward;
        }

        dashDirection = moveDir.normalized;
        dashCooldownTimer = dashCooldown;
        Fsm.ActivateState<PlayerDashState>(true);
    }

    public void RefreshInteractionTarget()
    {
        _selectorModule.RefreshCounter(transform.position, Forward, INTERACT_DISTANCE, countersLayerMask);
    }

    void InitFsm()
    {
        Fsm?.DestroyAllState();

        Fsm = new(this);
        Fsm.AddState<PlayerIdleState>();
        Fsm.AddState<PlayerMoveState>();
        Fsm.AddState<PlayerDashState>();
    }

    void TickDashCooldown(float deltaTime)
    {
        if (dashCooldownTimer <= 0f)
        {
            return;
        }

        dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - deltaTime);
    }

    void StartSoundLoop()
    {
        StopSoundLoop();

        _soundCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        PlayFootstepSoundLoopAsync(_soundCancellationTokenSource.Token).Forget(Debug.LogException);
    }

    void StopSoundLoop()
    {
        if (_soundCancellationTokenSource == null)
        {
            return;
        }

        _soundCancellationTokenSource.Cancel();
        _soundCancellationTokenSource.Dispose();
        _soundCancellationTokenSource = null;
    }

    async UniTask PlayFootstepSoundLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (isWalking)
                {
                    SoundManager.Instance.PlaySound(SoundKeys.FOOTSTEP, transform.position);
                }

                await UniTask.Delay(
                    FOOTSTEP_SOUND_INTERVAL_MILLISECONDS,
                    cancellationToken: cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
    }

    public Transform HoldPoint => holdableObjectPoint;

    public IHoldable CurrentHoldable => _currentHoldable;

    public bool HasHoldable => _currentHoldable != null;

    public bool CanAccept(IHoldable holdable)
    {
        return !HasHoldable && HoldPoint != null;
    }

    public void Attach(IHoldable holdableObject)
    {
        _currentHoldable = holdableObject;

        if (holdableObject != null)
        {
            SoundManager.Instance.PlaySound(SoundKeys.OBJECT_PICKUP, transform.position);
        }
    }

    public void Detach(IHoldable holdableObject)
    {
        if (!ReferenceEquals(_currentHoldable, holdableObject))
        {
            return;
        }

        _currentHoldable = null;
    }
}
