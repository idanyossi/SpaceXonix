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
        private static readonly List<GridCoordinate> FootprintBuffer = new List<GridCoordinate>();
        private readonly List<GridCoordinate> traversedCells = new List<GridCoordinate>();
        public GridCoordinate LogicalCell => board.WorldToGrid(transform.position);
        public Vector2 Velocity => movement != null ? movement.Velocity : Vector2.zero;
        public bool MovementEnabled => movement != null && movement.MovementEnabled;
        public EnemyDefinition Definition => definition;
        public float CollisionRadius => definition != null ? definition.collisionRadius : 0f;
        /// <summary>Stage-modifier speed scale (Overclocked Swarm). 1 leaves the definition speed untouched.</summary>
        public float SpeedMultiplier { get; private set; } = 1f;
        /// <summary>Stage-modifier scale for the Unstable speed-change cadence (Unstable Space).</summary>
        public float IntervalMultiplier { get; private set; } = 1f;
        public bool IsActiveEnemy { get; private set; }
        public IReadOnlyList<GridCoordinate> LastTraversedCells => traversedCells;
        public bool LastTrailHitAccepted { get; private set; }
        /// <summary>Set when this alien touched a shielded ship; its trail hits are ignored until its body has fully left the trail.</summary>
        public bool HasShieldPassThroughGrace { get; private set; }
        public event Action<EnemyController> LogicalCellChanged;
        public virtual void Activate(EnemyDefinition data, BoardManager boardManager, GameManager gameManager, Vector3 position, Vector2 direction)
        {
            definition = data; board = boardManager; game = gameManager;
            transform.position = position;
            movement = new EnemyMovementModel(new Vector2(position.x, position.y), direction.normalized * data.moveSpeed * SpeedMultiplier);
            traversedCells.Clear();
            LastTrailHitAccepted = false;
            HasShieldPassThroughGrace = false;
            IsActiveEnemy = true; gameObject.SetActive(true);
        }
        public virtual void Deactivate() { IsActiveEnemy = false; traversedCells.Clear(); LastTrailHitAccepted = false; HasShieldPassThroughGrace = false; gameObject.SetActive(false); }
        public void SetMovementSuspended(bool suspended) { if (movement != null) movement.MovementEnabled = !suspended; }

        /// <summary>Applies a stage modifier's speed scale, rescaling current motion without touching the definition.</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            multiplier = Mathf.Max(.01f, multiplier);
            if (movement != null && SpeedMultiplier > 0f) movement.SetSpeed(movement.Velocity.magnitude / SpeedMultiplier * multiplier);
            SpeedMultiplier = multiplier;
        }

        public void SetIntervalMultiplier(float multiplier) => IntervalMultiplier = Mathf.Max(.01f, multiplier);
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
            if (game != null && game.CurrentState == GameplayState.Briefing) return;
            var lifecycleGeneration = game != null ? game.PlayerLifecycleGeneration : 0;
            var previousCell = LogicalCell;
            var before = movement.Position;
            var intendedPosition = before + movement.EffectiveVelocity * deltaTime;
            board.GetTraversedCells(before, intendedPosition, traversedCells);
            AddFootprint(intendedPosition, traversedCells);
            LastTrailHitAccepted = false;
            var player = game != null ? game.PlayerController : null;
            var hitsPlayer = player != null && game.CanProcessPlayerContact(lifecycleGeneration) &&
                Vector2.Distance(intendedPosition, player.transform.position) <= GetPlayerContactDistance(player);
            if (hitsPlayer)
            {
                var accepted = game.ReportPlayerFailure(PlayerFailureReason.EnemyContact);
                if (accepted || !game.CanProcessPlayerContact(lifecycleGeneration)) return;
                if (game.IsShieldActive) HasShieldPassThroughGrace = true;
            }
            for (var i = 0; i < traversedCells.Count; i++)
            {
                if (board.Model.GetCell(traversedCells[i]) != BoardCellState.Trail) continue;
                if (HasShieldPassThroughGrace) break;
                // Trail under a shielded ship's body is ship contact, which the shield blocks; the rest of the trail stays vulnerable.
                if (game != null && game.IsShieldActive && player != null && IsUnderShip(traversedCells[i], player)) continue;
                if (game != null) LastTrailHitAccepted = game.ReportPlayerFailure(PlayerFailureReason.TrailHit);
                if (LastTrailHitAccepted || game != null && !game.CanProcessPlayerContact(lifecycleGeneration)) return;
                break;
            }
            if (game != null && game.PlayerLifecycleGeneration != lifecycleGeneration) return;
            var startsBlocked = !IsFootprintPassable(before);
            movement.Advance(deltaTime, startsBlocked ? (System.Func<Vector2, bool>)IsUncapturedWorld : IsFootprintPassable);
            transform.position = new Vector3(movement.Position.x, movement.Position.y, transform.position.z);
            if (LogicalCell != previousCell) LogicalCellChanged?.Invoke(this);
            if (player == null || !game.CanProcessPlayerContact(lifecycleGeneration)) return;
            var distance = Vector2.Distance(transform.position, player.transform.position);
            var touchingShip = distance <= GetPlayerContactDistance(player);
            if (touchingShip && !game.ReportPlayerFailure(PlayerFailureReason.EnemyContact) && game.IsShieldActive)
                HasShieldPassThroughGrace = true;
            if (HasShieldPassThroughGrace && !touchingShip && !IsBodyOnTrail()) HasShieldPassThroughGrace = false;
        }

        private bool IsBodyOnTrail()
        {
            board.GetCellsOverlappingCircle(transform.position, CollisionRadius, FootprintBuffer);
            for (var i = 0; i < FootprintBuffer.Count; i++)
                if (board.Model.GetCell(FootprintBuffer[i]) == BoardCellState.Trail) return true;
            return false;
        }
        /// <summary>Radius-free legacy definitions keep the original 0.6-cell contact distance.</summary>
        public float GetPlayerContactDistance(SpaceXonix.Player.PlayerController player) =>
            Mathf.Max(board.CellWorldSize * .6f, CollisionRadius + (player != null ? player.CollisionRadius : 0f));

        public void GetFootprintCells(List<GridCoordinate> cells) => board.GetCellsOverlappingCircle(transform.position, CollisionRadius, cells);

        private void AddFootprint(Vector2 center, List<GridCoordinate> cells)
        {
            if (CollisionRadius <= 0f) return;
            board.GetCellsOverlappingCircle(center, CollisionRadius, FootprintBuffer);
            for (var i = 0; i < FootprintBuffer.Count; i++) if (!cells.Contains(FootprintBuffer[i])) cells.Add(FootprintBuffer[i]);
        }

        private bool IsUnderShip(GridCoordinate cell, SpaceXonix.Player.PlayerController player) =>
            cell == board.PlayerCell || board.CellOverlapsCircle(cell, player.transform.position, player.CollisionRadius);

        /// <summary>The whole alien body must stay inside uncaptured space.</summary>
        private bool IsFootprintPassable(Vector2 world)
        {
            if (CollisionRadius <= 0f) return IsUncapturedWorld(world);
            board.GetCellsOverlappingCircle(world, CollisionRadius, FootprintBuffer);
            if (FootprintBuffer.Count == 0) return false;
            for (var i = 0; i < FootprintBuffer.Count; i++)
                if (board.Model.GetCell(FootprintBuffer[i]) != BoardCellState.Uncaptured) return false;
            return IsUncapturedWorld(world);
        }

        private bool IsUncapturedWorld(Vector2 world)
        {
            var cell = board.WorldToGrid(world);
            return board.Model.IsInBounds(cell) && board.Model.GetCell(cell) == BoardCellState.Uncaptured;
        }
    }
}
