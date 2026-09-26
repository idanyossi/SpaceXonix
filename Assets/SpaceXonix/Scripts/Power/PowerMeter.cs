using System;
using System.Collections.Generic;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using UnityEngine;

namespace SpaceXonix.Power
{
    public sealed class PowerMeter : MonoBehaviour
    {
        [SerializeField] private PowerDefinition definition;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private InputRouter inputRouter;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private SpaceXonix.Boss.BossController bossController;
        [SerializeField] private PoolService poolService;
        [SerializeField] private GameObject shotPrefab;

        private readonly List<PowerShotProjectile> activeShots = new List<PowerShotProjectile>();
        private PowerMeterModel model;

        public float Power => model?.Power ?? 0f;
        public float MaxPower => model?.MaxPower ?? 100f;
        public bool IsReady => model != null && model.IsFull;
        public IReadOnlyList<PowerShotProjectile> ActiveShots => activeShots;
        public event Action<float> PowerChanged;
        public event Action PowerFull;
        public event Action<PowerShotProjectile> ShotFired;
        public event Action<EnemyController> EnemyDestroyedByShot;
        /// <summary>Where a shot struck something: an alien it destroyed, or the boss it stunned.</summary>
        public event Action<Vector3> ShotImpact;

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (model != null) return;
            if (definition == null)
            {
                Debug.LogError("PowerMeter requires a PowerDefinition.", this);
                enabled = false;
                return;
            }
            model = new PowerMeterModel(definition.maxPower, definition.powerPerCapturedPercent);
        }

        private void OnEnable()
        {
            if (boardManager != null) boardManager.CaptureCompleted += OnCaptureCompleted;
            if (inputRouter != null) inputRouter.PowerShotRequested += OnPowerShotRequested;
            if (gameManager != null) gameManager.StageCompleted += ReleaseAllShots;
        }

        private void OnDisable()
        {
            if (boardManager != null) boardManager.CaptureCompleted -= OnCaptureCompleted;
            if (inputRouter != null) inputRouter.PowerShotRequested -= OnPowerShotRequested;
            if (gameManager != null) gameManager.StageCompleted -= ReleaseAllShots;
            ReleaseAllShots();
        }

        private void Update() => AdvanceShots(Time.deltaTime);

        public void SetGainMultiplier(float multiplier) => model?.SetGainMultiplier(multiplier);

        /// <summary>Scales how fast the Power Shot flies. Some ships charge faster but shoot slower.</summary>
        public void SetShotSpeedMultiplier(float multiplier) => ShotSpeedMultiplier = Mathf.Max(.1f, multiplier);
        public float ShotSpeedMultiplier { get; private set; } = 1f;

        /// <summary>Clears in-flight shots between stages; stored charge carries over.</summary>
        public void PrepareForStage() => ReleaseAllShots();

        public void ResetMeter()
        {
            if (model == null) return;
            ReleaseAllShots();
            model.Reset();
            PowerChanged?.Invoke(model.Power);
        }

        public bool TryFirePowerShot()
        {
            if (model == null || playerController == null || shotPrefab == null || poolService == null) return false;
            if (gameManager != null && gameManager.CurrentState != GameplayState.Playing) return false;
            if (!model.TryConsumeFull()) return false;
            var instance = poolService.Acquire(shotPrefab, transform);
            var shot = instance.GetComponent<PowerShotProjectile>();
            if (shot == null)
            {
                poolService.Release(shotPrefab, instance);
                Debug.LogError("Power shot prefab requires a PowerShotProjectile.", this);
                return false;
            }
            var origin = playerController.transform.position;
            origin.z -= .01f;
            shot.Launch(origin, playerController.FacingDirection, definition.shotSpeed * ShotSpeedMultiplier);
            activeShots.Add(shot);
            PowerChanged?.Invoke(model.Power);
            ShotFired?.Invoke(shot);
            return true;
        }

        public void AdvanceShots(float deltaTime)
        {
            if (activeShots.Count == 0) return;
            var enemies = enemyManager != null ? enemyManager.ActiveEnemies : null;
            var bounds = GetBoardBounds();
            for (var i = activeShots.Count - 1; i >= 0; i--)
            {
                var shot = activeShots[i];
                var from = (Vector2)shot.transform.position;
                var step = shot.Advance(deltaTime, enemies, definition.shotHitRadius, bounds, out var hitEnemy);
                // The boss cannot be shot down, but a hit stops its attack cycle and spends the shot.
                if (bossController != null && bossController.TryInterceptShot(from, shot.transform.position, definition.shotHitRadius))
                {
                    var struck = shot.transform.position;
                    ReleaseShotAt(i);
                    ShotImpact?.Invoke(struck);
                    continue;
                }
                if (step == PowerShotStep.Moving) continue;
                ReleaseShotAt(i);
                if (step != PowerShotStep.HitEnemy || hitEnemy == null) continue;
                var at = hitEnemy.transform.position;
                enemyManager.Despawn(hitEnemy);
                EnemyDestroyedByShot?.Invoke(hitEnemy);
                ShotImpact?.Invoke(at);
            }
        }

        private void OnCaptureCompleted(BoardCaptureResult result)
        {
            if (model == null) return;
            var wasFull = model.IsFull;
            if (model.AddCapture(result.PercentageGained) <= 0f) return;
            PowerChanged?.Invoke(model.Power);
            if (!wasFull && model.IsFull) PowerFull?.Invoke();
        }

        private void OnPowerShotRequested() => TryFirePowerShot();

        private Rect GetBoardBounds()
        {
            if (boardManager == null) return new Rect(float.MinValue / 2f, float.MinValue / 2f, float.MaxValue, float.MaxValue);
            var min = boardManager.transform.TransformPoint(Vector3.zero);
            var max = boardManager.transform.TransformPoint(new Vector3(boardManager.Columns * boardManager.CellWorldSize,
                boardManager.Rows * boardManager.CellWorldSize, 0f));
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private void ReleaseAllShots()
        {
            for (var i = activeShots.Count - 1; i >= 0; i--) ReleaseShotAt(i);
        }

        private void ReleaseShotAt(int index)
        {
            var shot = activeShots[index];
            activeShots.RemoveAt(index);
            if (shot == null) return;
            shot.Stop();
            if (poolService != null && shotPrefab != null) poolService.Release(shotPrefab, shot.gameObject);
        }
    }
}
