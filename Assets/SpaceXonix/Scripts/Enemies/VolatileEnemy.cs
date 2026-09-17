using UnityEngine;

namespace SpaceXonix.Enemies
{
    public sealed class VolatileEnemy : EnemyController
    {
        private float spawnProtectionRemaining;

        public float SpawnProtectionRemaining => spawnProtectionRemaining;
        public bool IsArmed => IsActiveEnemy && !HasDetonated && spawnProtectionRemaining <= 0f;
        public bool HasDetonated { get; private set; }

        public override void Activate(EnemyDefinition data, SpaceXonix.Board.BoardManager boardManager,
            SpaceXonix.Core.GameManager gameManager, Vector3 position, Vector2 direction)
        {
            base.Activate(data, boardManager, gameManager, position, direction);
            spawnProtectionRemaining = data.volatileSpawnProtection;
            HasDetonated = false;
        }

        public void AdvanceSpawnProtection(float deltaTime)
        {
            if (!IsActiveEnemy || HasDetonated || deltaTime <= 0f) return;
            spawnProtectionRemaining = Mathf.Max(0f, spawnProtectionRemaining - deltaTime);
        }

        public bool CanDetonateWith(EnemyController other)
        {
            if (!IsArmed || other == null || other == this || !other.IsActiveEnemy || other is VolatileEnemy) return false;
            var touching = Mathf.Max(definition.volatileCollisionRadius, CollisionRadius + other.CollisionRadius);
            return Vector2.Distance(transform.position, other.transform.position) <= touching;
        }

        public bool BeginDetonation()
        {
            if (!IsArmed) return false;
            HasDetonated = true;
            SetMovementSuspended(true);
            return true;
        }
    }
}
