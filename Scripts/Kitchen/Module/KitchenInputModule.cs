using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class KitchenInputModule : KitchenGameModule
{
    public KitchenInputModuleEvents Events { get; } = new();
    public Vector2 MoveInput { get; private set; }

    int _lastPauseFrame = -1;

    public enum Binding
    {
        MoveUp,
        MoveDown,
        MoveLeft,
        MoveRight,
        Interact,
        InteractAlternate,
        Pause,
        GamepadInteract,
        GamepadInteractAlternate,
        GamepadPause
    }

    public override void OnBegin()
    {
        base.OnBegin();
        InputSystem.onDeviceChange += OnDeviceChanged;
        Application.focusChanged += OnFocusChanged;
        Context.OnGamePaused += ResetInput;
        Context.OnStateChanged += OnStateChanged;
    }

    public override void OnEnd()
    {
        InputSystem.onDeviceChange -= OnDeviceChanged;
        Application.focusChanged -= OnFocusChanged;
        Context.OnGamePaused -= ResetInput;
        Context.OnStateChanged -= OnStateChanged;
        ResetInput();
        base.OnEnd();
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        if (!Application.isFocused)
        {
            MoveInput = Vector2.zero;
            return;
        }

        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        var input = KitchenInputLogic.GetKeyboardMoveInput(keyboard);
        MoveInput = input != Vector2.zero ? input : KitchenInputLogic.GetGamepadMoveInput(gamepad);

        if (KitchenInputLogic.IsInteractPressed(keyboard, gamepad))
        {
            InvokeInteract();
        }

        if (KitchenInputLogic.IsInteractAlternatePressed(keyboard, gamepad))
        {
            InvokeInteractAlternate();
        }

        if (KitchenInputLogic.IsDashPressed(keyboard, gamepad))
        {
            InvokeDash();
        }

        if (KitchenInputLogic.IsPausePressed(keyboard, gamepad))
        {
            InvokePauseOncePerFrame();
        }

        if (Context.IsPlayable)
        {
            UpdateTouchInput();
        }
        else
        {
            ResetTouchPress();
        }
    }

    void OnDeviceChanged(InputDevice device, InputDeviceChange change)
    {
        if (device is Keyboard or Gamepad or Mouse or Touchscreen &&
            change is InputDeviceChange.Removed or InputDeviceChange.Disconnected or InputDeviceChange.Disabled)
        {
            ResetInput();
        }
    }

    void OnFocusChanged(bool hasFocus)
    {
        if (!hasFocus)
        {
            ResetInput();
        }
    }

    void OnStateChanged(KitchenGameStateChangedEvent stateChangedEvent)
    {
        if (!Context.IsPlayable)
        {
            ResetInput();
        }
    }

    void ResetInput()
    {
        MoveInput = Vector2.zero;
        ResetTouchPress();
        Events.InvokeInputReset();
    }

    public string GetBindingText(Binding binding)
    {
        switch (binding)
        {
            default:
            case Binding.MoveUp:
                return "W";
            case Binding.MoveDown:
                return "S";
            case Binding.MoveLeft:
                return "A";
            case Binding.MoveRight:
                return "D";
            case Binding.Interact:
                return "Space";
            case Binding.InteractAlternate:
                return "Ctrl";
            case Binding.Pause:
                return "Esc";
            case Binding.GamepadInteract:
                return "A";
            case Binding.GamepadInteractAlternate:
                return "X";
            case Binding.GamepadPause:
                return "Start";
        }
    }

    void InvokeInteract()
    {
        Events.InvokeInteract();
    }

    void InvokeInteractAlternate()
    {
        Events.InvokeInteractAlternate();
    }

    void InvokeDash()
    {
        Events.InvokeDash();
    }

    void InvokePauseOncePerFrame()
    {
        if (_lastPauseFrame == Time.frameCount)
        {
            return;
        }

        _lastPauseFrame = Time.frameCount;
        Events.InvokePause();
    }
}
