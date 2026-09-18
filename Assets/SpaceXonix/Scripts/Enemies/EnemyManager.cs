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
        private readonly List<GridCoordinate> footprint = new List<GridCoordinate>();
        private readonly Dictionary<EnemyController, GameObject> prefabByInstance = new Dictionary<EnemyController, GameObject>();
        public IReadOnlyList<EnemyController> ActiveEnemies => activeEnemies;
        public bool IsMovementSuspended { get; private set; }
        public float SpeedMultiplier { get; private set; } = 1f;
        public float UnstableIntervalMultiplier { get; private set; } = 1f;
        public float VolatileRadiusMultiplier { get; private set; } = 1f;
        public SpaceXonix.Board.BoardManager BoardManager => boardManager;
        /// <summary>Definition of the most recent Volatile explosion, for presentation sizing.</summary>
        public EnemyDefinition LastExplosionDefinition { get; private set; }
        public event Action<VolatileEnemy> DetonationStarted;
        public event Action<Vector3, int, int> ExplosionOccurred;
        private void Awake()
        {
        }
        private void Start()
        {
            if (gameManager != null) gameManager.StageCompleted += OnStageCompleted;
            SpawnInitialEnemies();
        }
        private void OnDestroy()
        {
            if (gameManager != null) gameManager.StageCompleted -= OnStageCompleted;
        }
        private void OnStageCompleted() => SetMovementSuspended(true);
        private void Update()
        {
            SimulateVolatileInteractions(Time.deltaTime);
            RefreshOccupancy();
        }
        public void SimulateVolatileInteractions(float deltaTime)
        {
            if (gameManager != null && (gameManager.CurrentState != GameplayState.Playing || gameManager.IsPaused)) return;
            if (IsMovementSuspended) return;
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
                Vector2.Distance(position, playerController.transform.position) <= definition.volatileBlastRadius * VolatileRadiusMultiplier + playerController.CollisionRadius;
            var destroyedEnemies = 0;
            for (var i = activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = activeEnemies[i];
                if (enemy == source || enemy is VolatileEnemy) continue;
                if (Vector2.Distance(position, enemy.transform.position) > definition.volatileBlastRadius * VolatileRadiusMultiplier + enemy.CollisionRadius) continue;
                Despawn(enemy);
                destroyedEnemies++;
            }
            var radius = definition.volatileBlastRadius * VolatileRadiusMultiplier;
            Debug.DrawLine(position - Vector3.right * radius, position + Vector3.right * radius, Color.yellow, 1f);
            Debug.DrawLine(position - Vector3.up * radius, position + Vector3.up * radius, Color.yellow, 1f);
            var destroyedTerritory = boardManager.RemoveCapturedWithinRadius(source.LogicalCell, definition.volatileTerritoryRadiusCells * VolatileRadiusMultiplier);
            Despawn(source);
            LastExplosionDefinition = definition;
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
                if (enemy.CollisionRadius <= 0f) continue;
                enemy.GetFootprintCells(footprint);
                for (var j = 0; j < footprint.Count; j++) if (footprint[j] != enemy.LogicalCell) occupancy.Add(footprint[j]);
            }
            if (boardManager != null) boardManager.SetEnemyCells(occupancy);
        }
        public void SpawnInitialEnemies() => SpawnAll(initialSpawns);
        public int SpawnAll(IReadOnlyList<EnemySpawnRequest> requests)
        {
            if (requests == null) return 0;
            var spawned = 0;
            foreach (var request in requests)
                if (request != null && Spawn(request.prefab, request.definition, request.Cell, request.direction)) spawned++;
            return spawned;
        }
        /// <summary>
        /// Spawns extra aliens for the Dense Sector modifier by reusing the stage's first spawn as a template,
        /// placing each one on a random free cell. Returns how many actually made it onto the board.
        /// </summary>
        public int SpawnExtras(IReadOnlyList<EnemySpawnRequest> requests, int count, System.Random random)
        {
            if (requests == null || count <= 0 || boardManager == null || boardManager.Model == null) return 0;
            EnemySpawnRequest template = null;
            foreach (var request in requests)
                if (request != null && request.prefab != null && request.definition != null && request.definition.type == EnemyType.BasicBouncer) { template = request; break; }
            if (template == null) return 0;
            random ??= new System.Random();
            var spawned = 0;
            for (var i = 0; i < count; i++)
            {
                // A handful of attempts is plenty: most of the board is uncaptured when a stage loads.
                for (var attempt = 0; attempt < 32; attempt++)
                {
                    var cell = new GridCoordinate(random.Next(boardManager.Model.Width), random.Next(boardManager.Model.Height));
                    var angle = random.NextDouble() * System.Math.PI * 2d;
                    var direction = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle));
                    if (!Spawn(template.prefab, template.definition, cell, direction)) continue;
                    spawned++;
                    break;
                }
            }
            return spawned;
        }
        public void DespawnAll()
        {
            for (var i = activeEnemies.Count - 1; i >= 0; i--) Despawn(activeEnemies[i]);
            IsMovementSuspended = false;
            RefreshOccupancy();
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
            enemy.SetSpeedMultiplier(SpeedMultiplier);
            enemy.SetIntervalMultiplier(UnstableIntervalMultiplier);
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
            IsMovementSuspended = suspended;
            foreach (var enemy in activeEnemies) enemy.SetMovementSuspended(suspended);
        }
        /// <summary>Stage modifier: scales every active and future alien's speed.</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            SpeedMultiplier = Mathf.Max(.01f, multiplier);
            foreach (var enemy in activeEnemies) enemy.SetSpeedMultiplier(SpeedMultiplier);
        }

        /// <summary>Stage modifier: scales how often Unstable aliens change speed.</summary>
        public void SetUnstableIntervalMultiplier(float multiplier)
        {
            UnstableIntervalMultiplier = Mathf.Max(.01f, multiplier);
            foreach (var enemy in activeEnemies) enemy.SetIntervalMultiplier(UnstableIntervalMultiplier);
        }

        /// <summary>Stage modifier: scales Volatile blast and territory-removal radii.</summary>
        public void SetVolatileRadiusMultiplier(float multiplier) => VolatileRadiusMultiplier = Mathf.Max(.01f, multiplier);

        public void SetDrift(Vector2 drift)
        {
            foreach (var enemy in activeEnemies) enemy.SetDrift(drift);
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
