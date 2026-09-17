using UnityEngine;

namespace SpaceXonix.Power
{
    [CreateAssetMenu(menuName = "SpaceXonix/Power Definition")]
    public sealed class PowerDefinition : ScriptableObject
    {
        [Min(1f)] public float maxPower = 100f;
        [Min(0f)] public float powerPerCapturedPercent = 5f;
        [Min(.1f)] public float shotSpeed = 14f;
        [Tooltip("World-space distance from the shot's path at which a standard alien is hit.")]
        [Min(.01f)] public float shotHitRadius = .2f;
    }
}
