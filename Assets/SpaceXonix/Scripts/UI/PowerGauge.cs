using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// The Power Shot meter, drawn as a heavy energy gauge: a nine-sliced fill that grows to exactly
    /// the track's width, energy stripes streaming through it, cell dividers every tenth, and a colour
    /// that climbs from deep blue to cyan as it charges. When full it throbs white-hot inside a pulsing
    /// glow frame, so a ready shot cannot be missed.
    ///
    /// The fill is resized rather than image-filled: a filled image cannot nine-slice, so its frame
    /// stretched into thick dark ends and a full meter never looked full.
    /// </summary>
    public sealed class PowerGauge : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;
        [Tooltip("Energy stripes inside the fill, scrolled through their UVs.")]
        [SerializeField] private RawImage energy;
        [Tooltip("A frame around the track that glows when the meter is full.")]
        [SerializeField] private Image readyGlow;
        [SerializeField] private Text label;

        [Header("Colours")]
        [SerializeField] private Color emptyColor = new Color(.1f, .3f, .85f);
        [SerializeField] private Color chargedColor = new Color(.25f, .85f, 1f);
        [SerializeField] private Color readyColor = new Color(.5f, .95f, 1f);
        [SerializeField] private Color hotColor = Color.white;

        [Header("Motion")]
        [Tooltip("Stripe repeats per second while charging; the full meter streams faster.")]
        [SerializeField, Min(0f)] private float chargingFlow = .6f;
        [SerializeField, Min(0f)] private float readyFlow = 2.2f;
        [Tooltip("Throbs per second while the meter is full.")]
        [SerializeField, Min(0f)] private float readyPulse = 3f;

        private float flowOffset;

        public float Fraction { get; private set; }
        public bool IsReady { get; private set; }
        public Color CurrentFillColor => fillImage != null ? fillImage.color : Color.clear;

        /// <summary>Sets how full the meter is and whether the shot is ready.</summary>
        public void Show(float fraction, bool ready)
        {
            Fraction = Mathf.Clamp01(fraction);
            IsReady = ready;
            if (fill != null)
            {
                var max = fill.anchorMax;
                if (!Mathf.Approximately(max.x, Fraction)) fill.anchorMax = new Vector2(Fraction, max.y);
            }
            // An empty meter hides its fill, rather than drawing a sliver of frame.
            if (fillImage != null && fillImage.enabled != Fraction > 0f) fillImage.enabled = Fraction > 0f;
            if (energy != null && energy.enabled != Fraction > 0f) energy.enabled = Fraction > 0f;
        }

        private void Update() => Animate(Time.unscaledDeltaTime, Time.unscaledTime);

        /// <summary>Advances the colours, glow and stripes. Public so tests can drive one frame.</summary>
        public void Animate(float deltaTime, float time)
        {
            var throb = IsReady ? .5f + .5f * Mathf.Sin(time * readyPulse * Mathf.PI * 2f) : 0f;
            if (fillImage != null)
                fillImage.color = IsReady ? Color.Lerp(readyColor, hotColor, throb) : Color.Lerp(emptyColor, chargedColor, Fraction);
            if (readyGlow != null)
            {
                if (readyGlow.enabled != IsReady) readyGlow.enabled = IsReady;
                if (IsReady) readyGlow.color = new Color(1f, 1f, 1f, Mathf.Lerp(.35f, 1f, throb));
            }
            if (label != null && IsReady) label.color = Color.Lerp(readyColor, hotColor, throb);
            else if (label != null) label.color = Color.white;
            if (energy == null) return;
            flowOffset = Mathf.Repeat(flowOffset + (IsReady ? readyFlow : chargingFlow) * Mathf.Max(0f, deltaTime), 1f);
            var rect = energy.uvRect;
            rect.x = -flowOffset;
            energy.uvRect = rect;
        }
    }
}
