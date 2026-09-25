using UnityEngine;

namespace SpaceXonix.Boss
{
    /// <summary>
    /// One pooled Alien Core projectile. Deliberately dumb: it carries position, velocity and radius,
    /// and the controller resolves every contact, mirroring how PowerMeter drives PowerShotProjectile.
    /// </summary>
    public sealed class BossProjectile : MonoBehaviour
    {
        [Tooltip("Colour of a territory-breaking shot, so the player can tell it from the others.")]
        [SerializeField] private Color breakerTint = new Color(1f, .45f, .25f);

        private SpriteRenderer spriteRenderer;

        public Vector2 Velocity { get; private set; }
        public float Radius { get; private set; }
        public bool IsFlying { get; private set; }
        /// <summary>Cells of captured territory this shot breaks on impact; 0 means it passes over territory.</summary>
        public float TerritoryRadiusCells { get; private set; }
        public bool BreaksTerritory => TerritoryRadiusCells > 0f;

        public void Launch(Vector3 origin, Vector2 velocity, float radius, float territoryRadiusCells = 0f)
        {
            Velocity = velocity;
            Radius = Mathf.Max(.01f, radius);
            TerritoryRadiusCells = Mathf.Max(0f, territoryRadiusCells);
            IsFlying = true;
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null) spriteRenderer.color = BreaksTerritory ? breakerTint : Color.white;
            transform.position = origin;
            var diameter = Radius * 2f;
            // Shape the visual child when present so the logical root keeps unit scale.
            var shaped = transform.childCount > 0 ? transform.GetChild(0) : transform;
            shaped.localScale = new Vector3(diameter, diameter, diameter);
        }

        public void Stop() => IsFlying = false;

        /// <summary>Moves one frame and reports the segment travelled, so a fast shot cannot tunnel past the ship.</summary>
        public void Advance(float deltaTime, out Vector2 from, out Vector2 to)
        {
            from = transform.position;
            to = from + Velocity * Mathf.Max(0f, deltaTime);
            if (!IsFlying)
            {
                to = from;
                return;
            }
            transform.position = new Vector3(to.x, to.y, transform.position.z);
        }

        /// <summary>Shortest distance from a point to this frame's travel segment.</summary>
        public static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            var segment = to - from;
            var lengthSquared = segment.sqrMagnitude;
            var along = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point - from, segment) / lengthSquared) : 0f;
            return Vector2.Distance(point, from + segment * along);
        }
    }
}
