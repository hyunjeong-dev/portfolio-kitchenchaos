using System;
using Generated;
using UnityEngine;

public sealed class ToolObject : MonoBehaviour, IHoldable, IHolder
{
    [Serializable]
    public sealed class VfxStateObject
    {
        public IngredientObject.CookingState State;
        public GameObject VfxObject;
    }

    static readonly int StillHash = Animator.StringToHash("Still");
    static readonly int CookingHash = Animator.StringToHash("Cooking");
    static readonly int ProgressHash = Animator.StringToHash("Progress");

    public ActionType ActionType => _actionType;
    public Transform Transform => transform;
    public IHolder Holder => _holder;
    public IHoldable CurrentHoldable => _ingredient;
    public Transform HoldPoint => _ingredientHoldPoint;
    public bool HasHoldable => _ingredient != null;
    public IngredientObject Ingredient => _ingredient;
    public bool HideIngredientVisual => _hideIngredientVisual;

    [SerializeField] ActionType _actionType = ActionType.None;
    [SerializeField] Transform _ingredientHoldPoint;
    [SerializeField] bool _hideIngredientVisual;
    [SerializeField] VfxStateObject[] _vfxStateObjects = Array.Empty<VfxStateObject>();
    [SerializeField] Animator _animator;

    IHolder _holder;
    IngredientObject _ingredient;

    void Awake()
    {
        RefreshCookingVisual();
    }

    public void SetHolder(IHolder holder)
    {
        _holder = holder;
        RefreshCookingVisual();
    }

    public bool CanAccept(IHoldable holdable)
    {
        return !HasHoldable && HoldPoint != null && holdable is IngredientObject;
    }

    public void Attach(IHoldable holdable)
    {
        _ingredient = (IngredientObject)holdable;
        RefreshCookingVisual();
    }

    public void Detach(IHoldable holdable)
    {
        if (!ReferenceEquals(_ingredient, holdable))
        {
            return;
        }

        var ingredient = _ingredient;
        _ingredient = null;
        ingredient.StopCurrentAction();
        RefreshCookingVisual();
    }

    public void ResetContent()
    {
        _ingredient?.Release();
    }

    public void Release()
    {
        _holder?.Detach(this);
        _holder = null;
        ResetContent();
        gameObject.SetActive(false);
    }

    public void RefreshCookingVisual(bool isOnBurner = false)
    {
        var state = _ingredient != null
            ? _ingredient.CurrentCookingState
            : IngredientObject.CookingState.None;
        RefreshStateVfx(state, isOnBurner);

        if (_animator == null)
        {
            return;
        }

        _animator.gameObject.SetActive(_ingredient != null);
        if (_ingredient == null)
        {
            return;
        }

        var isCookingAnimation = state is IngredientObject.CookingState.Cooking or
            IngredientObject.CookingState.Completed;
        _animator.SetBool(CookingHash, isCookingAnimation);
        _animator.SetFloat(ProgressHash, _ingredient.ActionProgressNormalized);
        if (state == IngredientObject.CookingState.Ready)
        {
            _animator.Play(StillHash, 0, 0f);
        }

        _animator.Update(0f);
    }

    void RefreshStateVfx(IngredientObject.CookingState state, bool isOnBurner)
    {
        var requiresBurner = state is IngredientObject.CookingState.Cooking or IngredientObject.CookingState.Completed;

        foreach (var vfxStateObject in _vfxStateObjects)
        {
            if (vfxStateObject == null || vfxStateObject.VfxObject == null)
            {
                continue;
            }

            // 여러 상태가 같은 VFX를 사용해도 최종 표시 여부를 한 번에 결정합니다.
            var isActive = false;
            if (!requiresBurner || isOnBurner)
            {
                foreach (var matchingState in _vfxStateObjects)
                {
                    if (matchingState != null &&
                        matchingState.VfxObject == vfxStateObject.VfxObject &&
                        matchingState.State == state)
                    {
                        isActive = true;
                        break;
                    }
                }
            }

            if (vfxStateObject.VfxObject.activeSelf != isActive)
            {
                vfxStateObject.VfxObject.SetActive(isActive);
            }
        }
    }
}
