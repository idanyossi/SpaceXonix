using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Campaign;
using SpaceXonix.Enemies;
using SpaceXonix.Hazards;
using SpaceXonix.PowerUps;
using SpaceXonix.Scoring;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class StageModifierTests
    {
        [Test]
        public void Selection_OnlyOffersModifiersThatFitTheStage()
        {
            var owned = new List<UnityEngine.Object>();
            try
            {
                var basicStage = Stage(owned, EnemyType.BasicBouncer, lasers: 0);
                var laserStage = Stage(owned, EnemyType.BasicBouncer, lasers: 2);
                var volatileStage = Stage(owned, EnemyType.Volatile, lasers: 1);

                var swarm = Own(owned, Modifier("Overclocked Swarm"));
                var storm = Own(owned, Modifier("Laser Storm")); storm.requiresLasers = true;
                var matter = Own(owned, Modifier("Volatile Matter"));
                matter.requiresEnemyType = true; matter.requiredEnemyType = EnemyType.Volatile;

                var pool = new[] { swarm, storm, matter };
                var types = new List<EnemyType>();
                var compatible = new List<StageModifierDefinition>();

                StageModifierSelection.Select(pool, basicStage, new System.Random(1), types, compatible);
                Assert.That(compatible, Is.EquivalentTo(new[] { swarm }), "a laserless basic stage only fits the universal modifier");

                StageModifierSelection.Select(pool, laserStage, new System.Random(1), types, compatible);
                Assert.That(compatible, Is.EquivalentTo(new[] { swarm, storm }));

                StageModifierSelection.Select(pool, volatileStage, new System.Random(1), types, compatible);
                Assert.That(compatible, Is.EquivalentTo(new[] { swarm, storm, matter }));

                Assert.That(StageModifierSelection.Select(pool, null, new System.Random(1), types, compatible), Is.Null);
                Assert.That(StageModifierSelection.Select(null, laserStage, new System.Random(1), types, compatible), Is.Null);
                Assert.That(StageModifierSelection.Select(new[] { storm }, basicStage, new System.Random(1), types, compatible), Is.Null,
                    "no compatible modifier means no modifier, not a crash");
            }
            finally
            {
                Destroy(owned);
            }
        }

        [Test]
        public void Selection_KeepsBossAndNormalModifiersInSeparatePools()
        {
            var owned = new List<UnityEngine.Object>();
            try
            {
                var normalStage = Stage(owned, EnemyType.BasicBouncer, lasers: 1);
                var bossStage = Own(owned, ScriptableObject.CreateInstance<StageDefinition>());
                bossStage.enemySpawns = new EnemySpawnRequest[0];
                bossStage.lasers = new LaserPlacement[0];
                bossStage.boss = Own(owned, ScriptableObject.CreateInstance<SpaceXonix.Boss.BossDefinition>());
                Assert.That(bossStage.IsBossStage, Is.True);

                var swarm = Own(owned, Modifier("Overclocked Swarm"));
                var overdriven = Own(owned, Modifier("Overdriven Core"));
                overdriven.requiresBossStage = true;
                overdriven.bossFireIntervalMultiplier = .65f;

                var pool = new[] { swarm, overdriven };
                var types = new List<EnemyType>();
                var compatible = new List<StageModifierDefinition>();

                StageModifierSelection.Select(pool, normalStage, new System.Random(1), types, compatible);
                Assert.That(compatible, Is.EquivalentTo(new[] { swarm }), "a boss modifier never lands on a normal stage");

                StageModifierSelection.Select(pool, bossStage, new System.Random(1), types, compatible);
                Assert.That(compatible, Is.EquivalentTo(new[] { overdriven }),
                    "the alien-free boss stage never draws an alien modifier");
            }
            finally
            {
                Destroy(owned);
            }
        }

        [Test]
        public void Selection_IsDeterministicForASeedAndAlwaysCompatible()
        {
            var owned = new List<UnityEngine.Object>();
            try
            {
                var stage = Stage(owned, EnemyType.BasicBouncer, lasers: 1);
                var pool = new List<StageModifierDefinition>();
                for (var i = 0; i < 4; i++) pool.Add(Own(owned, Modifier("Mod" + i)));
                pool[3].requiresEnemyType = true; pool[3].requiredEnemyType = EnemyType.Volatile;

                var types = new List<EnemyType>();
                var compatible = new List<StageModifierDefinition>();
                var first = StageModifierSelection.Select(pool, stage, new System.Random(7), types, compatible);
                var second = StageModifierSelection.Select(pool, stage, new System.Random(7), types, compatible);
                Assert.That(second, Is.SameAs(first), "the same seed rolls the same modifier");

                for (var seed = 0; seed < 25; seed++)
                {
                    var rolled = StageModifierSelection.Select(pool, stage, new System.Random(seed), types, compatible);
                    Assert.That(rolled, Is.Not.SameAs(pool[3]), "a Volatile-only modifier never lands on a stage without Volatile aliens");
                    Assert.That(rolled, Is.Not.Null);
                }
            }
            finally
            {
                Destroy(owned);
            }
        }

        [Test]
        public void Manager_PushesTypedEffectsIntoTheSystemsAndClearsThemAgain()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Enemies.SpeedMultiplier, Is.EqualTo(1f));
                Assert.That(fixture.Lasers.CooldownMultiplier, Is.EqualTo(1f));
                Assert.That(fixture.Score.BonusMultiplier, Is.EqualTo(1f));

                var modifier = fixture.Only;
                modifier.enemySpeedMultiplier = 1.25f;
                modifier.laserCooldownMultiplier = .6f;
                modifier.unstableIntervalMultiplier = .5f;
                modifier.volatileRadiusMultiplier = 1.5f;
                modifier.pickupChanceMultiplier = .5f;
                modifier.scoreMultiplier = 1.15f;

                Assert.That(fixture.Manager.SelectFor(fixture.Stage), Is.SameAs(modifier));
                Assert.That(fixture.Enemies.SpeedMultiplier, Is.EqualTo(1.25f).Within(.0001f));
                Assert.That(fixture.Enemies.UnstableIntervalMultiplier, Is.EqualTo(.5f).Within(.0001f));
                Assert.That(fixture.Enemies.VolatileRadiusMultiplier, Is.EqualTo(1.5f).Within(.0001f));
                Assert.That(fixture.Lasers.CooldownMultiplier, Is.EqualTo(.6f).Within(.0001f));
                Assert.That(PickupMultiplier(fixture.PowerUps), Is.EqualTo(.5f).Within(.0001f));
                Assert.That(fixture.Score.BonusMultiplier, Is.EqualTo(1.15f).Within(.0001f));
                Assert.That(fixture.Manager.Describe(), Does.Contain("Overclocked Swarm").And.Contain("1.15"));

                fixture.Manager.Clear();
                Assert.That(fixture.Manager.Current, Is.Null);
                Assert.That(fixture.Enemies.SpeedMultiplier, Is.EqualTo(1f).Within(.0001f));
                Assert.That(fixture.Enemies.UnstableIntervalMultiplier, Is.EqualTo(1f).Within(.0001f));
                Assert.That(fixture.Enemies.VolatileRadiusMultiplier, Is.EqualTo(1f).Within(.0001f));
                Assert.That(fixture.Lasers.CooldownMultiplier, Is.EqualTo(1f).Within(.0001f));
                Assert.That(PickupMultiplier(fixture.PowerUps), Is.EqualTo(1f).Within(.0001f));
                Assert.That(fixture.Score.BonusMultiplier, Is.EqualTo(1f).Within(.0001f));
                Assert.That(fixture.Manager.Describe(), Is.EqualTo("none"));
            }
        }

        [Test]
        public void Enemy_SpeedMultiplierRescalesMotionAndLeavesTheDefinitionAlone()
        {
            var boardObject = new GameObject("Board");
            var enemyObject = new GameObject("Enemy");
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            try
            {
                var board = boardObject.AddComponent<SpaceXonix.Board.BoardManager>();
                board.Initialize();
                definition.moveSpeed = 4f;
                var enemy = enemyObject.AddComponent<BasicBouncer>();

                enemy.SetSpeedMultiplier(1.5f);
                enemy.Activate(definition, board, null, board.GetWorldPosition(new GridCoordinate(10, 10)), Vector2.right);
                Assert.That(enemy.Velocity.magnitude, Is.EqualTo(6f).Within(.0001f), "the spawn speed already carries the modifier");

                enemy.SetSpeedMultiplier(.5f);
                Assert.That(enemy.Velocity.magnitude, Is.EqualTo(2f).Within(.0001f), "changing it mid-stage rescales from the base speed, not the scaled one");
                Assert.That(enemy.Velocity.normalized, Is.EqualTo(Vector2.right).Using((IEqualityComparer<Vector2>)new VectorComparer()));

                enemy.SetSpeedMultiplier(1f);
                Assert.That(enemy.Velocity.magnitude, Is.EqualTo(4f).Within(.0001f));
                Assert.That(definition.moveSpeed, Is.EqualTo(4f), "definitions stay immutable");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(boardObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Laser_CooldownMultiplierShortensTheWaitBetweenShots()
        {
            var stormObject = new GameObject("StormEmitter");
            var plainObject = new GameObject("PlainEmitter");
            var definition = ScriptableObject.CreateInstance<LaserDefinition>();
            try
            {
                definition.warningDuration = .75f;
                definition.firingDuration = .25f;
                definition.cooldownDuration = 2f;

                var storm = stormObject.AddComponent<LaserEmitter>();
                storm.SetCooldownMultiplier(.5f);
                storm.SetDefinition(definition);
                storm.Initialize(null, null, null, null, null);

                var plain = plainObject.AddComponent<LaserEmitter>();
                plain.SetDefinition(definition);
                plain.Initialize(null, null, null, null, null);

                Assert.That(storm.State, Is.EqualTo(LaserState.Cooldown));
                Assert.That(storm.TimeRemaining, Is.EqualTo(1f).Within(.0001f), "Laser Storm halves the 2s cooldown");
                Assert.That(plain.TimeRemaining, Is.EqualTo(2f).Within(.0001f), "an unmodified emitter keeps the definition's cooldown");

                storm.Tick(.99f);
                plain.Tick(.99f);
                Assert.That(storm.State, Is.EqualTo(LaserState.Cooldown));
                Assert.That(storm.TimeRemaining, Is.EqualTo(.01f).Within(.0001f));
                Assert.That(plain.TimeRemaining, Is.EqualTo(1.01f).Within(.0001f));
                Assert.That(definition.cooldownDuration, Is.EqualTo(2f), "definitions stay immutable");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stormObject);
                UnityEngine.Object.DestroyImmediate(plainObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private sealed class VectorComparer : IEqualityComparer<Vector2>
        {
            public bool Equals(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < .0001f;
            public int GetHashCode(Vector2 value) => 0;
        }

        private static StageModifierDefinition Modifier(string name)
        {
            var modifier = ScriptableObject.CreateInstance<StageModifierDefinition>();
            modifier.displayName = name;
            modifier.effectText = name + " effect";
            return modifier;
        }

        private static StageDefinition Stage(List<UnityEngine.Object> owned, EnemyType type, int lasers)
        {
            var stage = Own(owned, ScriptableObject.CreateInstance<StageDefinition>());
            var enemyDefinition = Own(owned, ScriptableObject.CreateInstance<EnemyDefinition>());
            enemyDefinition.type = type;
            stage.enemySpawns = new[] { new EnemySpawnRequest { definition = enemyDefinition } };
            stage.lasers = new LaserPlacement[lasers];
            for (var i = 0; i < lasers; i++) stage.lasers[i] = new LaserPlacement();
            return stage;
        }

        private static T Own<T>(List<UnityEngine.Object> owned, T item) where T : UnityEngine.Object
        {
            owned.Add(item);
            return item;
        }

        private static void Destroy(List<UnityEngine.Object> owned)
        {
            foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
        }

        private static float PickupMultiplier(PowerUpManager manager) =>
            (float)typeof(PowerUpManager).GetField("pickupChanceMultiplier", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("StageModifierFixture");
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            public readonly EnemyManager Enemies;
            public readonly LaserManager Lasers;
            public readonly PowerUpManager PowerUps;
            public readonly ScoreManager Score;
            public readonly StageModifierManager Manager;
            public readonly StageDefinition Stage;
            public readonly StageModifierDefinition Only;

            public Fixture()
            {
                root.SetActive(false);
                Enemies = root.AddComponent<EnemyManager>();
                Lasers = root.AddComponent<LaserManager>();
                PowerUps = root.AddComponent<PowerUpManager>();
                Score = root.AddComponent<ScoreManager>();
                var scoring = Own(ScriptableObject.CreateInstance<ScoringDefinition>());
                Set(Score, "definition", scoring);
                Invoke(Score, "Awake");

                Stage = StageModifierTests.Stage(owned, EnemyType.BasicBouncer, lasers: 1);
                Only = Own(Modifier("Overclocked Swarm"));
                var set = Own(ScriptableObject.CreateInstance<StageModifierSetDefinition>());
                set.modifiers = new[] { Only };

                Manager = root.AddComponent<StageModifierManager>();
                Set(Manager, "modifierSet", set);
                Set(Manager, "enemyManager", Enemies);
                Set(Manager, "laserManager", Lasers);
                Set(Manager, "powerUpManager", PowerUps);
                Set(Manager, "scoreManager", Score);
                Manager.SetRandom(new System.Random(1));
                root.SetActive(true);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                Destroy(owned);
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
