using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.PowerUps;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// The Shield's look: a shimmering energy bubble round the ship, and a ring on the floor marking
    /// how much of the trail the bubble covers. The bubble pops in, breathes, flashes when it absorbs
    /// a hit and flickers through its last second. Lives on the shield object PowerUpManager turns
    /// on and off, and runs after it has placed that object at the ship. Presentation only.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class ShieldBubble : MonoBehaviour
    {
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private BoardManager boardManager;
        [Tooltip("The camera-facing bubble sprite.")]
        [SerializeField] private SpriteRenderer bubble;
        [Tooltip("A bright copy of the bubble shown briefly when the shield absorbs a hit.")]
        [SerializeField] private SpriteRenderer hitGlow;
        [Tooltip("The flat ring on the floor at the bubble's reach over the trail.")]
        [SerializeField] private Renderer footprint;
        [Tooltip("The footprint mesh's outer radius at scale 1.")]
        [SerializeField, Min(.01f)] private float footprintMeshRadius = .88f;

        [Header("Feel")]
        [Tooltip("Width of the bubble in world units. The ship is about 0.75 wide.")]
        [SerializeField, Min(.01f)] private float bubbleSize = 1.55f;
        [SerializeField, Min(.01f)] private float popSeconds = .3f;
        [SerializeField, Range(0f, .2f)] private float breathe = .035f;
        [SerializeField, Min(.01f)] private float hitFlashSeconds = .3f;
        [Tooltip("The bubble flickers for this long before it runs out.")]
        [SerializeField, Min(0f)] private float warnSeconds = 1f;
        [SerializeField] private Color footprintColor = new Color(.45f, 1f, .8f, .5f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;
        private Transform cameraTransform;
        private float age;
        private float hitFlash;

        public float BubbleScale { get; private set; }
        public float BubbleAlpha { get; private set; }
        public bool IsWarning { get; private set; }
        /// <summary>1 the moment a hit is absorbed, fading to 0.</summary>
        public float HitFlash => hitFlashSeconds > 0f ? Mathf.Clamp01(hitFlash / hitFlashSeconds) : 0f;

        private void OnEnable()
        {
            age = 0f;
            hitFlash = 0f;
            if (gameManager != null) gameManager.FailureShielded += OnShielded;
            Tick(0f, Time.time);
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.FailureShielded -= OnShielded;
        }

        private void OnShielded(PlayerFailureReason reason) => hitFlash = hitFlashSeconds;

        private void LateUpdate() => Tick(Time.deltaTime, Time.time);

        /// <summary>Animates the bubble and places the footprint. Public so tests can drive it.</summary>
        public void Tick(float deltaTime, float time)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            age += deltaTime;
            hitFlash = Mathf.Max(0f, hitFlash - deltaTime);

            var remaining = powerUpManager != null ? powerUpManager.GetEffectRemaining(PowerUpType.Shield) : float.MaxValue;
            IsWarning = remaining > 0f && remaining < warnSeconds;
            // Fast blinking in the last second tells the player the cover is about to drop.
            BubbleAlpha = IsWarning && Mathf.Repeat(time * 8f, 1f) < .5f ? .3f : 1f;

            var pop = Mathf.LerpUnclamped(.35f, 1f, EaseOutBack(Mathf.Clamp01(age / popSeconds)));
            var pulse = 1f + breathe * Mathf.Sin(time * Mathf.PI * 2f * 1.2f);
            BubbleScale = pop * pulse * (1f + .15f * HitFlash);

            PlaceBubble();
            PlaceFootprint(time);
        }

        private void PlaceBubble()
        {
            if (bubble == null) return;
            var width = bubble.sprite != null ? bubble.sprite.bounds.size.x : 1f;
            var t = bubble.transform;
            t.localPosition = Vector3.zero;
            t.localScale = Vector3.one * (bubbleSize / Mathf.Max(.01f, width) * BubbleScale);
            t.rotation = CameraRotation();
            var c = bubble.color; c.a = BubbleAlpha; bubble.color = c;
            if (hitGlow == null) return;
            hitGlow.enabled = HitFlash > 0f;
            var g = hitGlow.color; g.a = HitFlash; hitGlow.color = g;
        }

        private void PlaceFootprint(float time)
        {
            if (footprint == null) return;
            var radius = powerUpManager != null ? powerUpManager.ShieldTrailRadius : .75f;
            var origin = transform.position;
            var surface = boardManager != null ? boardManager.GetVisualSurfaceHeight(origin) : 0f;
            var t = footprint.transform;
            // Flat on whatever the ship is over, just above the trail strip.
            t.position = new Vector3(origin.x, origin.y, -surface - .035f);
            t.rotation = Quaternion.identity;
            t.localScale = Vector3.one * (radius / footprintMeshRadius * Mathf.Min(1f, BubbleScale));
            block ??= new MaterialPropertyBlock();
            var colour = footprintColor;
            colour.a *= (.8f + .2f * Mathf.Sin(time * Mathf.PI * 2f)) * BubbleAlpha;
            footprint.GetPropertyBlock(block);
            block.SetColor(BaseColorId, colour);
            footprint.SetPropertyBlock(block);
        }

        private Quaternion CameraRotation()
        {
            if (cameraTransform == null)
            {
                var main = Camera.main;
                if (main == null) return Quaternion.identity;
                cameraTransform = main.transform;
            }
            return cameraTransform.rotation;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
