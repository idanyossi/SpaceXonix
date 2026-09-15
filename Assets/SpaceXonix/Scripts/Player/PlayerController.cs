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
        private bool movementEnabled = true;

        public CardinalDirection CurrentDirection => movementModel != null ? movementModel.Direction : initialDirection;
        public CardinalDirection FacingDirection => CurrentDirection;
        public float MoveSpeed => moveSpeed;
        public bool MovementEnabled => movementEnabled;
        public Vector2 LogicalPosition => movementModel != null ? movementModel.Position : new Vector2(transform.position.x, transform.position.y);

        private void Awake()
        {
            movementModel = new PlayerMovementModel(new Vector2(transform.position.x, transform.position.y), moveSpeed, initialDirection);
        }

        private void Update()
        {
            if (!movementEnabled)
            {
                return;
            }

            var previousPosition = movementModel.Position;
            var position = movementModel.Advance(Time.deltaTime);
            var candidate = new Vector3(position.x, position.y, transform.position.z);
            if (boardManager != null)
            {
                candidate = boardManager.ClampToBoard(candidate);
                boardManager.TrackPlayerWorldPosition(new Vector3(previousPosition.x, previousPosition.y, transform.position.z), candidate);
                movementModel.SetPosition(new Vector2(candidate.x, candidate.y));
            }
            transform.position = candidate;
        }

        public void SetDirection(CardinalDirection direction)
        {
            if (movementModel == null)
            {
                initialDirection = direction;
                return;
            }

            movementModel.SetDirection(direction);
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
                connectedInputRouter.DirectionChanged -= SetDirection;
            }

            connectedInputRouter = inputRouter;
            connectedInputRouter.DirectionChanged += SetDirection;
            SetDirection(inputRouter.CurrentDirection);
        }

        public void ConnectBoard(BoardManager board)
        {
            boardManager = board;
            var spawn = boardManager.GetDefaultSpawnPosition();
            spawn.z = transform.position.z;
            transform.position = spawn;
            movementModel?.SetPosition(new Vector2(spawn.x, spawn.y));
            boardManager.ResetPlayerTracking(spawn);
        }

        public void RespawnAt(Vector3 worldPosition)
        {
            if (boardManager == null) return;
            var position = boardManager.ClampToBoard(worldPosition);
            position.z = transform.position.z;
            transform.position = position;
            movementModel?.SetPosition(new Vector2(position.x, position.y));
            movementModel?.SetDirection(initialDirection);
            boardManager.ResetPlayerTracking(position);
        }

        private void OnDestroy()
        {
            if (connectedInputRouter != null)
            {
                connectedInputRouter.DirectionChanged -= SetDirection;
            }
        }
    }
}
