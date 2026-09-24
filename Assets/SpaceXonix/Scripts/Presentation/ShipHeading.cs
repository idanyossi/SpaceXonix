using SpaceXonix.Player;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Points the ship's sprite along its direction of travel. Presentation only: it reads the
    /// player's facing and never touches movement.
    /// </summary>
    [RequireComponent(typeof(ActorVisual))]
    public sealed class ShipHeading : MonoBehaviour
    {
        [SerializeField] private PlayerController player;

        private ActorVisual actorVisual;

        private void Awake()
        {
            actorVisual = GetComponent<ActorVisual>();
            if (player == null) player = GetComponent<PlayerController>();
        }

        // Update runs before ActorVisual's LateUpdate, so the new heading is used the same frame.
        private void Update()
        {
            if (player != null && actorVisual != null) actorVisual.HeadingDegrees = DegreesFor(player.FacingDirection);
        }

        /// <summary>The sprite is drawn nose-up, so up is 0 and the rest turn counter-clockwise.</summary>
        public static float DegreesFor(CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.Left: return 90f;
                case CardinalDirection.Down: return 180f;
                case CardinalDirection.Right: return -90f;
                default: return 0f;
            }
        }
    }
}
