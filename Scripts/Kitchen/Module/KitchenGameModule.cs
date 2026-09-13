using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class KitchenGameModule
{
    protected KitchenGameContext Context { get; private set; }
    public bool IsReady { get; private set; }
    public virtual bool CanUpdate => true;

    public void SetOwner(KitchenGameContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    protected TModule GetModule<TModule>() where TModule : KitchenGameModule
    {
        return Context.GetModule<TModule>();
    }

    public virtual void OnRegister() { }

    public virtual UniTask PrepareAsync(CancellationToken cancellationToken)
    {
        return UniTask.CompletedTask;
    }

    public virtual void OnBegin()
    {
        IsReady = true;
    }

    public virtual void OnPostProcess() { }

    public virtual void OnUpdate() { }

    public virtual void OnEnd()
    {
        IsReady = false;
    }

    public virtual void OnUnregister() { }

    public void InjectModuleRefsForContext()
    {
        if (Context == null)
        {
            return;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var fields = GetType().GetFields(flags);

        for (var i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            if (!Attribute.IsDefined(field, typeof(ModuleRefAttribute)))
            {
                continue;
            }

            if (!typeof(KitchenGameModule).IsAssignableFrom(field.FieldType))
            {
                Debug.LogError(
                    $"[KitchenGameContext] [ModuleRef] field must inherit KitchenGameModule. " +
                    $"Target: {GetType().Name}.{field.Name}, Type: {field.FieldType.Name}");
                continue;
            }

            var module = Context.GetModule(field.FieldType);
            if (module == null)
            {
                Debug.LogError(
                    $"[KitchenGameContext] [ModuleRef] injection failed. " +
                    $"Target: {GetType().Name}.{field.Name}, Type: {field.FieldType.Name}");
                continue;
            }

            field.SetValue(this, module);
        }
    }
}
