using UnityEngine;
using UnityEngine.UI;

public class UIPlateIconCell : MonoBehaviour
{
    [SerializeField] Image _image;

    KitchenResourceModule _resourceModule;

    void Awake()
    {
        if (_image == null)
        {
            _image = GetComponent<Image>();
        }
    }

    public void Initialize(KitchenGameContext context)
    {
        _resourceModule = context.GetModule<KitchenResourceModule>();
    }

    public void SetIngredientId(int ingredientId)
    {
        var ingredientData = GameData.Instance.GetIngredientInfoData(ingredientId);
        if (ingredientData == null ||
            !_resourceModule.TryGetPreloadedIngredientIcon(ingredientData, out var sprite))
        {
            SetSprite(null);
            return;
        }

        SetSprite(sprite);
    }

    void SetSprite(Sprite sprite)
    {
        if (_image == null)
        {
            return;
        }

        _image.sprite = sprite;
    }
}
