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
        [SerializeField, Min(.01f)] private float length = .45f;
        [SerializeField, Min(.01f)] private float thickness = .12f;

        public CardinalDirection Direction { get; private set; }
        public float Speed { get; private set; }
        public bool IsFlying { get; private set; }

        public void Launch(Vector3 origin, CardinalDirection direction, float speed)
        {
            Direction = direction;
            Speed = speed;
            IsFlying = true;
            transform.position = origin;
            var horizontal = direction == CardinalDirection.Left || direction == CardinalDirection.Right;
            transform.rotation = Quaternion.identity;
            transform.localScale = horizontal ? new Vector3(length, thickness, thickness) : new Vector3(thickness, length, thickness);
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
                if ((point - closest).sqrMagnitude > hitRadius * hitRadius) continue;
                if (along >= bestAlong) continue;
                best = enemy;
                bestAlong = along;
            }
            return best;
        }
    }
}
