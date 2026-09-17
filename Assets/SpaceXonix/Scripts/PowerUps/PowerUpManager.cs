using System;
using System.Collections;
using System.Collections.Generic;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
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
        [SerializeField] private Transform arenaCamera;
        [SerializeField, Min(.01f)] private float cameraRollBlendSeconds = .25f;
        [Tooltip("0 uses a time-based seed.")]
        [SerializeField] private int randomSeed;

        private readonly PowerUpSlotModel slot = new PowerUpSlotModel();
        private readonly Dictionary<PowerUpType, Coroutine> effectRoutines = new Dictionary<PowerUpType, Coroutine>();
        private readonly Dictionary<PowerUpType, float> effectEndTimes = new Dictionary<PowerUpType, float>();
        private readonly Dictionary<Renderer, Material> frozenRenderers = new Dictionary<Renderer, Material>();
        private readonly List<GridCoordinate> occupancyBuffer = new List<GridCoordinate>();
        private System.Random random;
        private PowerUpPickup activePickup;
        private float tiltBaseMoveSpeed;
        private Quaternion cameraRestRotation;
        private Coroutine cameraRollRoutine;

        public PowerUpType? StoredPowerUp => slot.Stored;
        public PowerUpType? PendingOffer => slot.PendingOffer;
        public bool IsAwaitingDecision => slot.IsAwaitingDecision;
        public PowerUpPickup ActivePickup => activePickup;
        public Vector2 TiltDrift { get; private set; }
        public event Action<PowerUpPickup> PickupSpawned;
        public event Action<PowerUpType?> StoredChanged;
        public event Action<PowerUpType> DecisionRequested;
        public event Action<PowerUpType> EffectStarted;
        public event Action<PowerUpType> EffectEnded;

        private void Awake()
        {
            random = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
            if (shieldVisual != null) shieldVisual.SetActive(false);
            if (arenaCamera != null) cameraRestRotation = arenaCamera.localRotation;
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
            position.z -= .5f;
            shieldVisual.transform.position = position;
        }

        public void SetRandom(System.Random source) => random = source ?? new System.Random();

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
            if (boardManager.PlayerCell == activePickup.Cell) CollectActivePickup();
        }

        /// <summary>Rolls the capture-size spawn chance. Returns true when a pickup was placed.</summary>
        public bool TrySpawnFromCapture(float capturedPercentage)
        {
            if (activePickup != null || spawnDefinition == null || pickupPrefab == null || poolService == null) return false;
            if (gameManager != null && gameManager.CurrentState != GameplayState.Playing) return false;
            var chance = spawnDefinition.GetSpawnChance(capturedPercentage);
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

        public PickupCollectResult CollectActivePickup()
        {
            if (activePickup == null) return PickupCollectResult.Rejected;
            var offered = activePickup.Definition.type;
            var result = slot.Collect(offered);
            if (result == PickupCollectResult.Rejected) return result;
            ReleasePickup();
            if (result == PickupCollectResult.Stored)
            {
                StoredChanged?.Invoke(slot.Stored);
                return result;
            }
            gameManager?.SetPaused(true);
            DecisionRequested?.Invoke(offered);
            return result;
        }

        public bool ResolvePickupDecision(bool replace)
        {
            if (!slot.ResolveDecision(replace)) return false;
            if (replace) StoredChanged?.Invoke(slot.Stored);
            gameManager?.SetPaused(false);
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

        public void ResetForStage()
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
                    playerController?.SetMoveSpeed(tiltBaseMoveSpeed * (1f - definition.tiltPlayerSlow));
                    BlendCameraRoll(-side * definition.tiltCameraRollDegrees);
                    break;
            }
            effectEndTimes[type] = Time.time + definition.duration;
            if (isActiveAndEnabled) effectRoutines[type] = StartCoroutine(EffectDuration(type, definition.duration));
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
            if (arenaCamera == null) return;
            if (cameraRollRoutine != null) StopCoroutine(cameraRollRoutine);
            var target = cameraRestRotation * Quaternion.Euler(0f, 0f, degrees);
            if (!isActiveAndEnabled || !Application.isPlaying)
            {
                arenaCamera.localRotation = target;
                cameraRollRoutine = null;
                return;
            }
            cameraRollRoutine = StartCoroutine(BlendCameraRollRoutine(target));
        }

        private IEnumerator BlendCameraRollRoutine(Quaternion target)
        {
            var start = arenaCamera.localRotation;
            for (var elapsed = 0f; elapsed < cameraRollBlendSeconds; elapsed += Time.deltaTime)
            {
                arenaCamera.localRotation = Quaternion.Slerp(start, target, elapsed / cameraRollBlendSeconds);
                yield return null;
            }
            arenaCamera.localRotation = target;
            cameraRollRoutine = null;
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
            if (slot.IsAwaitingDecision)
            {
                slot.ResolveDecision(false);
                gameManager?.SetPaused(false);
            }
        }

        private PowerUpDefinition PickRandomDefinition()
        {
            if (powerUps == null || powerUps.Length == 0) return null;
            var count = 0;
            for (var i = 0; i < powerUps.Length; i++) if (powerUps[i] != null) count++;
            if (count == 0) return null;
            var pick = random.Next(count);
            for (var i = 0; i < powerUps.Length; i++)
            {
                if (powerUps[i] == null) continue;
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
