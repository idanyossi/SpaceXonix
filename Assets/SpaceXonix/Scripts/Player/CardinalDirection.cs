using UnityEngine;

namespace SpaceXonix.Player
{
    public enum CardinalDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public static class CardinalDirectionExtensions
    {
        public static Vector2 ToVector2(this CardinalDirection direction)
        {
            return direction switch
            {
                CardinalDirection.Up => Vector2.up,
                CardinalDirection.Down => Vector2.down,
                CardinalDirection.Left => Vector2.left,
                CardinalDirection.Right => Vector2.right,
                _ => Vector2.zero
            };
        }
    }
}
