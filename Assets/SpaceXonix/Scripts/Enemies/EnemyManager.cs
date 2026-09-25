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
        [Tooltip("How close, in cells, a hybrid must come to built territory for its charge to go off. Aliens bounce just short of territory, so this must be above zero.")]
        [SerializeField, Min(0f)] private float hybridContactCells = .5f;
        [Tooltip("Largest blast multiplier a hybrid reaches by absorbing Volatiles. Each one absorbed doubles it.")]
        [SerializeField, Min(1f)] private float maxHybridCharge = 4f;
        [Tooltip("Regular aliens one of which arrives, at random, for every alien a Volatile turns into a hybrid, so the stage never runs short of aliens.")]
        [SerializeField] private EnemySpawnRequest[] reinforcements;
        [Tooltip("Reinforcements appear at least this many cells from the ship.")]
        [SerializeField, Min(0)] private int reinforcementClearanceCells = 10;
        [Tooltip("Reinforcements appear within this many cells of the blast that made the hybrid, so the player sees where they came from.")]
        [SerializeField, Min(1)] private int reinforcementSpreadCells = 6;
        [SerializeField] private int randomSeed;
        private System.Random random;
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
        /// <summary>
        /// How much bigger than its definition the most recent explosion was: the stage modifier
        /// times a supercharged hybrid's charge. Presentation multiplies its sizes by this.
        /// </summary>
        public float LastExplosionScale { get; private set; } = 1f;
        public event Action<EnemyController> HybridSupercharged;
        public event Action<VolatileEnemy> DetonationStarted;
        public event Action<Vector3, int, int> ExplosionOccurred;
        private void Awake()
        {
        }
        private void Start()
        {
            if (gameManager != null) gameManager.StageCompleted += OnStageCompleted;
            if (boardManager != null) boardManager.CaptureCompleted += OnCaptureCompleted;
            SpawnInitialEnemies();
        }
        private void OnDestroy()
        {
            if (gameManager != null) gameManager.StageCompleted -= OnStageCompleted;
            if (boardManager != null) boardManager.CaptureCompleted -= OnCaptureCompleted;
        }
        private void OnCaptureCompleted(BoardCaptureResult result) => EjectTrappedEnemies();
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
            {
                if (activeEnemies[i] is VolatileEnemy volatileEnemy) volatileEnemy.AdvanceSpawnProtection(deltaTime);
                activeEnemies[i].AdvanceHybridProtection(deltaTime);
            }
            var reach = boardManager != null ? boardManager.CellWorldSize * hybridContactCells : 0f;
            for (var i = 0; i < activeEnemies.Count; i++)
            {
                if (!activeEnemies[i].IsHybridArmed || !activeEnemies[i].IsTouchingBuiltTerritory(reach, out var contact)) continue;
                ResolveHybridDetonation(activeEnemies[i], contact);
                return;
            }

            for (var i = 0; i < activeEnemies.Count; i++)
            {
                if (gameManager != null && gameManager.PlayerLifecycleGeneration != lifecycleGeneration) return;
                if (!(activeEnemies[i] is VolatileEnemy volatileEnemy) || !volatileEnemy.IsArmed) continue;
                for (var j = 0; j < activeEnemies.Count; j++)
                {
                    if (!volatileEnemy.CanSuperchargeHybrid(activeEnemies[j])) continue;
                    var hybrid = activeEnemies[j];
                    if (!hybrid.Supercharge(maxHybridCharge)) continue;
                    Despawn(volatileEnemy);
                    HybridSupercharged?.Invoke(hybrid);
                    return;
                }
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
            // Aliens caught in the blast are not destroyed, which made Volatiles a board-clearer:
            // each one comes back as a hybrid that keeps its movement and carries the charge.
            var destroyedEnemies = 0;
            for (var i = activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = activeEnemies[i];
                if (enemy == source || enemy is VolatileEnemy || enemy.IsHybrid) continue;
                if (Vector2.Distance(position, enemy.transform.position) > definition.volatileBlastRadius * VolatileRadiusMultiplier + enemy.CollisionRadius) continue;
                enemy.BecomeHybrid(definition);
                destroyedEnemies++;
            }
            var radius = definition.volatileBlastRadius * VolatileRadiusMultiplier;
            Debug.DrawLine(position - Vector3.right * radius, position + Vector3.right * radius, Color.yellow, 1f);
            Debug.DrawLine(position - Vector3.up * radius, position + Vector3.up * radius, Color.yellow, 1f);
            var destroyedTerritory = boardManager.RemoveCapturedWithinRadius(source.LogicalCell, definition.volatileTerritoryRadiusCells * VolatileRadiusMultiplier);
            Despawn(source);
            // One fresh regular alien per hybrid, right beside the blast, so a Volatile never thins
            // the stage out and the player can see the new alien come out of the mix.
            var blastCell = boardManager.WorldToGrid(position);
            for (var i = 0; i < destroyedEnemies; i++) SpawnReinforcement(blastCell);
            LastExplosionDefinition = definition;
            LastExplosionScale = VolatileRadiusMultiplier;
            ExplosionOccurred?.Invoke(position, destroyedEnemies, destroyedTerritory);
            if (hitsPlayer && gameManager.CanProcessPlayerContact(lifecycleGeneration))
                gameManager.ReportPlayerFailure(PlayerFailureReason.VolatileExplosion);
            return true;
        }
        /// <summary>
        /// A hybrid's charge goes off against the player's territory: it blows a hole the size of
        /// the Volatile blast that made it, hits the ship if close, and uses the hybrid up. It never
        /// affects other aliens, so hybrids cannot chain. The hole is centred on the territory it
        /// touched, not on the alien: aliens bounce short of territory, so a hole centred on the
        /// alien would only graze the edge.
        /// </summary>
        public bool ResolveHybridDetonation(EnemyController hybrid, GridCoordinate contact)
        {
            if (hybrid == null || !hybrid.IsHybrid || !activeEnemies.Contains(hybrid)) return false;
            var definition = hybrid.HybridSource;
            var lifecycleGeneration = gameManager != null ? gameManager.PlayerLifecycleGeneration : 0;
            // The blast, its ring and its reach to the ship all come from where the hole is.
            var position = boardManager.GetWorldPosition(contact);
            position.z = hybrid.transform.position.z;
            var scale = VolatileRadiusMultiplier * hybrid.HybridCharge;
            var radius = definition.volatileBlastRadius * scale;
            var hitsPlayer = gameManager != null && playerController != null &&
                gameManager.CanProcessPlayerContact(lifecycleGeneration) &&
                Vector2.Distance(position, playerController.transform.position) <= radius + playerController.CollisionRadius;
            var destroyedTerritory = boardManager.RemoveCapturedWithinRadius(contact, definition.volatileTerritoryRadiusCells * scale);
            Despawn(hybrid);
            LastExplosionDefinition = definition;
            LastExplosionScale = scale;
            ExplosionOccurred?.Invoke(position, 0, destroyedTerritory);
            if (hitsPlayer && gameManager.CanProcessPlayerContact(lifecycleGeneration))
                gameManager.ReportPlayerFailure(PlayerFailureReason.VolatileExplosion);
            return true;
        }

        /// <summary>
        /// Brings in one regular alien, chosen at random from <see cref="reinforcements"/>, on a free
        /// cell near <paramref name="near"/> and away from the ship. If nothing near is free it falls
        /// back to anywhere on the board. Returns false when none is configured or no cell was found.
        /// </summary>
        public bool SpawnReinforcement(GridCoordinate? near = null)
        {
            if (reinforcements == null || reinforcements.Length == 0 || boardManager == null || boardManager.Model == null) return false;
            random ??= randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
            var request = reinforcements[random.Next(reinforcements.Length)];
            if (request == null || request.prefab == null || request.definition == null) return false;
            var player = playerController != null ? boardManager.WorldToGrid(playerController.transform.position) : new GridCoordinate(-1000, -1000);
            // Plenty of attempts: a late stage can have most of the board captured.
            for (var attempt = 0; attempt < 96; attempt++)
            {
                var nearby = near.HasValue && attempt < 48;
                var cell = nearby
                    ? new GridCoordinate(near.Value.X + random.Next(-reinforcementSpreadCells, reinforcementSpreadCells + 1),
                        near.Value.Y + random.Next(-reinforcementSpreadCells, reinforcementSpreadCells + 1))
                    : new GridCoordinate(1 + random.Next(boardManager.Columns - 2), 1 + random.Next(boardManager.Rows - 2));
                if (!boardManager.Model.IsInBounds(cell)) continue;
                var dx = cell.X - player.X; var dy = cell.Y - player.Y;
                if (dx * dx + dy * dy < reinforcementClearanceCells * reinforcementClearanceCells) continue;
                var angle = random.NextDouble() * Math.PI * 2d;
                var direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                if (Spawn(request.prefab, request.definition, cell, direction)) return true;
            }
            return false;
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
        /// <summary>
        /// Frees any alien that captured territory just closed around. Without this an alien caught
        /// on a committed trail - which a Shield lets it survive - sits frozen in the new territory
        /// for the rest of the stage.
        /// </summary>
        public int EjectTrappedEnemies()
        {
            if (boardManager == null || boardManager.Model == null) return 0;
            var freed = 0;
            for (var i = activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = activeEnemies[i];
                if (enemy == null || !enemy.IsActiveEnemy || !enemy.IsTrappedInCapturedTerritory()) continue;
                if (!TryFindOpenPosition(enemy, out var position)) continue;
                enemy.Relocate(position);
                freed++;
            }
            if (freed > 0) RefreshOccupancy();
            return freed;
        }

        /// <summary>
        /// Searches outward from the alien for somewhere its whole body fits, so it re-enters the
        /// open area nearest to where it was trapped rather than jumping across the board.
        /// </summary>
        private bool TryFindOpenPosition(EnemyController enemy, out Vector3 position)
        {
            position = enemy.transform.position;
            var origin = enemy.LogicalCell;
            var maxRadius = Mathf.Max(boardManager.Columns, boardManager.Rows);
            for (var radius = 1; radius <= maxRadius; radius++)
            {
                for (var offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    for (var offsetY = -radius; offsetY <= radius; offsetY++)
                    {
                        // Only the ring at this radius; inner cells were checked already.
                        if (Mathf.Abs(offsetX) != radius && Mathf.Abs(offsetY) != radius) continue;
                        var cell = new GridCoordinate(origin.X + offsetX, origin.Y + offsetY);
                        if (!boardManager.Model.IsInBounds(cell)) continue;
                        if (boardManager.Model.GetCell(cell) != BoardCellState.Uncaptured) continue;
                        var candidate = boardManager.GetWorldPosition(cell);
                        if (!enemy.FitsAt(new Vector2(candidate.x, candidate.y))) continue;
                        position = new Vector3(candidate.x, candidate.y, enemy.transform.position.z);
                        return true;
                    }
                }
            }
            return false;
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
