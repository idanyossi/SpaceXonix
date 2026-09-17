using UnityEngine;

namespace SpaceXonix.PowerUps
{
    [CreateAssetMenu(menuName = "SpaceXonix/Power-Up Definition")]
    public sealed class PowerUpDefinition : ScriptableObject
    {
        public PowerUpType type;
        public string displayName = "Power-Up";
        [Min(.1f)] public float duration = 4f;
        [Tooltip("Material applied to this power-up's board pickup.")]
        public Material pickupMaterial;

        [Header("Arena Tilt")]
        [Min(0f)] public float tiltEnemyDrift = 1.2f;
        [Range(0f, .9f)] public float tiltPlayerSlow = .2f;
        [Range(0f, 30f)] public float tiltCameraRollDegrees = 6f;
    }
}
