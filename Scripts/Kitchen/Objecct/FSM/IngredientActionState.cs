using Generated;

public abstract class IngredientActionState : State<IngredientObject>
{
    protected ActionType ActionType => Owner.CurrentActionType;

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        if (!Owner.CanContinueCurrentAction(ActionType))
        {
            Owner.StopCurrentAction();
            return;
        }

        OnActionTick(deltaTime);

        if (Owner.TickCurrentAction(deltaTime))
        {
            Owner.OnActionProgressChanged(ActionType, 1f);
            CompleteAction();
            return;
        }

        Owner.OnActionProgressChanged(ActionType);
    }

    protected virtual void OnActionTick(float deltaTime) { }

    protected abstract void CompleteAction();
}
