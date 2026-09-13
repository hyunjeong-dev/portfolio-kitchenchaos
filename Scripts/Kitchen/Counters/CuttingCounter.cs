using System.Collections.Generic;
using Generated;
using UnityEngine;
using static Generated.GameData;

public sealed partial class CuttingCounter : BaseCounter, IIngredientActionHandler, IIngredientActionPresentation
{
    public bool IsCutting => CurrentHoldable is IngredientObject ingredientObject && ingredientObject.IsCutting;
    public bool CanProgressCutting => IsInitialized &&
                                      CurrentHoldable is IngredientObject ingredientObject &&
                                      TryGetCuttingActionInfo(ingredientObject, out _);

    [SerializeField] UIProgressBar _progressBar;

    readonly List<int> _recipeGroupIds = new();

    SelectorModule _selectorModule;
    IngredientObject _activeIngredientObject;

    protected override void OnInitialize(KitchenGameContext context)
    {
        _selectorModule = context != null ? context.GetModule<SelectorModule>() : null;
        RebuildRecipeGroupIds(context);
    }

    protected override void OnUninitialize()
    {
        if (CurrentHoldable is IngredientObject ingredient)
        {
            ingredient.StopCurrentAction();
        }

        ResetProcess();
        _selectorModule = null;
        _recipeGroupIds.Clear();
    }

    public override void Interact(PlayerBehaviour player)
    {
        if (!IsInitialized || player == null)
        {
            return;
        }

        if (!HasHoldable)
        {
            if (player.HasHoldable)
            {
                if (player.CurrentHoldable is IngredientObject ingredientObject &&
                    TryGetCuttingActionInfo(ingredientObject, out var actionInfoData))
                {
                    HoldableTransferLogic.TryMoveTo(ingredientObject, this);

                    SetProgress(GetProgressNormalized(
                        ingredientObject,
                        ActionType.Cut,
                        actionInfoData));
                }
            }
        }
        else
        {
            if (player.HasHoldable)
            {
                var playerHoldableObject = player.CurrentHoldable;
                var counterHoldableObject = CurrentHoldable;
                if ((playerHoldableObject is PlateObject playerPlate &&
                     KitchenPlateTransferLogic.TryTransferToPlate(playerPlate, counterHoldableObject)) ||
                    (counterHoldableObject is PlateObject counterPlate &&
                     KitchenPlateTransferLogic.TryTransferToPlate(counterPlate, playerHoldableObject)))
                {
                    ResetProcess();
                }
            }
            else
            {
                var holdableObject = CurrentHoldable;
                HoldableTransferLogic.TryMoveTo(holdableObject, player);
                ResetProcess();
            }
        }
    }

    public override void Detach(IHoldable holdable)
    {
        if (!ReferenceEquals(CurrentHoldable, holdable))
        {
            return;
        }

        base.Detach(holdable);
        if (holdable is IngredientObject ingredient)
        {
            ingredient.StopCurrentAction();
        }

        ResetProcess();
    }

    public override void InteractAlternate(PlayerBehaviour player)
    {
        if (!IsInitialized || player == null)
        {
            return;
        }

        if (CurrentHoldable is IngredientObject ingredientObject &&
            TryGetCuttingActionInfo(ingredientObject, out _))
        {
            ingredientObject.TryExecuteAction(ActionType.Cut, _recipeGroupIds, this, this);
        }
    }

    void ResetProcess()
    {
        _activeIngredientObject = null;

        SetProgress(0f);
    }

    bool IsCurrentSelectedCounter()
    {
        return _selectorModule != null && _selectorModule.CurrentCounter == this;
    }

    public bool CanContinueAction(IngredientObject ingredientObject, ActionType actionType)
    {
        return actionType == ActionType.Cut &&
               IsInitialized &&
               Context.IsGamePlaying &&
               ingredientObject != null &&
               ReferenceEquals(CurrentHoldable, ingredientObject) &&
               IsCurrentSelectedCounter();
    }

    public void OnActionStarted(IngredientObject ingredientObject, ActionType actionType)
    {
        if (actionType != ActionType.Cut)
        {
            return;
        }

        _activeIngredientObject = ingredientObject;
    }

    public void OnActionProgressChanged(
        IngredientObject ingredientObject,
        ActionType actionType,
        float progressNormalized)
    {
        if (actionType == ActionType.Cut && ReferenceEquals(_activeIngredientObject, ingredientObject))
        {
            SetProgress(progressNormalized);
        }
    }

    public void OnActionEffect(IngredientObject ingredientObject, ActionType actionType)
    {
        if (actionType != ActionType.Cut ||
            ingredientObject == null ||
            !ReferenceEquals(CurrentHoldable, ingredientObject))
        {
            return;
        }

        PlayCutVisual();
        SoundManager.Instance.PlaySound(SoundKeys.CHOP, transform.position);
    }

    public void OnActionCompleted(IngredientObject ingredientObject, ActionType actionType)
    {
        if (actionType != ActionType.Cut)
        {
            return;
        }

        _activeIngredientObject = null;
        SetProgress(1f);
    }

    public void OnActionStopped(IngredientObject ingredientObject, ActionType actionType)
    {
        if (actionType == ActionType.Cut)
        {
            ResetProcess();
        }
    }

    bool TryGetCuttingActionInfo(
        IngredientObject ingredientObject,
        out IngredientActionInfoData actionInfoData)
    {
        actionInfoData = null;
        if (!IsInitialized || ingredientObject == null)
        {
            return false;
        }

        EnsureRecipeGroupIds();
        if (_recipeGroupIds.Count == 0)
        {
            return false;
        }

        return ingredientObject.CanExecuteAction(
            ActionType.Cut,
            _recipeGroupIds,
            out actionInfoData);
    }

    void SetProgress(float progressNormalized)
    {
        if (_progressBar != null)
        {
            _progressBar.SetProgress(progressNormalized);
        }
    }

    void EnsureRecipeGroupIds()
    {
        if (_recipeGroupIds.Count > 0)
        {
            return;
        }

        RebuildRecipeGroupIds(Context);
    }

    void RebuildRecipeGroupIds(KitchenGameContext context)
    {
        _recipeGroupIds.Clear();
        var stageData = context != null ? context.CurrentStageData : null;
        GameData.Instance.CollectStageRecipeGroupIds(stageData, _recipeGroupIds);
    }

    float GetProgressNormalized(
        IngredientObject ingredientObject,
        ActionType actionType,
        IngredientActionInfoData actionInfoData)
    {
        var duration = actionInfoData != null ? actionInfoData.ActionValue : 0;
        if (ingredientObject == null || duration <= 0)
        {
            return 0f;
        }

        return ingredientObject.GetActionProgressNormalized(actionType, duration);
    }
}
