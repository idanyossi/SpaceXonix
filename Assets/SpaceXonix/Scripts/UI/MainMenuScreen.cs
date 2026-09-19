using SpaceXonix.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// The Main Menu: start the campaign, open settings or controls, and quit on PC.
    /// Shows the stored campaign high score so the record is visible before a run.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button controlsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Text highScoreLabel;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private DifficultyPanel difficultyPanel;

        private void OnEnable()
        {
            if (startButton != null) startButton.onClick.AddListener(StartCampaign);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (controlsButton != null) controlsButton.onClick.AddListener(OpenControls);
            if (quitButton != null)
            {
                // Quit is a PC-only requirement; mobile platforms hide it rather than show a dead button.
                quitButton.gameObject.SetActive(Application.platform != RuntimePlatform.Android &&
                                                Application.platform != RuntimePlatform.IPhonePlayer);
                quitButton.onClick.AddListener(SceneRouter.QuitGame);
            }
            if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (difficultyPanel != null) difficultyPanel.gameObject.SetActive(false);
            RefreshHighScore();
        }

        private void OnDisable()
        {
            if (startButton != null) startButton.onClick.RemoveListener(StartCampaign);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
            if (controlsButton != null) controlsButton.onClick.RemoveListener(OpenControls);
            if (quitButton != null) quitButton.onClick.RemoveListener(SceneRouter.QuitGame);
        }

        /// <summary>Start Campaign asks for a difficulty first; the panel itself loads the game scene.</summary>
        public void StartCampaign()
        {
            if (settingsPanel != null) settingsPanel.Close();
            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (difficultyPanel != null) difficultyPanel.Open();
            else SceneRouter.StartCampaign();
        }

        public void OpenSettings()
        {
            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (difficultyPanel != null) difficultyPanel.Close();
            if (settingsPanel != null) settingsPanel.Open();
        }

        public void OpenControls()
        {
            if (settingsPanel != null) settingsPanel.Close();
            if (difficultyPanel != null) difficultyPanel.Close();
            if (controlsPanel != null) controlsPanel.SetActive(true);
        }

        public void RefreshHighScore()
        {
            if (highScoreLabel == null) return;
            var settings = GameSettings.Current;
            var best = settings != null ? settings.CampaignHighScore : 0;
            highScoreLabel.text = best > 0
                ? $"Best run ({settings.Difficulty.DisplayName()}): {best}"
                : "No campaign completed yet";
        }
    }
}
