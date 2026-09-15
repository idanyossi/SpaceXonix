using System.Collections.Generic;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using UnityEngine;
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
        private void Awake()
        {
        }
        private void Start()
        {
            SpawnInitialEnemies();
        }
        private void Update()
        {
            RefreshOccupancy();
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
