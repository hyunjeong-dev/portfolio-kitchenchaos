using UnityEngine;

public sealed class KitchenPlayerModule : KitchenGameModule
{
    const string PLAYER_ROOT_NAME = "PlayerRoot";

    [ModuleRef] KitchenInputModule _inputModule;
    [ModuleRef] SelectorModule _selectorModule;
    [ModuleRef] KitchenTouchNavigationModule _touchNavigationModule;

    Transform _playerRoot;
    PlayerBehaviour _player;

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

        Context.OnStateChanged += OnStateChanged;
        Context.OnGamePaused += OnGamePaused;

        if (_player != null)
        {
            _player.Initialize(GetMoveInputNormalized, Context);
        }

        _inputModule.Events.OnInteract += OnInputInteract;
        _inputModule.Events.OnInteractAlternate += OnInputInteractAlternate;
        _inputModule.Events.OnDash += OnInputDash;
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        _player?.OnUpdate(Time.deltaTime);
        ExecuteTouchActionIfReady();
    }

    public override void OnEnd()
    {
        Context.OnStateChanged -= OnStateChanged;
        Context.OnGamePaused -= OnGamePaused;

        _inputModule.Events.OnInteract -= OnInputInteract;
        _inputModule.Events.OnInteractAlternate -= OnInputInteractAlternate;
        _inputModule.Events.OnDash -= OnInputDash;

        _touchNavigationModule.Cancel();
        _player?.Stop();
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

        _touchNavigationModule.Cancel();
        _player?.Interact();
    }

    void OnInputInteractAlternate()
    {
        if (!Context.IsPlayable)
        {
            return;
        }

        _touchNavigationModule.Cancel();
        _player?.InteractAlternate();
    }

    void OnInputDash()
    {
        if (!Context.IsPlayable)
        {
            return;
        }

        _touchNavigationModule.Cancel();
        _player?.RequestDash();
    }

    void OnStateChanged(KitchenGameStateChangedEvent stateChangedEvent)
    {
        if (!Context.IsPlayable)
        {
            _touchNavigationModule.Cancel();
            _player?.Stop();
        }
    }

    void OnGamePaused()
    {
        _touchNavigationModule.Cancel();
        _player?.Stop();
    }

    Vector2 GetMoveInputNormalized()
    {
        var manualInput = _inputModule.GetMoveInputNormalized();
        if (manualInput != Vector2.zero)
        {
            _touchNavigationModule.Cancel();
            return manualInput;
        }

        return _player != null
            ? _touchNavigationModule.GetMoveInput(
                _player.transform.position,
                _player.MoveSpeed * Time.deltaTime)
            : Vector2.zero;
    }

    void ExecuteTouchActionIfReady()
    {
        if (_player == null)
        {
            return;
        }

        if (!_touchNavigationModule.TryConsumeAction(
                _player.transform.position,
                out var counter,
                out var actionType))
        {
            return;
        }

        _player.FocusCounter(counter);
        if (actionType == KitchenTouchActionType.InteractAlternate)
        {
            _player.InteractAlternate();
            return;
        }

        _player.Interact();
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
