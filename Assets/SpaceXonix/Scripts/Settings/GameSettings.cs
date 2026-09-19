using System;
using UnityEngine;

namespace SpaceXonix.Settings
{
    /// <summary>
    /// Persistent settings service. Loads the player's preferences and campaign high score once,
    /// then writes them back whenever the model changes. Survives scene loads so the menu and the
    /// gameplay scene read the same instance.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class GameSettings : MonoBehaviour
    {
        private const string MasterVolumeKey = "spacexonix.audio.master";
        private const string MusicVolumeKey = "spacexonix.audio.music";
        private const string SfxVolumeKey = "spacexonix.audio.sfx";
        private const string VibrationKey = "spacexonix.haptics.vibration";
        private const string CameraShakeKey = "spacexonix.camera.shake";
        // The original key predates difficulty and always had modifiers on, so it stays Hard's record.
        private const string HardHighScoreKey = "spacexonix.campaign.highscore";
        private const string EasyHighScoreKey = "spacexonix.campaign.highscore.easy";
        private const string DifficultyKey = "spacexonix.campaign.difficulty";

        [Tooltip("Keeps this service alive across scene loads. Turn off for scene-local test rigs.")]
        [SerializeField] private bool persistAcrossScenes = true;

        private static GameSettings instance;
        private GameSettingsModel model;
        private ISettingsStore store;
        private bool suppressSave;

        /// <summary>The live settings, or null before any GameSettings has woken up.</summary>
        public static GameSettingsModel Current => instance != null ? instance.Model : null;
        public GameSettingsModel Model => model;
        public event Action<GameSettingsModel> SettingsChanged;

        private void Awake()
        {
            // Only a running game can have a stray duplicate to clean up; in the editor and in tests
            // several rigs may exist side by side, and destroying one here would be permanent.
            if (Application.isPlaying)
            {
                if (instance != null && instance != this)
                {
                    Destroy(gameObject);
                    return;
                }
                if (persistAcrossScenes && transform.parent == null) DontDestroyOnLoad(gameObject);
            }
            instance = this;
            Initialize(store ?? new PlayerPrefsSettingsStore());
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (model != null) model.Changed -= OnModelChanged;
        }

        /// <summary>Binds this service to a store and loads from it. Tests pass an in-memory store.</summary>
        public void Initialize(ISettingsStore settingsStore)
        {
            if (model != null) model.Changed -= OnModelChanged;
            store = settingsStore ?? new PlayerPrefsSettingsStore();
            model = new GameSettingsModel();
            Load();
            model.Changed += OnModelChanged;
        }

        public void Load()
        {
            if (model == null || store == null) return;
            // Loading must not write the just-read values straight back out.
            suppressSave = true;
            model.MasterVolume = store.GetFloat(MasterVolumeKey, GameSettingsModel.DefaultMasterVolume);
            model.MusicVolume = store.GetFloat(MusicVolumeKey, GameSettingsModel.DefaultMusicVolume);
            model.SfxVolume = store.GetFloat(SfxVolumeKey, GameSettingsModel.DefaultSfxVolume);
            model.VibrationEnabled = store.GetInt(VibrationKey, 1) != 0;
            model.CameraShakeEnabled = store.GetInt(CameraShakeKey, 1) != 0;
            model.Difficulty = (DifficultyMode)store.GetInt(DifficultyKey, (int)DifficultyMode.Hard);
            model.LoadHighScore(DifficultyMode.Hard, store.GetInt(HardHighScoreKey, 0));
            model.LoadHighScore(DifficultyMode.Easy, store.GetInt(EasyHighScoreKey, 0));
            suppressSave = false;
            SettingsChanged?.Invoke(model);
        }

        public void Save()
        {
            if (model == null || store == null) return;
            store.SetFloat(MasterVolumeKey, model.MasterVolume);
            store.SetFloat(MusicVolumeKey, model.MusicVolume);
            store.SetFloat(SfxVolumeKey, model.SfxVolume);
            store.SetInt(VibrationKey, model.VibrationEnabled ? 1 : 0);
            store.SetInt(CameraShakeKey, model.CameraShakeEnabled ? 1 : 0);
            store.SetInt(DifficultyKey, (int)model.Difficulty);
            store.SetInt(HardHighScoreKey, model.GetHighScore(DifficultyMode.Hard));
            store.SetInt(EasyHighScoreKey, model.GetHighScore(DifficultyMode.Easy));
            store.Save();
        }

        /// <summary>Records a finished campaign. Returns true when it set a new record.</summary>
        public bool ReportCampaignScore(int score) => model != null && model.TrySetHighScore(score);

        private void OnModelChanged()
        {
            if (suppressSave) return;
            Save();
            SettingsChanged?.Invoke(model);
        }
    }
}
