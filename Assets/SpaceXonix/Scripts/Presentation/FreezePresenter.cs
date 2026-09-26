using System.Collections.Generic;
using SpaceXonix.Enemies;
using SpaceXonix.PowerUps;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Makes Freeze look frozen: every alien turns icy and stops animating, with a faint shimmer, and
    /// a frost border fades in round the screen. Aliens that arrive during the freeze are caught too,
    /// and everything gets its own colours back when it ends. Presentation only.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class FreezePresenter : MonoBehaviour
    {
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private EnemyManager enemyManager;
        [Tooltip("The frost border drawn round the screen while Freeze lasts.")]
        [SerializeField] private Graphic frostOverlay;
        [SerializeField] private Color iceColour = new Color(.62f, .9f, 1f);
        [SerializeField, Range(0f, 1f)] private float overlayAlpha = .75f;
        [SerializeField, Min(.01f)] private float fadeSeconds = .3f;

        private sealed class Frozen
        {
            public SpriteRenderer Renderer;
            public Color Colour;
            public SpriteFrameAnimator Animator;
        }

        private readonly Dictionary<EnemyController, Frozen> frozen = new Dictionary<EnemyController, Frozen>();
        private float overlay;

        public bool IsFrozen { get; private set; }
        public int FrozenCount => frozen.Count;

        private void OnEnable()
        {
            if (powerUpManager == null) return;
            powerUpManager.EffectStarted += OnEffectStarted;
            powerUpManager.EffectEnded += OnEffectEnded;
        }

        private void OnDisable()
        {
            if (powerUpManager != null)
            {
                powerUpManager.EffectStarted -= OnEffectStarted;
                powerUpManager.EffectEnded -= OnEffectEnded;
            }
            Thaw();
        }

        private void OnEffectStarted(PowerUpType type) { if (type == PowerUpType.Freeze) IsFrozen = true; }

        private void OnEffectEnded(PowerUpType type) { if (type == PowerUpType.Freeze) Thaw(); }

        // Late, so the ice wins over anything that tinted the aliens this frame.
        private void LateUpdate() => Tick(Time.unscaledDeltaTime, Time.unscaledTime);

        /// <summary>Applies the ice and fades the border. Public so tests can drive it.</summary>
        public void Tick(float deltaTime, float time)
        {
            overlay = Mathf.MoveTowards(overlay, IsFrozen ? 1f : 0f, Mathf.Max(0f, deltaTime) / fadeSeconds);
            if (frostOverlay != null)
            {
                frostOverlay.enabled = overlay > 0f;
                var c = frostOverlay.color; c.a = overlay * overlayAlpha; frostOverlay.color = c;
            }
            if (!IsFrozen || enemyManager == null) return;
            var shimmer = .5f + .5f * Mathf.Sin(time * Mathf.PI * 2f * .7f);
            var enemies = enemyManager.ActiveEnemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null) continue;
                if (!frozen.TryGetValue(enemy, out var entry))
                {
                    var renderer = enemy.GetComponentInChildren<SpriteRenderer>();
                    if (renderer == null) continue;
                    entry = new Frozen { Renderer = renderer, Colour = renderer.color, Animator = renderer.GetComponent<SpriteFrameAnimator>() };
                    frozen.Add(enemy, entry);
                }
                entry.Renderer.color = Color.Lerp(iceColour, Color.white, shimmer * .25f);
                if (entry.Animator != null) entry.Animator.enabled = false;
            }
        }

        private void Thaw()
        {
            IsFrozen = false;
            foreach (var entry in frozen.Values)
            {
                if (entry.Renderer != null) entry.Renderer.color = entry.Colour;
                if (entry.Animator != null) entry.Animator.enabled = true;
            }
            frozen.Clear();
        }
    }
}
