namespace RFIDBaggage.Core
{
    public enum GameState
    {
        SystemInitializing,
        IdlePreparing,
        Idle,
        LevelInitializing,
        IntroPreparing,
        IntroPlaying,
        GamePreparing,
        Gameplay,
        SuccessPreparing,
        SuccessPlaying,
        FailurePreparing,
        FailurePlaying,
        Resetting,
        ErrorRecovery,
        GameplayStartPending
    }
}
