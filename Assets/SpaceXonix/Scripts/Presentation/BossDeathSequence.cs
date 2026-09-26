using SpaceXonix.Audio;
using SpaceXonix.Boss;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// The Alien Core's destruction: a chain of explosions bursts across its body while it shudders
    /// and the camera shakes, then one big blast and a white flash, and the core is gone. Runs on
    /// unscaled time, because the stage has already ended. The campaign screens hold the victory
    /// panel back until it has played (<see cref="Duration"/>). Presentation only.
    /// </summary>
    public sealed class BossDeathSequence : MonoBehaviour
    {
        [SerializeField] private BossController boss;
        [SerializeField] private SpriteBurstPresenter explosions;
        [SerializeField] private ArenaShaker shaker;
        [Tooltip("A full-screen white image flashed on the final blast.")]
        [SerializeField] private Graphic flash;

        [Header("Timing")]
        [SerializeField, Min(.1f)] private float chainSeconds = 1.6f;
        [SerializeField, Min(.02f)] private float chainInterval = .11f;
        [SerializeField, Min(.05f)] private float flashSeconds = .6f;

        [Header("Size")]
        [SerializeField, Min(.1f)] private float bodyRadius = 1.3f;
        [SerializeField, Min(.1f)] private float finalBlastSize = 4.5f;
        [SerializeField] private int randomSeed;

        private System.Random random;
        private float elapsed = -1f;
        private float nextBurst;
        private Vector3 bodyRest;
        private bool finished;

        /// <summary>How long the whole sequence takes, so the victory screen can wait for it.</summary>
        public float Duration => chainSeconds + flashSeconds;
        public bool IsPlaying => elapsed >= 0f && !finished;
        public int BurstCount { get; private set; }

        private void OnEnable() { if (boss != null) boss.Defeated += Play; }

        private void OnDisable() { if (boss != null) boss.Defeated -= Play; }

        /// <summary>Starts the sequence. Public so tests can run it without winning the stage.</summary>
        public void Play()
        {
            random ??= randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
            elapsed = 0f;
            nextBurst = 0f;
            finished = false;
            BurstCount = 0;
            if (boss != null && boss.Body != null) bodyRest = boss.Body.localPosition;
            if (shaker != null) shaker.Shake(.5f);
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        /// <summary>Advances the sequence. Public so tests can drive it.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsPlaying) return;
            elapsed += Mathf.Max(0f, deltaTime);
            var body = boss != null ? boss.Body : null;
            if (elapsed < chainSeconds)
            {
                // The core shudders harder as the chain builds.
                if (body != null)
                {
                    var shudder = .05f + .12f * (elapsed / chainSeconds);
                    body.localPosition = bodyRest + new Vector3(Next(-1f, 1f), Next(-1f, 1f), 0f) * shudder;
                }
                while (nextBurst <= elapsed)
                {
                    Burst(body, Next(.7f, 1.6f), bodyRadius);
                    nextBurst += chainInterval;
                }
                return;
            }
            if (body != null && body.gameObject.activeSelf)
            {
                // The final blast: the biggest explosion, a hard shake and a flash, and the core is gone.
                body.localPosition = bodyRest;
                Burst(body, finalBlastSize, 0f);
                if (shaker != null) shaker.Shake(1f);
                PlaySound(GameSfx.BossDestroyed);
                boss.HideBody();
            }
            var t = Mathf.Clamp01((elapsed - chainSeconds) / flashSeconds);
            if (flash != null)
            {
                flash.enabled = t < 1f;
                var c = flash.color; c.a = .85f * (1f - t) * (1f - t); flash.color = c;
            }
            if (t >= 1f) finished = true;
        }

        private void Burst(Transform body, float size, float spread)
        {
            if (explosions == null || body == null) return;
            var centre = body.position;
            var offset = new Vector3(Next(-1f, 1f), Next(-1f, 1f), 0f) * spread;
            // Nudged toward the camera so the blasts draw over the core.
            explosions.Play(centre + offset + Vector3.back * .5f, size);
            BurstCount++;
            if (BurstCount % 3 == 1) PlaySound(GameSfx.EnemyDestroyed);
        }

        private static void PlaySound(GameSfx sound)
        {
            var audio = AudioManager.Instance;
            if (audio != null) audio.Play(sound);
        }

        private float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
