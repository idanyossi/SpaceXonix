using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Campaign;
using SpaceXonix.Player;
using SpaceXonix.Power;
using SpaceXonix.PowerUps;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class UpgradeTests
    {
        [Test]
        public void RunModel_StacksUpgradesUpToTheirLimitAndComputesStats()
        {
            var model = new RunUpgradeModel();
            var thrusters = Definition(UpgradeType.ImprovedThrusters, .1f, 3);
            var hull = Definition(UpgradeType.ReinforcedHull, 1f, 2);
            try
            {
                Assert.That(model.MoveSpeedMultiplier, Is.EqualTo(1f));
                Assert.That(model.BonusLives, Is.Zero);

                Assert.That(model.Take(thrusters), Is.True);
                Assert.That(model.Take(thrusters), Is.True);
                Assert.That(model.MoveSpeedMultiplier, Is.EqualTo(1.2f).Within(.0001f));
                Assert.That(model.Take(thrusters), Is.True);
                Assert.That(model.Take(thrusters), Is.False, "stack limit reached");
                Assert.That(model.CanTake(thrusters), Is.False);
                Assert.That(model.MoveSpeedMultiplier, Is.EqualTo(1.3f).Within(.0001f));

                model.Take(hull); model.Take(hull);
                Assert.That(model.BonusLives, Is.EqualTo(2));
                Assert.That(model.Take(hull), Is.False);

                model.Reset();
                Assert.That(model.MoveSpeedMultiplier, Is.EqualTo(1f));
                Assert.That(model.BonusLives, Is.Zero);
                Assert.That(model.CanTake(thrusters), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(thrusters);
                UnityEngine.Object.DestroyImmediate(hull);
            }
        }

        [Test]
        public void RunModel_MapsEachUpgradeToItsOwnStat()
        {
            var definitions = new List<UpgradeDefinition>();
            try
            {
                var model = new RunUpgradeModel();
                foreach (var pair in new[]
                {
                    (UpgradeType.RapidCapacitor, .2f), (UpgradeType.ShieldCapacitor, .25f),
                    (UpgradeType.CryogenicCore, .25f), (UpgradeType.GravityStabilizer, .5f), (UpgradeType.ScavengerProtocol, .1f)
                })
                {
                    var definition = Definition(pair.Item1, pair.Item2, 2);
                    definitions.Add(definition);
                    model.Take(definition);
                }
                Assert.That(model.PowerGainMultiplier, Is.EqualTo(1.2f).Within(.0001f));
                Assert.That(model.ShieldDurationMultiplier, Is.EqualTo(1.25f).Within(.0001f));
                Assert.That(model.FreezeDurationMultiplier, Is.EqualTo(1.25f).Within(.0001f));
                Assert.That(model.TiltPenaltyMultiplier, Is.EqualTo(.5f).Within(.0001f), "gravity stabiliser halves the tilt penalty");
                Assert.That(model.PickupChanceBonus, Is.EqualTo(.1f).Within(.0001f));

                model.Take(definitions[3]);
                Assert.That(model.TiltPenaltyMultiplier, Is.Zero, "two stacks remove the penalty, never going negative");
            }
            finally
            {
                foreach (var definition in definitions) UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Offer_ReturnsThreeDistinctEligibleUpgrades()
        {
            var pool = new List<UpgradeDefinition>();
            try
            {
                foreach (UpgradeType type in Enum.GetValues(typeof(UpgradeType))) pool.Add(Definition(type, .1f, 1));
                var model = new RunUpgradeModel();
                var results = new List<UpgradeDefinition>();

                UpgradeOffer.Build(pool, model, 3, new System.Random(1), results);
                Assert.That(results.Count, Is.EqualTo(3));
                Assert.That(results, Is.Unique);

                foreach (var definition in pool) model.Take(definition);
                UpgradeOffer.Build(pool, model, 3, new System.Random(1), results);
                Assert.That(results, Is.Empty, "nothing is offered once every upgrade is maxed");

                model.Reset();
                model.Take(pool[0]);
                UpgradeOffer.Build(pool, model, 3, new System.Random(2), results);
                Assert.That(results, Has.No.Member(pool[0]), "maxed upgrades are not offered again");
            }
            finally
            {
                foreach (var definition in pool) UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Manager_AppliesTakenUpgradesToTheGameplaySystems()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Player.MoveSpeed, Is.EqualTo(5f).Within(.0001f));
                fixture.Manager.SetRandom(new System.Random(3));
                Assert.That(fixture.Manager.BuildOffer(), Is.True);
                Assert.That(fixture.Manager.CurrentOffer.Count, Is.EqualTo(3));

                fixture.TakeByType(UpgradeType.ImprovedThrusters);
                Assert.That(fixture.Player.MoveSpeed, Is.EqualTo(5.5f).Within(.0001f), "+10% base speed");

                fixture.TakeByType(UpgradeType.ShieldCapacitor);
                Assert.That(fixture.PowerUps.GetEffectDuration(fixture.ShieldDefinition), Is.EqualTo(5f).Within(.0001f), "4s shield +25%");

                fixture.TakeByType(UpgradeType.ReinforcedHull);
                Assert.That(fixture.Manager.Run.BonusLives, Is.EqualTo(1));
                Assert.That(fixture.Game.Lives, Is.EqualTo(4), "the extra life lands immediately, on top of the carried lives");
                Assert.That(fixture.Manager.Describe(), Does.Contain("Improved Thrusters").And.Contain("Reinforced Hull"));

                fixture.Manager.ResetRun();
                Assert.That(fixture.Player.MoveSpeed, Is.EqualTo(5f).Within(.0001f), "upgrades vanish with the run");
                Assert.That(fixture.PowerUps.GetEffectDuration(fixture.ShieldDefinition), Is.EqualTo(4f).Within(.0001f));
                Assert.That(fixture.Manager.Describe(), Is.EqualTo("none"));
            }
        }

        private static UpgradeDefinition Definition(UpgradeType type, float perStack, int maxStacks)
        {
            var definition = ScriptableObject.CreateInstance<UpgradeDefinition>();
            definition.type = type;
            definition.displayName = type.ToString();
            definition.perStack = perStack;
            definition.maxStacks = maxStacks;
            return definition;
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("UpgradeFixture");
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            public readonly PlayerController Player;
            public readonly SpaceXonix.Core.GameManager Game;
            public readonly PowerUpManager PowerUps;
            public readonly UpgradeManager Manager;
            public readonly PowerUpDefinition ShieldDefinition;

            public Fixture()
            {
                root.SetActive(false);
                var playerObject = Own(new GameObject("Player"));
                Player = playerObject.AddComponent<PlayerController>();
                Set(Player, "moveSpeed", 5f);
                Invoke(Player, "Awake");
                var input = root.AddComponent<SpaceXonix.Input.InputRouter>();
                var boardObject = Own(new GameObject("Board"));
                var board = boardObject.AddComponent<SpaceXonix.Board.BoardManager>();
                board.Initialize();
                Game = root.AddComponent<SpaceXonix.Core.GameManager>();
                Set(Game, "inputRouter", input); Set(Game, "playerController", Player); Set(Game, "boardManager", board);
                Invoke(Game, "Awake");
                PowerUps = root.AddComponent<PowerUpManager>();
                var meter = root.AddComponent<PowerMeter>();
                var powerDefinition = Own(ScriptableObject.CreateInstance<PowerDefinition>());
                Set(meter, "definition", powerDefinition);
                meter.Initialize();

                ShieldDefinition = Own(ScriptableObject.CreateInstance<PowerUpDefinition>());
                ShieldDefinition.type = PowerUpType.Shield;
                ShieldDefinition.duration = 4f;

                var set = Own(ScriptableObject.CreateInstance<UpgradeSetDefinition>());
                var definitions = new List<UpgradeDefinition>();
                foreach (var pair in new[]
                {
                    (UpgradeType.ReinforcedHull, 1f, "Reinforced Hull"), (UpgradeType.ImprovedThrusters, .1f, "Improved Thrusters"),
                    (UpgradeType.RapidCapacitor, .2f, "Rapid Capacitor"), (UpgradeType.ShieldCapacitor, .25f, "Shield Capacitor"),
                    (UpgradeType.CryogenicCore, .25f, "Cryogenic Core"), (UpgradeType.GravityStabilizer, .5f, "Gravity Stabilizer"),
                    (UpgradeType.ScavengerProtocol, .1f, "Scavenger Protocol")
                })
                {
                    var definition = Own(ScriptableObject.CreateInstance<UpgradeDefinition>());
                    definition.type = pair.Item1; definition.perStack = pair.Item2; definition.displayName = pair.Item3; definition.maxStacks = 3;
                    definitions.Add(definition);
                }
                set.upgrades = definitions.ToArray();

                Manager = root.AddComponent<UpgradeManager>();
                Set(Manager, "upgradeSet", set);
                Set(Manager, "gameManager", Game);
                Set(Manager, "playerController", Player);
                Set(Manager, "powerMeter", meter);
                Set(Manager, "powerUpManager", PowerUps);
                root.SetActive(true);
                Invoke(Game, "Start");
                Invoke(Manager, "Awake");
            }

            /// <summary>Forces the offer to contain the wanted upgrade, then takes it.</summary>
            public void TakeByType(UpgradeType type)
            {
                var set = (UpgradeSetDefinition)typeof(UpgradeManager).GetField("upgradeSet", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Manager);
                var offer = (List<UpgradeDefinition>)typeof(UpgradeManager).GetField("offer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Manager);
                offer.Clear();
                foreach (var definition in set.upgrades) if (definition.type == type) offer.Add(definition);
                Assert.That(Manager.Take(0), Is.True, type.ToString());
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }

            private T Own<T>(T item) where T : UnityEngine.Object
            {
                owned.Add(item);
                return item;
            }

            private static void Set(object target, string name, object value) =>
                target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

            private static void Invoke(object target, string name) =>
                target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        }
    }
}
