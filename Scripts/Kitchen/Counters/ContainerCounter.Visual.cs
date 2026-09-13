using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public partial class ContainerCounter
{
    static readonly int AnimatorHash = Animator.StringToHash("OpenClose");

    [SerializeField] Animator _containerAnimator;

    async UniTask PlayOpenCloseVisualAsync(CancellationToken cancellationToken)
    {
        if (_containerAnimator == null)
        {
            Debug.LogWarning("[ContainerCounter] Container animator is not assigned.", this);
            return;
        }

        _containerAnimator.SetTrigger(AnimatorHash);
        _containerAnimator.Update(0f);

        var aniTime = _containerAnimator.GetCurrentAnimatorStateInfo(0).length;
        await UniTask.WaitForSeconds(aniTime - 0.1f, cancellationToken: cancellationToken);

    }
}
