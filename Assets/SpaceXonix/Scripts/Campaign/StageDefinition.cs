using System.Collections.Generic;
using SpaceXonix.Enemies;
using SpaceXonix.Hazards;
using UnityEngine;

namespace SpaceXonix.Campaign
{
    [CreateAssetMenu(menuName = "SpaceXonix/Stage Definition")]
    public sealed class StageDefinition : ScriptableObject
    {
        [Min(1)] public int stageNumber = 1;
        public string stageName = "Stage";
        [TextArea] public string briefing;
        public EnemySpawnRequest[] enemySpawns;
        public LaserPlacement[] lasers;
        [Tooltip("Extra lasers added on top of the ones above on Hard only.")]
        public LaserPlacement[] hardModeLasers;
        [Tooltip("The Alien Core fought on this stage. Set only on the boss stage.")]
        public SpaceXonix.Boss.BossDefinition boss;

        public bool IsBossStage => boss != null;

        /// <summary>The lasers this stage fires at a difficulty: its own, plus its Hard-only ones on Hard.</summary>
        public List<LaserPlacement> LasersFor(Settings.DifficultyMode difficulty, List<LaserPlacement> results = null)
        {
            results ??= new List<LaserPlacement>();
            results.Clear();
            if (lasers != null) results.AddRange(lasers);
            if (difficulty == Settings.DifficultyMode.Hard && hardModeLasers != null) results.AddRange(hardModeLasers);
            return results;
        }

        /// <summary>Distinct enemy types in spawn order, for the briefing screen.</summary>
        public List<EnemyType> GetEnemyTypes(List<EnemyType> results)
        {
            results.Clear();
            if (enemySpawns == null) return results;
            foreach (var spawn in enemySpawns)
                if (spawn?.definition != null && !results.Contains(spawn.definition.type)) results.Add(spawn.definition.type);
            return results;
        }
    }
}
