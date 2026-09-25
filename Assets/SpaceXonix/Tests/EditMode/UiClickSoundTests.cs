using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Audio;
using SpaceXonix.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class UiClickSoundTests
    {
        [Test]
        public void MenuButtonsAndToggles_Click_ButGameplayButtonsDoNot()
        {
            var root = new GameObject("Canvas", typeof(RectTransform));
            var audioRoot = new GameObject("Audio");
            var library = ScriptableObject.CreateInstance<SfxLibrary>();
            var definition = ScriptableObject.CreateInstance<SfxDefinition>();
            var clip = AudioClip.Create("click", 441, 1, 44100, false);
            try
            {
                definition.sfx = GameSfx.UiInteraction;
                definition.clips = new[] { clip };
                library.definitions = new[] { definition };
                var manager = audioRoot.AddComponent<AudioManager>();
                manager.SetLibrary(library);
                Invoke(manager, "Awake");

                var menuButton = Child<Button>(root, "Start");
                var toggle = Child<Toggle>(root, "Vibration");
                var powerButton = Child<Button>(root, "Power");
                var touch = root.AddComponent<TouchControls>();
                typeof(TouchControls).GetField("powerButton", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(touch, powerButton);

                var clicks = root.AddComponent<UiClickSound>();
                Invoke(clicks, "Awake");
                Assert.That(clicks.HookedCount, Is.EqualTo(2), "the menu button and the toggle, not the power button");

                menuButton.onClick.Invoke();
                Assert.That(manager.LastPlayed, Is.EqualTo(GameSfx.UiInteraction));
                var played = manager.PlayedCount;

                powerButton.onClick.Invoke();
                Assert.That(manager.PlayedCount, Is.EqualTo(played), "the power shot has its own sound");

                toggle.isOn = !toggle.isOn;
                Assert.That(manager.PlayedCount, Is.EqualTo(played + 1), "flipping a toggle clicks too");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(audioRoot);
                Object.DestroyImmediate(library);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(clip);
            }
        }

        [TestCase("Assets/SpaceXonix/Scenes/MainMenu.unity")]
        [TestCase("Assets/SpaceXonix/Scenes/Game.unity")]
        public void EveryRootCanvas_PlaysClicks(string path)
        {
            var alreadyOpen = SceneManager.GetSceneByPath(path).isLoaded;
            var scene = alreadyOpen ? SceneManager.GetSceneByPath(path) : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (canvas.isRootCanvas)
                        Assert.That(canvas.GetComponent<UiClickSound>(), Is.Not.Null, $"{canvas.name} in {path} would be silent on click");
            }
            finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
        }

        private static T Child<T>(GameObject parent, string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<T>();
        }

        private static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    }
}
