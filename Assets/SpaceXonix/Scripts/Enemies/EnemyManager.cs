using System.Collections.Generic;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using UnityEngine;
using System;
namespace SpaceXonix.Enemies
{
    public sealed class EnemyManager : MonoBehaviour
    {
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PoolService poolService;
        [SerializeField] private EnemySpawnRequest[] initialSpawns;
        private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
        private readonly List<GridCoordinate> occupancy = new List<GridCoordinate>();
        private readonly Dictionary<EnemyController, GameObject> prefabByInstance = new Dictionary<EnemyController, GameObject>();
        public IReadOnlyList<EnemyController> ActiveEnemies => activeEnemies;
        public event Action<VolatileEnemy> DetonationStarted;
        public event Action<Vector3, int, int> ExplosionOccurred;
        private void Awake()
        {
        }
        private void Start()
        {
            SpawnInitialEnemies();
        }
        private void Update()
        {
            SimulateVolatileInteractions(Time.deltaTime);
            RefreshOccupancy();
        }
        public void SimulateVolatileInteractions(float deltaTime)
        {
            if (gameManager != null && gameManager.CurrentState != GameplayState.Playing) return;
            var lifecycleGeneration = gameManager != null ? gameManager.PlayerLifecycleGeneration : 0;
            for (var i = activeEnemies.Count - 1; i >= 0; i--)
                if (activeEnemies[i] is VolatileEnemy volatileEnemy) volatileEnemy.AdvanceSpawnProtection(deltaTime);

            for (var i = 0; i < activeEnemies.Count; i++)
            {
                if (gameManager != null && gameManager.PlayerLifecycleGeneration != lifecycleGeneration) return;
                if (!(activeEnemies[i] is VolatileEnemy volatileEnemy) || !volatileEnemy.IsArmed) continue;
                for (var j = 0; j < activeEnemies.Count; j++)
                {
                    if (!volatileEnemy.CanDetonateWith(activeEnemies[j])) continue;
                    ResolveVolatileExplosion(volatileEnemy);
                    return;
                }
            }
        }
        public bool ResolveVolatileExplosion(VolatileEnemy source)
        {
            if (source == null || !activeEnemies.Contains(source) || !source.BeginDetonation()) return false;
            var lifecycleGeneration = gameManager != null ? gameManager.PlayerLifecycleGeneration : 0;
            DetonationStarted?.Invoke(source);
            var position = source.transform.position;
            var definition = source.Definition;
            var hitsPlayer = gameManager != null && playerController != null &&
                gameManager.CanProcessPlayerContact(lifecycleGeneration) &&
                Vector2.Distance(position, playerController.transform.position) <= definition.volatileBlastRadius;
            var destroyedEnemies = 0;
            for (var i = activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = activeEnemies[i];
                if (enemy == source || enemy is VolatileEnemy) continue;
                if (Vector2.Distance(position, enemy.transform.position) > definition.volatileBlastRadius) continue;
                Despawn(enemy);
                destroyedEnemies++;
            }
            var radius = definition.volatileBlastRadius;
            Debug.DrawLine(position - Vector3.right * radius, position + Vector3.right * radius, Color.yellow, 1f);
            Debug.DrawLine(position - Vector3.up * radius, position + Vector3.up * radius, Color.yellow, 1f);
            var destroyedTerritory = boardManager.RemoveCapturedWithinRadius(source.LogicalCell, definition.volatileTerritoryRadiusCells);
            Despawn(source);
            ExplosionOccurred?.Invoke(position, destroyedEnemies, destroyedTerritory);
            if (hitsPlayer && gameManager.CanProcessPlayerContact(lifecycleGeneration))
                gameManager.ReportPlayerFailure(PlayerFailureReason.VolatileExplosion);
            return true;
        }
        public void Register(EnemyController enemy)
        {
            if (enemy != null && enemy.IsActiveEnemy && !activeEnemies.Contains(enemy))
            {
                activeEnemies.Add(enemy);
                enemy.LogicalCellChanged += OnEnemyLogicalCellChanged;
            }
            RefreshOccupancy();
        }
        public void Unregister(EnemyController enemy)
        {
            if (enemy != null) enemy.LogicalCellChanged -= OnEnemyLogicalCellChanged;
            activeEnemies.Remove(enemy);
            RefreshOccupancy();
        }
        public void RefreshOccupancy()
        {
            occupancy.Clear();
            for (var i = activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = activeEnemies[i];
                if (enemy == null || !enemy.IsActiveEnemy) { activeEnemies.RemoveAt(i); continue; }
                occupancy.Add(enemy.LogicalCell);
            }
            if (boardManager != null) boardManager.SetEnemyCells(occupancy);
        }
        public void SpawnInitialEnemies()
        {
            if (initialSpawns == null) return;
            foreach (var request in initialSpawns) Spawn(request.prefab, request.definition, request.Cell, request.direction);
        }
        public bool Spawn(GameObject prefab, EnemyDefinition definition, GridCoordinate cell, Vector2 direction)
        {
            if (prefab == null || definition == null || poolService == null || !IsValidSpawn(cell)) return false;
            var instance = poolService.Acquire(prefab, transform);
            var enemy = instance.GetComponent<EnemyController>();
            if (enemy == null) { poolService.Release(prefab, instance); return false; }
            prefabByInstance[enemy] = prefab;
            if (Spawn(enemy, definition, cell, direction)) return true;
            prefabByInstance.Remove(enemy);
            poolService.Release(prefab, instance);
            return false;
        }
        public bool Spawn(EnemyController enemy, EnemyDefinition definition, GridCoordinate cell, Vector2 direction)
        {
            if (enemy == null || definition == null || !IsValidSpawn(cell)) return false;
            enemy.Activate(definition, boardManager, gameManager, boardManager.GetWorldPosition(cell), direction);
            Register(enemy);
            return true;
        }
        public void Despawn(EnemyController enemy)
        {
            if (enemy == null) return;
            Unregister(enemy);
            if (prefabByInstance.TryGetValue(enemy, out var prefab)) { prefabByInstance.Remove(enemy); enemy.Deactivate(); poolService.Release(prefab, enemy.gameObject); }
            else enemy.Deactivate();
            RefreshOccupancy();
        }
        public void SetMovementSuspended(bool suspended)
        {
            foreach (var enemy in activeEnemies) enemy.SetMovementSuspended(suspended);
        }
        private bool IsValidSpawn(GridCoordinate cell)
        {
            return boardManager.Model.IsInBounds(cell) && boardManager.Model.GetCell(cell) == BoardCellState.Uncaptured && boardManager.WorldToGrid(playerController.transform.position) != cell;
        }
        private void OnEnemyLogicalCellChanged(EnemyController enemy) => RefreshOccupancy();
    }

    [System.Serializable]
    public sealed class EnemySpawnRequest
    {
        public GameObject prefab;
        public EnemyDefinition definition;
        [Min(0)] public int column;
        [Min(0)] public int row;
        public Vector2 direction = Vector2.right;
        public GridCoordinate Cell => new GridCoordinate(column, row);
    }
}
