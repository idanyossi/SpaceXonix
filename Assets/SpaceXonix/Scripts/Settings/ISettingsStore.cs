using UnityEngine;

namespace SpaceXonix.Settings
{
    /// <summary>
    /// Where settings are persisted. Abstracted so tests can run against memory instead of
    /// the editor's real PlayerPrefs, which are shared across every test in the session.
    /// </summary>
    public interface ISettingsStore
    {
        float GetFloat(string key, float defaultValue);
        void SetFloat(string key, float value);
        int GetInt(string key, int defaultValue);
        void SetInt(string key, int value);
        string GetString(string key, string defaultValue);
        void SetString(string key, string value);
        void Save();
    }

    /// <summary>The real store: Unity's PlayerPrefs, as the GDD requires.</summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public float GetFloat(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public string GetString(string key, string defaultValue) => PlayerPrefs.GetString(key, defaultValue);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Save() => PlayerPrefs.Save();
    }
}
