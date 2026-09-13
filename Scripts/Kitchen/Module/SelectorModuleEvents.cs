using System;

public sealed class SelectorModuleEvents
{
    public event Action<BaseCounter> OnSelectedCounterChanged;

    public void InvokeSelectedCounterChanged(BaseCounter selectedCounter)
    {
        OnSelectedCounterChanged?.Invoke(selectedCounter);
    }
}
