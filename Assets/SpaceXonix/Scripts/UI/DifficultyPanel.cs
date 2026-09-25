using SpaceXonix.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// The difficulty choice shown after Start Campaign. Picking a mode stores it and opens the
    /// hangar to choose a ship, which launches the run; without a hangar it starts the run at once.
    /// </summary>
    public sealed class DifficultyPanel : MonoBehaviour
    {
        [SerializeField] private Button easyButton;
        [SerializeField] private Button hardButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Text easyBestLabel;
        [SerializeField] private Text hardBestLabel;
        [Tooltip("The ship choice that follows the difficulty choice.")]
        [SerializeField] private SkinSelectPanel hangar;

        private void OnEnable()
        {
            if (easyButton != null) easyButton.onClick.AddListener(StartEasy);
            if (hardButton != null) hardButton.onClick.AddListener(StartHard);
            if (backButton != null) backButton.onClick.AddListener(Close);
            RefreshBestRuns();
        }

        private void OnDisable()
        {
            if (easyButton != null) easyButton.onClick.RemoveListener(StartEasy);
            if (hardButton != null) hardButton.onClick.RemoveListener(StartHard);
            if (backButton != null) backButton.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            RefreshBestRuns();
        }

        public void Close() => gameObject.SetActive(false);

        public void StartEasy() => StartOn(DifficultyMode.Easy);

        public void StartHard() => StartOn(DifficultyMode.Hard);

        /// <summary>Stores the chosen mode, then moves on to the ship choice. Public so tests can choose without a click.</summary>
        public void StartOn(DifficultyMode mode)
        {
            var settings = GameSettings.Current;
            if (settings != null) settings.Difficulty = mode;
            if (hangar != null) hangar.OpenForLaunch(SceneRouter.StartCampaign);
            else SceneRouter.StartCampaign();
        }

        /// <summary>Each mode keeps its own record, because Hard's modifier bonuses inflate its scores.</summary>
        public void RefreshBestRuns()
        {
            var settings = GameSettings.Current;
            if (easyBestLabel != null) easyBestLabel.text = BestRun(settings, DifficultyMode.Easy);
            if (hardBestLabel != null) hardBestLabel.text = BestRun(settings, DifficultyMode.Hard);
        }

        private static string BestRun(GameSettingsModel settings, DifficultyMode mode)
        {
            var best = settings != null ? settings.GetHighScore(mode) : 0;
            return best > 0 ? $"Best: {best}" : "No run yet";
        }
    }
}
