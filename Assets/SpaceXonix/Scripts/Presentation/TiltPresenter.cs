using SpaceXonix.PowerUps;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Shows which way Arena Tilt is tipping the board: a band of chevrons streams along the screen
    /// edge the aliens are sliding toward, fading in and out with the effect. The camera's roll
    /// already shows that the board tilts; this shows where everything is going. Presentation only.
    /// </summary>
    public sealed class TiltPresenter : MonoBehaviour
    {
        [SerializeField] private PowerUpManager powerUpManager;
        [Tooltip("The streaming chevrons, a RawImage whose texture repeats.")]
        [SerializeField] private RawImage streaks;
        [Tooltip("Chevron repeats per second streaming toward the downhill edge.")]
        [SerializeField, Min(0f)] private float flow = 1.6f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = .8f;
        [SerializeField, Min(.01f)] private float fadeSeconds = .25f;

        private RectTransform rect;
        private float visibility;
        private float offset;

        public bool IsTilting { get; private set; }
        /// <summary>-1 when the board tips left, +1 when it tips right.</summary>
        public int Side { get; private set; }

        private void Awake() => rect = streaks != null ? streaks.rectTransform : null;

        private void OnEnable()
        {
            if (powerUpManager == null) return;
            powerUpManager.EffectStarted += OnEffectStarted;
            powerUpManager.EffectEnded += OnEffectEnded;
        }

        private void OnDisable()
        {
            if (powerUpManager == null) return;
            powerUpManager.EffectStarted -= OnEffectStarted;
            powerUpManager.EffectEnded -= OnEffectEnded;
        }

        private void OnEffectStarted(PowerUpType type)
        {
            if (type != PowerUpType.ArenaTilt || powerUpManager == null) return;
            IsTilting = true;
            Side = powerUpManager.TiltDrift.x < 0f ? -1 : 1;
            PlaceOnSide();
        }

        private void OnEffectEnded(PowerUpType type) { if (type == PowerUpType.ArenaTilt) IsTilting = false; }

        private void Update() => Tick(Time.unscaledDeltaTime, Time.unscaledTime);

        /// <summary>Fades and streams the chevrons. Public so tests can drive it.</summary>
        public void Tick(float deltaTime, float time)
        {
            visibility = Mathf.MoveTowards(visibility, IsTilting ? 1f : 0f, Mathf.Max(0f, deltaTime) / fadeSeconds);
            if (streaks == null) return;
            streaks.enabled = visibility > 0f;
            if (!streaks.enabled) return;
            var pulse = .75f + .25f * Mathf.Sin(time * Mathf.PI * 2f * 1.2f);
            var c = streaks.color; c.a = visibility * maxAlpha * pulse; streaks.color = c;
            offset = Mathf.Repeat(offset + flow * Mathf.Max(0f, deltaTime), 1f);
            var uv = streaks.uvRect;
            // The texture points right; the band is mirrored for the left edge, so it always streams outward.
            uv.x = -offset;
            streaks.uvRect = uv;
        }

        /// <summary>Anchors the band to the downhill edge, mirrored on the left so the chevrons point outward.</summary>
        private void PlaceOnSide()
        {
            if (rect == null) return;
            var x = Side < 0 ? 0f : 1f;
            rect.anchorMin = new Vector2(x, 0f);
            rect.anchorMax = new Vector2(x, 1f);
            // Pivot in the middle, so mirroring flips the band in place rather than off the screen.
            rect.pivot = new Vector2(.5f, .5f);
            var half = rect.sizeDelta.x * .5f;
            rect.anchoredPosition = new Vector2(Side < 0 ? half : -half, 0f);
            rect.localScale = new Vector3(Side < 0 ? -1f : 1f, 1f, 1f);
        }
    }
}
