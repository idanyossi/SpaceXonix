using System.Collections.Generic;
using SpaceXonix.Boss;
using SpaceXonix.Enemies;
using SpaceXonix.Pooling;
using SpaceXonix.Power;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Plays pooled pixel-art explosions for things that blow up in the arena: an alien killed by the
    /// Power Shot, and the boss's territory-breaking shot hitting the player's territory. Presentation
    /// only; each explosion appears at the hover height the thing was seen at.
    /// </summary>
    public sealed class SpriteBurstPresenter : MonoBehaviour
    {
        [SerializeField] private PoolService poolService;
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private PowerMeter powerMeter;
        [SerializeField] private BossController bossController;
        [Tooltip("An alien's explosion is this many times the width of the alien.")]
        [SerializeField, Min(.1f)] private float enemyExplosionScale = 2.2f;
        [Tooltip("Width, in world units, of the explosion where a boss shot breaks territory.")]
        [SerializeField, Min(.01f)] private float territoryHitSize = 1f;

        private readonly List<SpriteBurst> active = new List<SpriteBurst>();

        public IReadOnlyList<SpriteBurst> Active => active;

        private void OnEnable()
        {
            if (powerMeter != null) powerMeter.EnemyDestroyedByShot += OnEnemyDestroyed;
            if (bossController != null) bossController.TerritoryBroken += OnTerritoryBroken;
        }

        private void OnDisable()
        {
            if (powerMeter != null) powerMeter.EnemyDestroyedByShot -= OnEnemyDestroyed;
            if (bossController != null) bossController.TerritoryBroken -= OnTerritoryBroken;
            for (var i = active.Count - 1; i >= 0; i--) Release(i);
        }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>Advances every explosion. Public so tests can drive it.</summary>
        public void Tick(float deltaTime)
        {
            for (var i = active.Count - 1; i >= 0; i--)
                if (!active[i].Tick(deltaTime)) Release(i);
        }

        public SpriteBurst Play(Vector3 position, float size)
        {
            if (explosionPrefab == null || poolService == null) return null;
            var instance = poolService.Acquire(explosionPrefab, transform);
            var burst = instance.GetComponent<SpriteBurst>();
            if (burst == null)
            {
                poolService.Release(explosionPrefab, instance);
                return null;
            }
            burst.Play(position, size);
            active.Add(burst);
            return burst;
        }

        private void OnEnemyDestroyed(EnemyController enemy)
        {
            if (enemy == null) return;
            // The alien is already back in the pool, but its transforms still hold where it was.
            var visual = enemy.GetComponent<ActorVisual>();
            var position = visual != null && visual.Visual != null ? visual.Visual.position : enemy.transform.position;
            var width = visual != null ? visual.VisualWidth() : enemy.CollisionRadius * 2f;
            Play(position, Mathf.Max(.2f, width) * enemyExplosionScale);
        }

        private void OnTerritoryBroken(Vector3 position, int cells) => Play(position, territoryHitSize);

        private void Release(int index)
        {
            var burst = active[index];
            active.RemoveAt(index);
            if (burst != null && poolService != null && explosionPrefab != null) poolService.Release(explosionPrefab, burst.gameObject);
        }
    }
}
