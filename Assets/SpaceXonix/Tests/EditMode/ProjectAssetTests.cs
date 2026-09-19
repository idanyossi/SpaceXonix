using NUnit.Framework;
using SpaceXonix.Campaign;
using UnityEditor;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    /// <summary>
    /// Guards the shipped ScriptableObject assets themselves. A ScriptableObject declared in a file
    /// named after a different type is saved with no script reference: it works in the session that
    /// created it and silently loads as null forever after, which is how the modifier and upgrade
    /// sets were both dead without a single test failing.
    /// </summary>
    public sealed class ProjectAssetTests
    {
        private const string ModifierSetPath = "Assets/SpaceXonix/ScriptableObjects/Modifiers/StageModifiers.asset";
        private const string UpgradeSetPath = "Assets/SpaceXonix/ScriptableObjects/Upgrades/CampaignUpgrades.asset";
        private const string CampaignPath = "Assets/SpaceXonix/ScriptableObjects/Campaign/Campaign.asset";

        [Test]
        public void StageModifierSet_LoadsAndHoldsEveryModifier()
        {
            var set = AssetDatabase.LoadAssetAtPath<StageModifierSetDefinition>(ModifierSetPath);
            Assert.That(set, Is.Not.Null, $"{ModifierSetPath} failed to load; its script reference is probably broken");
            Assert.That(set.modifiers, Is.Not.Null.And.Not.Empty);
            Assert.That(set.modifiers, Has.None.Null, "a null entry would be silently skipped when rolling");

            var bossOnly = 0;
            var normal = 0;
            foreach (var modifier in set.modifiers)
            {
                if (modifier.requiresBossStage) bossOnly++; else normal++;
            }
            Assert.That(normal, Is.GreaterThan(0), "normal stages need something to roll");
            Assert.That(bossOnly, Is.GreaterThan(0), "so does the boss stage");
        }

        [Test]
        public void UpgradeSet_LoadsAndHoldsEveryUpgrade()
        {
            var set = AssetDatabase.LoadAssetAtPath<UpgradeSetDefinition>(UpgradeSetPath);
            Assert.That(set, Is.Not.Null, $"{UpgradeSetPath} failed to load; its script reference is probably broken");
            Assert.That(set.upgrades, Is.Not.Null.And.Not.Empty);
            Assert.That(set.upgrades, Has.None.Null);
            foreach (var upgrade in set.upgrades)
                Assert.That(upgrade.maxStacks, Is.GreaterThan(0), $"{upgrade.name} could never be taken");
        }

        [Test]
        public void Campaign_LoadsWithItsNormalStagesAndBossStage()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);
            Assert.That(campaign, Is.Not.Null, $"{CampaignPath} failed to load");
            Assert.That(campaign.normalStages, Is.Not.Null.And.Not.Empty);
            Assert.That(campaign.normalStages, Has.None.Null);
            Assert.That(campaign.HasBossStage, Is.True, "the campaign ends on the Alien Core");
            Assert.That(campaign.bossStage.IsBossStage, Is.True, "the boss stage must carry a BossDefinition");
        }

        /// <summary>
        /// Every ScriptableObject asset in the project must resolve to a script. This is the general
        /// version of the bug above, so a new asset cannot reintroduce it.
        /// </summary>
        [Test]
        public void EveryScriptableObjectAsset_ResolvesToItsScript()
        {
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/SpaceXonix" });
            Assert.That(guids, Is.Not.Empty, "the search itself should find the project's assets");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.That(asset, Is.Not.Null,
                    $"{path} has no usable script reference. A ScriptableObject must live in a file named after it.");
            }
        }
    }
}
