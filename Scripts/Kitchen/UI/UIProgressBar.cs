using UnityEngine;
using UnityEngine.UI;

public sealed class UIProgressBar : MonoBehaviour
{
    const float FILL_ANIMATION_DURATION = 0.12f;

    [SerializeField] Image _barImage;

    CanvasGroup _canvasGroup;
    float _startProgressNormalized;
    float _targetProgressNormalized;
    float _animationElapsedTime;
    bool _isAnimating;

    void Awake()
    {
        CacheCanvasGroup();
        SetProgressImmediate(0f);
        Hide();
    }

    void Update()
    {
        if (!_isAnimating || _barImage == null)
        {
            return;
        }

        _animationElapsedTime += Time.deltaTime;
        var animationProgressNormalized = Mathf.Clamp01(_animationElapsedTime / FILL_ANIMATION_DURATION);
        var easedProgressNormalized = 1f - (1f - animationProgressNormalized) * (1f - animationProgressNormalized);

        _barImage.fillAmount = Mathf.Lerp(_startProgressNormalized, _targetProgressNormalized, easedProgressNormalized);
        if (animationProgressNormalized >= 1f)
        {
            _barImage.fillAmount = _targetProgressNormalized;
            _isAnimating = false;
        }
    }

    public void SetProgress(float progressNormalized)
    {
        progressNormalized = Mathf.Clamp01(progressNormalized);

        if (progressNormalized <= 0f || progressNormalized >= 1f)
        {
            SetProgressImmediate(progressNormalized);
            Hide();
            return;
        }

        Show();
        AnimateProgress(progressNormalized);
    }

    void AnimateProgress(float progressNormalized)
    {
        if (_barImage == null)
        {
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            SetProgressImmediate(progressNormalized);
            return;
        }

        _startProgressNormalized = _barImage.fillAmount;
        _targetProgressNormalized = progressNormalized;
        _animationElapsedTime = 0f;
        _isAnimating = !Mathf.Approximately(_startProgressNormalized, _targetProgressNormalized);
    }

    void SetProgressImmediate(float progressNormalized)
    {
        _isAnimating = false;
        _startProgressNormalized = progressNormalized;
        _targetProgressNormalized = progressNormalized;
        _animationElapsedTime = FILL_ANIMATION_DURATION;

        if (_barImage != null)
        {
            _barImage.fillAmount = progressNormalized;
        }
    }

    void CacheCanvasGroup()
    {
        if (_canvasGroup != null)
        {
            return;
        }

        if (!TryGetComponent(out _canvasGroup))
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    void Show()
    {
        CacheCanvasGroup();
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    void Hide()
    {
        CacheCanvasGroup();
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    void OnValidate()
    {
        if (_barImage != null)
        {
            _barImage.fillAmount = Mathf.Clamp01(_barImage.fillAmount);
        }
    }
}
