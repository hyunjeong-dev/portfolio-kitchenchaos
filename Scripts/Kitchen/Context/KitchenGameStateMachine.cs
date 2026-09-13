public sealed class KitchenGameStateMachine
{
    public KitchenGameStateMachine(KitchenGameState initialState)
    {
        CurrentState = initialState;
    }

    public KitchenGameState CurrentState { get; private set; }

    public bool BeginCountdown()
    {
        return TryChangeState(KitchenGameState.WaitingToStart, KitchenGameState.CountdownToStart);
    }

    public bool StartPlaying()
    {
        return TryChangeState(KitchenGameState.CountdownToStart, KitchenGameState.GamePlaying);
    }

    public bool GameOver()
    {
        return TryChangeState(KitchenGameState.GamePlaying, KitchenGameState.GameOver);
    }

    public bool Reset()
    {
        if (CurrentState == KitchenGameState.WaitingToStart)
        {
            return false;
        }

        CurrentState = KitchenGameState.WaitingToStart;
        return true;
    }

    public bool HasState(KitchenGameState stateMask)
    {
        return (CurrentState & stateMask) != 0;
    }

    bool TryChangeState(KitchenGameState expectedState, KitchenGameState nextState)
    {
        if (CurrentState != expectedState || CurrentState == nextState)
        {
            return false;
        }

        CurrentState = nextState;
        return true;
    }
}
