using UnityEngine;

namespace SpaceXonix.Campaign
{
    public enum UpgradeType
    {
        ReinforcedHull,
        ImprovedThrusters,
        RapidCapacitor,
        ShieldCapacitor,
        CryogenicCore,
        GravityStabilizer,
        ScavengerProtocol
    }

    [CreateAssetMenu(menuName = "SpaceXonix/Upgrade Definition")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        public UpgradeType type;
        public string displayName = "Upgrade";
        [TextArea] public string effectText = "";
        [Tooltip("How many times this upgrade may be taken in one run.")]
        [Min(1)] public int maxStacks = 3;
        [Tooltip("Effect size per stack: +1 life, +10% speed, +20% power gain, +25% duration, -50% tilt penalty, +10% pickup chance.")]
        public float perStack = .1f;
    }

    [CreateAssetMenu(menuName = "SpaceXonix/Upgrade Set")]
    public sealed class UpgradeSetDefinition : ScriptableObject
    {
        public UpgradeDefinition[] upgrades;
    }
}
