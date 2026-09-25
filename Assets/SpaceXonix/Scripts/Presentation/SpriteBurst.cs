using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Plays a run of sprite frames once, facing the camera, then reports that it is done so its
    /// owner can return it to the pool. Used for the ship's explosion: a pixel-art fireball reads as
    /// a ship blowing up, where a ring laid flat on the board looked like it was being squashed.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteBurst : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(.1f)] private float framesPerSecond = 12f;

        private SpriteRenderer spriteRenderer;
        private Transform cameraTransform;
        private float elapsed;

        public bool IsPlaying { get; private set; }
        public int CurrentFrame { get; private set; }
        public int FrameCount => frames != null ? frames.Length : 0;
        public float Duration => FrameCount / framesPerSecond;

        private void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

        /// <summary>Starts the burst at a world position, <paramref name="size"/> world units across.</summary>
        public void Play(Vector3 position, float size)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            transform.position = position;
            var sprite = FrameCount > 0 ? frames[0] : null;
            var native = sprite != null ? sprite.bounds.size.x : 1f;
            transform.localScale = Vector3.one * (size / Mathf.Max(.0001f, native));
            elapsed = 0f;
            CurrentFrame = 0;
            IsPlaying = FrameCount > 0;
            spriteRenderer.enabled = IsPlaying;
            Show();
            FaceCamera();
        }

        /// <summary>Advances the burst; returns false once the last frame has finished.</summary>
        public bool Tick(float deltaTime)
        {
            if (!IsPlaying) return false;
            elapsed += Mathf.Max(0f, deltaTime);
            var frame = (int)(elapsed * framesPerSecond);
            if (frame >= FrameCount)
            {
                IsPlaying = false;
                spriteRenderer.enabled = false;
                return false;
            }
            if (frame != CurrentFrame) { CurrentFrame = frame; Show(); }
            return true;
        }

        private void LateUpdate() => FaceCamera();

        private void FaceCamera()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null) transform.rotation = cameraTransform.rotation;
        }

        private void Show()
        {
            if (spriteRenderer != null && FrameCount > 0 && frames[CurrentFrame] != null) spriteRenderer.sprite = frames[CurrentFrame];
        }
    }
}
