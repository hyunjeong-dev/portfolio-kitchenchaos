using UnityEngine;

public sealed partial class CuttingCounter
{
    static readonly int AnimatorHash = Animator.StringToHash("Base Layer.CuttingCounterCut");

    [SerializeField] Animator _cuttingAnimator;

    void PlayCutVisual()
    {
        if (_cuttingAnimator == null)
        {
            Debug.LogWarning("[CuttingCounter] Cutting animator is not assigned.", this);
            return;
        }

        _cuttingAnimator.Play(AnimatorHash, 0, 0f);
    }
}
