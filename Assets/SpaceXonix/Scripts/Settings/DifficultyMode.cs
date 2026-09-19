namespace SpaceXonix.Settings
{
    /// <summary>
    /// How demanding a campaign run is. Easy drops the stage modifiers and grants an extra life;
    /// Hard is the full game. Everything else - enemies, lasers, capture target - is identical.
    /// </summary>
    public enum DifficultyMode
    {
        Easy,
        Hard
    }

    public static class DifficultyModeExtensions
    {
        public static string DisplayName(this DifficultyMode mode) => mode == DifficultyMode.Easy ? "Easy" : "Hard";

        /// <summary>Only Hard rolls a stage modifier, which is what its score bonus pays for.</summary>
        public static bool UsesStageModifiers(this DifficultyMode mode) => mode == DifficultyMode.Hard;

        /// <summary>Lives granted on top of the configured starting total.</summary>
        public static int BonusStartingLives(this DifficultyMode mode) => mode == DifficultyMode.Easy ? 1 : 0;
    }
}
