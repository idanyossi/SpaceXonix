using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Enemies;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using SpaceXonix.Power;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class PowerMeterTests
    {
        [TestCase(1f, 5f)]
        [TestCase(6.5f, 32.5f)]
        [TestCase(20f, 100f)]
        public void AddCapture_GainsFivePowerPerCapturedPercent(float percentage, float expected)
        {
            var model = new PowerMeterModel(100f, 5f);
            Assert.That(model.AddCapture(percentage), Is.EqualTo(expected).Within(.0001f));
            Assert.That(model.Power, Is.EqualTo(expected).Within(.0001f));
        }

        [Test]
        public void AddCapture_CapsAtMaxAndBecomesFull()
        {
            var model = new PowerMeterModel(100f, 5f);
            model.AddCapture(15f);
            Assert.That(model.IsFull, Is.False);
            Assert.That(model.AddCapture(15f), Is.EqualTo(25f).Within(.0001f));
            Assert.That(model.Power, Is.EqualTo(100f));
            Assert.That(model.IsFull, Is.True);
            Assert.That(model.AddCapture(5f), Is.Zero);
        }

        [Test]
        public void TryConsumeFull_OnlySucceedsAtFullAndEmptiesMeter()
        {
            var model = new PowerMeterModel(100f, 5f);
            model.AddCapture(19f);
            Assert.That(model.TryConsumeFull(), Is.False);
            Assert.That(model.Power, Is.EqualTo(95f).Within(.0001f));
            model.AddCapture(1f);
            Assert.That(model.TryConsumeFull(), Is.True);
            Assert.That(model.Power, Is.Zero);
            Assert.That(model.TryConsumeFull(), Is.False);
        }

        [Test]
        public void GainMultiplier_ScalesGainAndResetRestoresDefaults()
        {
            var model = new PowerMeterModel(100f, 5f);
            model.SetGainMultiplier(1.2f);
            Assert.That(model.AddCapture(10f), Is.EqualTo(60f).Within(.0001f));
            model.Reset();
            Assert.That(model.Power, Is.Zero);
            Assert.That(model.GainMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void NonPositiveCapture_AddsNothing()
        {
            var model = new PowerMeterModel(100f, 5f);
            Assert.That(model.AddCapture(0f), Is.Zero);
            Assert.That(model.AddCapture(-4f), Is.Zero);
            Assert.That(model.Power, Is.Zero);
        }

        [Test]
        public void FindFirstHit_ReturnsNearestEnemyAlongPathOnly()
        {
            using (var enemies = new EnemyList())
            {
                var far = enemies.Add(new Vector2(3f, 0f));
                var near = enemies.Add(new Vector2(1f, .1f));
                enemies.Add(new Vector2(-1f, 0f));
                enemies.Add(new Vector2(2f, 1f));
                Assert.That(PowerShotProjectile.FindFirstHit(Vector2.zero, new Vector2(5f, 0f), enemies.List, .2f), Is.SameAs(near));
                near.Deactivate();
                Assert.That(PowerShotProjectile.FindFirstHit(Vector2.zero, new Vector2(5f, 0f), enemies.List, .2f), Is.SameAs(far));
                Assert.That(PowerShotProjectile.FindFirstHit(Vector2.zero, new Vector2(2f, 0f), enemies.List, .2f), Is.Null);
            }
        }

        [Test]
        public void CaptureEvents_ChargeMeterAndRaisePowerFullOnce()
        {
            using (var fixture = new Fixture())
            {
                var fullEvents = 0;
                fixture.Meter.PowerFull += () => fullEvents++;
                fixture.CaptureTopRows(3);
                Assert.That(fixture.Meter.Power, Is.GreaterThan(0f));
                Assert.That(fixture.Meter.IsReady, Is.False);
                Fixture.Charge(fixture.Meter, 100f);
                Assert.That(fixture.Meter.IsReady, Is.True);
                Assert.That(fullEvents, Is.EqualTo(0), "direct charge bypasses the capture event");
                fixture.ResetModel();
                fixture.CaptureTopRows(30);
                Assert.That(fixture.Meter.IsReady, Is.True);
                Assert.That(fullEvents, Is.EqualTo(1));
            }
        }

        [Test]
        public void FiringWithoutFullMeter_DoesNothing()
        {
            using (var fixture = new Fixture())
            {
                Fixture.Charge(fixture.Meter, 99f);
                Assert.That(fixture.Input.RequestPowerShot(), Is.True);
                Assert.That(fixture.Meter.ActiveShots, Is.Empty);
                Assert.That(fixture.Meter.Power, Is.EqualTo(99f));
            }
        }

        [Test]
        public void PowerShot_ConsumesMeterAndDestroysFirstEnemyInFacingDirection()
        {
            using (var fixture = new Fixture())
            {
                fixture.PlacePlayer(new GridCoordinate(0, 20), CardinalDirection.Right);
                var near = fixture.SpawnEnemy(new GridCoordinate(10, 20));
                var far = fixture.SpawnEnemy(new GridCoordinate(30, 20));
                var offAxis = fixture.SpawnEnemy(new GridCoordinate(5, 30));
                EnemyController destroyed = null;
                fixture.Meter.EnemyDestroyedByShot += enemy => destroyed = enemy;
                Vector3? impact = null;
                var nearPosition = near.transform.position;
                fixture.Meter.ShotImpact += at => impact = at;
                Fixture.Charge(fixture.Meter, 100f);

                Assert.That(fixture.Input.RequestPowerShot(), Is.True);
                Assert.That(fixture.Meter.Power, Is.Zero);
                Assert.That(fixture.Meter.ActiveShots.Count, Is.EqualTo(1));
                for (var i = 0; i < 200 && fixture.Meter.ActiveShots.Count > 0; i++) fixture.Meter.AdvanceShots(.02f);

                Assert.That(destroyed, Is.SameAs(near));
                Assert.That(impact.HasValue, Is.True, "the hit is announced for its impact effects");
                Assert.That(Vector2.Distance(impact.Value, nearPosition), Is.LessThan(.5f));
                Assert.That(near.IsActiveEnemy, Is.False);
                Assert.That(far.IsActiveEnemy, Is.True);
                Assert.That(offAxis.IsActiveEnemy, Is.True);
                Assert.That(fixture.Manager.ActiveEnemies, Has.No.Member(near));
                Assert.That(fixture.Meter.ActiveShots, Is.Empty);
            }
        }

        [TestCase(CardinalDirection.Up)]
        [TestCase(CardinalDirection.Down)]
        [TestCase(CardinalDirection.Left)]
        public void PowerShot_FollowsFacingDirectionOnEachAxis(CardinalDirection facing)
        {
            using (var fixture = new Fixture())
            {
                fixture.PlacePlayer(new GridCoordinate(20, 40), facing);
                var offset = facing.ToVector2();
                var target = fixture.SpawnEnemy(new GridCoordinate(20 + (int)offset.x * 8, 40 + (int)offset.y * 8));
                var behind = fixture.SpawnEnemy(new GridCoordinate(20 - (int)offset.x * 8, 40 - (int)offset.y * 8));
                Fixture.Charge(fixture.Meter, 100f);
                Assert.That(fixture.Meter.TryFirePowerShot(), Is.True);
                for (var i = 0; i < 200 && fixture.Meter.ActiveShots.Count > 0; i++) fixture.Meter.AdvanceShots(.02f);
                Assert.That(target.IsActiveEnemy, Is.False);
                Assert.That(behind.IsActiveEnemy, Is.True);
            }
        }

        [Test]
        public void MissedShot_LeavesBoardAndReturnsToPoolForReuse()
        {
            using (var fixture = new Fixture())
            {
                fixture.PlacePlayer(new GridCoordinate(0, 20), CardinalDirection.Right);
                Fixture.Charge(fixture.Meter, 100f);
                Assert.That(fixture.Meter.TryFirePowerShot(), Is.True);
                var first = fixture.Meter.ActiveShots[0];
                for (var i = 0; i < 500 && fixture.Meter.ActiveShots.Count > 0; i++) fixture.Meter.AdvanceShots(.05f);
                Assert.That(fixture.Meter.ActiveShots, Is.Empty);
                Assert.That(first.gameObject.activeSelf, Is.False);

                Fixture.Charge(fixture.Meter, 100f);
                Assert.That(fixture.Meter.TryFirePowerShot(), Is.True);
                Assert.That(fixture.Meter.ActiveShots[0], Is.SameAs(first));
                Assert.That(first.gameObject.activeSelf, Is.True);
                Assert.That(first.IsFlying, Is.True);
            }
        }

        [Test]
        public void DisabledGameplayInput_CannotFire()
        {
            using (var fixture = new Fixture())
            {
                Fixture.Charge(fixture.Meter, 100f);
                fixture.Input.SetGameplayInputEnabled(false);
                Assert.That(fixture.Input.RequestPowerShot(), Is.False);
                Assert.That(fixture.Meter.IsReady, Is.True);
                Assert.That(fixture.Meter.ActiveShots, Is.Empty);
            }
        }

        private sealed class EnemyList : IDisposable
        {
            public readonly List<EnemyController> List = new List<EnemyController>();
            private readonly BoardManager board;
            private readonly EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();

            public EnemyList()
            {
                board = new GameObject("Board").AddComponent<BoardManager>();
                board.Initialize();
            }

            public EnemyController Add(Vector2 position)
            {
                var enemy = new GameObject("Enemy").AddComponent<BasicBouncer>();
                enemy.Activate(definition, board, null, position, Vector2.right);
                List.Add(enemy);
                return enemy;
            }

            public void Dispose()
            {
                foreach (var enemy in List) UnityEngine.Object.DestroyImmediate(enemy.gameObject);
                UnityEngine.Object.DestroyImmediate(board.gameObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("PowerFixture");
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            private readonly GameObject shotPrefab;
            private readonly EnemyDefinition enemyDefinition;
            private readonly GameObject enemyPrefab;
            public readonly BoardManager Board;
            public readonly InputRouter Input;
            public readonly PlayerController Player;
            public readonly EnemyManager Manager;
            public readonly PowerMeter Meter;

            public Fixture()
            {
                root.SetActive(false);
                Board = root.AddComponent<BoardManager>();
                Board.Initialize();
                var playerObject = new GameObject("Player"); owned.Add(playerObject);
                Player = playerObject.AddComponent<PlayerController>();
                Input = root.AddComponent<InputRouter>();
                var pool = root.AddComponent<PoolService>();
                Manager = root.AddComponent<EnemyManager>();
                Set(Manager, "boardManager", Board); Set(Manager, "playerController", Player); Set(Manager, "poolService", pool);

                shotPrefab = new GameObject("PowerShotPrefab"); shotPrefab.AddComponent<PowerShotProjectile>(); shotPrefab.SetActive(false); owned.Add(shotPrefab);
                enemyPrefab = new GameObject("EnemyPrefab"); enemyPrefab.AddComponent<BasicBouncer>(); enemyPrefab.SetActive(false); owned.Add(enemyPrefab);
                enemyDefinition = ScriptableObject.CreateInstance<EnemyDefinition>(); owned.Add(enemyDefinition);
                var power = ScriptableObject.CreateInstance<PowerDefinition>(); owned.Add(power);

                Meter = root.AddComponent<PowerMeter>();
                Set(Meter, "definition", power); Set(Meter, "boardManager", Board); Set(Meter, "inputRouter", Input);
                Set(Meter, "playerController", Player); Set(Meter, "enemyManager", Manager); Set(Meter, "poolService", pool);
                Set(Meter, "shotPrefab", shotPrefab);
                root.SetActive(true);
                Meter.Initialize();
                typeof(PowerMeter).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Meter, null);
                Input.SetGameplayInputEnabled(true);
                PlacePlayer(new GridCoordinate(0, 1), CardinalDirection.Right);
            }

            public void PlacePlayer(GridCoordinate cell, CardinalDirection facing)
            {
                Set(Player, "initialDirection", facing);
                Player.transform.position = Board.GetWorldPosition(cell);
                typeof(PlayerController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Player, null);
            }

            public EnemyController SpawnEnemy(GridCoordinate cell)
            {
                Assert.That(Manager.Spawn(enemyPrefab, enemyDefinition, cell, Vector2.right), Is.True);
                return Manager.ActiveEnemies[Manager.ActiveEnemies.Count - 1];
            }

            public void CaptureTopRows(int rows)
            {
                var y = Board.Rows - 1 - rows;
                for (var x = 1; x < Board.Columns; x++) Board.Model.MoveTo(new GridCoordinate(x, y));
            }

            public void ResetModel() => Meter.ResetMeter();

            public static void Charge(PowerMeter meter, float power)
            {
                var model = (PowerMeterModel)typeof(PowerMeter).GetField("model", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(meter);
                typeof(PowerMeterModel).GetProperty("Power").SetValue(model, power);
            }

            public static void Set(object target, string name, object value)
            {
                target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
        }
    }
}
