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
        [Tooltip("The Alien Core fought on this stage. Set only on the boss stage.")]
        public SpaceXonix.Boss.BossDefinition boss;

        public bool IsBossStage => boss != null;

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
