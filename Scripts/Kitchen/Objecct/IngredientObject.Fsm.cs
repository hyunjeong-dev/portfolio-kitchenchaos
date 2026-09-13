using System.Collections.Generic;
using Generated;
using UnityEngine;
using static Generated.GameData;

public sealed partial class IngredientObject
{
    // 저장된 별도 상태가 아니라 재료의 조리 이력과 진행량에서 계산합니다.
    public enum CookingState
    {
        None = 0,
        Ready = 1,
        Cooking = 2,
        Completed = 3,
        Burnt = 4,
    }

    internal bool IsCooking => _fsm != null && _fsm.IsActivateState<IngredientTimedCookState>();
    internal float ActionProgressNormalized => IsBurned
        ? 1f
        : GetActionProgressNormalized(
            CurrentActionType,
            _currentActionInfoData != null ? _currentActionInfoData.ActionValue : 0f);

    internal CookingState CurrentCookingState
    {
        get
        {
            if (IsBurned)
            {
                return CookingState.Burnt;
            }

            if (_currentActionInfoData == null ||
                (_currentActionInfoData.ResultAction != ActionType.PanFry &&
                 _currentActionInfoData.ResultAction != ActionType.Boil))
            {
                return CookingState.Ready;
            }

            return HasCompletedAction(_currentActionInfoData.ResultAction)
                ? CookingState.Completed
                : CookingState.Cooking;
        }
    }

    void InitFsm()
    {
        _fsm?.DestroyAllState();

        _fsm = new(this);
        _fsm.AddState<IngredientIdleState>();
        _fsm.AddState<IngredientTimedCookState>();
        _fsm.AddState<IngredientCutState>();
        _fsm.ActivateState<IngredientIdleState>();
    }

    internal bool TryExecuteAction(
        ActionType actionType,
        IReadOnlyList<int> recipeGroupIds,
        IIngredientActionHandler actionHandler,
        IIngredientActionPresentation actionPresentation)
    {
        if (!CanExecuteAction(actionType, recipeGroupIds, out var actionInfoData))
        {
            return false;
        }

        return TryExecuteAction(actionInfoData, actionHandler, actionPresentation, false);
    }

    internal bool TryStartOvercookingAction(
        IngredientActionInfoData actionInfoData,
        IIngredientActionHandler actionHandler,
        IIngredientActionPresentation actionPresentation)
    {
        return TryExecuteAction(actionInfoData, actionHandler, actionPresentation, true);
    }

    bool TryExecuteAction(
        IngredientActionInfoData actionInfoData,
        IIngredientActionHandler actionHandler,
        IIngredientActionPresentation actionPresentation,
        bool isOvercookingAction)
    {
        if (actionPresentation == null ||
            !CanStartActionState(actionInfoData, actionHandler, isOvercookingAction))
        {
            return false;
        }

        BeginAction(actionInfoData);
        _currentActionInfoData = actionInfoData;
        _actionCondition = actionHandler;
        _actionPresentation = actionPresentation;

        if (!ActivateActionFsmState(actionInfoData.ResultAction))
        {
            ClearCurrentAction();
            return false;
        }

        _actionPresentation.OnActionStarted(this, actionInfoData.ResultAction);
        OnActionProgressChanged(actionInfoData.ResultAction);
        return true;
    }

    bool CanStartActionState(
        IngredientActionInfoData actionInfoData,
        IIngredientActionHandler actionHandler,
        bool isOvercookingAction)
    {
        if (IsActionProcessing ||
            actionInfoData == null ||
            actionHandler == null ||
            actionInfoData.IngredientId != IngredientId ||
            !KitchenIngredientLogic.CanTrackAction(actionInfoData.ResultAction))
        {
            return false;
        }

        // 도구를 바꿔도 보존된 조리 진행량을 다른 action으로 이어 쓰지 않습니다.
        if (_currentActionInfoData != null &&
            (_currentActionInfoData.ResultAction == ActionType.PanFry ||
             _currentActionInfoData.ResultAction == ActionType.Boil) &&
            _currentActionInfoData.ResultAction != actionInfoData.ResultAction)
        {
            return false;
        }

        if (IsBurned || (isOvercookingAction
                ? !HasCompletedAction(actionInfoData.ResultAction)
                : !CanExecuteAction(actionInfoData)))
        {
            return false;
        }

        return actionHandler.CanContinueAction(this, actionInfoData.ResultAction);
    }

    bool ActivateActionFsmState(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.Cut:
                return _fsm.ActivateState<IngredientCutState>() != null;
            case ActionType.PanFry:
            case ActionType.Boil:
                return _fsm.ActivateState<IngredientTimedCookState>() != null;
            default:
                return false;
        }
    }

    internal void StopCurrentAction()
    {
        StopCurrentAction(true);
    }

    void StopCurrentAction(bool notify)
    {
        var actionPresentation = _actionPresentation;
        // 실행 환경만 분리합니다. 진행량, 결과 정보, 과조리 단계는 재개/담기까지 유지합니다.
        _actionCondition = null;
        _actionPresentation = null;

        if (_fsm != null && !_fsm.IsActivateState<IngredientIdleState>())
        {
            _fsm.ActivateState<IngredientIdleState>();
        }

        if (notify)
        {
            actionPresentation?.OnActionStopped(this, CurrentActionType);
        }
    }

    internal bool CanContinueCurrentAction(ActionType actionType)
    {
        return _currentActionInfoData != null &&
               _currentActionInfoData.ResultAction == actionType &&
               _actionCondition != null &&
               _actionCondition.CanContinueAction(this, actionType);
    }

    internal bool TickCurrentAction(float deltaTime)
    {
        if (_currentActionInfoData == null)
        {
            return true;
        }

        var requiredActionValue = _currentActionInfoData.ActionValue;
        if (requiredActionValue <= 0f)
        {
            return true;
        }

        _currentActionValue = Mathf.Min(_currentActionValue + deltaTime, requiredActionValue);
        return _currentActionValue >= requiredActionValue;
    }

    internal void OnActionProgressChanged(ActionType actionType)
    {
        OnActionProgressChanged(
            actionType,
            GetActionProgressNormalized(
                actionType,
                _currentActionInfoData != null ? _currentActionInfoData.ActionValue : 0f));
    }

    internal void OnActionProgressChanged(ActionType actionType, float progressNormalized)
    {
        _actionPresentation?.OnActionProgressChanged(this, actionType, progressNormalized);
    }

    internal void OnActionEffect(ActionType actionType)
    {
        _actionPresentation?.OnActionEffect(this, actionType);
    }

    internal void CompleteCuttingAction()
    {
        var actionInfoData = _currentActionInfoData;
        var actionPresentation = _actionPresentation;
        var holder = _holder;

        var previousCompletedActionFlags = CompletedActionFlags;
        if (!CompleteAction(actionInfoData))
        {
            StopCurrentAction();
            return;
        }

        var resultIngredientObject = _poolModule.ReplaceWithActionResult(this, actionInfoData, holder);
        if (resultIngredientObject == null)
        {
            CompletedActionFlags = previousCompletedActionFlags;
            StopActionAfterResultReplacementFailed(actionPresentation, ActionType.Cut);
            return;
        }

        actionPresentation?.OnActionCompleted(resultIngredientObject, ActionType.Cut);
    }

    internal void CompleteCookingAction(ActionType actionType)
    {
        var actionInfoData = _currentActionInfoData;
        var actionHandler = _actionCondition;
        var actionPresentation = _actionPresentation;
        if (HasCompletedAction(actionInfoData.ResultAction))
        {
            MarkBurned();
            StopCurrentAction(false);
            actionPresentation?.OnActionCompleted(this, actionType);
            return;
        }

        if (!CompleteAction(actionInfoData))
        {
            StopCurrentAction();
            return;
        }

        StopCurrentAction(false);
        ClearActionState();
        actionPresentation?.OnActionCompleted(this, actionType);
        TryStartOvercookingAction(actionInfoData, actionHandler, actionPresentation);
    }

    void ClearCurrentAction()
    {
        _currentActionInfoData = null;
        _actionCondition = null;
        _actionPresentation = null;
    }

    void StopActionAfterResultReplacementFailed(
        IIngredientActionPresentation actionPresentation,
        ActionType actionType)
    {
        if (_currentActionInfoData != null)
        {
            StopCurrentAction();
            return;
        }

        actionPresentation?.OnActionStopped(this, actionType);
    }

    static bool TryGetCompletedActionFlag(ActionType actionType, out IngredientActionFlags actionFlag)
    {
        switch (actionType)
        {
            case ActionType.Cut:
                actionFlag = IngredientActionFlags.Cut;
                return true;
            case ActionType.PanFry:
                actionFlag = IngredientActionFlags.PanFry;
                return true;
            case ActionType.Boil:
                actionFlag = IngredientActionFlags.Boil;
                return true;
            case ActionType.Plate:
                actionFlag = IngredientActionFlags.Plate;
                return true;
            default:
                actionFlag = IngredientActionFlags.None;
                return false;
        }
    }
}
