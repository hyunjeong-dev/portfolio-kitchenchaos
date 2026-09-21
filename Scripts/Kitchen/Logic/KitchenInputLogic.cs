using UnityEngine;
using UnityEngine.InputSystem;

public static class KitchenInputLogic
{
    public static Vector2 GetKeyboardMoveInput(Keyboard keyboard)
    {
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        var inputVector = Vector2.zero;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            inputVector.y += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            inputVector.y -= 1f;
        }

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            inputVector.x -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            inputVector.x += 1f;
        }

        return inputVector;
    }

    public static Vector2 GetGamepadMoveInput(Gamepad gamepad)
    {
        if (gamepad == null)
        {
            return Vector2.zero;
        }

        // Input System의 StickDeadzone 처리 결과와 아날로그 크기를 그대로 사용한다.
        return gamepad.leftStick.ReadValue();
    }

    public static bool IsInteractPressed(Keyboard keyboard, Gamepad gamepad)
    {
        return (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
               || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
    }

    public static bool IsInteractAlternatePressed(Keyboard keyboard, Gamepad gamepad)
    {
        return (keyboard != null && (keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.rightCtrlKey.wasPressedThisFrame))
               || (gamepad != null && gamepad.buttonWest.wasPressedThisFrame);
    }

    public static bool IsDashPressed(Keyboard keyboard, Gamepad gamepad)
    {
        return (keyboard != null && (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame))
               || (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame);
    }

    public static bool IsPausePressed(Keyboard keyboard, Gamepad gamepad)
    {
        return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
               || (gamepad != null && gamepad.startButton.wasPressedThisFrame);
    }

    public static KitchenTouchActionType ResolveTouchActionType(BaseCounter counter)
    {
        if (counter is CuttingCounter cuttingCounter)
        {
            if (cuttingCounter.IsCutting)
            {
                return KitchenTouchActionType.Interact;
            }

            return cuttingCounter.CanProgressCutting
                ? KitchenTouchActionType.InteractAlternate
                : KitchenTouchActionType.Interact;
        }

        return KitchenTouchActionType.Interact;
    }
}
