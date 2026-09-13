using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIRecipeCell : MonoBehaviour
{
    const float SHAKE_PROGRESS_NORMALIZED_THRESHOLD = 0.1f;
    const float SHAKE_STRENGTH = 8f;
    const float SHAKE_SPEED = 48f;
    const float SHAKE_VERTICAL_RATE = 0.45f;
    const float SHAKE_VERTICAL_SPEED_RATE = 1.37f;
    const float DISAPPEAR_DURATION = 0.22f;
    const float DISAPPEAR_SCALE = 0.82f;

    [SerializeField] Image _recipeIcon;

    [SerializeField] RectTransform _ingredientRoot;
    [SerializeField] UIRecipeIngredientCell _ingredientCellAsset;
    [SerializeField] RectTransform _progressFillRect;
    [SerializeField] Image _progressFillImage;

    List<UIRecipeIngredientCell> _ingredientCells = new();
    RectTransform _rectTransform;
    CanvasGroup _canvasGroup;
    KitchenResourceModule _resourceModule;
    Vector2 _shakeBaseAnchoredPosition;
    Vector3 _baseLocalScale;
    Color _currentProgressColor;
    float _shakeTime;
    float _currentProgressNormalized = -1f;
    bool _isShaking;
    bool _isBaseLocalScaleCached;
    bool _hasProgressColor;

    public KitchenMenuData CurrentMenuData { get; private set; }
    public bool IsDisappearing { get; private set; }

    void Awake()
    {
        CacheTransformState();
    }

    void Update()
    {
        if (!_isShaking || _rectTransform == null)
        {
            return;
        }

        _shakeTime += Time.deltaTime;
        var offsetX = Mathf.Sin(_shakeTime * SHAKE_SPEED) * SHAKE_STRENGTH;
        var offsetY = Mathf.Sin(_shakeTime * SHAKE_SPEED * SHAKE_VERTICAL_SPEED_RATE) *
                      SHAKE_STRENGTH *
                      SHAKE_VERTICAL_RATE;

        _rectTransform.anchoredPosition = _shakeBaseAnchoredPosition + new Vector2(offsetX, offsetY);
    }

    void OnDisable()
    {
        StopShake();
    }

    public void Initialize(KitchenGameContext context)
    {
        _resourceModule = context.GetModule<KitchenResourceModule>();
        CacheIngredientCells();
    }

    public void SetMenuData(KitchenMenuData menuData)
    {
        if (ReferenceEquals(CurrentMenuData, menuData) && !IsDisappearing)
        {
            return;
        }

        ResetPresentation();
        CurrentMenuData = menuData;
        SetRecipeIcon(menuData);
        SetIngredientIcons(menuData);
    }

    public void SetProgress(float progressNormalized)
    {
        progressNormalized = Mathf.Clamp01(progressNormalized);
        if (!Mathf.Approximately(_currentProgressNormalized, progressNormalized))
        {
            _currentProgressNormalized = progressNormalized;
            if (_progressFillRect != null)
            {
                var anchorMax = _progressFillRect.anchorMax;
                anchorMax.x = progressNormalized;
                _progressFillRect.anchorMax = anchorMax;
            }
        }

        if (_progressFillImage != null)
        {
            var progressColor = KitchenUILogic.GetRecipeProgressColor(progressNormalized);
            if (!_hasProgressColor || _currentProgressColor != progressColor)
            {
                _hasProgressColor = true;
                _currentProgressColor = progressColor;
                _progressFillImage.color = progressColor;
            }
        }

        UpdateShake(progressNormalized);
    }

    public async UniTask PlayDisappearAsync(CancellationToken cancellationToken)
    {
        CacheTransformState();
        StopShake();
        IsDisappearing = true;
        SetProgress(0f);

        var canvasGroup = GetCanvasGroup();
        var startScale = transform.localScale;
        var endScale = _baseLocalScale * DISAPPEAR_SCALE;
        var elapsedTime = 0f;

        try
        {
            while (elapsedTime < DISAPPEAR_DURATION)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

                elapsedTime += Time.deltaTime;
                var animationProgressNormalized = Mathf.Clamp01(elapsedTime / DISAPPEAR_DURATION);
                var easedProgressNormalized = 1f - (1f - animationProgressNormalized) * (1f - animationProgressNormalized);

                canvasGroup.alpha = 1f - easedProgressNormalized;
                transform.localScale = Vector3.LerpUnclamped(startScale, endScale, easedProgressNormalized);
            }

            canvasGroup.alpha = 0f;
            transform.localScale = endScale;
            gameObject.SetActive(false);
            CurrentMenuData = null;
        }
        finally
        {
            if (this != null)
            {
                IsDisappearing = false;
                canvasGroup.alpha = 1f;
                transform.localScale = _baseLocalScale;
            }
        }
    }

    void SetRecipeIcon(KitchenMenuData menuData)
    {
        if (_recipeIcon == null)
        {
            return;
        }

        Sprite sprite = null;
        var hasSprite = menuData != null &&
                        _resourceModule != null &&
                        _resourceModule.TryGetPreloadedRecipeIcon(menuData.RecipeInfoData, out sprite);
        _recipeIcon.sprite = sprite;
        _recipeIcon.gameObject.SetActive(hasSprite);
    }

    void SetIngredientIcons(KitchenMenuData menuData)
    {
        CacheIngredientCells();

        foreach (var ingredientCell in _ingredientCells)
        {
            ingredientCell.gameObject.SetActive(false);
        }

        if (menuData == null)
        {
            return;
        }

        var ingredientCount = menuData.IngredientIds.Count;
        var recipeGroupId = menuData.RecipeInfoData.RecipeGroupDataId;
        for (var i = 0; i < ingredientCount; ++i)
        {
            if (i >= _ingredientCells.Count)
            {
                var ingredientCell = Instantiate(_ingredientCellAsset, _ingredientRoot);
                _ingredientCells.Add(ingredientCell);
            }

            var cell = _ingredientCells[i];
            cell.Build(menuData.IngredientIds[i], recipeGroupId);
            cell.gameObject.SetActive(true);
        }

        MarkIngredientLayoutDirty();
    }
    
    void MarkIngredientLayoutDirty()
    {
        if (_ingredientRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_ingredientRoot);

            var parentRect = _ingredientRoot.parent as RectTransform;
            if (parentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }
    }
    
    void UpdateShake(float progressNormalized)
    {
        if (IsDisappearing)
        {
            return;
        }

        if (progressNormalized > 0f && progressNormalized <= SHAKE_PROGRESS_NORMALIZED_THRESHOLD)
        {
            StartShake();
            return;
        }

        StopShake();
    }

    void StartShake()
    {
        CacheTransformState();

        if (_isShaking || _rectTransform == null)
        {
            return;
        }

        _isShaking = true;
        _shakeTime = 0f;
        _shakeBaseAnchoredPosition = _rectTransform.anchoredPosition;
    }

    void StopShake()
    {
        if (!_isShaking)
        {
            return;
        }

        _isShaking = false;
        _shakeTime = 0f;

        if (_rectTransform != null)
        {
            _rectTransform.anchoredPosition = _shakeBaseAnchoredPosition;
        }
    }

    void ResetPresentation()
    {
        CacheTransformState();
        StopShake();

        IsDisappearing = false;
        transform.localScale = _baseLocalScale;
        _currentProgressNormalized = -1f;
        _hasProgressColor = false;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }
    }

    void CacheTransformState()
    {
        if (_rectTransform == null)
        {
            _rectTransform = transform as RectTransform;
        }

        if (_isBaseLocalScaleCached)
        {
            return;
        }

        _baseLocalScale = transform.localScale;
        _isBaseLocalScaleCached = true;
    }

    CanvasGroup GetCanvasGroup()
    {
        if (_canvasGroup != null)
        {
            return _canvasGroup;
        }

        if (!TryGetComponent(out _canvasGroup))
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        return _canvasGroup;
    }

    void CacheIngredientCells()
    {
        if (_ingredientCells.Count > 0 || _ingredientRoot == null)
        {
            return;
        }

        _ingredientRoot.GetComponentsInChildren(true, _ingredientCells);
    }
}
