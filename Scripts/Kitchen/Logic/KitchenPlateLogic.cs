using System.Collections.Generic;

public static class KitchenPlateLogic
{
    public static bool IsSubsetOfRecipe(IReadOnlyList<int> candidateIngredientIds, int[] recipeIngredientIds)
    {
        if (recipeIngredientIds == null || candidateIngredientIds == null || candidateIngredientIds.Count > recipeIngredientIds.Length)
        {
            return false;
        }

        for (var i = 0; i < candidateIngredientIds.Count; i++)
        {
            if (!KitchenCollectionLogic.Contains(recipeIngredientIds, candidateIngredientIds[i]))
            {
                return false;
            }
        }

        return true;
    }
}
