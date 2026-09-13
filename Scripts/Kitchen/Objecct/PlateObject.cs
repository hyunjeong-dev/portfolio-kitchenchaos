using System;
using System.Collections.Generic;
using UnityEngine;

public class PlateObject : MonoBehaviour, IHoldable
{
    const float DEFAULT_VISUAL_HEIGHT_OFFSET = 0.02f;

    public event Action<int> OnIngredientAdded;

    [SerializeField] Transform plateVisualRoot;
    [SerializeField] float visualHeightOffset = DEFAULT_VISUAL_HEIGHT_OFFSET;

    readonly List<int> _ingredientIds = new();
    IHolder _holder;
    KitchenPlateModule _plateModule;
    UIPlateIconList _plateIcons;
    GameObject _runtimePlateVisualObject;
    GameObject _runtimePlateVisualPrefab;

    public Transform Transform => transform;
    public IHolder Holder => _holder;

    public IReadOnlyList<int> IngredientIds => _ingredientIds;
    public KitchenPlateCombinationKey CombinationKey => new(_ingredientIds);

    void Awake()
    {
        _plateIcons = GetComponentInChildren<UIPlateIconList>(true);
    }

    public void Initialize(KitchenGameContext context)
    {
        _plateModule = context.GetModule<KitchenPlateModule>();
        _plateIcons?.Initialize(context);
    }

    public void SetHolder(IHolder holder)
    {
        _holder = holder;
    }

    public void Release()
    {
        _holder?.Detach(this);
        _holder = null;

        Destroy(gameObject);
    }

    public bool TryAddIngredient(IngredientObject ingredientObject)
    {
        if (ingredientObject == null)
        {
            return false;
        }

        if (_plateModule != null)
        {
            return _plateModule.TryAddIngredient(this, ingredientObject);
        }

        return false;
    }

    public void ApplyPlateVisual(GameObject visualPrefab)
    {
        if (visualPrefab == null)
        {
            return;
        }

        if (_runtimePlateVisualPrefab == visualPrefab)
        {
            return;
        }

        ClearPlateVisual();

        _runtimePlateVisualObject = Instantiate(visualPrefab, GetPlateVisualRoot(), false);
        _runtimePlateVisualObject.name = visualPrefab.name;
        _runtimePlateVisualObject.transform.localPosition += new Vector3(0f, visualHeightOffset, 0f);
        _runtimePlateVisualPrefab = visualPrefab;
    }

    public bool AddIngredient(int ingredientId)
    {
        if (ingredientId == 0 || _ingredientIds.Contains(ingredientId))
        {
            return false;
        }

        _ingredientIds.Add(ingredientId);

        OnIngredientAdded?.Invoke(ingredientId);

        return true;
    }

    public bool ContainsIngredient(int ingredientId)
    {
        for (var i = 0; i < _ingredientIds.Count; i++)
        {
            if (_ingredientIds[i] == ingredientId)
            {
                return true;
            }
        }

        return false;
    }

    public static void DisableVisualGameplayComponents(GameObject visualGameObject)
    {
        if (visualGameObject == null)
        {
            return;
        }

        if (visualGameObject.TryGetComponent<PlateObject>(out var plateObject))
        {
            plateObject.enabled = false;
        }

        var colliders = visualGameObject.GetComponentsInChildren<Collider>(true);
        for (var i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    [InspectorButton("Clear Runtime Ingredients")]
    void ClearRuntimeIngredients()
    {
        _ingredientIds.Clear();
        ClearPlateVisual();
    }

    Transform GetPlateVisualRoot()
    {
        return plateVisualRoot != null ? plateVisualRoot : transform;
    }

    void ClearPlateVisual()
    {
        if (_runtimePlateVisualObject != null)
        {
            Destroy(_runtimePlateVisualObject);
        }

        _runtimePlateVisualObject = null;
        _runtimePlateVisualPrefab = null;
    }
}
