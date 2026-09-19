using SpaceXonix.Campaign;
using SpaceXonix.Core;
using SpaceXonix.Input;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// The in-game pause menu: Resume, Restart Campaign, Settings and Main Menu.
    /// Pausing routes through <see cref="GameManager.SetPaused"/> so the existing time-scale pause,
    /// input gating and damage rejection all behave exactly as they do for the ability pause.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private InputRouter inputRouter;
        [SerializeField] private CampaignManager campaignManager;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private SettingsPanel settingsPanel;

        public bool IsOpen { get; private set; }

        private void OnEnable()
        {
            if (inputRouter != null) inputRouter.PauseToggleRequested += Toggle;
            if (gameManager != null) gameManager.GameOver += ForceClose;
            if (pauseButton != null) pauseButton.onClick.AddListener(Toggle);
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (restartButton != null) restartButton.onClick.AddListener(RestartCampaign);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
            ApplyOpenState(false);
        }

        private void OnDisable()
        {
            if (inputRouter != null) inputRouter.PauseToggleRequested -= Toggle;
            if (gameManager != null) gameManager.GameOver -= ForceClose;
            if (pauseButton != null) pauseButton.onClick.RemoveListener(Toggle);
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
            if (restartButton != null) restartButton.onClick.RemoveListener(RestartCampaign);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(GoToMainMenu);
        }

        public void Toggle()
        {
            if (IsOpen) Resume();
            else Open();
        }

        /// <summary>Opens the menu and pauses the game. Refused once the run is over or between stages.</summary>
        public bool Open()
        {
            if (IsOpen || !CanPause()) return false;
            ApplyOpenState(true);
            if (gameManager != null) gameManager.SetPaused(true);
            return true;
        }

        public void Resume()
        {
            if (!IsOpen) return;
            ApplyOpenState(false);
            if (gameManager != null) gameManager.SetPaused(false);
        }

        public void RestartCampaign()
        {
            Resume();
            if (campaignManager != null) campaignManager.RetryCampaign();
        }

        public void GoToMainMenu()
        {
            Resume();
            SceneRouter.GoToMainMenu();
        }

        public void OpenSettings()
        {
            if (settingsPanel != null) settingsPanel.Open();
        }

        /// <summary>Pausing only makes sense while a stage is actually being played.</summary>
        private bool CanPause() => gameManager == null || gameManager.CurrentState == GameplayState.Playing ||
                                   gameManager.CurrentState == GameplayState.Respawning;

        private void ForceClose()
        {
            if (!IsOpen) return;
            ApplyOpenState(false);
            if (gameManager != null) gameManager.SetPaused(false);
        }

        private void ApplyOpenState(bool open)
        {
            IsOpen = open;
            if (panelRoot != null) panelRoot.SetActive(open);
            if (!open && settingsPanel != null) settingsPanel.Close();
            if (pauseButton != null) pauseButton.gameObject.SetActive(!open);
        }
    }
}
