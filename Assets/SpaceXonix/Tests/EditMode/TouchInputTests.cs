using System;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class TouchInputTests
    {
        [Test]
        public void Swipe_ResolvesToTheAxisThatMovedFurthest()
        {
            var origin = new Vector2(500f, 500f);
            Assert.That(Resolve(origin, origin + new Vector2(120f, 20f)), Is.EqualTo(CardinalDirection.Right));
            Assert.That(Resolve(origin, origin + new Vector2(-120f, 20f)), Is.EqualTo(CardinalDirection.Left));
            Assert.That(Resolve(origin, origin + new Vector2(20f, 120f)), Is.EqualTo(CardinalDirection.Up));
            Assert.That(Resolve(origin, origin + new Vector2(20f, -120f)), Is.EqualTo(CardinalDirection.Down));

            // A diagonal is not ambiguous in practice; the rule is documented as horizontal wins.
            Assert.That(Resolve(origin, origin + new Vector2(100f, 100f)), Is.EqualTo(CardinalDirection.Right));
        }

        [Test]
        public void Swipe_IgnoresMovementShorterThanTheThreshold()
        {
            var origin = new Vector2(400f, 400f);
            Assert.That(SwipeModel.TryResolve(origin, origin, 50f, out _), Is.False, "a tap never steers");
            Assert.That(SwipeModel.TryResolve(origin, origin + new Vector2(30f, 20f), 50f, out _), Is.False,
                "a shaky finger below the threshold is ignored");
            Assert.That(SwipeModel.TryResolve(origin, origin + new Vector2(0f, 51f), 50f, out var direction), Is.True);
            Assert.That(direction, Is.EqualTo(CardinalDirection.Up));
        }

        [Test]
        public void Threshold_ScalesWithTheScreensShorterEdge()
        {
            // Portrait phone: the short edge is the width.
            Assert.That(SwipeModel.ThresholdPixels(.03f, 1080, 1920), Is.EqualTo(32.4f).Within(.01f));
            // Landscape tablet: the short edge is now the height, so the gesture stays comparable.
            Assert.That(SwipeModel.ThresholdPixels(.03f, 1920, 1080), Is.EqualTo(32.4f).Within(.01f));
            Assert.That(SwipeModel.ThresholdPixels(.03f, 0, 0), Is.GreaterThan(0f), "a zero screen never yields a zero threshold");
        }

        [Test]
        public void SwipeSource_SteersThroughTheSameRouterAsTheKeyboard()
        {
            using (var fixture = new Fixture())
            {
                fixture.Router.SetGameplayInputEnabled(true);
                CardinalDirection? steered = null;
                fixture.Router.DirectionChanged += value => steered = value;

                var origin = new Vector2(300f, 300f);
                Assert.That(fixture.Touch.ResolveSwipe(origin, origin + new Vector2(5f, 5f)), Is.False);
                Assert.That(steered, Is.Null, "a tiny drag produces no direction at all");

                Assert.That(fixture.Touch.ResolveSwipe(origin, origin + new Vector2(0f, 400f)), Is.True);
                Assert.That(steered, Is.EqualTo(CardinalDirection.Up));
                Assert.That(fixture.Router.CurrentDirection, Is.EqualTo(CardinalDirection.Up));
            }
        }

        [Test]
        public void SwipeSource_IsGatedWithTheRestOfGameplayInput()
        {
            using (var fixture = new Fixture())
            {
                // Respawns and transitions disable gameplay input; a swipe must obey that too.
                fixture.Router.SetGameplayInputEnabled(false);
                var origin = new Vector2(300f, 300f);
                Assert.That(fixture.Touch.ResolveSwipe(origin, origin + new Vector2(0f, 400f)), Is.False);
            }
        }

        [Test]
        public void TouchButtons_RequestAbilityAndPowerThroughTheRouter()
        {
            using (var fixture = new Fixture())
            {
                fixture.Router.SetGameplayInputEnabled(true);
                var abilityRequests = 0;
                var powerRequests = 0;
                fixture.Router.AbilityRequested += () => abilityRequests++;
                fixture.Router.PowerShotRequested += () => powerRequests++;

                fixture.AbilityButton.onClick.Invoke();
                fixture.PowerButton.onClick.Invoke();

                Assert.That(abilityRequests, Is.EqualTo(1));
                Assert.That(powerRequests, Is.EqualTo(1));
            }
        }

        [Test]
        public void TouchButtons_AreHiddenOnDesktopAndShownWhenForced()
        {
            using (var fixture = new Fixture())
            {
                fixture.Controls.ApplyVisibility(false);
                Assert.That(fixture.AbilityButton.gameObject.activeSelf, Is.False);
                Assert.That(fixture.PowerButton.gameObject.activeSelf, Is.False);

                fixture.Controls.ApplyVisibility(true);
                Assert.That(fixture.Controls.ButtonsVisible, Is.True);
                Assert.That(fixture.AbilityButton.gameObject.activeSelf, Is.True);
                Assert.That(fixture.PowerButton.gameObject.activeSelf, Is.True);
            }
        }

        private static CardinalDirection Resolve(Vector2 start, Vector2 end)
        {
            Assert.That(SwipeModel.TryResolve(start, end, 50f, out var direction), Is.True);
            return direction;
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("TouchFixture");
            public readonly InputRouter Router;
            public readonly TouchInputSource Touch;
            public readonly TouchControls Controls;
            public readonly Button AbilityButton;
            public readonly Button PowerButton;

            public Fixture()
            {
                root.SetActive(false);
                Router = root.AddComponent<InputRouter>();
                Touch = root.AddComponent<TouchInputSource>();
                Set(Touch, "inputRouter", Router);

                AbilityButton = NewButton("Ability");
                PowerButton = NewButton("Power");
                Controls = root.AddComponent<TouchControls>();
                Set(Controls, "inputRouter", Router);
                Set(Controls, "abilityButton", AbilityButton);
                Set(Controls, "powerButton", PowerButton);
                root.SetActive(true);
                // EditMode does not run OnEnable, which is where the clicks are hooked up.
                typeof(TouchControls).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(Controls, null);
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);

            private Button NewButton(string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(root.transform, false);
                return go.AddComponent<Button>();
            }

            private static void Set(object target, string name, object value) =>
                target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        }
    }
}
