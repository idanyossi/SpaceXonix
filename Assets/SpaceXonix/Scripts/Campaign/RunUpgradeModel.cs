using System.Collections.Generic;
using UnityEngine;

namespace SpaceXonix.Campaign
{
    /// <summary>
    /// Upgrades taken during one campaign run and the effective stats they produce.
    /// Definitions stay immutable; everything here is run state and disappears on reset.
    /// </summary>
    public sealed class RunUpgradeModel
    {
        private readonly Dictionary<UpgradeType, int> stacks = new Dictionary<UpgradeType, int>();
        private readonly Dictionary<UpgradeType, float> perStack = new Dictionary<UpgradeType, float>();

        public IReadOnlyDictionary<UpgradeType, int> Stacks => stacks;

        public int GetStacks(UpgradeType type) => stacks.TryGetValue(type, out var count) ? count : 0;

        public bool CanTake(UpgradeDefinition definition) => definition != null && GetStacks(definition.type) < definition.maxStacks;

        /// <summary>Returns false when the upgrade is already at its stack limit.</summary>
        public bool Take(UpgradeDefinition definition)
        {
            if (!CanTake(definition)) return false;
            stacks[definition.type] = GetStacks(definition.type) + 1;
            perStack[definition.type] = definition.perStack;
            return true;
        }

        public void Reset()
        {
            stacks.Clear();
            perStack.Clear();
        }

        private float Total(UpgradeType type) => GetStacks(type) * (perStack.TryGetValue(type, out var value) ? value : 0f);

        /// <summary>Extra lives granted at the start of following stages (Reinforced Hull).</summary>
        public int BonusLives => Mathf.RoundToInt(Total(UpgradeType.ReinforcedHull));

        public float MoveSpeedMultiplier => 1f + Total(UpgradeType.ImprovedThrusters);

        public float PowerGainMultiplier => 1f + Total(UpgradeType.RapidCapacitor);

        public float ShieldDurationMultiplier => 1f + Total(UpgradeType.ShieldCapacitor);

        public float FreezeDurationMultiplier => 1f + Total(UpgradeType.CryogenicCore);

        /// <summary>Scales the Arena Tilt movement penalty; Gravity Stabilizer reduces it toward zero.</summary>
        public float TiltPenaltyMultiplier => Mathf.Clamp01(1f - Total(UpgradeType.GravityStabilizer));

        /// <summary>Added to the pickup spawn chance (Scavenger Protocol), before the configured maximum.</summary>
        public float PickupChanceBonus => Total(UpgradeType.ScavengerProtocol);
    }
}
