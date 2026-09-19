using UnityEngine;

namespace SpaceXonix.Campaign
{
    [CreateAssetMenu(menuName = "SpaceXonix/Campaign Definition")]
    public sealed class CampaignDefinition : ScriptableObject
    {
        [Tooltip("Normal stages played in Game.unity, in order.")]
        public StageDefinition[] normalStages;
        [Tooltip("Optional final Alien Core stage, played after the normal stages. Leave empty for a boss-less campaign.")]
        public StageDefinition bossStage;

        public bool HasBossStage => bossStage != null;
    }
}
