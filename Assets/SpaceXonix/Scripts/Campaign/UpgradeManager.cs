using System;
using System.Collections.Generic;
using SpaceXonix.Core;
using SpaceXonix.Player;
using SpaceXonix.Power;
using SpaceXonix.PowerUps;
using UnityEngine;

namespace SpaceXonix.Campaign
{
    /// <summary>
    /// Owns the run's upgrades: offers three random choices between stages and applies their
    /// effective stats to the gameplay systems at each stage load. Run-only, cleared on retry.
    /// </summary>
    public sealed class UpgradeManager : MonoBehaviour
    {
        [SerializeField] private UpgradeSetDefinition upgradeSet;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PowerMeter powerMeter;
        [SerializeField] private PowerUpManager powerUpManager;
        [Tooltip("The ship being flown; its stats multiply on top of the upgrades.")]
        [SerializeField] private SpaceXonix.Presentation.PlayerShipSkin playerShip;
        [SerializeField, Min(1)] private int offerCount = 3;
        [Tooltip("0 uses a time-based seed.")]
        [SerializeField] private int randomSeed;

        private readonly RunUpgradeModel run = new RunUpgradeModel();
        private readonly List<UpgradeDefinition> offer = new List<UpgradeDefinition>();
        private System.Random random;
        private float baseMoveSpeed = -1f;

        public RunUpgradeModel Run => run;
        public IReadOnlyList<UpgradeDefinition> CurrentOffer => offer;
        public bool HasOffer => offer.Count > 0;
        public event Action<UpgradeDefinition> UpgradeTaken;

        private void Awake() => random ??= randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();

        public void SetRandom(System.Random source) => random = source ?? new System.Random();

        /// <summary>Builds the between-stage choice. Returns false when nothing can still be taken.</summary>
        public bool BuildOffer()
        {
            random ??= new System.Random();
            UpgradeOffer.Build(upgradeSet != null ? upgradeSet.upgrades : null, run, offerCount, random, offer);
            return offer.Count > 0;
        }

        public bool Take(int index)
        {
            if (index < 0 || index >= offer.Count) return false;
            var definition = offer[index];
            if (!run.Take(definition)) return false;
            offer.Clear();
            // Reinforced Hull is felt immediately rather than at the next stage, because lives carry across stages.
            if (definition.type == UpgradeType.ReinforcedHull && gameManager != null)
                gameManager.AddLives(Mathf.RoundToInt(definition.perStack));
            ApplyToSystems();
            UpgradeTaken?.Invoke(definition);
            return true;
        }

        public void ClearOffer() => offer.Clear();

        public void ResetRun()
        {
            run.Reset();
            offer.Clear();
            ApplyToSystems();
        }

        /// <summary>Pushes the run's effective stats into the gameplay systems. Safe to call at every stage load.</summary>
        public void ApplyToSystems()
        {
            // The flown ship's stats multiply on top of the run's upgrades.
            var ship = playerShip != null ? playerShip.Stats : SpaceXonix.Presentation.ShipStats.Neutral;
            if (playerController != null)
            {
                if (baseMoveSpeed < 0f) baseMoveSpeed = playerController.MoveSpeed;
                playerController.SetMoveSpeed(baseMoveSpeed * run.MoveSpeedMultiplier * ship.speed);
                playerController.SetZoneSpeedMultipliers(ship.safeSpeed, ship.exposedSpeed);
            }
            if (powerMeter != null)
            {
                powerMeter.SetGainMultiplier(run.PowerGainMultiplier * ship.powerCharge);
                powerMeter.SetShotSpeedMultiplier(ship.shotSpeed);
            }
            if (powerUpManager != null)
            {
                powerUpManager.SetDurationMultiplier(PowerUpType.Shield, run.ShieldDurationMultiplier * ship.abilityDuration);
                powerUpManager.SetDurationMultiplier(PowerUpType.Freeze, run.FreezeDurationMultiplier * ship.abilityDuration);
                powerUpManager.SetDurationMultiplier(PowerUpType.ArenaTilt, ship.abilityDuration);
                powerUpManager.SetTiltPenaltyMultiplier(run.TiltPenaltyMultiplier);
                powerUpManager.SetPickupChanceBonus(run.PickupChanceBonus);
                powerUpManager.SetShipPickupChanceMultiplier(ship.pickupChance);
            }
        }

        public string Describe()
        {
            if (run.Stacks.Count == 0) return "none";
            var parts = new List<string>();
            if (upgradeSet != null && upgradeSet.upgrades != null)
                foreach (var definition in upgradeSet.upgrades)
                {
                    if (definition == null) continue;
                    var stacks = run.GetStacks(definition.type);
                    if (stacks > 0) parts.Add(stacks > 1 ? $"{definition.displayName} x{stacks}" : definition.displayName);
                }
            return parts.Count > 0 ? string.Join(", ", parts) : "none";
        }
    }
}
