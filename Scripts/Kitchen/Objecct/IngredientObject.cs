using System.Collections.Generic;
using Generated;
using UnityEngine;
using static Generated.GameData;

public sealed partial class IngredientObject : MonoBehaviour, IHoldable
{
    public Transform Transform => transform;
    public IHolder Holder => _holder;

    [InspectorReadOnly]
    [SerializeField] float _currentActionValue;
    [InspectorReadOnly]
    [SerializeField] bool _isBurned;

    BehaviourFsm<IngredientObject> _fsm;
    IHolder _holder;
    KitchenGameContext _context;
    IngredientPoolModule _poolModule;
    KitchenResourceModule _resourceModule;
    GameObject _runtimeVisualObject;
    GameObject _runtimeVisualPrefab;
    Renderer[] _visualRenderers;
    IngredientActionInfoData _currentActionInfoData;
    IIngredientActionHandler _actionCondition;
    IIngredientActionPresentation _actionPresentation;

    internal int IngredientId { get; private set; }
    internal IngredientActionFlags CompletedActionFlags { get; private set; }
    internal string CurrentPrefabPath { get; private set; }
    internal IngredientActionInfoData CurrentActionInfoData => _currentActionInfoData;
    internal ActionType CurrentActionType => _currentActionInfoData != null
        ? _currentActionInfoData.ResultAction
        : ActionType.None;
    internal bool IsActionProcessing => CurrentActionType != ActionType.None && !IsIdle;
    bool IsIdle => _fsm == null || _fsm.IsActivateState<IngredientIdleState>();
    internal bool IsCutting => _fsm != null && _fsm.IsActivateState<IngredientCutState>();
    internal bool IsBurned => _isBurned;
    internal bool IsOvercookingAction => IsActionProcessing &&
                                          HasCompletedAction(CurrentActionType);

    void Awake()
    {
        InitFsm();
    }

    void OnDestroy()
    {
        _fsm?.DestroyAllState();
        _fsm = null;
    }

    void Update()
    {
        if (_context == null || !_context.IsGamePlaying)
        {
            return;
        }

        _fsm?.Update(Time.deltaTime);
    }

    internal void Initialize(KitchenGameContext context)
    {
        if (_fsm == null)
        {
            InitFsm();
        }

        _context = context;
        _poolModule = context.GetModule<IngredientPoolModule>();
        _resourceModule = context.GetModule<KitchenResourceModule>();
    }

    internal void SetGameData(int ingredientId, IngredientActionFlags completedActions, string prefabPath)
    {
        StopCurrentAction(false);
        ClearCurrentAction();

        IngredientId = ingredientId;
        CompletedActionFlags = completedActions;
        CurrentPrefabPath = prefabPath;
        _isBurned = false;
        ClearActionState();
    }

    public void SetHolder(IHolder holder)
    {
        _holder = holder;
        RefreshRuntimeVisual();
        RefreshVisualVisibility();
    }

    void BeginAction(IngredientActionInfoData actionInfoData)
    {
        if (actionInfoData == null || CurrentActionType == actionInfoData.ResultAction)
        {
            return;
        }

        _currentActionValue = 0f;
    }

    internal float GetActionProgressNormalized(ActionType actionType, float requiredActionValue)
    {
        if (CurrentActionType != actionType || requiredActionValue <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(_currentActionValue / requiredActionValue);
    }

    void ClearActionState()
    {
        _currentActionValue = 0f;
    }

    internal bool HasCompletedAction(ActionType actionType)
    {
        if (!TryGetCompletedActionFlag(actionType, out var actionFlag))
        {
            return false;
        }

        return (CompletedActionFlags & actionFlag) == actionFlag;
    }

    bool HasRequiredActions(ActionType[] requiredActions)
    {
        if (requiredActions == null || requiredActions.Length == 0)
        {
            return true;
        }

        for (var i = 0; i < requiredActions.Length; i++)
        {
            if (!HasCompletedAction(requiredActions[i]))
            {
                return false;
            }
        }

        return true;
    }

    internal bool CanExecuteAction(IngredientActionInfoData actionInfoData)
    {
        return actionInfoData != null &&
               actionInfoData.IngredientId == IngredientId &&
               actionInfoData.ResultAction != ActionType.None &&
               !HasCompletedAction(actionInfoData.ResultAction) &&
               HasRequiredActions(actionInfoData.RequiredActions);
    }

    internal bool CanExecuteAction(
        ActionType actionType,
        IReadOnlyList<int> recipeGroupIds,
        out IngredientActionInfoData actionInfoData)
    {
        actionInfoData = null;
        if (recipeGroupIds == null || actionType == ActionType.None)
        {
            return false;
        }

        for (var i = 0; i < recipeGroupIds.Count; i++)
        {
            var candidateActionInfoData = GameData.Instance.GetIngredientActionInfoData(
                    IngredientId,
                    recipeGroupIds[i],
                    actionType);
            if (!CanExecuteAction(candidateActionInfoData))
            {
                continue;
            }

            actionInfoData = candidateActionInfoData;
            return true;
        }

        return false;
    }

    internal bool CompleteAction(IngredientActionInfoData actionInfoData)
    {
        return actionInfoData != null && CompleteAction(actionInfoData.ResultAction);
    }

    internal bool CompleteAction(ActionType actionType)
    {
        if (!TryGetCompletedActionFlag(actionType, out var actionFlag))
        {
            return false;
        }

        CompletedActionFlags |= actionFlag;
        return true;
    }

    internal IngredientObject CreateActionResult(IngredientActionInfoData actionInfoData)
    {
        if (_poolModule == null || actionInfoData == null)
        {
            return null;
        }

        return _poolModule.GetIngredientActionResult(
            actionInfoData,
            CompletedActionFlags,
            null);
    }

    void MarkBurned()
    {
        _isBurned = true;
    }

    public void Release()
    {
        _poolModule.ReleaseIngredientObject(this);
    }

    internal void ResetForPool(Transform poolRoot)
    {
        _holder?.Detach(this);
        StopCurrentAction(false);
        _holder = null;
        _context = null;
        ClearCurrentAction();
        CompletedActionFlags = IngredientActionFlags.None;
        RefreshVisualVisibility();
        transform.parent = poolRoot;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        _isBurned = false;
        ClearActionState();
        gameObject.SetActive(false);
    }

    void SetRuntimeVisual(GameObject visualObject, GameObject visualPrefab)
    {
        _runtimeVisualObject = visualObject;
        _runtimeVisualPrefab = visualPrefab;
        _visualRenderers = visualObject.GetComponentsInChildren<Renderer>(true);
        RefreshVisualVisibility();
    }

    internal static IngredientObject CreateFromPrefab(GameObject prefab)
    {
        var instance = Instantiate(prefab);
        var ingredientObject = instance.GetComponent<IngredientObject>();
        if (ingredientObject != null)
        {
            return ingredientObject;
        }

        var root = new GameObject(string.Concat(prefab.name, "_Runtime"));
        ingredientObject = root.AddComponent<IngredientObject>();
        ingredientObject.SetRuntimeVisual(instance, prefab);
        instance.transform.SetParent(root.transform, false);
        return ingredientObject;
    }

    void RefreshRuntimeVisual()
    {
        if (_runtimeVisualObject == null || IngredientId == 0 || string.IsNullOrEmpty(CurrentPrefabPath))
        {
            return;
        }

        if (_resourceModule == null)
        {
            return;
        }

        var ingredientData = GameData.Instance.GetIngredientInfoData(IngredientId);
        var visualPrefab = _resourceModule.LoadIngredientPrefab(ingredientData, CurrentPrefabPath);
        if (visualPrefab == null ||
            visualPrefab == _runtimeVisualPrefab ||
            visualPrefab.GetComponent<IngredientObject>() != null)
        {
            return;
        }

        Destroy(_runtimeVisualObject);

        SetRuntimeVisual(Instantiate(visualPrefab, transform, false), visualPrefab);
    }

    void RefreshVisualVisibility()
    {
        _visualRenderers ??= GetComponentsInChildren<Renderer>(true);
        var hideVisual = _holder is ToolObject toolObject && toolObject.HideIngredientVisual;

        // 재료의 Update와 조리는 유지하고 렌더링만 숨긴다.
        foreach (var visualRenderer in _visualRenderers)
        {
            if (visualRenderer != null)
            {
                visualRenderer.forceRenderingOff = hideVisual;
            }
        }
    }

}
