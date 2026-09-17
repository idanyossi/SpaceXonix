using UnityEngine;

namespace SpaceXonix.PowerUps
{
    [CreateAssetMenu(menuName = "SpaceXonix/Power-Up Spawn Definition")]
    public sealed class PowerUpSpawnDefinition : ScriptableObject
    {
        [Min(0f)] public float minimumCapturePercentage = 5f;
        [Range(0f, 1f)] public float baseChance = .15f;
        [Range(0f, 1f)] public float chancePerCapturedPercent = .02f;
        [Range(0f, 1f)] public float maxChance = .6f;
        [Tooltip("Seconds an uncollected pickup stays on the board. 0 keeps it until collected.")]
        [Min(0f)] public float pickupLifetime = 12f;
        [Min(0)] public int minimumEnemyDistanceCells = 3;
        [Tooltip("World-space pickup radius; collected when it touches the ship's collision radius. Keep the visual the same size.")]
        [Min(0f)] public float pickupRadius = .15f;

        public float GetSpawnChance(float capturedPercentage) =>
            PowerUpSpawnRules.GetSpawnChance(capturedPercentage, minimumCapturePercentage, baseChance, chancePerCapturedPercent, maxChance);
    }

    public static class PowerUpSpawnRules
    {
        public static float GetSpawnChance(float capturedPercentage, float minimumPercentage, float baseChance, float perPercent, float maxChance)
        {
            if (capturedPercentage < minimumPercentage || capturedPercentage <= 0f) return 0f;
            return Mathf.Clamp(baseChance + perPercent * capturedPercentage, 0f, maxChance);
        }
    }
}
