using UnityEngine;

namespace SpaceXonix.UI
{
    /// <summary>
    /// Eases an overlay screen in whenever it opens: it fades up and settles from slightly small to
    /// full size, instead of popping on. Runs on unscaled time, because menus open over a paused game.
    /// Added to every "...Overlay" screen by SpaceXonix > Apply UI Skin.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PanelTransition : MonoBehaviour
    {
        [SerializeField, Min(.01f)] private float seconds = .18f;
        [SerializeField, Range(.5f, 1f)] private float startScale = .94f;
        [Tooltip("The panel that settles in. The overlay itself only fades, so its full-screen dim never shows gaps. None skips the scaling.")]
        [SerializeField] private RectTransform scaleTarget;

        private CanvasGroup group;
        private float elapsed;

        public bool IsPlaying => elapsed < seconds;

        private void Awake() => group = GetComponent<CanvasGroup>();

        private void OnEnable()
        {
            elapsed = 0f;
            Pose(0f);
        }

        private void OnDisable() => Pose(1f);

        private void Update() => Advance(Time.unscaledDeltaTime);

        /// <summary>Moves the transition on. Public so tests can drive it.</summary>
        public void Advance(float deltaTime)
        {
            if (!IsPlaying) return;
            elapsed = Mathf.Min(seconds, elapsed + Mathf.Max(0f, deltaTime));
            Pose(elapsed / seconds);
        }

        private void Pose(float t)
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            var eased = 1f - (1f - t) * (1f - t);
            group.alpha = eased;
            if (scaleTarget != null) scaleTarget.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, eased);
        }
    }
}
