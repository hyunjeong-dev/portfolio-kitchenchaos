using UnityEngine;

public readonly struct KitchenTouchCommand
{
    public readonly Vector2 ScreenPosition;

    public KitchenTouchCommand(Vector2 screenPosition)
    {
        ScreenPosition = screenPosition;
    }
}
