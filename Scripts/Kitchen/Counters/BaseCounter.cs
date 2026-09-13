using System;
using UnityEngine;

public class BaseCounter : MonoBehaviour, IHolder
{
    [SerializeField] protected Transform counterTopPoint;
    [SerializeField] string _soundKey = SoundKeys.OBJECT_DROP;

    IHoldable _currentHoldable;

    protected KitchenGameContext Context { get; private set; }
    protected bool IsInitialized { get; private set; }

    public event Action<KitchenGameContext> OnInitialized;
    public event Action OnUninitialized;

    public void Initialize(KitchenGameContext context)
    {
        if (IsInitialized && Context == context)
        {
            return;
        }
        
        Uninitialize();

        Context = context;
        IsInitialized = true;
        OnInitialize(context);
        OnInitialized?.Invoke(context);
    }

    public void Uninitialize()
    {
        if (!IsInitialized)
        {
            Context = null;
            return;
        }

        OnUninitialized?.Invoke();
        OnUninitialize();
        Context = null;
        IsInitialized = false;
    }

    public bool TryGetContext(out KitchenGameContext context)
    {
        context = Context;
        return IsInitialized && context != null;
    }

    public virtual void Interact(PlayerBehaviour player)
    {
        Debug.LogError("BaseCounter.Interact();");
    }

    public virtual void InteractAlternate(PlayerBehaviour player)
    {
        //Debug.LogError("BaseCounter.InteractAlternate();");
    }

    public virtual Transform HoldPoint => counterTopPoint;

    public IHoldable CurrentHoldable => _currentHoldable;

    public bool HasHoldable => _currentHoldable != null;

    public virtual bool CanAccept(IHoldable holdable)
    {
        return !HasHoldable && counterTopPoint != null;
    }

    public virtual void Attach(IHoldable holdableObject)
    {
        _currentHoldable = holdableObject;

        if (holdableObject != null)
        {
            SoundManager.Instance.PlaySound(_soundKey, transform.position);
        }
    }

    public virtual void Detach(IHoldable holdableObject)
    {
        if (!ReferenceEquals(_currentHoldable, holdableObject))
        {
            return;
        }

        _currentHoldable = null;
    }

    protected virtual void OnInitialize(KitchenGameContext context)
    {
    }

    protected virtual void OnUninitialize()
    {
    }
}
