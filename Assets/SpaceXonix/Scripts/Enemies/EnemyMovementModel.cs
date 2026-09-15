using UnityEngine;
namespace SpaceXonix.Enemies
{
    public sealed class EnemyMovementModel
    {
        public EnemyMovementModel(Vector2 position, Vector2 velocity) { Position = position; Velocity = velocity; }
        public Vector2 Position { get; private set; }
        public Vector2 Velocity { get; private set; }
        public bool MovementEnabled { get; set; } = true;
        public void Reset(Vector2 position, Vector2 velocity) { Position = position; Velocity = velocity; MovementEnabled = true; }
        public void Advance(float deltaTime, System.Func<Vector2, bool> isPassable)
        {
            if (!MovementEnabled) return;
            var candidate = Position + Velocity * deltaTime;
            if (isPassable(candidate)) { Position = candidate; return; }
            var horizontal = new Vector2(Position.x + Velocity.x * deltaTime, Position.y);
            var vertical = new Vector2(Position.x, Position.y + Velocity.y * deltaTime);
            var reflectX = !isPassable(horizontal); var reflectY = !isPassable(vertical);
            if (reflectX) Velocity = new Vector2(-Velocity.x, Velocity.y);
            if (reflectY) Velocity = new Vector2(Velocity.x, -Velocity.y);
        }
        public void SetSpeed(float speed) => Velocity = Velocity.normalized * speed;
    }
}
