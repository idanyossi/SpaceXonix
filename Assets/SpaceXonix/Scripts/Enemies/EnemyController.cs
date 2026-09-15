using SpaceXonix.Board;
using SpaceXonix.Core;
using System;
using UnityEngine;
namespace SpaceXonix.Enemies
{
    public class EnemyController : MonoBehaviour
    {
        protected BoardManager board;
        protected GameManager game;
        protected EnemyDefinition definition;
        protected EnemyMovementModel movement;
        public GridCoordinate LogicalCell => board.WorldToGrid(transform.position);
        public Vector2 Velocity => movement != null ? movement.Velocity : Vector2.zero;
        public bool MovementEnabled => movement != null && movement.MovementEnabled;
        public EnemyDefinition Definition => definition;
        public bool IsActiveEnemy { get; private set; }
        public event Action<EnemyController> LogicalCellChanged;
        public virtual void Activate(EnemyDefinition data, BoardManager boardManager, GameManager gameManager, Vector3 position, Vector2 direction)
        {
            definition = data; board = boardManager; game = gameManager;
            transform.position = position;
            movement = new EnemyMovementModel(new Vector2(position.x, position.y), direction.normalized * data.moveSpeed);
            IsActiveEnemy = true; gameObject.SetActive(true);
        }
        public virtual void Deactivate() { IsActiveEnemy = false; gameObject.SetActive(false); }
        public void SetMovementSuspended(bool suspended) { if (movement != null) movement.MovementEnabled = !suspended; }
        public bool IsTrailContact(Vector2 worldPosition)
        {
            var cell = board.WorldToGrid(worldPosition);
            return board.Model.IsInBounds(cell) && board.Model.GetCell(cell) == BoardCellState.Trail;
        }
        protected virtual void Update()
        {
            if (!IsActiveEnemy || movement == null || board == null) return;
            var previousCell = LogicalCell;
            var before = movement.Position;
            movement.Advance(Time.deltaTime, IsUncapturedWorld);
            if (movement.Position == before && IsTrailContact(before + movement.Velocity * Time.deltaTime) && game != null && game.CurrentState == GameplayState.Playing)
                game.ReportPlayerFailure(PlayerFailureReason.TrailHit);
            transform.position = new Vector3(movement.Position.x, movement.Position.y, transform.position.z);
            if (LogicalCell != previousCell) LogicalCellChanged?.Invoke(this);
            var player = game != null ? game.PlayerController : null;
            if (player == null || game.CurrentState != GameplayState.Playing) return;
            var distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance > board.CellWorldSize * .6f) return;
            if (board.IsPlayerExposed) game.ReportPlayerFailure(PlayerFailureReason.EnemyContact);
        }
        private bool IsUncapturedWorld(Vector2 world)
        {
            var cell = board.WorldToGrid(world);
            return board.Model.IsInBounds(cell) && board.Model.GetCell(cell) == BoardCellState.Uncaptured;
        }
    }
}
