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
    /// Moves between the Boot, Main Menu and Game scenes, fading through black (<see cref="ScreenFader"/>).
    /// The time scale is restored before the next scene loads, because leaving a paused game would
    /// otherwise carry the freeze into it.
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
            if (!Application.isPlaying) Time.timeScale = 1f;
            ScreenFader.LoadScene(sceneName);
        }
    }
}
