using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIRecipeList : MonoBehaviour
{
    [SerializeField] Transform _container;
    [SerializeField] UIRecipeCell _recipeTemplate;

    readonly List<UIRecipeCell> _recipeCells = new();
    KitchenGameContext _context;
    int _maxMenuCount;

    void Awake()
    {
        if (_recipeTemplate != null)
        {
            _recipeTemplate.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (_recipeCells.Count == 0)
        {
            return;
        }

        UpdateVisibleProgress();
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Initialize(KitchenGameContext context, int maxMenuCount)
    {
        _context = context;
        _maxMenuCount = maxMenuCount;

        EnsureRecipeCellCapacity(_maxMenuCount);
        for (var i = 0; i < _recipeCells.Count; i++)
        {
            _recipeCells[i]?.Initialize(_context);
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void UpdateMenus(IReadOnlyList<KitchenMenuData> waitingMenus)
    {
        if (waitingMenus == null)
        {
            HideRecipeCellsFromIndex(0);
            RebuildLayout();
            return;
        }

        if (!EnsureRecipeCellCapacity(waitingMenus.Count + GetDisappearingCellCount()))
        {
            return;
        }

        UpdateRecipeCells(waitingMenus);
        RebuildLayout();
    }

    public void PlayExpiredMenu(KitchenMenuData expiredMenu, IReadOnlyList<KitchenMenuData> waitingMenus)
    {
        var recipeCell = FindRecipeCell(expiredMenu);
        if (recipeCell == null)
        {
            UpdateMenus(waitingMenus);
            return;
        }

        PlayExpiredMenuAsync(recipeCell, waitingMenus, this.GetCancellationTokenOnDestroy()).Forget(Debug.LogException);
    }

    bool EnsureRecipeCellCapacity(int targetCount)
    {
        if (_recipeTemplate == null || _container == null)
        {
            Debug.LogError("[UIRecipeList] Recipe template or container is not assigned.");
            return false;
        }

        if (_maxMenuCount <= 0)
        {
            Debug.LogError("[UIRecipeList] Max menu count is invalid.");
            return false;
        }

        if (targetCount < _maxMenuCount)
        {
            targetCount = _maxMenuCount;
        }

        var createCount = targetCount - _recipeCells.Count;
        for (var i = 0; i < createCount; i++)
        {
            _recipeCells.Add(CreateRecipeCell());
        }

        return true;
    }

    void UpdateRecipeCells(IReadOnlyList<KitchenMenuData> waitingMenus)
    {
        var recipeCellIndex = 0;
        for (var i = 0; i < waitingMenus.Count; i++)
        {
            if (!TryGetBindableRecipeCell(ref recipeCellIndex, out var recipeCell))
            {
                break;
            }

            var menuData = waitingMenus[i];
            recipeCell.SetMenuData(menuData);
            recipeCell.SetProgress(menuData?.RemainingTimeNormalized ?? 1f);
            recipeCell.gameObject.SetActive(true);
        }

        HideRecipeCellsFromIndex(recipeCellIndex);
    }

    void UpdateVisibleProgress()
    {
        for (var i = 0; i < _recipeCells.Count; i++)
        {
            var recipeCell = _recipeCells[i];
            if (recipeCell == null || recipeCell.IsDisappearing || !recipeCell.gameObject.activeSelf)
            {
                continue;
            }

            var menuData = recipeCell.CurrentMenuData;
            recipeCell.SetProgress(menuData?.RemainingTimeNormalized ?? 1f);
        }
    }

    void HideRecipeCellsFromIndex(int startIndex)
    {
        for (var i = startIndex; i < _recipeCells.Count; i++)
        {
            var recipeCell = _recipeCells[i];
            if (recipeCell != null && !recipeCell.IsDisappearing)
            {
                recipeCell.gameObject.SetActive(false);
            }
        }
    }

    async UniTask PlayExpiredMenuAsync(
        UIRecipeCell recipeCell,
        IReadOnlyList<KitchenMenuData> waitingMenus,
        CancellationToken cancellationToken)
    {
        var isCanceled = await recipeCell.PlayDisappearAsync(cancellationToken).SuppressCancellationThrow();
        if (isCanceled)
        {
            return;
        }

        UpdateMenus(waitingMenus);
    }

    UIRecipeCell FindRecipeCell(KitchenMenuData menuData)
    {
        if (menuData == null)
        {
            return null;
        }

        for (var i = 0; i < _recipeCells.Count; i++)
        {
            var recipeCell = _recipeCells[i];
            if (recipeCell != null &&
                !recipeCell.IsDisappearing &&
                ReferenceEquals(recipeCell.CurrentMenuData, menuData))
            {
                return recipeCell;
            }
        }

        return null;
    }

    bool TryGetBindableRecipeCell(ref int startIndex, out UIRecipeCell recipeCell)
    {
        while (startIndex < _recipeCells.Count)
        {
            recipeCell = _recipeCells[startIndex];
            startIndex++;

            if (recipeCell != null && !recipeCell.IsDisappearing)
            {
                return true;
            }
        }

        recipeCell = null;
        return false;
    }

    UIRecipeCell CreateRecipeCell()
    {
        var recipeCell = Instantiate(_recipeTemplate, _container);
        recipeCell.Initialize(_context);
        recipeCell.gameObject.SetActive(false);
        return recipeCell;
    }

    int GetDisappearingCellCount()
    {
        var count = 0;
        for (var i = 0; i < _recipeCells.Count; i++)
        {
            if (_recipeCells[i] != null && _recipeCells[i].IsDisappearing)
            {
                count++;
            }
        }

        return count;
    }

    void RebuildLayout()
    {
        var containerRect = _container as RectTransform;
        if (containerRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);

        var parentRect = containerRect.parent as RectTransform;
        if (parentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }

        Canvas.ForceUpdateCanvases();
    }
}
