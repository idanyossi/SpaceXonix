using SpaceXonix.Input;
using UnityEngine;

namespace SpaceXonix.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField] private CardinalDirection initialDirection = CardinalDirection.Right;

        private PlayerMovementModel movementModel;
        private InputRouter connectedInputRouter;
        private bool movementEnabled = true;

        public CardinalDirection CurrentDirection => movementModel != null ? movementModel.Direction : initialDirection;
        public CardinalDirection FacingDirection => CurrentDirection;
        public float MoveSpeed => moveSpeed;
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

            var position = movementModel.Advance(Time.deltaTime);
            transform.position = new Vector3(position.x, position.y, transform.position.z);
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

        private void OnDestroy()
        {
            if (connectedInputRouter != null)
            {
                connectedInputRouter.DirectionChanged -= SetDirection;
            }
        }
    }
}
