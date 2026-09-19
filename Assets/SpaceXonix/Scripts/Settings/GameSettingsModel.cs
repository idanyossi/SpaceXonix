using System;
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
        private int campaignHighScore;

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

        /// <summary>Best campaign score so far. Raised only through <see cref="TrySetHighScore"/>.</summary>
        public int CampaignHighScore => campaignHighScore;

        /// <summary>The effective volume a music source should play at, after the master volume.</summary>
        public float EffectiveMusicVolume => masterVolume * musicVolume;

        /// <summary>The effective volume a sound effect should play at, after the master volume.</summary>
        public float EffectiveSfxVolume => masterVolume * sfxVolume;

        /// <summary>Records a finished run. Returns true only when it beat the stored best.</summary>
        public bool TrySetHighScore(int score)
        {
            if (score <= campaignHighScore) return false;
            campaignHighScore = score;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Restores the stored best without raising a new record, for loading from disk.</summary>
        public void LoadHighScore(int score) => campaignHighScore = Mathf.Max(0, score);

        public void ResetToDefaults()
        {
            masterVolume = DefaultMasterVolume;
            musicVolume = DefaultMusicVolume;
            sfxVolume = DefaultSfxVolume;
            vibrationEnabled = true;
            cameraShakeEnabled = true;
            // The high score is a record, not a preference, so resetting settings never clears it.
            Changed?.Invoke();
        }

        private void Assign<T>(ref T field, T value) where T : IEquatable<T>
        {
            if (field.Equals(value)) return;
            field = value;
            Changed?.Invoke();
        }
    }
}
