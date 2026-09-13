public sealed class IngredientTimedCookState : IngredientActionState
{
    protected override void CompleteAction()
    {
        Owner.CompleteCookingAction(ActionType);
    }
}
