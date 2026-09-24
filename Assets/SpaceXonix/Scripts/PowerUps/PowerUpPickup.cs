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
            if (pickupRenderer is SpriteRenderer sprite)
            {
                // One pooled pickup serves every power-up, so its look is set per spawn.
                sprite.color = definition.pickupTint;
                var animator = sprite.GetComponent<SpaceXonix.Presentation.SpriteFrameAnimator>();
                if (animator != null && definition.pickupFrames != null && definition.pickupFrames.Length > 0)
                    animator.SetFrames(definition.pickupFrames, 6f);
                else if (definition.pickupFrames != null && definition.pickupFrames.Length > 0)
                    sprite.sprite = definition.pickupFrames[0];
            }
            else if (pickupRenderer != null && definition.pickupMaterial != null)
            {
                pickupRenderer.sharedMaterial = definition.pickupMaterial;
            }
        }

        /// <summary>Returns false once the pickup's lifetime has run out.</summary>
        public bool Tick(float deltaTime)
        {
            // Spin a mesh visual only. Rotating pixel art smears it, and a sprite already animates.
            if (!(pickupRenderer is SpriteRenderer))
            {
                var spinTarget = pickupRenderer != null ? pickupRenderer.transform : transform;
                spinTarget.Rotate(0f, 0f, spinDegreesPerSecond * deltaTime, Space.Self);
            }
            if (!Expires) return true;
            LifetimeRemaining -= deltaTime;
            return LifetimeRemaining > 0f;
        }
    }
}
