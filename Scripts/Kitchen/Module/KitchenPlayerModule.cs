using System;
using UnityEngine;

public sealed class KitchenPlayerModule : KitchenGameModule
{
    const string PLAYER_ROOT_NAME = "PlayerRoot";

    [Flags]
    enum PlayerAction
    {
        None = 0,
        Interact = 1,
        InteractAlternate = 2,
        Dash = 4
    }

    [ModuleRef] KitchenInputModule _inputModule;
    [ModuleRef] KitchenTouchNavigationModule _touchNavigationModule;

    Transform _playerRoot;
    PlayerBehaviour _player;
    PlayerAction _pendingActions;

    public Transform PlayerTransform => _player != null ? _player.transform : null;

    public override bool CanUpdate => Context.IsPlayable;

    public override void OnRegister()
    {
        base.OnRegister();

        _playerRoot = ResolvePlayerRoot();
        _player = ResolvePlayer();
    }

    public override void OnBegin()
    {
        base.OnBegin();

        if (_player != null)
        {
            _player.Initialize(Context);
        }

        _inputModule.Events.OnInteract += OnInputInteract;
        _inputModule.Events.OnInteractAlternate += OnInputInteractAlternate;
        _inputModule.Events.OnDash += OnInputDash;
        _inputModule.Events.OnInputReset += ResetInput;
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        var actions = _pendingActions;
        _pendingActions = PlayerAction.None;
        if (_player == null)
        {
            return;
        }

        if (actions != PlayerAction.None)
        {
            _touchNavigationModule.Cancel();
        }

        var deltaTime = Time.deltaTime;
        var moveInput = ResolveMoveInput(deltaTime);
        _player.OnUpdate(deltaTime, moveInput, (actions & PlayerAction.Dash) != 0);

        BaseCounter touchCounter = null;
        if (_touchNavigationModule.TryConsumeAction(_player.transform.position, out var counter, out var actionType))
        {
            touchCounter = counter;
            _player.FocusCounter(counter);
            actions |= actionType == KitchenTouchActionType.InteractAlternate
                ? PlayerAction.InteractAlternate
                : PlayerAction.Interact;
        }

        if (touchCounter == null && (actions & (PlayerAction.Interact | PlayerAction.InteractAlternate)) != 0)
        {
            _player.CompleteFacing();
        }

        // 방향을 확정한 뒤 한 번만 선택하며, Touch도 같은 물리 탐색 결과를 사용한다.
        _player.RefreshInteractionTarget();
        if (touchCounter != null && _player.SelectedCounter != touchCounter)
        {
            return;
        }

        if ((actions & PlayerAction.Interact) != 0)
        {
            _player.Interact();
        }

        if ((actions & PlayerAction.InteractAlternate) != 0)
        {
            _player.InteractAlternate();
        }
    }

    public override void OnEnd()
    {
        _inputModule.Events.OnInteract -= OnInputInteract;
        _inputModule.Events.OnInteractAlternate -= OnInputInteractAlternate;
        _inputModule.Events.OnDash -= OnInputDash;
        _inputModule.Events.OnInputReset -= ResetInput;

        ResetInput();
        _player?.Uninitialize();

        base.OnEnd();
    }

    public override void OnUnregister()
    {
        _player = null;
        _playerRoot = null;

        base.OnUnregister();
    }

    void OnInputInteract()
    {
        if (!Context.IsPlayable)
        {
            return;
        }

        _pendingActions |= PlayerAction.Interact;
    }

    void OnInputInteractAlternate()
    {
        if (!Context.IsPlayable)
        {
            return;
        }

        _pendingActions |= PlayerAction.InteractAlternate;
    }

    void OnInputDash()
    {
        if (!Context.IsPlayable)
        {
            return;
        }

        _pendingActions |= PlayerAction.Dash;
    }

    void ResetInput()
    {
        _pendingActions = PlayerAction.None;
        _touchNavigationModule.Cancel();
        _player?.Stop();
    }

    Vector2 ResolveMoveInput(float deltaTime)
    {
        var manualInput = _inputModule.MoveInput;
        if (manualInput != Vector2.zero)
        {
            _touchNavigationModule.Cancel();
            return manualInput;
        }

        return _touchNavigationModule.GetMoveInput(
            _player.transform.position,
            _player.MoveSpeed * deltaTime);
    }

    Transform ResolvePlayerRoot()
    {
        if (Context.PlayerRoot != null)
        {
            return Context.PlayerRoot;
        }

        var playerRoot = Context.transform.Find(PLAYER_ROOT_NAME);
        if (playerRoot != null)
        {
            return playerRoot;
        }

        Debug.LogError("[KitchenPlayerModule] PlayerRoot is not assigned.");
        return null;
    }

    PlayerBehaviour ResolvePlayer()
    {
        var player = _playerRoot != null
            ? _playerRoot.GetComponentInChildren<PlayerBehaviour>(true)
            : Context.GetComponentInChildren<PlayerBehaviour>(true);
        if (player == null)
        {
            Debug.LogError("[KitchenPlayerModule] PlayerBehaviour is not found.");
        }

        return player;
    }
}
