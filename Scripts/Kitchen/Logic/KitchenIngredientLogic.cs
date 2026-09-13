using Generated;

public static class KitchenIngredientLogic
{
    public static bool CanTrackAction(ActionType actionType)
    {
        return actionType == ActionType.Cut ||
               actionType == ActionType.PanFry ||
               actionType == ActionType.Boil;
    }

    public static bool TryGetIngredientCodeByResourceSuffix(string suffix, out string code)
    {
        code = null;
        if (string.IsNullOrEmpty(suffix))
        {
            return false;
        }

        var ingredientData = GameData.Instance.GetIngredientInfoDataByResourceSuffix(suffix) ??
                             GameData.Instance.GetIngredientInfoDataByCode(suffix);
        code = ingredientData != null ? ingredientData.Code : null;
        return ingredientData != null;
    }

    public static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
