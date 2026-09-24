using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Loops a short run of sprite frames, such as a thruster flicker or a pulsing alien.
    /// Deliberately tiny: an Animator and controller per two-frame loop would be all overhead.
    /// Runs on unscaled time only when asked, so ordinary animations freeze with the game on pause.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFrameAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(.1f)] private float framesPerSecond = 8f;
        [Tooltip("Start on a random frame so a group of the same alien does not pulse in lockstep.")]
        [SerializeField] private bool randomStartFrame = true;

        private SpriteRenderer spriteRenderer;
        private float elapsed;
        private int current;

        public int CurrentFrame => current;
        public int FrameCount => frames != null ? frames.Length : 0;

        private void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

        private void OnEnable()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            current = randomStartFrame && FrameCount > 1 ? Random.Range(0, FrameCount) : 0;
            elapsed = 0f;
            Show();
        }

        private void Update() => Advance(Time.deltaTime);

        /// <summary>Steps the loop by a time delta. Public so tests can drive it deterministically.</summary>
        public void Advance(float deltaTime)
        {
            if (FrameCount < 2 || deltaTime <= 0f) return;
            elapsed += deltaTime;
            var frameDuration = 1f / framesPerSecond;
            if (elapsed < frameDuration) return;
            var steps = (int)(elapsed / frameDuration);
            elapsed -= steps * frameDuration;
            current = (current + steps) % FrameCount;
            Show();
        }

        public void SetFrames(Sprite[] value, float fps)
        {
            frames = value;
            framesPerSecond = Mathf.Max(.1f, fps);
            current = 0;
            Show();
        }

        private void Show()
        {
            if (spriteRenderer != null && FrameCount > 0 && frames[current] != null) spriteRenderer.sprite = frames[current];
        }
    }
}
