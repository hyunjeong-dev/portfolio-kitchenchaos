[System.Flags]
public enum KitchenGameState
{
    None = 0,
    WaitingToStart = 1 << 0,
    CountdownToStart = 1 << 1,
    GamePlaying = 1 << 2,
    GameOver = 1 << 3,
}
