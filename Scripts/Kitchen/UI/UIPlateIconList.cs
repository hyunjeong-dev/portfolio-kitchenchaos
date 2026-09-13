using System.Collections.Generic;
using UnityEngine;

public class UIPlateIconList : MonoBehaviour
{
    const float ICON_SPACING = 23f;

    [SerializeField] PlateObject _plateObject;
    [SerializeField] UIPlateIconCell _iconTemplate;

    readonly List<UIPlateIconCell> _iconCells = new();
    KitchenGameContext _context;
    bool _isSubscribed;

    void Awake()
    {
        if (_iconTemplate != null)
        {
            _iconTemplate.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        Subscribe();
        UpdateVisual();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    public void Initialize(KitchenGameContext context)
    {
        _context = context;
        for (var i = 0; i < _iconCells.Count; i++)
        {
            _iconCells[i].Initialize(_context);
        }

        UpdateVisual();
    }

    void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        if (_plateObject == null)
        {
            _plateObject = GetComponentInParent<PlateObject>();
        }

        if (_plateObject == null)
        {
            return;
        }

        _plateObject.OnIngredientAdded += PlateObject_OnIngredientAdded;
        _isSubscribed = true;
    }

    void Unsubscribe()
    {
        if (!_isSubscribed || _plateObject == null)
        {
            return;
        }

        _plateObject.OnIngredientAdded -= PlateObject_OnIngredientAdded;
        _isSubscribed = false;
    }

    void PlateObject_OnIngredientAdded(int ingredientId)
    {
        UpdateVisual();
    }

    void UpdateVisual()
    {
        if (_context == null || _plateObject == null || _iconTemplate == null)
        {
            return;
        }

        var ingredientIds = _plateObject.IngredientIds;
        EnsureIconCellCount(ingredientIds.Count);

        for (var i = 0; i < ingredientIds.Count; i++)
        {
            var iconCell = _iconCells[i];
            iconCell.gameObject.SetActive(true);
            KitchenUILogic.SetPlateIconCellPosition(iconCell, i, ingredientIds.Count, ICON_SPACING);
            iconCell.SetIngredientId(ingredientIds[i]);
        }

        for (var i = ingredientIds.Count; i < _iconCells.Count; i++)
        {
            _iconCells[i].gameObject.SetActive(false);
        }
    }

    void EnsureIconCellCount(int targetCount)
    {
        while (_iconCells.Count < targetCount)
        {
            var iconCell = Instantiate(_iconTemplate, transform);
            iconCell.Initialize(_context);
            _iconCells.Add(iconCell);
        }
    }
}
