using Generated;

public interface IIngredientActionPresentation
{
    void OnActionStarted(IngredientObject ingredientObject, ActionType actionType);
    void OnActionProgressChanged(IngredientObject ingredientObject, ActionType actionType, float progressNormalized);
    void OnActionEffect(IngredientObject ingredientObject, ActionType actionType);
    void OnActionCompleted(IngredientObject ingredientObject, ActionType actionType);
    void OnActionStopped(IngredientObject ingredientObject, ActionType actionType);
}
