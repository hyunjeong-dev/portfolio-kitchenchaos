using System;

public sealed class KitchenInputModuleEvents
{
    public event Action OnInteract;
    public event Action OnInteractAlternate;
    public event Action OnDash;
    public event Action OnPause;
    public event Action<KitchenTouchCommand> OnTouchCommand;

    public void InvokeInteract()
    {
        OnInteract?.Invoke();
    }

    public void InvokeInteractAlternate()
    {
        OnInteractAlternate?.Invoke();
    }

    public void InvokeDash()
    {
        OnDash?.Invoke();
    }

    public void InvokePause()
    {
        OnPause?.Invoke();
    }

    public void InvokeTouchCommand(KitchenTouchCommand command)
    {
        OnTouchCommand?.Invoke(command);
    }
}
