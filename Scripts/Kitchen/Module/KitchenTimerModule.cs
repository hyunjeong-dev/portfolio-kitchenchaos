using UnityEngine;

public sealed class KitchenTimerModule : KitchenGameModule
{
    float _countdownToStartTimer = 3f;
    float _gamePlayingTimer;
    float _gamePlayingTimerMax;

    public float CountdownToStartTimer => _countdownToStartTimer;

    public float GamePlayingTimeMax => _gamePlayingTimerMax;
    public float GamePlayingTimerNormalized => _gamePlayingTimerMax > 0f ? 1 - (_gamePlayingTimer / _gamePlayingTimerMax) : 0f;
    public float GamePlayingTimeRemaining => Mathf.Max(0f, _gamePlayingTimer);

    public override void OnRegister()
    {
        base.OnRegister();

        var stageData = Context.CurrentStageData;
        if (stageData == null || stageData.PlayTime <= 0)
        {
            Debug.LogError("[KitchenTimerModule] Stage play time is invalid.");
            return;
        }

        _gamePlayingTimerMax = stageData.PlayTime;
    }

    public bool TickCountdown(float deltaTime)
    {
        _countdownToStartTimer -= deltaTime;
        return _countdownToStartTimer < 0f;
    }

    public void StartPlayingTimer()
    {
        _gamePlayingTimer = _gamePlayingTimerMax;
    }

    public bool TickGamePlaying(float deltaTime)
    {
        _gamePlayingTimer -= deltaTime;
        return _gamePlayingTimer < 0f;
    }
}
