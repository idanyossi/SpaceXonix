using UnityEngine;

namespace SpaceXonix.Player
{
    public sealed class PlayerMovementModel
    {
        public PlayerMovementModel(Vector2 initialPosition, float moveSpeed, CardinalDirection initialDirection)
        {
            Position = initialPosition;
            MoveSpeed = moveSpeed;
            Direction = initialDirection;
        }

        public Vector2 Position { get; private set; }
        public float MoveSpeed { get; set; }
        public CardinalDirection Direction { get; private set; }

        public void SetDirection(CardinalDirection direction)
        {
            Direction = direction;
        }

        public Vector2 Advance(float deltaTime)
        {
            Position += Direction.ToVector2() * (MoveSpeed * deltaTime);
            return Position;
        }
    }
}
