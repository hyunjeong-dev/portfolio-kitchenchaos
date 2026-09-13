public static class KitchenPlateTransferLogic
{
    public static bool TryTransferToPlate(PlateObject plateObject, IHoldable source)
    {
        if (plateObject == null)
        {
            return false;
        }

        if (source is IngredientObject ingredient)
        {
            if (!plateObject.TryAddIngredient(ingredient))
            {
                return false;
            }

            ingredient.Release();
            return true;
        }

        if (source is ToolObject tool)
        {
            return TryTransferToolIngredientToPlate(plateObject, tool);
        }

        return false;
    }

    static bool TryTransferToolIngredientToPlate(PlateObject plateObject, ToolObject tool)
    {
        var ingredient = tool.Ingredient;
        if (ingredient == null || ingredient.IsBurned || !ingredient.HasCompletedAction(tool.ActionType))
        {
            return false;
        }

        var actionInfo = ingredient.CurrentActionInfoData;
        if (actionInfo == null || actionInfo.ResultAction != tool.ActionType)
        {
            return false;
        }

        var result = ingredient.CreateActionResult(actionInfo);
        if (result == null)
        {
            return false;
        }

        var isTransferred = plateObject.TryAddIngredient(result);
        result.Release();
        if (!isTransferred)
        {
            return false;
        }

        tool.ResetContent();
        return true;
    }
}
