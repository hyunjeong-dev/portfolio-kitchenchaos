using UnityEngine;

public sealed class UIBurnerProgressBar : MonoBehaviour
{
    static readonly int IsFlashingAnimatorHash = Animator.StringToHash("IsFlashing");

    [SerializeField] UIProgressBar _progressBar;
    [SerializeField] UIBurnerWarning _warning;
    [SerializeField] Animator _animator;

    void Awake()
    {
        SetBurnWarningActive(false);
    }

    public void SetProgress(float progressNormalized)
    {
        if (_progressBar != null)
        {
            _progressBar.SetProgress(progressNormalized);
        }
    }

    public void SetBurnWarningActive(bool active)
    {
        if (_animator != null)
        {
            _animator.SetBool(IsFlashingAnimatorHash, active);
        }

        if (_warning != null)
        {
            _warning.SetActive(active);
        }
    }
}
