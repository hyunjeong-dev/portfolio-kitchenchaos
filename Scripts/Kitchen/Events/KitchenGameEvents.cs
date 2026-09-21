using System;

public sealed class KitchenGameEvents
{
    public event Action<KitchenGameStateChangedEvent> OnStateChanged;
    public event Action<KitchenStageResultData> OnStageResultFinalized;
    public event Action OnGamePaused;
    public event Action OnGameUnpaused;

    public void InvokeStateChanged(KitchenGameState previousState, KitchenGameState currentState)
    {
        OnStateChanged?.Invoke(new KitchenGameStateChangedEvent(previousState, currentState));
    }

    public void InvokeStageResultFinalized(KitchenStageResultData stageResult)
    {
        OnStageResultFinalized?.Invoke(stageResult);
    }

    public void InvokeGamePaused()
    {
        OnGamePaused?.Invoke();
    }

    public void InvokeGameUnpaused()
    {
        OnGameUnpaused?.Invoke();
    }
}

public readonly struct KitchenGameStateChangedEvent
{
    public KitchenGameStateChangedEvent(KitchenGameState previousState, KitchenGameState currentState)
    {
        PreviousState = previousState;
        CurrentState = currentState;
    }

    public KitchenGameState PreviousState { get; }
    public KitchenGameState CurrentState { get; }
}
