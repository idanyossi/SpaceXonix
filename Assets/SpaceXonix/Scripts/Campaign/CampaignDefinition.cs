using UnityEngine;

namespace SpaceXonix.Campaign
{
    [CreateAssetMenu(menuName = "SpaceXonix/Campaign Definition")]
    public sealed class CampaignDefinition : ScriptableObject
    {
        [Tooltip("Normal stages played in Game.unity, in order. The boss stage is added in its own phase.")]
        public StageDefinition[] normalStages;
    }
}
