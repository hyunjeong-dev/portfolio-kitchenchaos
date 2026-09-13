using Generated;

public interface IIngredientActionHandler
{
    bool CanContinueAction(IngredientObject ingredientObject, ActionType actionType);
}
