using System.Collections.Generic;
using Generated;
using UnityEngine;

public partial class BurnerCounter : BaseCounter, IIngredientActionHandler, IIngredientActionPresentation
{
    public ActionType ActionType => actionType;

    [SerializeField] ActionType actionType = ActionType.PanFry;
    [SerializeField] GameObject uiRoot;
    [SerializeField] UIBurnerProgressBar _progressBar;

    readonly List<int> _recipeGroupIds = new();

    ToolObject CurrentToolObject => CurrentHoldable as ToolObject;
    IngredientObject CurrentIngredient => CurrentToolObject != null ? CurrentToolObject.Ingredient : null;
    bool IsCookingVisualActive => CurrentIngredient != null &&
                                  (CurrentIngredient.IsCooking || CurrentIngredient.IsBurned);
    bool IsCookingCompletedVisualActive => CurrentIngredient != null &&
                                           CurrentIngredient.IsCooking && CurrentIngredient.IsOvercookingAction;

    protected override void OnInitialize(KitchenGameContext context)
    {
        GameData.Instance.CollectStageRecipeGroupIds(context.CurrentStageData, _recipeGroupIds);
        RefreshCookingPresentation();
    }

    protected override void OnUninitialize()
    {
        CurrentIngredient?.StopCurrentAction();
        CurrentToolObject?.RefreshCookingVisual();
        _cookingOnGameObject?.SetActive(false);
        _progressBar?.SetBurnWarningActive(false);
        SetProgress(0f);
        StopAllSound();
        _recipeGroupIds.Clear();
    }

    public void SetActionType(ActionType value)
    {
        if (value != ActionType.PanFry && value != ActionType.Boil)
        {
            return;
        }

        CurrentIngredient?.StopCurrentAction();
        actionType = value;
        RefreshCookingPresentation();
    }

    void Start()
    {
        if (uiRoot != null)
        {
            uiRoot.SetActive(true);
        }

        InitializeVisual();
    }

    void OnDestroy()
    {
        StopAllSound();
    }

    public override bool CanAccept(IHoldable holdable)
    {
        return base.CanAccept(holdable) && holdable is ToolObject tool && tool.ActionType == actionType;
    }

    public override void Attach(IHoldable holdable)
    {
        base.Attach(holdable);
        TryStartCooking();
        RefreshCookingPresentation();
    }

    public override void Detach(IHoldable holdable)
    {
        if (!ReferenceEquals(CurrentHoldable, holdable))
        {
            return;
        }

        var tool = CurrentToolObject;
        var ingredient = CurrentIngredient;
        base.Detach(holdable);
        // 버너가 실행 대상을 잃으면 조리를 중단합니다. 재료의 진행 정보는 유지합니다.
        ingredient?.StopCurrentAction();
        tool.RefreshCookingVisual();
        RefreshCookingPresentation();
    }

    public override void Interact(PlayerBehaviour player)
    {
        if (!IsInitialized || player == null)
        {
            return;
        }

        if (!HasHoldable)
        {
            if (player.CurrentHoldable is ToolObject tool)
            {
                TryPlaceToolObject(tool);
            }

            return;
        }

        if (!player.HasHoldable)
        {
            HoldableTransferLogic.TryMoveTo(CurrentHoldable, player);
            return;
        }

        if (player.CurrentHoldable is PlateObject plate)
        {
            if (KitchenPlateTransferLogic.TryTransferToPlate(plate, CurrentHoldable))
            {
                RefreshCookingPresentation();
            }

            return;
        }

        if (player.CurrentHoldable is IngredientObject ingredient &&
            !ingredient.IsActionProcessing &&
            HoldableTransferLogic.TryMoveTo(ingredient, CurrentToolObject))
        {
            TryStartCooking();
            RefreshCookingPresentation();
        }
    }

    public bool TryPlaceToolObject(ToolObject toolObject)
    {
        return HoldableTransferLogic.TryMoveTo(toolObject, this);
    }

    void TryStartCooking()
    {
        var ingredient = CurrentIngredient;
        if (!IsInitialized || ingredient == null || ingredient.IsBurned || ingredient.IsActionProcessing ||
            CurrentToolObject.ActionType != actionType)
        {
            return;
        }

        if (ingredient.HasCompletedAction(actionType))
        {
            var actionInfo = ingredient.CurrentActionInfoData;
            if (actionInfo != null && actionInfo.ResultAction == actionType)
            {
                ingredient.TryStartOvercookingAction(actionInfo, this, this);
            }

            return;
        }

        ingredient.TryExecuteAction(actionType, _recipeGroupIds, this, this);
    }

    public bool CanContinueAction(IngredientObject ingredientObject, ActionType actionType)
    {
        return IsInitialized && Context.IsGamePlaying && actionType == this.actionType &&
               CurrentToolObject != null && CurrentToolObject.ActionType == actionType &&
               ReferenceEquals(CurrentIngredient, ingredientObject);
    }

    public void OnActionStarted(IngredientObject ingredientObject, ActionType actionType)
    {
        RefreshCookingPresentation();
    }

    public void OnActionProgressChanged(
        IngredientObject ingredientObject,
        ActionType actionType,
        float progressNormalized)
    {
        SetProgress(progressNormalized);
        CurrentToolObject.RefreshCookingVisual(true);
    }

    public void OnActionEffect(IngredientObject ingredientObject, ActionType actionType)
    {
    }

    public void OnActionCompleted(IngredientObject ingredientObject, ActionType actionType)
    {
        RefreshCookingPresentation();
    }

    public void OnActionStopped(IngredientObject ingredientObject, ActionType actionType)
    {
        RefreshCookingPresentation();
    }

    void RefreshCookingPresentation()
    {
        SetProgress(CurrentIngredient != null ? CurrentIngredient.ActionProgressNormalized : 0f);
        CurrentToolObject?.RefreshCookingVisual(IsCookingVisualActive);
        RefreshSound();
        Refresh();
    }

    void SetProgress(float progressNormalized)
    {
        if (_progressBar != null)
        {
            _progressBar.SetProgress(progressNormalized);
        }
    }
}
