using System;
using UnityEngine;

public sealed class KitchenPauseModule : KitchenGameModule
{
    public event Action OnGamePaused;
    public event Action OnGameUnpaused;

    public bool IsGamePaused => _isGamePaused;

    bool _isGamePaused;

    public void TogglePause()
    {
        ChangePauseState(!_isGamePaused);
    }

    public override void OnEnd()
    {
        Time.timeScale = 1f;
        _isGamePaused = false;

        base.OnEnd();
    }

    void ChangePauseState(bool isPaused)
    {
        _isGamePaused = isPaused;

        if (_isGamePaused)
        {
            Time.timeScale = 0f;
            OnGamePaused?.Invoke();
            return;
        }

        Time.timeScale = 1f;
        OnGameUnpaused?.Invoke();
    }
}
