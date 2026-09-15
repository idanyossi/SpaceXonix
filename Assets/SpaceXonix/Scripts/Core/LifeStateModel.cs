namespace SpaceXonix.Core
{
    /// <summary>Pure state gate ensuring only one life can be lost outside Playing.</summary>
    public sealed class LifeStateModel
    {
        public LifeStateModel(int startingLives)
        {
            StartingLives = startingLives < 1 ? 1 : startingLives;
            Lives = StartingLives;
            State = GameplayState.Playing;
        }

        public int StartingLives { get; }
        public int Lives { get; private set; }
        public GameplayState State { get; private set; }

        public bool TryFail()
        {
            if (State != GameplayState.Playing) return false;
            Lives--;
            State = Lives == 0 ? GameplayState.GameOver : GameplayState.Respawning;
            return true;
        }

        public bool CompleteRespawn()
        {
            if (State != GameplayState.Respawning) return false;
            State = GameplayState.Playing;
            return true;
        }

        public void SetState(GameplayState state)
        {
            State = state;
        }
    }
}
