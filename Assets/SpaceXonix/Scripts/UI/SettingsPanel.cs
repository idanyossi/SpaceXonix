using SpaceXonix.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// Two-way binding between the settings controls and <see cref="GameSettingsModel"/>.
    /// Writes from the controls into the model and refreshes the controls when the model changes,
    /// guarding against the feedback loop that would otherwise bounce between the two.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private Toggle cameraShakeToggle;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resetButton;

        private GameSettingsModel model;
        private bool refreshing;

        private void OnEnable()
        {
            Bind(GameSettings.Current);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (resetButton != null) resetButton.onClick.AddListener(ResetToDefaults);
        }

        private void OnDisable()
        {
            if (model != null) model.Changed -= Refresh;
            model = null;
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (resetButton != null) resetButton.onClick.RemoveListener(ResetToDefaults);
            RemoveControlListeners();
        }

        /// <summary>Binds to a settings model. Public so tests can supply one without a service.</summary>
        public void Bind(GameSettingsModel settings)
        {
            if (model != null) model.Changed -= Refresh;
            RemoveControlListeners();
            model = settings;
            if (model == null) return;
            AddControlListeners();
            model.Changed += Refresh;
            Refresh();
        }

        public void Close() => gameObject.SetActive(false);

        public void Open()
        {
            gameObject.SetActive(true);
            Refresh();
        }

        public void ResetToDefaults() => model?.ResetToDefaults();

        /// <summary>Pushes the model's values into the controls without echoing them back.</summary>
        public void Refresh()
        {
            if (model == null) return;
            refreshing = true;
            if (masterVolumeSlider != null) masterVolumeSlider.value = model.MasterVolume;
            if (musicVolumeSlider != null) musicVolumeSlider.value = model.MusicVolume;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = model.SfxVolume;
            // Silently, so opening the panel does not sound like the player clicked the toggles.
            if (vibrationToggle != null) vibrationToggle.SetIsOnWithoutNotify(model.VibrationEnabled);
            if (cameraShakeToggle != null) cameraShakeToggle.SetIsOnWithoutNotify(model.CameraShakeEnabled);
            refreshing = false;
        }

        private void AddControlListeners()
        {
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicChanged);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxChanged);
            if (vibrationToggle != null) vibrationToggle.onValueChanged.AddListener(OnVibrationChanged);
            if (cameraShakeToggle != null) cameraShakeToggle.onValueChanged.AddListener(OnCameraShakeChanged);
        }

        private void RemoveControlListeners()
        {
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(OnMasterChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveListener(OnMusicChanged);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxChanged);
            if (vibrationToggle != null) vibrationToggle.onValueChanged.RemoveListener(OnVibrationChanged);
            if (cameraShakeToggle != null) cameraShakeToggle.onValueChanged.RemoveListener(OnCameraShakeChanged);
        }

        private void OnMasterChanged(float value) { if (!refreshing && model != null) model.MasterVolume = value; }
        private void OnMusicChanged(float value) { if (!refreshing && model != null) model.MusicVolume = value; }
        private void OnSfxChanged(float value) { if (!refreshing && model != null) model.SfxVolume = value; }
        private void OnVibrationChanged(bool value) { if (!refreshing && model != null) model.VibrationEnabled = value; }
        private void OnCameraShakeChanged(bool value) { if (!refreshing && model != null) model.CameraShakeEnabled = value; }
    }
}
