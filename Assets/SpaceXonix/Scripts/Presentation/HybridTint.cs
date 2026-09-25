using SpaceXonix.Enemies;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Shows that an alien is a hybrid: its sprite pulses toward the Volatile's orange, so the player
    /// can tell which aliens will blow a hole in their territory. Presentation only.
    /// </summary>
    public sealed class HybridTint : MonoBehaviour
    {
        [SerializeField] private EnemyController enemy;
        [SerializeField] private Color chargeColor = new Color(1f, .65f, .3f);
        [Tooltip("Pulses per second between the alien's own colour and the charge colour.")]
        [SerializeField, Min(0f)] private float pulseRate = 3f;
        [SerializeField, Range(0f, 1f)] private float minimumCharge = .45f;

        private SpriteRenderer[] renderers;
        private Color[] baseColors;

        public bool IsShowingCharge { get; private set; }

        private void Awake()
        {
            if (enemy == null) enemy = GetComponent<EnemyController>();
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
        }

        private void OnEnable()
        {
            if (enemy != null) enemy.HybridChanged += OnHybridChanged;
            Apply(enemy != null && enemy.IsHybrid ? 1f : 0f);
        }

        private void OnDisable()
        {
            if (enemy != null) enemy.HybridChanged -= OnHybridChanged;
            Apply(0f);
        }

        private void OnHybridChanged(EnemyController _) => Apply(enemy.IsHybrid ? 1f : 0f);

        private void Update()
        {
            if (enemy == null || !enemy.IsHybrid) return;
            // Each doubling of the charge doubles the pulse rate. Once supercharged it stops fading
            // and hard-blinks between its own colour and full orange, so it reads as about to blow.
            var rate = pulseRate * enemy.HybridCharge;
            var wave = Mathf.Sin(Time.time * rate * Mathf.PI * 2f);
            if (enemy.HybridCharge > 1f) Apply(wave >= 0f ? 1f : 0f);
            else Apply(Mathf.Lerp(minimumCharge, 1f, .5f + .5f * wave));
        }

        /// <summary>0 shows the alien's own colours, 1 the full charge colour.</summary>
        private void Apply(float charge)
        {
            IsShowingCharge = charge > 0f;
            if (renderers == null) return;
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = Color.Lerp(baseColors[i], chargeColor, charge);
        }
    }
}
