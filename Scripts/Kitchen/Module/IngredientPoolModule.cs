using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Generated;
using UnityEngine;
using UnityEngine.Pool;
using static Generated.GameData;

public sealed class IngredientPoolModule : KitchenGameModule
{
    readonly Dictionary<string, GameObject> _prefabByPath = new();
    readonly Dictionary<string, ObjectPool<IngredientObject>> _poolByPrefabPath = new();

    [ModuleRef] KitchenResourceModule _resourceModule;

    public override void OnRegister()
    {
        base.OnRegister();

        _prefabByPath.Clear();
        _poolByPrefabPath.Clear();
    }

    public override UniTask PrepareAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return UniTask.CompletedTask;
    }

    public IngredientObject GetIngredientObject(IngredientInfoData ingredientInfoData, IHolder holder)
    {
        if (ingredientInfoData == null)
        {
            return null;
        }

        return GetIngredientObject(
            ingredientInfoData,
            IngredientActionFlags.None,
            ingredientInfoData.PrefabPath,
            holder);
    }

    public IngredientObject GetIngredientActionResult(
        IngredientActionInfoData actionInfoData,
        IngredientActionFlags completedActions,
        IHolder holder)
    {
        if (actionInfoData == null || string.IsNullOrEmpty(actionInfoData.PrefabPath))
        {
            return null;
        }

        var ingredientInfoData = GameData.Instance.GetIngredientInfoData(actionInfoData.IngredientId);
        if (ingredientInfoData == null)
        {
            return null;
        }

        return GetIngredientObject(
            ingredientInfoData,
            completedActions,
            actionInfoData.PrefabPath,
            holder);
    }

    IngredientObject GetIngredientObject(
        IngredientInfoData ingredientInfoData,
        IngredientActionFlags completedActions,
        string prefabPath,
        IHolder holder)
    {
        var pool = GetOrCreatePool(ingredientInfoData, prefabPath);
        var ingredientObject = pool.Get();
        ingredientObject.Initialize(Context);
        ingredientObject.SetGameData(ingredientInfoData.IngredientId, completedActions, prefabPath);
        if (holder != null && !HoldableTransferLogic.TryMoveTo(ingredientObject, holder))
        {
            pool.Release(ingredientObject);
            return null;
        }

        return ingredientObject;
    }

    public IngredientObject ReplaceWithActionResult(
        IngredientObject ingredientObject,
        IngredientActionInfoData actionInfoData,
        IHolder holder)
    {
        if (ingredientObject == null || actionInfoData == null || holder == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(actionInfoData.PrefabPath) ||
            GameData.Instance.GetIngredientInfoData(actionInfoData.IngredientId) == null)
        {
            return null;
        }

        var completedActions = ingredientObject.CompletedActionFlags;
        ingredientObject.Release();
        return GetIngredientActionResult(actionInfoData, completedActions, holder);
    }

    public void ReleaseIngredientObject(IngredientObject ingredientObject)
    {
        var prefabPath = ingredientObject.CurrentPrefabPath;
        GetOrCreatePool(GameData.Instance.GetIngredientInfoData(ingredientObject.IngredientId), prefabPath)
            .Release(ingredientObject);
    }

    public override void OnEnd()
    {
        foreach (var pair in _poolByPrefabPath)
        {
            pair.Value.Clear();
        }

        _poolByPrefabPath.Clear();
        _prefabByPath.Clear();

        base.OnEnd();
    }

    ObjectPool<IngredientObject> GetOrCreatePool(IngredientInfoData ingredientInfoData, string prefabPath)
    {
        if (_poolByPrefabPath.TryGetValue(prefabPath, out var pool))
        {
            return pool;
        }

        pool = new(
            createFunc: () => CreateIngredientObject(ingredientInfoData, prefabPath),
            actionOnGet: OnGetIngredientObjectAction,
            actionOnRelease: OnReleaseIngredientObjectAction,
            actionOnDestroy: OnDestroyIngredientObjectAction,
            collectionCheck: false,
            defaultCapacity: 10,
            maxSize: 50);
        _poolByPrefabPath.Add(prefabPath, pool);

        return pool;
    }

    IngredientObject CreateIngredientObject(IngredientInfoData ingredientInfoData, string prefabPath)
    {
        var prefab = GetIngredientPrefab(ingredientInfoData, prefabPath);
        var ingredientObject = IngredientObject.CreateFromPrefab(prefab);
        return ingredientObject;
    }

    void OnGetIngredientObjectAction(IngredientObject ingredientObject)
    {
        ingredientObject.gameObject.SetActive(true);
    }

    void OnReleaseIngredientObjectAction(IngredientObject ingredientObject)
    {
        ingredientObject.ResetForPool(Context.transform);
    }

    void OnDestroyIngredientObjectAction(IngredientObject ingredientObject)
    {
        Object.Destroy(ingredientObject.gameObject);
    }

    GameObject GetIngredientPrefab(IngredientInfoData ingredientInfoData, string prefabPath)
    {
        if (_prefabByPath.TryGetValue(prefabPath, out var prefab))
        {
            return prefab;
        }

        prefab = _resourceModule.LoadIngredientPrefab(ingredientInfoData, prefabPath);
        _prefabByPath.Add(prefabPath, prefab);
        return prefab;
    }
}
