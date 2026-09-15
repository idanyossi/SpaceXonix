using UnityEngine;

namespace SpaceXonix.Hazards
{
    [CreateAssetMenu(menuName = "SpaceXonix/Laser Definition")]
    public sealed class LaserDefinition : ScriptableObject
    {
        public LaserAxis axis;
        [Min(.01f)] public float warningDuration = .75f;
        [Min(.01f)] public float firingDuration = .35f;
        [Min(.01f)] public float cooldownDuration = 2f;
        [Min(.01f)] public float beamWidth = .12f;
    }
}
