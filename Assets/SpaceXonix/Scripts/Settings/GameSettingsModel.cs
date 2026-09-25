using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceXonix.Settings
{
    /// <summary>
    /// The player's persisted preferences and campaign high score, as pure state.
    /// Values are clamped on the way in, and every change raises <see cref="Changed"/>
    /// so the service can persist it and the UI can re-read it.
    /// </summary>
    public sealed class GameSettingsModel
    {
        public const float DefaultMasterVolume = 1f;
        public const float DefaultMusicVolume = .7f;
        public const float DefaultSfxVolume = 1f;

        private float masterVolume = DefaultMasterVolume;
        private float musicVolume = DefaultMusicVolume;
        private float sfxVolume = DefaultSfxVolume;
        private bool vibrationEnabled = true;
        private bool cameraShakeEnabled = true;
        private DifficultyMode difficulty = DifficultyMode.Hard;
        private string shipSkin = string.Empty;
        // One record per difficulty: Hard's modifier bonuses inflate its scores, so a shared
        // record would make an Easy run permanently uncompetitive.
        private readonly int[] campaignHighScores = new int[2];

        public event Action Changed;

        public float MasterVolume
        {
            get => masterVolume;
            set => Assign(ref masterVolume, Mathf.Clamp01(value));
        }

        public float MusicVolume
        {
            get => musicVolume;
            set => Assign(ref musicVolume, Mathf.Clamp01(value));
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set => Assign(ref sfxVolume, Mathf.Clamp01(value));
        }

        public bool VibrationEnabled
        {
            get => vibrationEnabled;
            set => Assign(ref vibrationEnabled, value);
        }

        public bool CameraShakeEnabled
        {
            get => cameraShakeEnabled;
            set => Assign(ref cameraShakeEnabled, value);
        }

        /// <summary>The difficulty the next run will start on. Remembered between sessions.</summary>
        public DifficultyMode Difficulty
        {
            get => difficulty;
            set => Assign(ref difficulty, value);
        }

        /// <summary>
        /// The id of the ship skin the player picked, or empty for the default ship. Like the
        /// difficulty it is a choice rather than a preference, so resetting the settings keeps it.
        /// </summary>
        public string ShipSkin
        {
            get => shipSkin;
            set
            {
                value ??= string.Empty;
                if (shipSkin == value) return;
                shipSkin = value;
                Changed?.Invoke();
            }
        }

        /// <summary>Best campaign score on the selected difficulty.</summary>
        public int CampaignHighScore => GetHighScore(difficulty);

        public int GetHighScore(DifficultyMode mode) => campaignHighScores[(int)mode];

        /// <summary>The effective volume a music source should play at, after the master volume.</summary>
        public float EffectiveMusicVolume => masterVolume * musicVolume;

        /// <summary>The effective volume a sound effect should play at, after the master volume.</summary>
        public float EffectiveSfxVolume => masterVolume * sfxVolume;

        /// <summary>Records a finished run on the selected difficulty. True only when it beat the best.</summary>
        public bool TrySetHighScore(int score) => TrySetHighScore(difficulty, score);

        public bool TrySetHighScore(DifficultyMode mode, int score)
        {
            if (score <= campaignHighScores[(int)mode]) return false;
            campaignHighScores[(int)mode] = score;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Restores a stored best without raising a new record, for loading from disk.</summary>
        public void LoadHighScore(DifficultyMode mode, int score) => campaignHighScores[(int)mode] = Mathf.Max(0, score);

        public void ResetToDefaults()
        {
            masterVolume = DefaultMasterVolume;
            musicVolume = DefaultMusicVolume;
            sfxVolume = DefaultSfxVolume;
            vibrationEnabled = true;
            cameraShakeEnabled = true;
            // Difficulty, the ship skin and the high scores are choices and records, not
            // preferences, so resetting the settings leaves them alone.
            Changed?.Invoke();
        }

        // Constrained to struct rather than IEquatable, because enums do not satisfy that constraint.
        private void Assign<T>(ref T field, T value) where T : struct
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            Changed?.Invoke();
        }
    }
}
