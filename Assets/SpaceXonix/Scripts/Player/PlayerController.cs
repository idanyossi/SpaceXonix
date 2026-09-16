using SpaceXonix.Input;
using SpaceXonix.Board;
using UnityEngine;

namespace SpaceXonix.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField] private CardinalDirection initialDirection = CardinalDirection.Right;

        private PlayerMovementModel movementModel;
        private InputRouter connectedInputRouter;
        private BoardManager boardManager;
        private bool movementEnabled;
        private bool awaitingDirectionInput = true;
        private CardinalDirection? pendingDirection;

        public CardinalDirection CurrentDirection => movementModel != null ? movementModel.Direction : initialDirection;
        public CardinalDirection InitialDirection => initialDirection;
        public CardinalDirection FacingDirection => CurrentDirection;
        public float MoveSpeed => moveSpeed;
        public bool MovementEnabled => movementEnabled;
        public bool IsAwaitingDirectionInput => awaitingDirectionInput;
        public CardinalDirection? PendingDirection => pendingDirection;
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
            if (!movementEnabled || awaitingDirectionInput)
            {
                return false;
            }

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
                if (result == BoardMoveResult.TrailFailed)
                {
                    movementModel.SetPosition(new Vector2(transform.position.x, transform.position.y));
                    boardManager.ResetPlayerTracking(transform.position);
                    return false;
                }
                if (result == BoardMoveResult.SafeMove || result == BoardMoveResult.Reconnected)
                {
                    candidate = boardManager.GetWorldPosition(boardManager.PlayerCell);
                    candidate.z = transform.position.z;
                    ContinueHeldSafeMovementOrStop();
                }
                movementModel.SetPosition(new Vector2(candidate.x, candidate.y));
            }
            transform.position = candidate;
            return transform.position != transformBefore;
        }

        private void RequestDirection(CardinalDirection direction)
        {
            if (!IsDirectionLegal(direction)) return;
            pendingDirection = direction;
            awaitingDirectionInput = false;
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
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
            awaitingDirectionInput = true;
        }

        public void RespawnAt(Vector3 worldPosition, CardinalDirection direction)
        {
            if (boardManager == null) return;
            var position = boardManager.ClampToBoard(worldPosition);
            position.z = transform.position.z;
            transform.position = position;
            movementModel?.SetPosition(new Vector2(position.x, position.y));
            movementModel?.SetDirection(direction);
            RequireFreshDirectionInput();
            boardManager.ResetPlayerTracking(position);
        }

        private void RequireFreshDirectionInput()
        {
            pendingDirection = null;
            awaitingDirectionInput = true;
        }

        private void ContinueHeldSafeMovementOrStop()
        {
            if (connectedInputRouter != null && connectedInputRouter.IsDirectionHeld &&
                IsDirectionLegal(connectedInputRouter.CurrentDirection))
            {
                pendingDirection = connectedInputRouter.CurrentDirection;
                awaitingDirectionInput = false;
                return;
            }

            RequireFreshDirectionInput();
        }

        private void StopSafeMovement()
        {
            if (boardManager != null && boardManager.IsPlayerExposed) return;
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
