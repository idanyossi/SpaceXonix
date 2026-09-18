using System.Collections.Generic;
using SpaceXonix.Enemies;
using SpaceXonix.Pooling;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>Spawns pooled blast rings on the board floor when a Volatile Alien explodes.</summary>
    public sealed class ExplosionRingPresenter : MonoBehaviour
    {
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private PoolService poolService;
        [SerializeField] private GameObject ringPrefab;
        [Tooltip("Ring radius relative to the Volatile's territory blast radius in cells.")]
        [SerializeField, Min(.1f)] private float radiusScale = 1f;
        [SerializeField, Min(.01f)] private float fallbackRadius = .75f;

        private readonly List<ExplosionRing> activeRings = new List<ExplosionRing>();

        public IReadOnlyList<ExplosionRing> ActiveRings => activeRings;

        private void OnEnable()
        {
            if (enemyManager != null) enemyManager.ExplosionOccurred += OnExplosion;
        }

        private void OnDisable()
        {
            if (enemyManager != null) enemyManager.ExplosionOccurred -= OnExplosion;
            for (var i = activeRings.Count - 1; i >= 0; i--) Release(i);
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            for (var i = activeRings.Count - 1; i >= 0; i--)
                if (!activeRings[i].Tick(deltaTime)) Release(i);
        }

        public ExplosionRing Spawn(Vector3 position, float radius)
        {
            if (ringPrefab == null || poolService == null) return null;
            var instance = poolService.Acquire(ringPrefab, transform);
            var ring = instance.GetComponent<ExplosionRing>();
            if (ring == null)
            {
                poolService.Release(ringPrefab, instance);
                Debug.LogError("Explosion ring prefab requires an ExplosionRing.", this);
                return null;
            }
            ring.Play(position, radius);
            activeRings.Add(ring);
            return ring;
        }

        private void OnExplosion(Vector3 position, int destroyedEnemies, int destroyedTerritory) => Spawn(position, ResolveRadius());

        private float ResolveRadius()
        {
            var cellSize = enemyManager != null && enemyManager.BoardManager != null ? enemyManager.BoardManager.CellWorldSize : 0f;
            var definition = enemyManager != null ? enemyManager.LastExplosionDefinition : null;
            if (definition == null || cellSize <= 0f) return fallbackRadius * radiusScale;
            return definition.volatileTerritoryRadiusCells * cellSize * radiusScale;
        }

        private void Release(int index)
        {
            var ring = activeRings[index];
            activeRings.RemoveAt(index);
            if (ring != null && poolService != null && ringPrefab != null) poolService.Release(ringPrefab, ring.gameObject);
        }
    }
}
