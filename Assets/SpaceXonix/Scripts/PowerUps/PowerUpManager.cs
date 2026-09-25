using System;
using System.Collections;
using System.Collections.Generic;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using SpaceXonix.Presentation;
using UnityEngine;

namespace SpaceXonix.PowerUps
{
    public sealed class PowerUpManager : MonoBehaviour
    {
        private const int PlacementAttempts = 64;

        [SerializeField] private GameManager gameManager;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private InputRouter inputRouter;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private PoolService poolService;
        [SerializeField] private PowerUpSpawnDefinition spawnDefinition;
        [SerializeField] private PowerUpDefinition[] powerUps;
        [SerializeField] private GameObject pickupPrefab;

        [Header("Presentation")]
        [SerializeField] private GameObject shieldVisual;
        [SerializeField] private Material frozenEnemyMaterial;
        [SerializeField] private ArenaCameraRig cameraRig;
        [Tooltip("0 uses a time-based seed.")]
        [SerializeField] private int randomSeed;

        private readonly PowerUpSlotModel slot = new PowerUpSlotModel();
        private readonly Dictionary<PowerUpType, Coroutine> effectRoutines = new Dictionary<PowerUpType, Coroutine>();
        private readonly Dictionary<PowerUpType, float> effectEndTimes = new Dictionary<PowerUpType, float>();
        private readonly Dictionary<Renderer, Material> frozenRenderers = new Dictionary<Renderer, Material>();
        private readonly List<GridCoordinate> occupancyBuffer = new List<GridCoordinate>();
        private readonly Dictionary<PowerUpType, float> durationMultipliers = new Dictionary<PowerUpType, float>();
        private float tiltPenaltyMultiplier = 1f;
        private float pickupChanceBonus;
        private float pickupChanceMultiplier = 1f;
        private float shipPickupChanceMultiplier = 1f;
        private readonly HashSet<PowerUpType> excludedTypes = new HashSet<PowerUpType>();
        private System.Random random;
        private PowerUpPickup activePickup;
        private SpaceXonix.Presentation.ActorVisual playerVisual;
        private float tiltBaseMoveSpeed;

        public PowerUpType? StoredPowerUp => slot.Stored;
        public PowerUpPickup ActivePickup => activePickup;
        public Vector2 TiltDrift { get; private set; }
        public event Action<PowerUpPickup> PickupSpawned;
        public event Action<PowerUpType?> StoredChanged;
        public event Action<PowerUpType> EffectStarted;
        public event Action<PowerUpType> EffectEnded;

        private void Awake()
        {
            random = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
            if (shieldVisual != null) shieldVisual.SetActive(false);
        }

        private void OnEnable()
        {
            if (boardManager != null) boardManager.CaptureCompleted += OnCaptureCompleted;
            if (inputRouter != null) inputRouter.AbilityRequested += OnAbilityRequested;
            if (gameManager != null)
            {
                gameManager.StageCompleted += EndRun;
                gameManager.GameOver += EndRun;
            }
        }

        private void OnDisable()
        {
            if (boardManager != null) boardManager.CaptureCompleted -= OnCaptureCompleted;
            if (inputRouter != null) inputRouter.AbilityRequested -= OnAbilityRequested;
            if (gameManager != null)
            {
                gameManager.StageCompleted -= EndRun;
                gameManager.GameOver -= EndRun;
            }
            EndAllEffects();
        }

        private void Update() => Tick(Time.deltaTime);

        private void LateUpdate()
        {
            if (shieldVisual == null || !shieldVisual.activeSelf || playerController == null) return;
            var position = playerController.transform.position;
            // Sit at the ship's hovering visual rather than on the board plane.
            if (playerVisual == null) playerVisual = playerController.GetComponent<SpaceXonix.Presentation.ActorVisual>();
            position.z -= playerVisual != null ? playerVisual.HoverHeight : .55f;
            shieldVisual.transform.position = position;
        }

        public void SetRandom(System.Random source) => random = source ?? new System.Random();

        /// <summary>Run upgrades lengthen an ability without mutating its definition asset.</summary>
        public void SetDurationMultiplier(PowerUpType type, float multiplier) => durationMultipliers[type] = Mathf.Max(0f, multiplier);

        /// <summary>Run upgrades soften the Arena Tilt movement penalty (1 = full penalty, 0 = none).</summary>
        public void SetTiltPenaltyMultiplier(float multiplier) => tiltPenaltyMultiplier = Mathf.Clamp01(multiplier);

        /// <summary>Run upgrades raise the pickup spawn chance before the configured maximum.</summary>
        public void SetPickupChanceBonus(float bonus) => pickupChanceBonus = Mathf.Max(0f, bonus);

        /// <summary>Stage modifier (Resource Shortage): scales the rolled pickup chance.</summary>
        public void SetPickupChanceMultiplier(float multiplier) => pickupChanceMultiplier = Mathf.Max(0f, multiplier);

        /// <summary>The flown ship's pickup multiplier. Kept apart from the stage modifier's so neither overwrites the other.</summary>
        public void SetShipPickupChanceMultiplier(float multiplier) => shipPickupChanceMultiplier = Mathf.Max(0f, multiplier);

        /// <summary>Keeps a pickup type out of the roll for one stage, e.g. Arena Tilt on the alien-free boss stage.</summary>
        public void SetTypeExcluded(PowerUpType type, bool excluded)
        {
            if (excluded) excludedTypes.Add(type); else excludedTypes.Remove(type);
        }

        public void ClearExcludedTypes() => excludedTypes.Clear();
        public bool IsTypeExcluded(PowerUpType type) => excludedTypes.Contains(type);

        public float GetEffectDuration(PowerUpDefinition definition) =>
            definition == null ? 0f : definition.duration * (durationMultipliers.TryGetValue(definition.type, out var multiplier) ? multiplier : 1f);

        public bool IsEffectActive(PowerUpType type) => effectEndTimes.ContainsKey(type);

        public float GetEffectRemaining(PowerUpType type) =>
            effectEndTimes.TryGetValue(type, out var end) ? Mathf.Max(0f, end - Time.time) : 0f;

        public PowerUpDefinition GetDefinition(PowerUpType type)
        {
            if (powerUps == null) return null;
            for (var i = 0; i < powerUps.Length; i++) if (powerUps[i] != null && powerUps[i].type == type) return powerUps[i];
            return null;
        }

        public void Tick(float deltaTime)
        {
            if (activePickup == null || (gameManager != null && gameManager.IsPaused)) return;
            if (!activePickup.Tick(deltaTime))
            {
                ReleasePickup();
                return;
            }
            if (gameManager != null && gameManager.CurrentState != GameplayState.Playing) return;
            if (IsTouchingPlayer(activePickup)) CollectActivePickup();
        }

        /// <summary>Rolls the capture-size spawn chance. Returns true when a pickup was placed.</summary>
        public bool TrySpawnFromCapture(float capturedPercentage)
        {
            if (activePickup != null || spawnDefinition == null || pickupPrefab == null || poolService == null) return false;
            if (gameManager != null && gameManager.CurrentState != GameplayState.Playing) return false;
            var chance = spawnDefinition.GetSpawnChance(capturedPercentage);
            if (chance > 0f) chance = Mathf.Min(spawnDefinition.maxChance, chance + pickupChanceBonus) * pickupChanceMultiplier * shipPickupChanceMultiplier;
            if (chance <= 0f || random.NextDouble() >= chance) return false;
            var definition = PickRandomDefinition();
            if (definition == null || !TryFindSpawnCell(out var cell)) return false;
            var instance = poolService.Acquire(pickupPrefab, transform);
            var pickup = instance.GetComponent<PowerUpPickup>();
            if (pickup == null)
            {
                poolService.Release(pickupPrefab, instance);
                Debug.LogError("Pickup prefab requires a PowerUpPickup.", this);
                return false;
            }
            var position = boardManager.GetWorldPosition(cell);
            position.z -= .01f;
            pickup.Configure(definition, cell, position, spawnDefinition.pickupLifetime);
            activePickup = pickup;
            PickupSpawned?.Invoke(pickup);
            return true;
        }

        /// <summary>Stores the board pickup, immediately replacing any held ability. Returns false when no pickup exists.</summary>
        public bool CollectActivePickup()
        {
            if (activePickup == null) return false;
            slot.Collect(activePickup.Definition.type);
            ReleasePickup();
            StoredChanged?.Invoke(slot.Stored);
            return true;
        }

        public bool TryUseStoredAbility()
        {
            if (gameManager != null && (gameManager.CurrentState != GameplayState.Playing || gameManager.IsPaused)) return false;
            if (!slot.HasStored || GetDefinition(slot.Stored.Value) == null) return false;
            slot.TryConsume(out var type);
            StoredChanged?.Invoke(null);
            Activate(GetDefinition(type));
            return true;
        }

        public void ExpireEffect(PowerUpType type)
        {
            if (effectRoutines.TryGetValue(type, out var routine) && routine != null) StopCoroutine(routine);
            effectRoutines.Remove(type);
            if (!effectEndTimes.Remove(type)) return;
            switch (type)
            {
                case PowerUpType.Shield:
                    gameManager?.SetShieldActive(false);
                    if (shieldVisual != null) shieldVisual.SetActive(false);
                    break;
                case PowerUpType.Freeze:
                    RestoreFrozenMaterials();
                    if (enemyManager != null && (gameManager == null || gameManager.CurrentState != GameplayState.StageComplete))
                        enemyManager.SetMovementSuspended(false);
                    break;
                case PowerUpType.ArenaTilt:
                    TiltDrift = Vector2.zero;
                    enemyManager?.SetDrift(Vector2.zero);
                    playerController?.SetMoveSpeed(tiltBaseMoveSpeed);
                    BlendCameraRoll(0f);
                    break;
            }
            EffectEnded?.Invoke(type);
        }

        public void EndAllEffects()
        {
            ExpireEffect(PowerUpType.Shield);
            ExpireEffect(PowerUpType.Freeze);
            ExpireEffect(PowerUpType.ArenaTilt);
        }

        /// <summary>Ends effects and board pickups between stages; the stored ability carries over.</summary>
        public void PrepareForStage() => EndRun();

        public void ResetForCampaign()
        {
            EndRun();
            slot.Clear();
            StoredChanged?.Invoke(null);
        }

        private void Activate(PowerUpDefinition definition)
        {
            var type = definition.type;
            ExpireEffect(type);
            switch (type)
            {
                case PowerUpType.Shield:
                    gameManager?.SetShieldActive(true);
                    if (shieldVisual != null) shieldVisual.SetActive(true);
                    break;
                case PowerUpType.Freeze:
                    enemyManager?.SetMovementSuspended(true);
                    ApplyFrozenMaterials();
                    break;
                case PowerUpType.ArenaTilt:
                    tiltBaseMoveSpeed = playerController != null ? playerController.MoveSpeed : 0f;
                    var side = random.Next(2) == 0 ? -1f : 1f;
                    TiltDrift = new Vector2(side * definition.tiltEnemyDrift, 0f);
                    enemyManager?.SetDrift(TiltDrift);
                    playerController?.SetMoveSpeed(tiltBaseMoveSpeed * (1f - definition.tiltPlayerSlow * tiltPenaltyMultiplier));
                    // A positive camera roll lowers the right side of the arena on screen, so aliens drift downhill.
                    BlendCameraRoll(side * definition.tiltCameraRollDegrees);
                    break;
            }
            var duration = GetEffectDuration(definition);
            effectEndTimes[type] = Time.time + duration;
            if (isActiveAndEnabled) effectRoutines[type] = StartCoroutine(EffectDuration(type, duration));
            EffectStarted?.Invoke(type);
        }

        private IEnumerator EffectDuration(PowerUpType type, float duration)
        {
            yield return new WaitForSeconds(duration);
            effectRoutines.Remove(type);
            ExpireEffect(type);
        }

        private void BlendCameraRoll(float degrees)
        {
            if (cameraRig != null) cameraRig.SetRoll(degrees);
        }

        private void ApplyFrozenMaterials()
        {
            if (frozenEnemyMaterial == null || enemyManager == null) return;
            var enemies = enemyManager.ActiveEnemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemyRenderer = enemies[i].GetComponentInChildren<Renderer>();
                if (enemyRenderer == null || frozenRenderers.ContainsKey(enemyRenderer)) continue;
                frozenRenderers[enemyRenderer] = enemyRenderer.sharedMaterial;
                enemyRenderer.sharedMaterial = frozenEnemyMaterial;
            }
        }

        private void RestoreFrozenMaterials()
        {
            foreach (var pair in frozenRenderers) if (pair.Key != null) pair.Key.sharedMaterial = pair.Value;
            frozenRenderers.Clear();
        }

        private void OnCaptureCompleted(BoardCaptureResult result) => TrySpawnFromCapture(result.PercentageGained);

        private void OnAbilityRequested() => TryUseStoredAbility();

        private void EndRun()
        {
            EndAllEffects();
            ReleasePickup();
        }

        private bool IsTouchingPlayer(PowerUpPickup pickup)
        {
            if (boardManager.PlayerCell == pickup.Cell) return true;
            if (playerController == null || spawnDefinition == null) return false;
            var reach = spawnDefinition.pickupRadius + playerController.CollisionRadius;
            return Vector2.Distance(playerController.transform.position, pickup.transform.position) <= reach;
        }

        private bool IsRollable(PowerUpDefinition definition) => definition != null && !excludedTypes.Contains(definition.type);

        private PowerUpDefinition PickRandomDefinition()
        {
            if (powerUps == null || powerUps.Length == 0) return null;
            var count = 0;
            for (var i = 0; i < powerUps.Length; i++) if (IsRollable(powerUps[i])) count++;
            if (count == 0) return null;
            var pick = random.Next(count);
            for (var i = 0; i < powerUps.Length; i++)
            {
                if (!IsRollable(powerUps[i])) continue;
                if (pick-- == 0) return powerUps[i];
            }
            return null;
        }

        private bool TryFindSpawnCell(out GridCoordinate cell)
        {
            occupancyBuffer.Clear();
            if (enemyManager != null)
                for (var i = 0; i < enemyManager.ActiveEnemies.Count; i++) occupancyBuffer.Add(enemyManager.ActiveEnemies[i].LogicalCell);
            var model = boardManager.Model;
            var minDistance = spawnDefinition.minimumEnemyDistanceCells;
            for (var attempt = 0; attempt < PlacementAttempts; attempt++)
            {
                var candidate = new GridCoordinate(random.Next(1, model.Width - 1), random.Next(1, model.Height - 1));
                if (model.GetCell(candidate) != BoardCellState.Uncaptured || candidate == boardManager.PlayerCell) continue;
                if (IsNearEnemy(candidate, minDistance)) continue;
                cell = candidate;
                return true;
            }
            cell = default;
            return false;
        }

        private bool IsNearEnemy(GridCoordinate cell, int minDistance)
        {
            for (var i = 0; i < occupancyBuffer.Count; i++)
            {
                var enemy = occupancyBuffer[i];
                if (Mathf.Abs(enemy.X - cell.X) + Mathf.Abs(enemy.Y - cell.Y) < minDistance) return true;
            }
            return false;
        }

        private void ReleasePickup()
        {
            if (activePickup == null) return;
            poolService.Release(pickupPrefab, activePickup.gameObject);
            activePickup = null;
        }
    }
}
