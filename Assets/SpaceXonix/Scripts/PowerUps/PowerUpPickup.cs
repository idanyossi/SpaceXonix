using SpaceXonix.Board;
using UnityEngine;

namespace SpaceXonix.PowerUps
{
    public sealed class PowerUpPickup : MonoBehaviour
    {
        [SerializeField] private Renderer pickupRenderer;
        [SerializeField, Min(0f)] private float spinDegreesPerSecond = 90f;

        public PowerUpDefinition Definition { get; private set; }
        public GridCoordinate Cell { get; private set; }
        public float LifetimeRemaining { get; private set; }
        public bool Expires { get; private set; }

        public void Configure(PowerUpDefinition definition, GridCoordinate cell, Vector3 worldPosition, float lifetime)
        {
            Definition = definition;
            Cell = cell;
            Expires = lifetime > 0f;
            LifetimeRemaining = lifetime;
            transform.position = worldPosition;
            if (pickupRenderer == null) pickupRenderer = GetComponentInChildren<Renderer>();
            if (pickupRenderer != null && definition.pickupMaterial != null) pickupRenderer.sharedMaterial = definition.pickupMaterial;
        }

        /// <summary>Returns false once the pickup's lifetime has run out.</summary>
        public bool Tick(float deltaTime)
        {
            // Spin the visual only; the logical root stays axis-aligned on the board plane.
            var spinTarget = pickupRenderer != null ? pickupRenderer.transform : transform;
            spinTarget.Rotate(0f, 0f, spinDegreesPerSecond * deltaTime, Space.Self);
            if (!Expires) return true;
            LifetimeRemaining -= deltaTime;
            return LifetimeRemaining > 0f;
        }
    }
}
