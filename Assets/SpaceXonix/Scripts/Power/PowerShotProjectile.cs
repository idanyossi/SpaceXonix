using System.Collections.Generic;
using SpaceXonix.Enemies;
using SpaceXonix.Player;
using UnityEngine;

namespace SpaceXonix.Power
{
    public enum PowerShotStep
    {
        Moving,
        HitEnemy,
        LeftBoard
    }

    public sealed class PowerShotProjectile : MonoBehaviour
    {
        [Tooltip("Size of a mesh visual (the original box). A sprite visual keeps its own size and is turned to face the direction of travel.")]
        [SerializeField, Min(.01f)] private float length = .45f;
        [SerializeField, Min(.01f)] private float thickness = .12f;
        [Tooltip("Optional streak left behind the bolt. Cleared on every launch, so a pooled shot never draws a line from where it last was.")]
        [SerializeField] private TrailRenderer streak;

        public CardinalDirection Direction { get; private set; }
        public float Speed { get; private set; }
        public bool IsFlying { get; private set; }

        public void Launch(Vector3 origin, CardinalDirection direction, float speed)
        {
            Direction = direction;
            Speed = speed;
            IsFlying = true;
            transform.position = origin;
            var visual = GetComponent<SpaceXonix.Presentation.ActorVisual>();
            var shaped = transform.childCount > 0 ? transform.GetChild(0) : transform;
            if (shaped.GetComponent<SpriteRenderer>() != null)
            {
                // A sprite bolt is drawn pointing up the board. Set its turn outright on every launch:
                // a pooled bolt keeps its last rotation, and the hover visual leaves a heading of 0 alone.
                var heading = HeadingFor(direction);
                shaped.rotation = Quaternion.Euler(0f, 0f, heading);
                if (visual != null) visual.HeadingDegrees = heading;
            }
            else
            {
                // Shape a mesh visual so the logical root keeps unit scale.
                var horizontal = direction == CardinalDirection.Left || direction == CardinalDirection.Right;
                shaped.rotation = Quaternion.identity;
                shaped.localScale = horizontal ? new Vector3(length, thickness, thickness) : new Vector3(thickness, length, thickness);
            }
            if (streak != null)
            {
                // Put the visual where it will hover before clearing, so the first streak segment starts at the ship.
                if (visual != null && visual.Visual != null) visual.Visual.position = origin + Vector3.back * visual.HoverHeight;
                streak.Clear();
            }
        }

        /// <summary>In-plane rotation for a visual drawn pointing up the board.</summary>
        public static float HeadingFor(CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.Right: return -90f;
                case CardinalDirection.Down: return 180f;
                case CardinalDirection.Left: return 90f;
                default: return 0f;
            }
        }

        public void Stop() => IsFlying = false;

        /// <summary>Sweeps this frame's straight segment so a fast shot cannot tunnel past an alien.</summary>
        public PowerShotStep Advance(float deltaTime, IReadOnlyList<EnemyController> enemies, float hitRadius, Rect bounds,
            out EnemyController hitEnemy)
        {
            hitEnemy = null;
            if (!IsFlying) return PowerShotStep.LeftBoard;
            var from = (Vector2)transform.position;
            var to = from + Direction.ToVector2() * Speed * Mathf.Max(0f, deltaTime);
            hitEnemy = FindFirstHit(from, to, enemies, hitRadius);
            if (hitEnemy != null)
            {
                transform.position = new Vector3(hitEnemy.transform.position.x, hitEnemy.transform.position.y, transform.position.z);
                IsFlying = false;
                return PowerShotStep.HitEnemy;
            }
            transform.position = new Vector3(to.x, to.y, transform.position.z);
            if (bounds.Contains(to)) return PowerShotStep.Moving;
            IsFlying = false;
            return PowerShotStep.LeftBoard;
        }

        public static EnemyController FindFirstHit(Vector2 from, Vector2 to, IReadOnlyList<EnemyController> enemies, float hitRadius)
        {
            if (enemies == null) return null;
            var segment = to - from;
            var lengthSquared = segment.sqrMagnitude;
            EnemyController best = null;
            var bestAlong = float.MaxValue;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsActiveEnemy) continue;
                var point = (Vector2)enemy.transform.position;
                var along = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point - from, segment) / lengthSquared) : 0f;
                var closest = from + segment * along;
                var reach = hitRadius + enemy.CollisionRadius;
                if ((point - closest).sqrMagnitude > reach * reach) continue;
                if (along >= bestAlong) continue;
                best = enemy;
                bestAlong = along;
            }
            return best;
        }
    }
}
