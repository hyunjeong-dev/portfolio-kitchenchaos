using UnityEngine;

public sealed class SelectorModule : KitchenGameModule
{
    public SelectorModuleEvents Events { get; } = new();
    public BaseCounter CurrentCounter { get; private set; }

    public void RefreshCounter(Vector3 position, Vector3 forward, float distance, LayerMask layerMask)
    {
        BaseCounter counter = null;
        if (Physics.Raycast(position, forward, out var hit, distance, layerMask, QueryTriggerInteraction.Ignore))
        {
            counter = hit.collider.GetComponentInParent<BaseCounter>();
        }

        SelectCounter(counter);
    }

    public void SelectCounter(BaseCounter counter)
    {
        if (counter == null)
        {
            ReleaseCounter();
            return;
        }

        if (CurrentCounter == counter)
        {
            return;
        }

        CurrentCounter = counter;
        Events.InvokeSelectedCounterChanged(CurrentCounter);
    }

    public void ReleaseCounter()
    {
        if (CurrentCounter == null)
        {
            return;
        }

        CurrentCounter = null;
        Events.InvokeSelectedCounterChanged(null);
    }

    public void ReleaseCounter(BaseCounter counter)
    {
        if (CurrentCounter != counter)
        {
            return;
        }

        ReleaseCounter();
    }

    public override void OnEnd()
    {
        ReleaseCounter();

        base.OnEnd();
    }

    public override void OnUnregister()
    {
        CurrentCounter = null;

        base.OnUnregister();
    }
}
