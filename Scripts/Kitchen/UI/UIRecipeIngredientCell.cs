using System.Collections.Generic;
using Generated;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIRecipeIngredientCell : MonoBehaviour
{
    [SerializeField] Image _ingredientIcon;
    [SerializeField] Image _toolIcon;
    
    static readonly List<ActionType> toolActions = new()
    {
        ActionType.Boil,
        ActionType.PanFry,
    };
    
    public void Build(int ingredientDataId, int recipeGroupId)
    {
        SetIngredientIcon(ingredientDataId);
        SetToolIcon(ingredientDataId, recipeGroupId);
    }

    void SetIngredientIcon(int ingredientDataId)
    {
        var ingredientData = GameData.Instance.GetIngredientInfoData(ingredientDataId);
        var sprite = AssetManager.Instance.Load<Sprite>(ingredientData.IconPath);
        _ingredientIcon.sprite = sprite;
        _ingredientIcon.gameObject.SetActive(sprite);
    }

        
    void SetToolIcon(int ingredientDataId, int recipeGroupId)
    {
        var targetActionType = ActionType.None;
        for (var i = 0; i < toolActions.Count; i++)
        {
            var actionType = toolActions[i];
            if (GameData.Instance.GetIngredientActionInfoData(ingredientDataId, recipeGroupId, actionType) != null)
            {
                targetActionType = actionType;
                break;
            }
        }

        if (targetActionType != ActionType.None)
        {
            var actionData = GameData.Instance.GetActionInfoData(targetActionType);
            if (actionData == null)
            {
                _toolIcon.gameObject.SetActive(false);
                return;
            }

            _toolIcon.sprite = AssetManager.Instance.Load<Sprite>(actionData.IconPath);
            _toolIcon.gameObject.SetActive(true);
            return;
        }
        
        _toolIcon.gameObject.SetActive(false);
    }
}
