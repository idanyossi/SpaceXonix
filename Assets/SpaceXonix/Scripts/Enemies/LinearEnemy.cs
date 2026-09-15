using UnityEngine;
namespace SpaceXonix.Enemies
{
    public sealed class LinearEnemy : EnemyController
    {
        public override void Activate(EnemyDefinition data, SpaceXonix.Board.BoardManager board, SpaceXonix.Core.GameManager game, Vector3 position, Vector2 direction)
        {
            var axisDirection = data.linearAxis == EnemyAxis.Horizontal ? new Vector2(Mathf.Sign(direction.x == 0 ? 1 : direction.x), 0) : new Vector2(0, Mathf.Sign(direction.y == 0 ? 1 : direction.y));
            base.Activate(data, board, game, position, axisDirection);
        }
    }
}
