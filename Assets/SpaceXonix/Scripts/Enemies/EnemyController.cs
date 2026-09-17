using SpaceXonix.Board;
using SpaceXonix.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpaceXonix.Enemies
{
    public class EnemyController : MonoBehaviour
    {
        protected BoardManager board;
        protected GameManager game;
        protected EnemyDefinition definition;
        protected EnemyMovementModel movement;
        private readonly List<GridCoordinate> traversedCells = new List<GridCoordinate>();
        public GridCoordinate LogicalCell => board.WorldToGrid(transform.position);
        public Vector2 Velocity => movement != null ? movement.Velocity : Vector2.zero;
        public bool MovementEnabled => movement != null && movement.MovementEnabled;
        public EnemyDefinition Definition => definition;
        public bool IsActiveEnemy { get; private set; }
        public IReadOnlyList<GridCoordinate> LastTraversedCells => traversedCells;
        public bool LastTrailHitAccepted { get; private set; }
        public event Action<EnemyController> LogicalCellChanged;
        public virtual void Activate(EnemyDefinition data, BoardManager boardManager, GameManager gameManager, Vector3 position, Vector2 direction)
        {
            definition = data; board = boardManager; game = gameManager;
            transform.position = position;
            movement = new EnemyMovementModel(new Vector2(position.x, position.y), direction.normalized * data.moveSpeed);
            traversedCells.Clear();
            LastTrailHitAccepted = false;
            IsActiveEnemy = true; gameObject.SetActive(true);
        }
        public virtual void Deactivate() { IsActiveEnemy = false; traversedCells.Clear(); LastTrailHitAccepted = false; gameObject.SetActive(false); }
        public void SetMovementSuspended(bool suspended) { if (movement != null) movement.MovementEnabled = !suspended; }
        public void SetDrift(Vector2 drift) { if (movement != null) movement.Drift = drift; }
        public Vector2 Drift => movement != null ? movement.Drift : Vector2.zero;
        public bool IsTrailContact(Vector2 worldPosition)
        {
            var cell = board.WorldToGrid(worldPosition);
            return board.Model.IsInBounds(cell) && board.Model.GetCell(cell) == BoardCellState.Trail;
        }
        protected virtual void Update()
        {
            AdvanceMovement(Time.deltaTime);
        }

        public void AdvanceMovement(float deltaTime)
        {
            if (!IsActiveEnemy || movement == null || board == null) return;
            var lifecycleGeneration = game != null ? game.PlayerLifecycleGeneration : 0;
            var previousCell = LogicalCell;
            var before = movement.Position;
            var intendedPosition = before + movement.EffectiveVelocity * deltaTime;
            board.GetTraversedCells(before, intendedPosition, traversedCells);
            LastTrailHitAccepted = false;
            var player = game != null ? game.PlayerController : null;
            var hitsPlayer = player != null && game.CanProcessPlayerContact(lifecycleGeneration) &&
                Vector2.Distance(intendedPosition, player.transform.position) <= board.CellWorldSize * .6f;
            if (hitsPlayer)
            {
                var accepted = game.ReportPlayerFailure(PlayerFailureReason.EnemyContact);
                if (accepted || !game.CanProcessPlayerContact(lifecycleGeneration)) return;
            }
            for (var i = 0; i < traversedCells.Count; i++)
            {
                if (board.Model.GetCell(traversedCells[i]) != BoardCellState.Trail) continue;
                if (game != null) LastTrailHitAccepted = game.ReportPlayerFailure(PlayerFailureReason.TrailHit);
                if (LastTrailHitAccepted || game != null && !game.CanProcessPlayerContact(lifecycleGeneration)) return;
                break;
            }
            if (game != null && game.PlayerLifecycleGeneration != lifecycleGeneration) return;
            movement.Advance(deltaTime, IsUncapturedWorld);
            transform.position = new Vector3(movement.Position.x, movement.Position.y, transform.position.z);
            if (LogicalCell != previousCell) LogicalCellChanged?.Invoke(this);
            if (player == null || !game.CanProcessPlayerContact(lifecycleGeneration)) return;
            var distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance > board.CellWorldSize * .6f) return;
            game.ReportPlayerFailure(PlayerFailureReason.EnemyContact);
        }
        private bool IsUncapturedWorld(Vector2 world)
        {
            var cell = board.WorldToGrid(world);
            return board.Model.IsInBounds(cell) && board.Model.GetCell(cell) == BoardCellState.Uncaptured;
        }
    }
}
