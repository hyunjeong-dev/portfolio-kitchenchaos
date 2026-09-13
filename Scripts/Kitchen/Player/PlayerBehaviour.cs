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
    const int MOVE_CAST_HIT_CAPACITY = 16;
    const int FOOTSTEP_SOUND_INTERVAL_MILLISECONDS = 100;
    const string FLOOR_COLLIDER_NAME = "Floor";

    public static PlayerBehaviour Instance { get; private set; }

    public BehaviourFsm<PlayerBehaviour> Fsm { get; private set; }
    public float MoveSpeed => moveSpeed;
    public float DashSpeed => dashSpeed;
    public float DashDuration => dashDuration;
    public Vector3 DashDirection => dashDirection;
    public bool HasMoveInput => GetMoveInput() != Vector2.zero;
    public bool CanDash => Fsm != null && dashCooldownTimer <= 0f && !Fsm.IsActivateState<PlayerDashState>();
    public bool IsWalking => isWalking;
    public BaseCounter SelectedCounter => _selectorModule.CurrentCounter;

    [SerializeField] float moveSpeed = 7f;
    [SerializeField] float dashSpeed = 18f;
    [SerializeField] float dashDuration = .15f;
    [SerializeField] float dashCooldown = .5f;
    [SerializeField] LayerMask countersLayerMask;
    [SerializeField] Transform holdableObjectPoint;

    bool isWalking;
    readonly RaycastHit[] _moveCastHits = new RaycastHit[MOVE_CAST_HIT_CAPACITY];
    Vector3 lastInteractDir;
    Vector3 dashDirection;
    float dashCooldownTimer;
    IHoldable _currentHoldable;
    Func<Vector2> _moveInputResolver;
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

    public void Initialize(Func<Vector2> moveInputResolver, KitchenGameContext context)
    {
        _moveInputResolver = moveInputResolver;
        _selectorModule = context.GetModule<SelectorModule>();
        lastInteractDir = transform.forward;

        InitFsm();
        Fsm.ActivateState<PlayerIdleState>();
        StartSoundLoop();
    }

    public void Uninitialize()
    {
        StopSoundLoop();

        Fsm?.DestroyAllState();
        Fsm = null;

        _moveInputResolver = null;
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

    public void OnUpdate(float deltaTime)
    {
        TickDashCooldown(deltaTime);
        Fsm?.Update(deltaTime);
        RefreshInteractionTarget();
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
            lastInteractDir = interactDir.normalized;
            transform.forward = lastInteractDir;
        }

        SetSelectedCounter(counter);
    }

    public Vector3 GetMoveDirection()
    {
        Vector2 inputVector = GetMoveInput();
        return new(inputVector.x, 0f, inputVector.y);
    }

    public void SetWalking(bool isWalking)
    {
        this.isWalking = isWalking;
    }

    public void Stop()
    {
        isWalking = false;
        dashDirection = Vector3.zero;
        SetSelectedCounter(null);

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

        var moveDistance = speed * deltaTime;
        var canMove = CanMove(moveDir, moveDistance);

        if (!canMove)
        {
            Vector3 moveDirX = new(moveDir.x, 0, 0);
            moveDirX.Normalize();
            if (moveDir.x < -.5f || moveDir.x > +.5f)
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
                moveDirZ.Normalize();
                if (moveDir.z < -.5f || moveDir.z > +.5f)
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

        isWalking = canMove && moveDir != Vector3.zero;

        if (isWalking)
        {
            transform.forward = Vector3.Slerp(transform.forward, moveDir, deltaTime * ROTATE_SPEED);
        }
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

        var moveDir = GetMoveDirection();
        if (moveDir == Vector3.zero)
        {
            moveDir = lastInteractDir;
        }

        if (moveDir == Vector3.zero)
        {
            moveDir = transform.forward;
        }

        dashDirection = moveDir.normalized;
        dashCooldownTimer = dashCooldown;
        Fsm.ActivateState<PlayerDashState>(true);
    }

    void RefreshInteractionTarget()
    {
        Vector3 moveDir = GetMoveDirection();

        if (moveDir != Vector3.zero)
        {
            lastInteractDir = moveDir;
        }

        if (Physics.Raycast(transform.position, lastInteractDir, out RaycastHit raycastHit, INTERACT_DISTANCE, countersLayerMask))
        {
            if (raycastHit.transform.TryGetComponent(out BaseCounter baseCounter))
            {
                if (baseCounter != SelectedCounter)
                {
                    SetSelectedCounter(baseCounter);
                }
            }
            else
            {
                SetSelectedCounter(null);
            }
        }
        else
        {
            SetSelectedCounter(null);
        }
    }

    Vector2 GetMoveInput()
    {
        return _moveInputResolver != null ? _moveInputResolver.Invoke() : Vector2.zero;
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

    void SetSelectedCounter(BaseCounter selectedCounter)
    {
        _selectorModule.SelectCounter(selectedCounter);
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
