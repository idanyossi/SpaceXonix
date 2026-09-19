using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Unity starts Play Mode from whichever scene is open, ignoring the Build Settings order, so
    /// pressing Play while Game.unity is open drops straight into stage 1 and skips the menu.
    /// This points Play Mode at the Boot scene instead, so the editor follows the real startup path.
    ///
    /// Toggle it off from SpaceXonix > Always Play From Boot Scene when iterating on one scene,
    /// where clicking through the menu on every Play would just be in the way.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromBootScene
    {
        private const string MenuPath = "SpaceXonix/Always Play From Boot Scene";
        private const string BootScenePath = "Assets/SpaceXonix/Scenes/Boot.unity";
        // Per-user and per-project, so this never fights another machine's preference.
        private static readonly string PreferenceKey = $"SpaceXonix.PlayFromBoot.{Application.productName}";

        static PlayFromBootScene()
        {
            // Deferred: asset loading is unreliable while the editor is still compiling and reloading.
            EditorApplication.delayCall += Apply;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PreferenceKey, true);
            set => EditorPrefs.SetBool(PreferenceKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void Apply()
        {
            if (!Enabled)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }
            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
            if (boot == null)
            {
                // Not an error worth failing over: the setting simply cannot apply without the scene.
                Debug.LogWarning($"Play From Boot Scene is on, but {BootScenePath} was not found. Play Mode will start from the open scene.");
                EditorSceneManager.playModeStartScene = null;
                return;
            }
            EditorSceneManager.playModeStartScene = boot;
        }
    }
}
