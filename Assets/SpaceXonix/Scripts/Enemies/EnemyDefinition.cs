using UnityEngine;
namespace SpaceXonix.Enemies
{
    [CreateAssetMenu(menuName = "SpaceXonix/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public EnemyType type;
        [Min(0.01f)] public float moveSpeed = 1.5f;
        public EnemyAxis linearAxis = EnemyAxis.Horizontal;
        [Min(0.01f)] public float unstableMinSpeed = 0.8f;
        [Min(0.01f)] public float unstableMaxSpeed = 2.2f;
        [Min(0.01f)] public float unstableInterval = 1.5f;
        [Min(0f)] public float volatileSpawnProtection = 0.5f;
        [Min(0.01f)] public float volatileCollisionRadius = 0.2f;
        [Min(0.01f)] public float volatileBlastRadius = 0.75f;
        [Min(0f)] public float volatileTerritoryRadiusCells = 4f;
    }
}
