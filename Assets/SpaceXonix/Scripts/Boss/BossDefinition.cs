using UnityEngine;

namespace SpaceXonix.Boss
{
    /// <summary>Tuning for the stationary Alien Core. Immutable at runtime; modifiers scale it through multipliers.</summary>
    [CreateAssetMenu(menuName = "SpaceXonix/Boss Definition")]
    public sealed class BossDefinition : ScriptableObject
    {
        public string displayName = "Alien Core";

        [Header("Placement")]
        [Tooltip("Board column the core sits on, as a fraction of the board width.")]
        [Range(0f, 1f)] public float columnFraction = .5f;
        [Tooltip("Board row the core sits on, as a fraction of the board height.")]
        [Range(0f, 1f)] public float rowFraction = .78f;
        [Min(.1f)] public float bodyRadius = 1.6f;

        [Header("Attacks")]
        [Min(.1f)] public float fireInterval = 2.2f;
        [Min(.1f)] public float projectileSpeed = 4.5f;
        [Min(1)] public int projectilesPerVolley = 3;
        [Tooltip("Total spread of a volley in degrees, centred on the aim direction.")]
        [Min(0f)] public float volleySpreadDegrees = 24f;
        [Min(.01f)] public float projectileRadius = .28f;
        [Tooltip("One shot of each volley, chosen at random, is charged: it breaks captured territory within this many cells of where it lands. 0 turns it off. (The name predates the random choice; it was always the middle shot.)")]
        [Min(0f)] public float middleShotTerritoryRadiusCells = 2.5f;
        [Tooltip("How long a Power Shot hit stops the attack cycle.")]
        [Min(0f)] public float interruptDuration = 2f;

        [Header("Damage feedback")]
        [Tooltip("Visual damage steps the core passes through as the arena is captured.")]
        [Min(1)] public int damageStages = 4;
    }
}
