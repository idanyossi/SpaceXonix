using SpaceXonix.Input;
using SpaceXonix.Board;
using SpaceXonix.Core;
using UnityEngine;

namespace SpaceXonix.Player
{
    public enum PlayerControlState
    {
        SafeIdle,
        SafeMoving,
        ExposedMoving,
        Respawning,
        GameOver
    }

    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField] private CardinalDirection initialDirection = CardinalDirection.Right;

        private PlayerMovementModel movementModel;
        private InputRouter connectedInputRouter;
        private BoardManager boardManager;
        private GameManager lifecycle;
        private PlayerControlState controlState = PlayerControlState.Respawning;
        private CardinalDirection? pendingDirection;

        public CardinalDirection CurrentDirection => movementModel != null ? movementModel.Direction : initialDirection;
        public CardinalDirection InitialDirection => initialDirection;
        public CardinalDirection FacingDirection => CurrentDirection;
        public float MoveSpeed => moveSpeed;
        public bool MovementEnabled => controlState == PlayerControlState.SafeIdle ||
            controlState == PlayerControlState.SafeMoving || controlState == PlayerControlState.ExposedMoving;
        public bool IsAwaitingDirectionInput => controlState != PlayerControlState.SafeMoving &&
            controlState != PlayerControlState.ExposedMoving;
        public CardinalDirection? PendingDirection => pendingDirection;
        public PlayerControlState ControlState => controlState;
        public Vector2 LogicalPosition => movementModel != null ? movementModel.Position : new Vector2(transform.position.x, transform.position.y);

        private void Awake()
        {
            movementModel = new PlayerMovementModel(new Vector2(transform.position.x, transform.position.y), moveSpeed, initialDirection);
        }

        private void Update()
        {
            AdvanceMovement(Time.deltaTime);
        }

        public bool AdvanceMovement(float deltaTime)
        {
            if (controlState != PlayerControlState.SafeMoving && controlState != PlayerControlState.ExposedMoving)
            {
                return false;
            }

            var lifecycleGeneration = lifecycle != null ? lifecycle.PlayerLifecycleGeneration : 0;
            ConsumePendingDirection();
            if (!EnsureCurrentDirectionIsLegal()) return false;

            var transformBefore = transform.position;
            var previousPosition = movementModel.Position;
            var position = movementModel.Advance(deltaTime);
            var candidate = new Vector3(position.x, position.y, transform.position.z);
            if (boardManager != null)
            {
                candidate = boardManager.ClampToBoard(candidate);
                var result = boardManager.TrackPlayerWorldPosition(new Vector3(previousPosition.x, previousPosition.y, transform.position.z), candidate);
                if (lifecycle != null && lifecycle.PlayerLifecycleGeneration != lifecycleGeneration) return false;
                if (controlState == PlayerControlState.Respawning || controlState == PlayerControlState.GameOver) return false;
                if (result == BoardMoveResult.TrailFailed) return false;
                if (result == BoardMoveResult.SafeMove || result == BoardMoveResult.Reconnected)
                {
                    candidate = boardManager.GetWorldPosition(boardManager.PlayerCell);
                    candidate.z = transform.position.z;
                    if (result == BoardMoveResult.Reconnected) StopAfterCapture();
                    else ContinueHeldSafeMovementOrStop();
                }
                else if (result == BoardMoveResult.TrailStarted || result == BoardMoveResult.TrailExtended)
                    controlState = PlayerControlState.ExposedMoving;
                movementModel.SetPosition(new Vector2(candidate.x, candidate.y));
            }
            transform.position = candidate;
            return transform.position != transformBefore;
        }

        private void RequestDirection(CardinalDirection direction)
        {
            if (controlState == PlayerControlState.Respawning || controlState == PlayerControlState.GameOver) return;
            var effectiveDirection = pendingDirection ?? CurrentDirection;
            if (controlState == PlayerControlState.ExposedMoving && IsOpposite(effectiveDirection, direction)) return;
            if (!IsDirectionLegal(direction)) return;
            pendingDirection = direction;
            controlState = controlState == PlayerControlState.ExposedMoving ||
                (boardManager != null && boardManager.IsPlayerExposed)
                ? PlayerControlState.ExposedMoving
                : PlayerControlState.SafeMoving;
        }

        private static bool IsOpposite(CardinalDirection current, CardinalDirection requested)
        {
            return current.ToVector2() + requested.ToVector2() == Vector2.zero;
        }

        public void SetMovementEnabled(bool enabled)
        {
            SetGameplayState(enabled ? GameplayState.Playing : GameplayState.Respawning);
        }

        public void SetGameplayState(GameplayState state)
        {
            pendingDirection = null;
            if (state == GameplayState.Playing) controlState = PlayerControlState.SafeIdle;
            else if (state == GameplayState.GameOver) controlState = PlayerControlState.GameOver;
            else controlState = PlayerControlState.Respawning;
        }

        public void SetMoveSpeed(float speed)
        {
            moveSpeed = Mathf.Max(0f, speed);
            if (movementModel != null)
            {
                movementModel.MoveSpeed = moveSpeed;
            }
        }

        public void ConnectInput(InputRouter inputRouter)
        {
            if (connectedInputRouter != null)
            {
                connectedInputRouter.DirectionChanged -= RequestDirection;
                connectedInputRouter.DirectionReleased -= StopSafeMovement;
            }

            connectedInputRouter = inputRouter;
            connectedInputRouter.DirectionChanged += RequestDirection;
            connectedInputRouter.DirectionReleased += StopSafeMovement;
        }

        public void ConnectBoard(BoardManager board)
        {
            boardManager = board;
            var spawn = boardManager.GetDefaultSpawnPosition();
            spawn.z = transform.position.z;
            transform.position = spawn;
            movementModel?.SetPosition(new Vector2(spawn.x, spawn.y));
            boardManager.ResetPlayerTracking(spawn);
            pendingDirection = null;
            controlState = PlayerControlState.SafeIdle;
        }

        public void ConnectLifecycle(GameManager gameManager)
        {
            lifecycle = gameManager;
        }

        public void RespawnAt(Vector3 worldPosition, CardinalDirection direction)
        {
            if (boardManager == null) return;
            RestoreSafeManualState(boardManager.WorldToGrid(boardManager.ClampToBoard(worldPosition)), direction);
        }

        public bool RestoreSafeManualState(GridCoordinate cell, CardinalDirection direction)
        {
            if (boardManager == null || movementModel == null || !boardManager.IsValidRespawnCell(cell)) return false;
            boardManager.CancelActiveTrail();
            var position = boardManager.GetWorldPosition(cell);
            position.z = transform.position.z;
            boardManager.ResetPlayerTracking(position);
            movementModel.SetPosition(new Vector2(position.x, position.y));
            movementModel.SetDirection(direction);
            transform.position = position;
            RequireFreshDirectionInput();
            connectedInputRouter?.ResetDirection(direction);
            if (boardManager.PlayerCell != cell || boardManager.IsPlayerExposed ||
                movementModel.Position != (Vector2)transform.position) return false;
            controlState = PlayerControlState.SafeIdle;
            return true;
        }

        public void PrepareForRespawn()
        {
            RequireFreshDirectionInput();
            movementModel?.SetDirection(initialDirection);
            connectedInputRouter?.ResetDirection(initialDirection);
        }

        private void RequireFreshDirectionInput()
        {
            pendingDirection = null;
            if (controlState != PlayerControlState.Respawning && controlState != PlayerControlState.GameOver)
                controlState = PlayerControlState.SafeIdle;
        }

        private void ContinueHeldSafeMovementOrStop()
        {
            if (connectedInputRouter != null && connectedInputRouter.IsDirectionHeld &&
                IsDirectionLegal(connectedInputRouter.CurrentDirection))
            {
                pendingDirection = connectedInputRouter.CurrentDirection;
                controlState = PlayerControlState.SafeMoving;
                return;
            }

            RequireFreshDirectionInput();
        }

        private void StopAfterCapture()
        {
            controlState = PlayerControlState.SafeIdle;
            pendingDirection = null;
            connectedInputRouter?.ResetDirection(CurrentDirection);
        }

        private void StopSafeMovement()
        {
            if (controlState == PlayerControlState.ExposedMoving || controlState == PlayerControlState.Respawning ||
                controlState == PlayerControlState.GameOver) return;
            if (boardManager != null)
            {
                var position = boardManager.GetWorldPosition(boardManager.PlayerCell);
                position.z = transform.position.z;
                transform.position = position;
                movementModel?.SetPosition(new Vector2(position.x, position.y));
            }
            RequireFreshDirectionInput();
        }

        private void ConsumePendingDirection()
        {
            if (!pendingDirection.HasValue) return;
            if (IsDirectionLegal(pendingDirection.Value)) movementModel.SetDirection(pendingDirection.Value);
            pendingDirection = null;
        }

        private bool EnsureCurrentDirectionIsLegal()
        {
            if (IsDirectionLegal(CurrentDirection)) return true;
            var directions = new[]
            {
                CardinalDirection.Up,
                CardinalDirection.Down,
                CardinalDirection.Left,
                CardinalDirection.Right
            };
            foreach (var direction in directions)
            {
                if (!IsDirectionLegal(direction)) continue;
                movementModel.SetDirection(direction);
                connectedInputRouter?.ResetDirection(direction);
                return true;
            }
            return false;
        }

        private bool IsDirectionLegal(CardinalDirection direction)
        {
            if (boardManager == null) return true;
            var offset = direction.ToVector2();
            var cell = boardManager.PlayerCell;
            return boardManager.IsLegalPlayerStep(new GridCoordinate(cell.X + (int)offset.x, cell.Y + (int)offset.y));
        }

        private void OnDestroy()
        {
            if (connectedInputRouter != null)
            {
                connectedInputRouter.DirectionChanged -= RequestDirection;
                connectedInputRouter.DirectionReleased -= StopSafeMovement;
            }
        }
    }
}
