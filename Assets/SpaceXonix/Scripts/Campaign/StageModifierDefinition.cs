using System.Collections.Generic;
using SpaceXonix.Enemies;
using UnityEngine;

namespace SpaceXonix.Campaign
{
    [CreateAssetMenu(menuName = "SpaceXonix/Stage Modifier Definition")]
    public sealed class StageModifierDefinition : ScriptableObject
    {
        public string displayName = "Modifier";
        [TextArea] public string effectText = "";
        [Tooltip("Score multiplier awarded while this modifier is active.")]
        [Min(1f)] public float scoreMultiplier = 1.15f;

        [Header("Compatibility")]
        [Tooltip("Only offered on stages that spawn this enemy type.")]
        public bool requiresEnemyType;
        public EnemyType requiredEnemyType = EnemyType.Unstable;
        [Tooltip("Only offered on stages that have laser emitters.")]
        public bool requiresLasers;

        [Header("Typed effects (1 = unchanged)")]
        [Min(.1f)] public float enemySpeedMultiplier = 1f;
        [Min(.1f)] public float laserCooldownMultiplier = 1f;
        [Min(.1f)] public float unstableIntervalMultiplier = 1f;
        [Min(.1f)] public float volatileRadiusMultiplier = 1f;
        [Min(0f)] public float pickupChanceMultiplier = 1f;
        [Tooltip("Extra standard aliens spawned on top of the stage's own spawns.")]
        [Min(0)] public int extraEnemies;

        /// <summary>A modifier only appears on a stage that actually contains what it modifies.</summary>
        public bool IsCompatibleWith(StageDefinition stage, List<EnemyType> buffer)
        {
            if (stage == null) return false;
            if (requiresLasers && (stage.lasers == null || stage.lasers.Length == 0)) return false;
            if (extraEnemies > 0 && (stage.enemySpawns == null || stage.enemySpawns.Length == 0)) return false;
            if (!requiresEnemyType) return true;
            return stage.GetEnemyTypes(buffer).Contains(requiredEnemyType);
        }
    }

    [CreateAssetMenu(menuName = "SpaceXonix/Stage Modifier Set")]
    public sealed class StageModifierSetDefinition : ScriptableObject
    {
        public StageModifierDefinition[] modifiers;
    }

    /// <summary>Picks one compatible modifier per stage.</summary>
    public static class StageModifierSelection
    {
        public static StageModifierDefinition Select(IReadOnlyList<StageModifierDefinition> pool, StageDefinition stage,
            System.Random random, List<EnemyType> buffer, List<StageModifierDefinition> compatibleBuffer)
        {
            compatibleBuffer.Clear();
            if (pool == null || stage == null) return null;
            for (var i = 0; i < pool.Count; i++)
                if (pool[i] != null && pool[i].IsCompatibleWith(stage, buffer)) compatibleBuffer.Add(pool[i]);
            if (compatibleBuffer.Count == 0) return null;
            random ??= new System.Random();
            return compatibleBuffer[random.Next(compatibleBuffer.Count)];
        }
    }
}
