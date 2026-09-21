using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed partial class KitchenInputModule
{
    const float TOUCH_MAX_TAP_MOVE_DISTANCE = 24f;
    const int INVALID_TOUCH_POINTER_ID = -1;

    readonly List<RaycastResult> _uiRaycastResults = new();
    EventSystem _eventSystem;
    PointerEventData _pointerEventData;
    bool _isTouchPressed;
    int _touchPointerId = INVALID_TOUCH_POINTER_ID;
    Vector2 _touchPressScreenPosition;

    void UpdateTouchInput()
    {
        var touchScreen = Touchscreen.current;
        if (touchScreen != null)
        {
            var touch = touchScreen.primaryTouch;
            var screenPosition = touch.position.ReadValue();
            var pointerId = touch.touchId.ReadValue();

            if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                ResetTouchPress();
                return;
            }

            if (touch.press.wasPressedThisFrame)
            {
                BeginTouchPress(screenPosition, pointerId);
            }

            if (touch.press.wasReleasedThisFrame)
            {
                EndTouchPress(screenPosition, pointerId);
                return;
            }

            if (touch.press.isPressed || touch.press.wasPressedThisFrame)
            {
                return;
            }
        }

        if (_isTouchPressed && _touchPointerId >= 0)
        {
            ResetTouchPress();
        }

        var mouse = Mouse.current;
        if (mouse == null)
        {
            ResetTouchPress();
            return;
        }

        var mousePosition = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame)
        {
            BeginTouchPress(mousePosition, -1);
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            EndTouchPress(mousePosition, -1);
        }
    }

    void BeginTouchPress(Vector2 screenPosition, int pointerId)
    {
        if (IsPointerOverUI(screenPosition, pointerId))
        {
            ResetTouchPress();
            return;
        }

        BeginTouchState(screenPosition, pointerId);
    }

    void EndTouchPress(Vector2 screenPosition, int pointerId)
    {
        if (!IsMatchingTouchPointer(pointerId) || IsPointerOverUI(screenPosition, pointerId))
        {
            ResetTouchPress();
            return;
        }

        var moveDistance = Vector2.Distance(_touchPressScreenPosition, screenPosition);
        ResetTouchPress();

        if (moveDistance > TOUCH_MAX_TAP_MOVE_DISTANCE)
        {
            return;
        }

        Events.InvokeTouchCommand(new KitchenTouchCommand(screenPosition));
    }

    void BeginTouchState(Vector2 screenPosition, int pointerId)
    {
        _isTouchPressed = true;
        _touchPointerId = pointerId;
        _touchPressScreenPosition = screenPosition;
    }

    bool IsMatchingTouchPointer(int pointerId)
    {
        return _isTouchPressed && _touchPointerId == pointerId;
    }

    bool IsPointerOverUI(Vector2 screenPosition, int pointerId)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        if (pointerId >= 0 && eventSystem.IsPointerOverGameObject(pointerId))
        {
            return true;
        }

        if (pointerId < 0 && eventSystem.IsPointerOverGameObject())
        {
            return true;
        }

        EnsurePointerEventData(eventSystem);
        _pointerEventData.Reset();
        _pointerEventData.position = screenPosition;

        _uiRaycastResults.Clear();
        eventSystem.RaycastAll(_pointerEventData, _uiRaycastResults);
        return _uiRaycastResults.Count > 0;
    }

    void EnsurePointerEventData(EventSystem eventSystem)
    {
        if (_pointerEventData != null && _eventSystem == eventSystem)
        {
            return;
        }

        _eventSystem = eventSystem;
        _pointerEventData = new PointerEventData(eventSystem);
    }

    void ResetTouchPress()
    {
        _isTouchPressed = false;
        _touchPointerId = INVALID_TOUCH_POINTER_ID;
        _touchPressScreenPosition = Vector2.zero;
    }
}
