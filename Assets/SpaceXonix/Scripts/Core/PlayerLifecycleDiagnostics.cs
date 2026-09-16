#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using SpaceXonix.Board;
using SpaceXonix.Input;
using SpaceXonix.Player;
using UnityEngine;

namespace SpaceXonix.Core
{
    internal sealed class PlayerLifecycleDiagnostics
    {
        private const int Capacity = 50;
        private readonly Queue<string> history = new Queue<string>(Capacity);
        private readonly GameManager game;
        private readonly PlayerController player;
        private readonly BoardManager board;
        private readonly InputRouter input;
        private string previousState;
        private bool dumped;
        private PlayerFailureReason? activeFailureReason;

        public PlayerLifecycleDiagnostics(GameManager game, PlayerController player, BoardManager board, InputRouter input)
        {
            this.game = game;
            this.player = player;
            this.board = board;
            this.input = input;
        }

        public void Record(string reason, PlayerFailureReason? failureReason = null, int operationGeneration = -1)
        {
            if (board.Model == null) return;
            if (failureReason.HasValue && reason == "FailureAccepted") activeFailureReason = failureReason;
            var boardCell = board.PlayerCell;
            var logicalCell = board.WorldToGrid(new Vector3(player.LogicalPosition.x, player.LogicalPosition.y, 0f));
            var transformCell = board.WorldToGrid(board.ClampToBoard(player.transform.position));
            var cellType = board.Model.IsInBounds(boardCell) ? board.Model.GetCell(boardCell).ToString() : "OutOfBounds";
            var lastSafe = board.HasTrackedSafeCell ? board.TrackedLastSafeCell.ToString() : "none";
            var state = $"game={game.CurrentState} control={player.ControlState} logical={logicalCell}/{player.LogicalPosition} " +
                $"board={boardCell} transformCell={transformCell} transform={player.transform.position} cell={cellType} " +
                $"exposed={board.IsPlayerExposed} trail={board.Model.ActiveTrail.Count} lastSafe={lastSafe} " +
                $"direction={player.CurrentDirection} pending={player.PendingDirection?.ToString() ?? "none"} " +
                $"movement={player.MovementEnabled} input={input.GameplayInputEnabled}/{input.IsDirectionHeld}/{input.CurrentDirection} " +
                $"invulnerable={game.IsInvulnerable}/{game.InvulnerabilityRemaining:0.000}";
            if (state == previousState && failureReason == null && operationGeneration < 0) return;
            previousState = state;

            var entry = $"frame={Time.frameCount} gen={game.PlayerLifecycleGeneration} event={reason} " +
                $"failure={activeFailureReason?.ToString() ?? "none"} reported={failureReason?.ToString() ?? "none"} " +
                $"opGen={operationGeneration} {state}";
            if (history.Count == Capacity) history.Dequeue();
            history.Enqueue(entry);

            var invalidReason = FindInvalidReason(boardCell, logicalCell, transformCell, cellType, operationGeneration);
            if (invalidReason == null || dumped) return;
            dumped = true;
            var dump = new StringBuilder(4096);
            dump.AppendLine("=== SPACEXONIX PLAYER LIFECYCLE INVALID STATE ===");
            dump.Append("First invalid transition: ").Append(reason).Append(" — ").AppendLine(invalidReason);
            foreach (var item in history) dump.AppendLine(item);
            dump.AppendLine("=== END PLAYER LIFECYCLE DIAGNOSTIC ===");
            Debug.LogError(dump.ToString(), game);
        }

        private string FindInvalidReason(GridCoordinate boardCell, GridCoordinate logicalCell,
            GridCoordinate transformCell, string cellType, int operationGeneration)
        {
            if (operationGeneration >= 0 && operationGeneration != game.PlayerLifecycleGeneration)
                return $"old-generation write ({operationGeneration} != {game.PlayerLifecycleGeneration})";
            if (game.CurrentState == GameplayState.Respawning && player.MovementEnabled)
                return "Respawning with movement enabled";
            if (game.CurrentState != GameplayState.Playing) return null;
            if (logicalCell != boardCell || transformCell != boardCell || player.LogicalPosition != (Vector2)player.transform.position)
                return "logical, BoardManager, and Transform positions disagree";
            if (board.IsPlayerExposed && board.Model.ActiveTrail.Count == 0)
                return "exposed with no active trail";
            if (!board.IsPlayerExposed && cellType == BoardCellState.Uncaptured.ToString())
                return "Playing on uncaptured terrain while not exposed";
            if (player.ControlState == PlayerControlState.SafeIdle && cellType != BoardCellState.Captured.ToString())
                return "SafeIdle on a non-captured cell";
            return null;
        }
    }
}
#endif
