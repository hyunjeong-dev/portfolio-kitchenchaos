using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public partial class BurnerCounter
{
    const int WARNING_SOUND_INTERVAL_MILLISECONDS = 200;

    CancellationTokenSource _warningSoundCancellationTokenSource;
    int _cookingSoundHandle = SoundManager.INVALID_SOUND_HANDLE;

    void RefreshSound()
    {
        var shouldPlayCookingSound = IsCookingVisualActive;
        if (shouldPlayCookingSound)
        {
            PlayCookingSound();
        }
        else
        {
            StopCookingSound();
        }

        if (IsCookingCompletedVisualActive)
        {
            StartWarningSoundLoop();
        }
        else
        {
            StopWarningSoundLoop();
        }
    }

    void PlayCookingSound()
    {
        if (_cookingSoundHandle != SoundManager.INVALID_SOUND_HANDLE)
        {
            return;
        }

        _cookingSoundHandle = SoundManager.Instance.PlayLoopingSound(SoundKeys.PAN_SIZZLE_LOOP, transform.position);
    }

    void StopCookingSound()
    {
        if (_cookingSoundHandle == SoundManager.INVALID_SOUND_HANDLE)
        {
            return;
        }

        SoundManager.Instance.StopLoopingSound(_cookingSoundHandle);
        _cookingSoundHandle = SoundManager.INVALID_SOUND_HANDLE;
    }

    void StartWarningSoundLoop()
    {
        StopWarningSoundLoop();

        _warningSoundCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        PlayWarningSoundLoopAsync(_warningSoundCancellationTokenSource.Token).Forget(Debug.LogException);
    }

    void StopWarningSoundLoop()
    {
        if (_warningSoundCancellationTokenSource == null)
        {
            return;
        }

        _warningSoundCancellationTokenSource.Cancel();
        _warningSoundCancellationTokenSource.Dispose();
        _warningSoundCancellationTokenSource = null;
    }

    async UniTask PlayWarningSoundLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                SoundManager.Instance.PlaySound(SoundKeys.WARNING, transform.position);

                await UniTask.Delay(
                    WARNING_SOUND_INTERVAL_MILLISECONDS,
                    cancellationToken: cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    void StopAllSound()
    {
        StopCookingSound();
        StopWarningSoundLoop();
    }
}
