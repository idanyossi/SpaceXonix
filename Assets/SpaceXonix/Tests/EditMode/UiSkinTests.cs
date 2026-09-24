using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpaceXonix.Tests.EditMode
{
    /// <summary>
    /// Guards the ship UI skin: a button, panel or label added without re-running
    /// SpaceXonix > Apply UI Skin would show up in the old flat style next to the new one.
    /// </summary>
    public sealed class UiSkinTests
    {
        private const string SettingsOverlay = "Assets/SpaceXonix/Prefabs/UI/SettingsOverlay.prefab";
        private static readonly string[] Scenes =
        {
            "Assets/SpaceXonix/Scenes/MainMenu.unity",
            "Assets/SpaceXonix/Scenes/Game.unity",
        };

        [Test]
        public void SettingsOverlay_IsSkinned()
        {
            var root = PrefabUtility.LoadPrefabContents(SettingsOverlay);
            try { AssertSkinned(root, SettingsOverlay); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [TestCaseSource(nameof(Scenes))]
        public void Scene_IsSkinned(string path)
        {
            // Reuse the scene if it is the one open in the editor, and only close what the test opened.
            var alreadyOpen = SceneManager.GetSceneByPath(path).isLoaded;
            var scene = alreadyOpen ? SceneManager.GetSceneByPath(path) : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponentInChildren<Canvas>(true) != null) AssertSkinned(root, path);
            }
            finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void UiSprites_AreNineSliced()
        {
            foreach (var name in new[] { "UI_Panel", "UI_Button", "UI_ButtonPressed", "UI_Slot" })
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/SpaceXonix/Art/Generated/UI/{name}.png");
                Assert.That(sprite, Is.Not.Null, name);
                Assert.That(sprite.border.x, Is.GreaterThan(0f), $"{name} has no nine-slice border");
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), $"{name} would blur");
            }
        }

        private static void AssertSkinned(GameObject root, string where)
        {
            var problems = new List<string>();
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var image = button.targetGraphic as Image;
                if (image == null || image.sprite == null || !image.sprite.name.StartsWith("UI_Button"))
                    problems.Add($"button {button.name}");
                else if (button.transition != Selectable.Transition.SpriteSwap || button.spriteState.pressedSprite == null)
                    problems.Add($"button {button.name} has no pressed state");
            }
            foreach (var text in root.GetComponentsInChildren<Text>(true))
                if (text.font == null || !text.font.name.StartsWith("Kenney"))
                    problems.Add($"text {text.transform.parent.name}/{text.name}");
            Assert.That(problems, Is.Empty, $"Unskinned UI in {where}; run SpaceXonix > Apply UI Skin.");
        }
    }
}
