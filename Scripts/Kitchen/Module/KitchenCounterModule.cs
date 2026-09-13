using System.Collections.Generic;
using UnityEngine;

public sealed class KitchenCounterModule : KitchenGameModule
{
    [ModuleRef] KitchenResourceModule _resourceModule;

    BaseCounter[] _counters;
    BurnerCounter[] _burnerCounters;
    readonly List<ToolObject> _runtimeToolObjects = new();

    public override void OnRegister()
    {
        base.OnRegister();

        CacheSceneObjects();
    }

    public override void OnBegin()
    {
        base.OnBegin();

        InitializeCounters();
        CreateInitialBurnerTools();
    }

    public override void OnEnd()
    {
        ReleaseRuntimeToolObjects();
        UninitializeCounters();

        base.OnEnd();
    }

    public override void OnUnregister()
    {
        _counters = null;
        _burnerCounters = null;
        _runtimeToolObjects.Clear();

        base.OnUnregister();
    }

    void CacheSceneObjects()
    {
        _counters = Context.GetComponentsInChildren<BaseCounter>(true);
        _burnerCounters = Context.GetComponentsInChildren<BurnerCounter>(true);
    }

    void InitializeCounters()
    {
        if (_counters == null)
        {
            return;
        }

        foreach (var counter in _counters)
        {
            counter?.Initialize(Context);
        }
    }

    void UninitializeCounters()
    {
        if (_counters == null)
        {
            return;
        }

        foreach (var counter in _counters)
        {
            counter?.Uninitialize();
        }
    }

    void CreateInitialBurnerTools()
    {
        if (_burnerCounters == null || _resourceModule == null)
        {
            return;
        }

        for (var i = 0; i < _burnerCounters.Length; i++)
        {
            CreateInitialBurnerTool(_burnerCounters[i]);
        }
    }

    void CreateInitialBurnerTool(BurnerCounter burnerCounter)
    {
        if (burnerCounter == null || burnerCounter.HasHoldable)
        {
            return;
        }

        var prefab = _resourceModule.LoadToolPrefab(burnerCounter.ActionType);
        if (prefab == null)
        {
            Debug.LogWarning($"[KitchenCounterModule] Tool prefab is not found. actionType: {burnerCounter.ActionType}");
            return;
        }

        var instance = UnityEngine.Object.Instantiate(prefab);
        if (!instance.TryGetComponent<ToolObject>(out var toolObject))
        {
            Debug.LogError($"[KitchenCounterModule] Tool prefab has no ToolObject component. prefab: {prefab.name}");
            UnityEngine.Object.Destroy(instance);
            return;
        }

        if (!burnerCounter.TryPlaceToolObject(toolObject))
        {
            UnityEngine.Object.Destroy(instance);
            return;
        }

        _runtimeToolObjects.Add(toolObject);
    }

    void ReleaseRuntimeToolObjects()
    {
        for (var i = 0; i < _runtimeToolObjects.Count; i++)
        {
            var toolObject = _runtimeToolObjects[i];
            if (toolObject == null)
            {
                continue;
            }

            toolObject.Release();
            UnityEngine.Object.Destroy(toolObject.gameObject);
        }

        _runtimeToolObjects.Clear();
    }

}
