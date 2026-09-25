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

        /// <summary>
        /// A hybrid is an alien a Volatile blast caught. It keeps its own movement but carries the
        /// Volatile's charge, which goes off when it touches territory the player built.
        /// </summary>
        public bool IsHybrid { get; private set; }
        /// <summary>The Volatile whose blast made this a hybrid; its blast sizes the hybrid's.</summary>
        public EnemyDefinition HybridSource { get; private set; }
        public float HybridProtectionRemaining { get; private set; }
        /// <summary>Blast size multiplier: 1 for a new hybrid, doubled each time a Volatile is absorbed.</summary>
        public float HybridCharge { get; private set; } = 1f;
        public bool IsHybridArmed => IsActiveEnemy && IsHybrid && HybridProtectionRemaining <= 0f;
        public event Action<EnemyController> HybridChanged;

        public void BecomeHybrid(EnemyDefinition source)
        {
            if (!IsActiveEnemy || source == null) return;
            IsHybrid = true;
            HybridSource = source;
            HybridCharge = 1f;
            // The same grace a new Volatile gets, so it cannot go off in the blast that made it.
            HybridProtectionRemaining = source.volatileSpawnProtection;
            HybridChanged?.Invoke(this);
        }

        /// <summary>
        /// A Volatile ran into this hybrid and was absorbed: the charge doubles, up to
        /// <paramref name="maximum"/>. Returns false when it is already at the maximum.
        /// </summary>
        public bool Supercharge(float maximum)
        {
            if (!IsActiveEnemy || !IsHybrid || HybridCharge * 2f > maximum) return false;
            HybridCharge *= 2f;
            HybridChanged?.Invoke(this);
            return true;
        }

        public void AdvanceHybridProtection(float deltaTime)
        {
            if (IsHybrid && deltaTime > 0f) HybridProtectionRemaining = Mathf.Max(0f, HybridProtectionRemaining - deltaTime);
        }

        /// <summary>
        /// True when territory the player captured lies within <paramref name="reach"/> of the body,
        /// with <paramref name="contact"/> the nearest such cell. The permanent border does not count:
        /// it was never the player's to lose.
        /// </summary>
        public bool IsTouchingBuiltTerritory(float reach, out GridCoordinate contact)
        {
            contact = default;
            if (!IsActiveEnemy || board == null || board.Model == null) return false;
            board.GetCellsOverlappingCircle(transform.position, CollisionRadius + Mathf.Max(0f, reach), FootprintBuffer);
            var found = false;
            var nearest = float.MaxValue;
            for (var i = 0; i < FootprintBuffer.Count; i++)
            {
                var cell = FootprintBuffer[i];
                if (board.Model.GetCell(cell) != BoardCellState.Captured || board.Model.IsStructural(cell)) continue;
                var distance = ((Vector2)(board.GetWorldPosition(cell) - transform.position)).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; contact = cell; found = true;
            }
            return found;
        }

        private void ClearHybrid()
        {
            var was = IsHybrid;
            IsHybrid = false; HybridSource = null; HybridProtectionRemaining = 0f; HybridCharge = 1f;
            if (was) HybridChanged?.Invoke(this);
        }
        public virtual void Activate(EnemyDefinition data, BoardManager boardManager, GameManager gameManager, Vector3 position, Vector2 direction)
        {
            definition = data; board = boardManager; game = gameManager;
            transform.position = position;
            movement = new EnemyMovementModel(new Vector2(position.x, position.y), direction.normalized * data.moveSpeed * SpeedMultiplier);
            traversedCells.Clear();
            LastTrailHitAccepted = false;
            HasShieldPassThroughGrace = false;
            ClearHybrid();
            IsActiveEnemy = true; gameObject.SetActive(true);
        }
        public virtual void Deactivate() { IsActiveEnemy = false; traversedCells.Clear(); LastTrailHitAccepted = false; HasShieldPassThroughGrace = false; ClearHybrid(); gameObject.SetActive(false); }
        public void SetMovementSuspended(bool suspended) { if (movement != null) movement.MovementEnabled = !suspended; }

        /// <summary>Applies a stage modifier's speed scale, rescaling current motion without touching the definition.</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            multiplier = Mathf.Max(.01f, multiplier);
            if (movement != null && SpeedMultiplier > 0f) movement.SetSpeed(movement.Velocity.magnitude / SpeedMultiplier * multiplier);
            SpeedMultiplier = multiplier;
        }

        public void SetIntervalMultiplier(float multiplier) => IntervalMultiplier = Mathf.Max(.01f, multiplier);
        /// <summary>
        /// True when the alien's own cell is no longer open space. Captured territory can close
        /// around it when a trail it was standing on is committed, and from there every candidate
        /// move is blocked, so it would sit in place flipping its velocity forever.
        /// </summary>
        public bool IsTrappedInCapturedTerritory()
        {
            if (!IsActiveEnemy || board == null || board.Model == null) return false;
            var cell = LogicalCell;
            return board.Model.IsInBounds(cell) && board.Model.GetCell(cell) != BoardCellState.Uncaptured;
        }

        /// <summary>Moves the alien to open space, keeping its heading, speed, drift and frozen state.</summary>
        public void Relocate(Vector3 worldPosition)
        {
            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            if (movement != null) movement.SetPosition(new Vector2(worldPosition.x, worldPosition.y));
        }

        /// <summary>True when the whole body fits in open space at this position.</summary>
        public bool FitsAt(Vector2 world) => IsFootprintPassable(world);

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
