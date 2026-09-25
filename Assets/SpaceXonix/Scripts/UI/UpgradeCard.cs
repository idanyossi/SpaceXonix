using SpaceXonix.Campaign;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// One upgrade drawn as a holographic card: a header band in the upgrade's colour, a pixel-art
    /// picture on a scanline screen, pips for how many the run already has, and the effect text.
    /// It deals in from below when shown and idles with a slow float and streaming scanlines.
    /// Runs on unscaled time, because the game is paused while the player chooses.
    /// </summary>
    public sealed class UpgradeCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image band;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Image art;
        [SerializeField] private RawImage scanlines;
        [SerializeField] private Image[] pips = new Image[0];
        [SerializeField] private Sprite pipFull;
        [SerializeField] private Sprite pipEmpty;
        [SerializeField] private Text effectLabel;

        [Header("Motion")]
        [SerializeField, Min(.01f)] private float dealSeconds = .35f;
        [SerializeField] private float dealDistance = 220f;
        [SerializeField, Min(0f)] private float floatAmplitude = 5f;
        [SerializeField, Min(0f)] private float floatRate = .35f;
        [SerializeField, Min(0f)] private float scanlineFlow = .25f;
        [Tooltip("Blinks per second of the pip this card would add.")]
        [SerializeField, Min(0f)] private float nextPipBlink = 2f;

        private RectTransform rect;
        private Vector2 restPosition;
        private bool restCaptured;
        private float shownAt;
        private float delay;
        private int nextPip = -1;

        public Button Button => button;
        public UpgradeDefinition Definition { get; private set; }
        public string DisplayedName => nameLabel != null ? nameLabel.text : string.Empty;
        public string DisplayedEffect => effectLabel != null ? effectLabel.text : string.Empty;
        public int FilledPips { get; private set; }
        public int VisiblePips { get; private set; }

        /// <summary>Fills the card for an upgrade the run already holds <paramref name="stacks"/> of, dealing it in after <paramref name="dealDelay"/>.</summary>
        public void Present(UpgradeDefinition definition, int stacks, float dealDelay)
        {
            Definition = definition;
            CaptureRest();
            if (nameLabel != null) nameLabel.text = definition.displayName.ToUpperInvariant();
            if (effectLabel != null) effectLabel.text = definition.effectText;
            if (band != null) band.color = definition.cardAccent;
            if (art != null)
            {
                art.sprite = definition.cardArt;
                art.enabled = definition.cardArt != null;
            }
            VisiblePips = Mathf.Min(pips.Length, definition.maxStacks);
            FilledPips = Mathf.Clamp(stacks, 0, VisiblePips);
            nextPip = FilledPips < VisiblePips ? FilledPips : -1;
            for (var i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                pips[i].gameObject.SetActive(i < VisiblePips);
                pips[i].sprite = i < FilledPips ? pipFull : pipEmpty;
                pips[i].color = i < FilledPips ? definition.cardAccent : Color.white;
            }
            delay = Mathf.Max(0f, dealDelay);
            shownAt = Time.unscaledTime;
            gameObject.SetActive(true);
            Animate(0f);
        }

        private void Update() => Animate(Time.unscaledTime - shownAt);

        /// <summary>Poses the card at a time since it was presented. Public so tests can check the deal.</summary>
        public void Animate(float sinceShown)
        {
            CaptureRest();
            var t = Mathf.Clamp01((sinceShown - delay) / dealSeconds);
            var eased = 1f - (1f - t) * (1f - t) * (1f - t);
            var bob = t >= 1f ? Mathf.Sin((sinceShown - delay) * floatRate * Mathf.PI * 2f) * floatAmplitude : 0f;
            if (rect != null)
            {
                rect.anchoredPosition = restPosition + new Vector2(0f, (1f - eased) * -dealDistance + bob);
                rect.localScale = Vector3.one * Mathf.Lerp(.85f, 1f, eased);
            }
            if (group != null)
            {
                group.alpha = eased;
                // Not clickable until it has landed, so a tap cannot pick a card still in flight.
                group.interactable = t >= 1f;
                group.blocksRaycasts = t >= 1f;
            }
            if (scanlines != null)
            {
                var uv = scanlines.uvRect;
                uv.y = Mathf.Repeat(sinceShown * scanlineFlow, 1f);
                scanlines.uvRect = uv;
            }
            if (nextPip >= 0 && nextPip < pips.Length && pips[nextPip] != null && Definition != null)
            {
                var on = Mathf.Repeat(sinceShown * nextPipBlink, 1f) < .5f;
                pips[nextPip].sprite = on ? pipFull : pipEmpty;
                pips[nextPip].color = on ? Color.Lerp(Definition.cardAccent, Color.white, .5f) : Color.white;
            }
        }

        public bool IsLanded(float sinceShown) => sinceShown >= delay + dealSeconds;

        private void CaptureRest()
        {
            if (restCaptured) return;
            rect = transform as RectTransform;
            if (rect != null) restPosition = rect.anchoredPosition;
            restCaptured = true;
        }
    }
}
