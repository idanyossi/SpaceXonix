namespace SpaceXonix.Board
{
    public enum BoardMoveResult
    {
        Ignored,
        SafeMove,
        TrailStarted,
        TrailExtended,
        TrailFailed,
        Reconnected,
        OutOfBounds
    }
}
