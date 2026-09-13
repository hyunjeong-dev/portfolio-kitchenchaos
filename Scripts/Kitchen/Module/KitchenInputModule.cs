using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class KitchenInputModule : KitchenGameModule
{
    public KitchenInputModuleEvents Events { get; } = new();

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

    public override void OnUpdate()
    {
        base.OnUpdate();

        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;

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

        UpdateTouchInput();
    }

    public Vector2 GetMoveInputNormalized()
    {
        var inputVector = KitchenInputLogic.GetKeyboardMoveInput(Keyboard.current);
        if (inputVector != Vector2.zero)
        {
            return inputVector.normalized;
        }

        inputVector = KitchenInputLogic.GetGamepadMoveInput(Gamepad.current);
        if (inputVector != Vector2.zero)
        {
            return inputVector.normalized;
        }

        return Vector2.zero;
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
