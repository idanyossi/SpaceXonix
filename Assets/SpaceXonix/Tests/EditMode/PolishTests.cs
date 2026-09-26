using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Boss;
using SpaceXonix.Pooling;
using SpaceXonix.Presentation;
using SpaceXonix.PowerUps;
using SpaceXonix.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.Tests.EditMode
{
    /// <summary>The Phase 20 polish: capture flash, Freeze and Arena Tilt presentation, the boss's end, and panel transitions.</summary>
    public sealed class PolishTests
    {
        [Test]
        public void Capture_FlashesTheNewTerritoryOnceAndFades()
        {
            var root = new GameObject("Board");
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                root.SetActive(false);
                var board = root.AddComponent<BoardManager>();
                var view = root.AddComponent<BoardRenderer>();
                Set(board, "boardRenderer", view);
                Set(view, "flashMaterial", material);
                root.SetActive(true);
                board.Initialize();
                view.Initialize(board);

                // A column cut: the smaller side and the trail rise, and flash.
                for (var row = 1; row < board.Rows - 1; row++) board.Model.MoveTo(new GridCoordinate(10, row));
                board.Model.MoveTo(new GridCoordinate(10, board.Rows - 1));

                Assert.That(view.FlashCellCount, Is.GreaterThan(0), "the new territory flashes");
                Assert.That(view.FlashStrength, Is.EqualTo(1f));
                view.Tick(.2f);
                Assert.That(view.FlashStrength, Is.InRange(.01f, .99f), "and fades");
                view.Tick(1f);
                Assert.That(view.FlashStrength, Is.Zero, "once, briefly");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Freeze_FadesTheFrostBorderInAndOutWithTheEffect()
        {
            var root = new GameObject("Freeze");
            try
            {
                var frost = new GameObject("Frost", typeof(RectTransform)).AddComponent<Image>();
                frost.transform.SetParent(root.transform, false);
                frost.color = new Color(1f, 1f, 1f, 0f);
                var presenter = root.AddComponent<FreezePresenter>();
                Set(presenter, "frostOverlay", frost);

                Invoke(presenter, "OnEffectStarted", PowerUpType.Freeze);
                Assert.That(presenter.IsFrozen, Is.True);
                presenter.Tick(1f, 0f);
                Assert.That(frost.enabled, Is.True);
                Assert.That(frost.color.a, Is.GreaterThan(.5f), "the frost border shows while frozen");

                Invoke(presenter, "OnEffectEnded", PowerUpType.Freeze);
                presenter.Tick(1f, 0f);
                Assert.That(presenter.IsFrozen, Is.False);
                Assert.That(frost.enabled, Is.False, "and clears when the freeze ends");

                Invoke(presenter, "OnEffectStarted", PowerUpType.Shield);
                Assert.That(presenter.IsFrozen, Is.False, "other abilities do not freeze the screen");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void Tilt_StreamsChevronsOnTheDownhillEdge(float drift)
        {
            var root = new GameObject("Tilt", typeof(RectTransform));
            try
            {
                var powerUps = root.AddComponent<PowerUpManager>();
                typeof(PowerUpManager).GetField("<TiltDrift>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(powerUps, new Vector2(drift, 0f));
                var streaks = new GameObject("Streaks", typeof(RectTransform)).AddComponent<RawImage>();
                streaks.transform.SetParent(root.transform, false);
                streaks.rectTransform.sizeDelta = new Vector2(150f, 0f);
                var presenter = root.AddComponent<TiltPresenter>();
                Set(presenter, "powerUpManager", powerUps);
                Set(presenter, "streaks", streaks);
                Invoke(presenter, "Awake");

                Invoke(presenter, "OnEffectStarted", PowerUpType.ArenaTilt);
                presenter.Tick(1f, 0f);
                Assert.That(presenter.Side, Is.EqualTo((int)drift));
                Assert.That(streaks.enabled, Is.True);
                Assert.That(streaks.rectTransform.anchorMin.x, Is.EqualTo(drift < 0f ? 0f : 1f), "on the side the aliens slide to");
                Assert.That(streaks.rectTransform.localScale.x, Is.EqualTo(drift < 0f ? -1f : 1f), "pointing outward");

                Invoke(presenter, "OnEffectEnded", PowerUpType.ArenaTilt);
                presenter.Tick(1f, 0f);
                Assert.That(streaks.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BossDeath_ChainsExplosionsThenABigBlastAndHidesTheCore()
        {
            var root = new GameObject("Boss");
            var prefab = new GameObject("ExplosionPrefab");
            try
            {
                var burst = prefab.AddComponent<SpriteBurst>();
                var frames = new Sprite[5];
                for (var i = 0; i < frames.Length; i++) frames[i] = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
                Set(burst, "frames", frames);
                prefab.SetActive(false);
                var explosions = root.AddComponent<SpriteBurstPresenter>();
                Set(explosions, "poolService", root.AddComponent<PoolService>());
                Set(explosions, "explosionPrefab", prefab);

                var body = new GameObject("Body").transform;
                body.SetParent(root.transform, false);
                var boss = root.AddComponent<BossController>();
                Set(boss, "bodyVisual", body);
                var flash = new GameObject("Flash", typeof(RectTransform)).AddComponent<Image>();
                flash.transform.SetParent(root.transform, false);

                var sequence = root.AddComponent<BossDeathSequence>();
                Set(sequence, "boss", boss);
                Set(sequence, "explosions", explosions);
                Set(sequence, "flash", flash);
                Set(sequence, "randomSeed", 7);

                sequence.Play();
                Assert.That(sequence.IsPlaying, Is.True);
                for (var t = 0f; t < 1.5f; t += .05f) sequence.Tick(.05f);
                Assert.That(sequence.BurstCount, Is.GreaterThan(8), "a chain of explosions across the core");
                Assert.That(body.gameObject.activeSelf, Is.True, "the core is still there during the chain");

                for (var t = 0f; t < sequence.Duration; t += .05f) sequence.Tick(.05f);
                Assert.That(body.gameObject.activeSelf, Is.False, "the final blast removes the core");
                Assert.That(sequence.IsPlaying, Is.False);
                Assert.That(flash.enabled, Is.False, "the flash has faded");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Panels_FadeAndSettleInWhenOpened()
        {
            var overlay = new GameObject("TestOverlay", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                overlay.SetActive(false);
                var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
                panel.SetParent(overlay.transform, false);
                var transition = overlay.AddComponent<PanelTransition>();
                Set(transition, "scaleTarget", panel);
                var group = overlay.GetComponent<CanvasGroup>();

                overlay.SetActive(true);
                Invoke(transition, "OnEnable");
                Assert.That(group.alpha, Is.Zero, "it starts invisible");
                Assert.That(panel.localScale.x, Is.LessThan(1f), "and slightly small");
                transition.Advance(1f);
                Assert.That(group.alpha, Is.EqualTo(1f));
                Assert.That(panel.localScale.x, Is.EqualTo(1f));
                Assert.That(transition.IsPlaying, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(overlay);
            }
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static void Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args.Length == 0 ? null : args);
    }
}
