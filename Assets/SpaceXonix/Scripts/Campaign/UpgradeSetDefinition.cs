using UnityEngine;

namespace SpaceXonix.Campaign
{
    /// <summary>
    /// Every run upgrade the campaign can offer. In its own file for the same reason as
    /// <see cref="StageModifierSetDefinition"/>: a ScriptableObject sharing a file with another type
    /// is saved without a script reference and loads as null once the editor reloads.
    /// </summary>
    [CreateAssetMenu(menuName = "SpaceXonix/Upgrade Set")]
    public sealed class UpgradeSetDefinition : ScriptableObject
    {
        public UpgradeDefinition[] upgrades;
    }
}
