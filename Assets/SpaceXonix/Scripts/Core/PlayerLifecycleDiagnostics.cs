#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using SpaceXonix.Board;
using SpaceXonix.Input;
using SpaceXonix.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceXonix.Core
{
    internal sealed class PlayerLifecycleDiagnostics
    {
        private const int MovementCapacity = 50;
        private const int CriticalCapacity = 30;
        private readonly Queue<string> movementHistory = new Queue<string>(MovementCapacity);
        private readonly Queue<string> criticalHistory = new Queue<string>(CriticalCapacity);
        private readonly GameManager game;
        private readonly PlayerController player;
        private readonly BoardManager board;
        private readonly InputRouter input;
        private string previousState;
        private bool dumped;
        private PlayerFailureReason? activeFailureReason;
        private int lastAcceptedFailureFrame = -1;
        private int? observedGeneration;
        private GameplayState? observedGameState;
        private PlayerControlState? observedControlState;
        private bool? observedInvulnerability;

        public PlayerLifecycleDiagnostics(GameManager game, PlayerController player, BoardManager board, InputRouter input)
        {
            this.game = game;
            this.player = player;
            this.board = board;
            this.input = input;
        }

        public void Record(string reason, PlayerFailureReason? failureReason = null, int operationGeneration = -1,
            bool force = false)
        {
            if (board.Model == null) return;
            if (failureReason.HasValue && reason == "FailureAccepted")
            {
                activeFailureReason = failureReason;
                lastAcceptedFailureFrame = Time.frameCount;
            }
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
                $"damageable={game.IsPlayerDamageable} " +
                $"invulnerable={game.IsInvulnerable}/{game.InvulnerabilityRemaining:0.000} " +
                $"respawnActive={game.HasActiveRespawnOperation}";
            if (!force && state == previousState && failureReason == null && operationGeneration < 0) return;
            previousState = state;

            var entry = $"frame={Time.frameCount} gen={game.PlayerLifecycleGeneration} event={reason} " +
                $"failure={activeFailureReason?.ToString() ?? "none"} reported={failureReason?.ToString() ?? "none"} " +
                $"opGen={operationGeneration} {state}";
            RecordDerivedCriticalTransitions(reason, state, failureReason);
            var targetHistory = IsCriticalReason(reason) ? criticalHistory : movementHistory;
            Enqueue(targetHistory, targetHistory == criticalHistory ? CriticalCapacity : MovementCapacity, entry);

            var invalidReason = FindInvalidReason(reason, boardCell, logicalCell, transformCell, cellType,
                operationGeneration);
            if (invalidReason == null || dumped) return;
            dumped = true;
            var dump = new StringBuilder(4096);
            dump.AppendLine("=== SPACEXONIX PLAYER LIFECYCLE INVALID STATE ===");
            dump.Append("First invalid transition: ").Append(reason).Append(" — ").AppendLine(invalidReason);
            AppendHistories(dump);
            dump.AppendLine("=== END PLAYER LIFECYCLE DIAGNOSTIC ===");
            Debug.LogError(dump.ToString(), game);
        }

        public void DumpManual()
        {
            if (board.Model == null)
            {
                Debug.Log("=== SPACEXONIX MANUAL PLAYER LIFECYCLE DUMP (F8) ===\nBoard model is not initialized.\n" +
                    "=== END PLAYER LIFECYCLE DIAGNOSTIC ===", game);
                return;
            }

            Record("ManualF8Dump", operationGeneration: game.PlayerLifecycleGeneration);
            var boardCell = board.PlayerCell;
            var logicalCell = board.WorldToGrid(new Vector3(player.LogicalPosition.x, player.LogicalPosition.y, 0f));
            var transformCell = board.WorldToGrid(board.ClampToBoard(player.transform.position));
            var cellType = board.Model.IsInBounds(boardCell) ? board.Model.GetCell(boardCell).ToString() : "OutOfBounds";
            var lastSafe = board.HasTrackedSafeCell ? board.TrackedLastSafeCell.ToString() : "none";
            var rawKeys = GetRawKeyboardState();
            var dump = new StringBuilder(8192);
            dump.AppendLine("=== SPACEXONIX MANUAL PLAYER LIFECYCLE DUMP (F8) ===");
            dump.AppendLine("CURRENT COMPLETE PLAYER STATE:");
            dump.Append("frame=").Append(Time.frameCount)
                .Append(" gen=").Append(game.PlayerLifecycleGeneration)
                .Append(" failure=").Append(activeFailureReason?.ToString() ?? "none")
                .Append(" lastAcceptedFailureFrame=")
                .Append(lastAcceptedFailureFrame >= 0 ? lastAcceptedFailureFrame.ToString() : "none")
                .Append(" game=").Append(game.CurrentState)
                .Append(" control=").Append(player.ControlState)
                .Append(" logical=").Append(logicalCell).Append('/').Append(player.LogicalPosition)
                .Append(" board=").Append(boardCell)
                .Append(" transformCell=").Append(transformCell).Append(" transform=").Append(player.transform.position)
                .Append(" cell=").Append(cellType)
                .Append(" lastSafe=").Append(lastSafe)
                .Append(" trail=").Append(board.Model.ActiveTrail.Count)
                .Append(" exposed=").Append(board.IsPlayerExposed)
                .Append(" direction=").Append(player.CurrentDirection)
                .Append(" pending=").Append(player.PendingDirection?.ToString() ?? "none")
                .Append(" movement=").Append(player.MovementEnabled)
                .Append(" input=").Append(input.GameplayInputEnabled).Append('/')
                .Append(input.IsDirectionHeld).Append('/').Append(input.CurrentDirection)
                .Append(" damageable=").Append(game.IsPlayerDamageable)
                .Append(" rawKeys=").Append(rawKeys)
                .Append(" invulnerable=").Append(game.IsInvulnerable).Append('/')
                .Append(game.InvulnerabilityRemaining.ToString("0.000"))
                .Append(" respawnActive=").AppendLine(game.HasActiveRespawnOperation.ToString());
            AppendHistories(dump);
            dump.AppendLine("=== END PLAYER LIFECYCLE DIAGNOSTIC ===");
            Debug.Log(dump.ToString(), game);
        }

        private void RecordDerivedCriticalTransitions(string reason, string state, PlayerFailureReason? failureReason)
        {
            var frame = Time.frameCount;
            var generation = game.PlayerLifecycleGeneration;
            if (failureReason.HasValue)
                AddCritical($"frame={frame} gen={generation} event=FailureReported:{failureReason.Value} {state}");
            if (reason == "FailureAccepted")
                AddCritical($"frame={frame} gen={generation} event=DeathTrailCleared {state}");
            if (reason == "RespawnCompleted")
                AddCritical($"frame={frame} gen={generation} event=PlayerRestorationCompleted {state}");
            if (observedGeneration.HasValue && observedGeneration.Value != generation)
                AddCritical($"frame={frame} gen={generation} event=LifecycleGenerationChanged:" +
                    $"{observedGeneration.Value}->{generation} {state}");
            if (observedGameState.HasValue && observedGameState.Value != game.CurrentState)
                AddCritical($"frame={frame} gen={generation} event=GameManagerStateChanged:" +
                    $"{observedGameState.Value}->{game.CurrentState} {state}");
            if (observedControlState.HasValue && observedControlState.Value != player.ControlState)
                AddCritical($"frame={frame} gen={generation} event=ControlStateChanged:" +
                    $"{observedControlState.Value}->{player.ControlState} {state}");
            if (observedInvulnerability.HasValue && observedInvulnerability.Value != game.IsInvulnerable)
                AddCritical($"frame={frame} gen={generation} event=Invulnerability" +
                    $"{(game.IsInvulnerable ? "Started" : "Ended")} {state}");

            observedGeneration = generation;
            observedGameState = game.CurrentState;
            observedControlState = player.ControlState;
            observedInvulnerability = game.IsInvulnerable;
        }

        private static bool IsCriticalReason(string reason)
        {
            return reason.StartsWith("Failure") || reason.StartsWith("Respawn") ||
                reason.StartsWith("GameplayStateApplied") || reason.StartsWith("ControlState:") ||
                reason.StartsWith("RuntimeInvariantRepair") || reason == "PreparedForRespawn" ||
                reason == "GameStarted" || reason == "GameOver";
        }

        private void AddCritical(string entry) => Enqueue(criticalHistory, CriticalCapacity, entry);

        private static void Enqueue(Queue<string> target, int capacity, string entry)
        {
            if (target.Count == capacity) target.Dequeue();
            target.Enqueue(entry);
        }

        private void AppendHistories(StringBuilder dump)
        {
            dump.AppendLine("CRITICAL LIFECYCLE HISTORY:");
            foreach (var item in criticalHistory) dump.AppendLine(item);
            dump.AppendLine("RECENT MOVEMENT / INPUT HISTORY:");
            foreach (var item in movementHistory) dump.AppendLine(item);
        }

        private static string GetRawKeyboardState()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return "keyboard:none";
            return $"W:{keyboard.wKey.isPressed},A:{keyboard.aKey.isPressed},S:{keyboard.sKey.isPressed},D:{keyboard.dKey.isPressed}," +
                $"Up:{keyboard.upArrowKey.isPressed},Left:{keyboard.leftArrowKey.isPressed}," +
                $"Down:{keyboard.downArrowKey.isPressed},Right:{keyboard.rightArrowKey.isPressed}";
        }

        private string FindInvalidReason(string reason, GridCoordinate boardCell, GridCoordinate logicalCell,
            GridCoordinate transformCell, string cellType, int operationGeneration)
        {
            if (operationGeneration >= 0 && operationGeneration != game.PlayerLifecycleGeneration)
                return $"old-generation write ({operationGeneration} != {game.PlayerLifecycleGeneration})";
            if (game.CurrentState == GameplayState.Respawning && player.MovementEnabled)
                return "Respawning with movement enabled";
            if (game.CurrentState != GameplayState.Playing) return null;
            if (ShouldCheckPositionConsistency(reason) &&
                (logicalCell != boardCell || transformCell != boardCell ||
                    player.LogicalPosition != (Vector2)player.transform.position))
                return "logical, BoardManager, and Transform positions disagree";
            if (board.IsPlayerExposed && board.Model.ActiveTrail.Count == 0)
                return "exposed with no active trail";
            if (!board.IsPlayerExposed && cellType == BoardCellState.Uncaptured.ToString())
                return "Playing on uncaptured terrain while not exposed";
            if (player.ControlState == PlayerControlState.SafeIdle && cellType != BoardCellState.Captured.ToString())
                return "SafeIdle on a non-captured cell";
            return null;
        }

        private static bool ShouldCheckPositionConsistency(string reason)
        {
            return reason.StartsWith("LogicalStepCommitted") || reason.StartsWith("LogicalStepAborted") ||
                reason == "CaptureCompleted" || reason == "RespawnCompleted" ||
                reason == "RuntimeInvariantRepairCompleted" || reason == "SafeDirectionReleased" ||
                reason == "FailureAccepted" || reason == "GameStarted";
        }
    }
}
#endif
