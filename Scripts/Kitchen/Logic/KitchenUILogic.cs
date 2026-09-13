using UnityEngine;

public static class KitchenUILogic
{
    const float RECIPE_PROGRESS_NORMALIZED_WARNING_THRESHOLD = 0.66f;
    const float RECIPE_PROGRESS_NORMALIZED_DANGER_THRESHOLD = 0.33f;

    static readonly Color RecipeProgressSafeColor = new(0.22352943f, 0.6117647f, 0.10588236f, 1f);
    static readonly Color RecipeProgressWarningColor = new(0.7647059f, 0.8392157f, 0.17254902f, 1f);
    static readonly Color RecipeProgressDangerColor = new(0.9529412f, 0.427451f, 0.13725491f, 1f);

    public static Color GetRecipeProgressColor(float progressNormalized)
    {
        if (progressNormalized > RECIPE_PROGRESS_NORMALIZED_WARNING_THRESHOLD)
        {
            return RecipeProgressSafeColor;
        }

        return progressNormalized > RECIPE_PROGRESS_NORMALIZED_DANGER_THRESHOLD
            ? RecipeProgressWarningColor
            : RecipeProgressDangerColor;
    }

    public static void SetPlateIconCellPosition(UIPlateIconCell iconCell, int index, int count, float spacing)
    {
        var positionX = (index - (count - 1) * 0.5f) * spacing;
        if (iconCell.transform is RectTransform rectTransform)
        {
            rectTransform.anchoredPosition = new Vector2(positionX, 0f);
            return;
        }

        iconCell.transform.localPosition = new Vector3(positionX, 0f, 0f);
    }
}
