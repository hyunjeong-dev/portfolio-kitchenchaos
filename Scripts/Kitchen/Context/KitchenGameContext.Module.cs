using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed partial class KitchenGameContext
{
    readonly List<KitchenGameModule> _modules = new();
    readonly Dictionary<Type, KitchenGameModule> _moduleTable = new();

    void PreBuildModules()
    {
        AddModule<KitchenTimerModule>();
        AddModule<KitchenPauseModule>();
        AddModule<KitchenDeliveryModule>();
        AddModule<KitchenPlatformProgressionModule>();
        AddModule<KitchenResourceModule>();
        AddModule<KitchenPlateModule>();
        AddModule<IngredientPoolModule>();
        AddModule<KitchenCounterModule>();
        AddModule<KitchenInputModule>();
        AddModule<SelectorModule>();
        AddModule<KitchenTouchNavigationModule>();
        AddModule<KitchenPlayerModule>();
        AddModule<KitchenCameraModule>();
        AddModule<KitchenUIModule>();

        InjectModuleRefsForContext();

        for (var i = 0; i < _modules.Count; i++)
        {
            _modules[i].OnRegister();
        }
    }

    void BeginModules()
    {
        for (var i = 0; i < _modules.Count; i++)
        {
            _modules[i].OnBegin();
        }
    }

    void PostProcessModules()
    {
        for (var i = 0; i < _modules.Count; i++)
        {
            _modules[i].OnPostProcess();
        }
    }

    async UniTask PrepareModulesAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < _modules.Count; i++)
        {
            await _modules[i].PrepareAsync(cancellationToken);
        }
    }

    void ResetModules()
    {
        for (var i = _modules.Count - 1; i >= 0; i--)
        {
            try
            {
                _modules[i].OnEnd();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        for (var i = _modules.Count - 1; i >= 0; i--)
        {
            try
            {
                _modules[i].OnUnregister();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        _modules.Clear();
        _moduleTable.Clear();
    }

    void AddModule<T>() where T : KitchenGameModule, new()
    {
        var type = typeof(T);
        if (_moduleTable.ContainsKey(type))
        {
            Debug.LogError($"[KitchenGameContext] Module already registered. Type: {type.Name}");
            return;
        }

        var module = new T();
        module.SetOwner(this);
        _moduleTable.Add(type, module);
        _modules.Add(module);
    }

    public TModule GetModule<TModule>() where TModule : KitchenGameModule
    {
        return _moduleTable.TryGetValue(typeof(TModule), out var module) ? module as TModule : null;
    }

    public KitchenGameModule GetModule(Type type)
    {
        if (_moduleTable.TryGetValue(type, out var module))
        {
            return module;
        }

        for (var i = 0; i < _modules.Count; i++)
        {
            var target = _modules[i];
            if (type.IsInstanceOfType(target))
            {
                return target;
            }
        }

        return null;
    }

    void InjectModuleRefsForContext()
    {
        for (var i = 0; i < _modules.Count; i++)
        {
            _modules[i].InjectModuleRefsForContext();
        }
    }
}