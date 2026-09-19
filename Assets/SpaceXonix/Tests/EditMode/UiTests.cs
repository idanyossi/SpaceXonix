using NUnit.Framework;
using SpaceXonix.Settings;
using SpaceXonix.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class UiTests
    {
        [Test]
        public void SafeArea_FullScreenAreaProducesTheFullRect()
        {
            SafeAreaFitter.CalculateAnchors(new Rect(0f, 0f, 1080f, 1920f), new Vector2Int(1080, 1920), true, true,
                out var min, out var max);
            Assert.That(min, Is.EqualTo(Vector2.zero));
            Assert.That(max, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void SafeArea_NotchAndGestureBarInsetTheRect()
        {
            // A 1080x1920 portrait phone with a 120px notch on top and a 60px gesture bar below.
            SafeAreaFitter.CalculateAnchors(new Rect(0f, 60f, 1080f, 1740f), new Vector2Int(1080, 1920), true, true,
                out var min, out var max);
            Assert.That(min.x, Is.Zero);
            Assert.That(max.x, Is.EqualTo(1f));
            Assert.That(min.y, Is.EqualTo(60f / 1920f).Within(.0001f));
            Assert.That(max.y, Is.EqualTo(1800f / 1920f).Within(.0001f));
        }

        [Test]
        public void SafeArea_CanIgnoreAnAxisAndSurvivesBadPlatformData()
        {
            var safeArea = new Rect(30f, 60f, 1020f, 1740f);
            var screen = new Vector2Int(1080, 1920);

            SafeAreaFitter.CalculateAnchors(safeArea, screen, false, true, out var min, out var max);
            Assert.That(min.x, Is.Zero, "the horizontal inset is ignored when asked");
            Assert.That(max.x, Is.EqualTo(1f));
            Assert.That(min.y, Is.GreaterThan(0f), "the vertical inset still applies");

            SafeAreaFitter.CalculateAnchors(safeArea, Vector2Int.zero, true, true, out min, out max);
            Assert.That(min, Is.EqualTo(Vector2.zero), "a zero-sized screen falls back rather than dividing by zero");
            Assert.That(max, Is.EqualTo(Vector2.one));

            SafeAreaFitter.CalculateAnchors(new Rect(0f, 0f, 0f, 0f), screen, true, true, out min, out max);
            Assert.That(max.x, Is.GreaterThan(min.x), "a degenerate area never collapses the panel");
            Assert.That(max.y, Is.GreaterThan(min.y));
        }

        [Test]
        public void SettingsPanel_BindsBothWaysWithoutFeedingBackOnItself()
        {
            var root = new GameObject("SettingsPanelFixture");
            root.SetActive(false);
            try
            {
                var panel = root.AddComponent<SettingsPanel>();
                var master = NewSlider(root, "Master");
                var music = NewSlider(root, "Music");
                var shake = root.AddComponent<Toggle>();

                Set(panel, "masterVolumeSlider", master);
                Set(panel, "musicVolumeSlider", music);
                Set(panel, "cameraShakeToggle", shake);
                root.SetActive(true);

                var model = new GameSettingsModel();
                var changes = 0;
                model.Changed += () => changes++;
                panel.Bind(model);

                Assert.That(master.value, Is.EqualTo(model.MasterVolume).Within(.0001f), "binding pushes the model into the controls");
                Assert.That(music.value, Is.EqualTo(model.MusicVolume).Within(.0001f));
                Assert.That(changes, Is.Zero, "the initial refresh must not write back into the model");

                // A player dragging a slider writes through.
                music.value = .25f;
                Assert.That(model.MusicVolume, Is.EqualTo(.25f).Within(.0001f));
                Assert.That(changes, Is.EqualTo(1));

                shake.isOn = false;
                Assert.That(model.CameraShakeEnabled, Is.False);

                // A change from elsewhere (the pause menu, a reset) refreshes the controls without echoing.
                var changesBefore = changes;
                model.MasterVolume = .4f;
                Assert.That(master.value, Is.EqualTo(.4f).Within(.0001f));
                Assert.That(changes, Is.EqualTo(changesBefore + 1), "the refresh itself raised no extra change");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Slider NewSlider(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(target, value);
    }
}
