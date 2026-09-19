using System;
using System.Collections.Generic;
using SpaceXonix.Enemies;
using SpaceXonix.Hazards;
using SpaceXonix.PowerUps;
using SpaceXonix.Scoring;
using UnityEngine;

namespace SpaceXonix.Campaign
{
    /// <summary>
    /// Rolls one compatible modifier per stage and applies its typed effects to the gameplay systems.
    /// Definitions stay immutable; every effect is a runtime multiplier that is cleared between stages.
    /// </summary>
    public sealed class StageModifierManager : MonoBehaviour
    {
        [SerializeField] private StageModifierSetDefinition modifierSet;
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private LaserManager laserManager;
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private SpaceXonix.Boss.BossController bossController;
        [Tooltip("0 uses a time-based seed.")]
        [SerializeField] private int randomSeed;

        private readonly List<EnemyType> typeBuffer = new List<EnemyType>();
        private readonly List<StageModifierDefinition> compatibleBuffer = new List<StageModifierDefinition>();
        private System.Random random;

        public StageModifierDefinition Current { get; private set; }
        public float ScoreMultiplier => Current != null ? Current.scoreMultiplier : 1f;
        public int ExtraEnemies => Current != null ? Current.extraEnemies : 0;
        public event Action<StageModifierDefinition> ModifierSelected;

        private void Awake() => random ??= randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();

        public void SetRandom(System.Random source) => random = source ?? new System.Random();

        /// <summary>Rolls a modifier for the stage and applies it. Returns the chosen modifier, or null when none fits.</summary>
        public StageModifierDefinition SelectFor(StageDefinition stage)
        {
            random ??= new System.Random();
            Current = StageModifierSelection.Select(modifierSet != null ? modifierSet.modifiers : null, stage, random, typeBuffer, compatibleBuffer);
            Apply();
            ModifierSelected?.Invoke(Current);
            return Current;
        }

        /// <summary>Spawns the modifier's extra aliens once the stage's own spawns are already on the board.</summary>
        public int SpawnExtras(StageDefinition stage)
        {
            if (ExtraEnemies <= 0 || enemyManager == null || stage == null) return 0;
            random ??= new System.Random();
            return enemyManager.SpawnExtras(stage.enemySpawns, ExtraEnemies, random);
        }

        public void Clear()
        {
            Current = null;
            Apply();
        }

        public string Describe() => Current == null
            ? "none"
            : $"{Current.displayName} (score x{Current.scoreMultiplier:0.00}) - {Current.effectText}";

        private void Apply()
        {
            var enemySpeed = Current != null ? Current.enemySpeedMultiplier : 1f;
            var laserCooldown = Current != null ? Current.laserCooldownMultiplier : 1f;
            var unstableInterval = Current != null ? Current.unstableIntervalMultiplier : 1f;
            var volatileRadius = Current != null ? Current.volatileRadiusMultiplier : 1f;
            var pickupChance = Current != null ? Current.pickupChanceMultiplier : 1f;

            if (enemyManager != null)
            {
                enemyManager.SetSpeedMultiplier(enemySpeed);
                enemyManager.SetUnstableIntervalMultiplier(unstableInterval);
                enemyManager.SetVolatileRadiusMultiplier(volatileRadius);
            }
            if (bossController != null)
            {
                bossController.SetFireIntervalMultiplier(Current != null ? Current.bossFireIntervalMultiplier : 1f);
                bossController.SetProjectileSpeedMultiplier(Current != null ? Current.bossProjectileSpeedMultiplier : 1f);
            }
            if (laserManager != null) laserManager.SetCooldownMultiplier(laserCooldown);
            if (powerUpManager != null) powerUpManager.SetPickupChanceMultiplier(pickupChance);
            if (scoreManager != null) scoreManager.SetBonusMultiplier(ScoreMultiplier);
        }
    }
}
