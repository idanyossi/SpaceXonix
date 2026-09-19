using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceXonix.UI
{
    /// <summary>The scenes in Build Settings. Kept in one place so nothing hard-codes a scene name.</summary>
    public static class SceneNames
    {
        public const string Boot = "Boot";
        public const string MainMenu = "MainMenu";
        public const string Game = "Game";
    }

    /// <summary>
    /// Moves between the Boot, Main Menu and Game scenes. Always restores the time scale first,
    /// because leaving a paused game would otherwise carry the freeze into the next scene.
    /// </summary>
    public static class SceneRouter
    {
        public static void GoToMainMenu() => Load(SceneNames.MainMenu);

        public static void StartCampaign() => Load(SceneNames.Game);

        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void Load(string sceneName)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }
    }
}
