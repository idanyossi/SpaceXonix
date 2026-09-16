using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class VolatileEnemyTests
    {
        [TestCase(typeof(BasicBouncer))]
        [TestCase(typeof(LinearEnemy))]
        [TestCase(typeof(UnstableEnemy))]
        public void ProtectedVolatile_ArmsThenDetonatesWithNormalEnemy(Type targetType)
        {
            using (var fixture = new Fixture())
            {
                var volatileEnemy = fixture.SpawnVolatile(new GridCoordinate(10, 10));
                var target = fixture.SpawnTarget(targetType, new GridCoordinate(11, 10));
                target.transform.position = volatileEnemy.transform.position;

                fixture.Manager.SimulateVolatileInteractions(.49f);
                Assert.That(volatileEnemy.IsActiveEnemy, Is.True);
                fixture.Manager.SimulateVolatileInteractions(.01f);

                Assert.That(volatileEnemy.IsActiveEnemy, Is.False);
                Assert.That(target.IsActiveEnemy, Is.False);
                Assert.That(fixture.Manager.ActiveEnemies, Is.Empty);
                Assert.That(fixture.Occupancy, Is.Empty);
            }
        }

        [Test]
        public void Explosion_FiresOnceDestroysNearEnemyAndLeavesDistantEnemy()
        {
            using (var fixture = new Fixture())
            {
                var volatileEnemy = fixture.SpawnVolatile(new GridCoordinate(10, 10));
                var near = fixture.SpawnTarget(typeof(BasicBouncer), new GridCoordinate(11, 10));
                var nearPrefab = fixture.LastTargetPrefab; var nearDefinition = fixture.LastTargetDefinition;
                var far = fixture.SpawnTarget(typeof(BasicBouncer), new GridCoordinate(20, 20));
                near.transform.position = volatileEnemy.transform.position;
                var explosions = 0;
                fixture.Manager.ExplosionOccurred += (_, __, ___) => explosions++;

                fixture.Manager.SimulateVolatileInteractions(.5f);
                fixture.Manager.SimulateVolatileInteractions(10f);

                Assert.That(explosions, Is.EqualTo(1));
                Assert.That(near.IsActiveEnemy, Is.False);
                Assert.That(far.IsActiveEnemy, Is.True);
                Assert.That(fixture.Manager.ActiveEnemies, Has.Exactly(1).SameAs(far));
                Assert.That(fixture.Occupancy, Has.Member(far.LogicalCell));
                Assert.That(fixture.Manager.Spawn(nearPrefab, nearDefinition, new GridCoordinate(25, 25), Vector2.up), Is.True);
                Assert.That(fixture.Manager.ActiveEnemies, Has.Member(near));
                Assert.That(near.LogicalCell, Is.EqualTo(new GridCoordinate(25, 25)));
            }
        }

        [Test]
        public void VolatilePoolReuse_ResetsProtectionDetonationPauseAndOccupancy()
        {
            using (var fixture = new Fixture())
            {
                var oldCell = new GridCoordinate(8, 8);
                var first = fixture.SpawnVolatile(oldCell);
                first.AdvanceSpawnProtection(.5f);
                first.SetMovementSuspended(true);
                Assert.That(first.BeginDetonation(), Is.True);
                fixture.Manager.Despawn(first);

                fixture.VolatileDefinition.volatileSpawnProtection = 2f;
                var newCell = new GridCoordinate(14, 14);
                Assert.That(fixture.Manager.Spawn(fixture.VolatilePrefab, fixture.VolatileDefinition, newCell, Vector2.up), Is.True);
                var reused = (VolatileEnemy)fixture.Manager.ActiveEnemies[0];

                Assert.That(reused, Is.SameAs(first));
                Assert.That(reused.HasDetonated, Is.False);
                Assert.That(reused.IsArmed, Is.False);
                Assert.That(reused.SpawnProtectionRemaining, Is.EqualTo(2f).Within(.0001f));
                Assert.That(reused.MovementEnabled, Is.True);
                Assert.That(reused.Velocity, Is.EqualTo(Vector2.up * fixture.VolatileDefinition.moveSpeed));
                Assert.That(reused.LogicalCell, Is.EqualTo(newCell));
                Assert.That(fixture.Occupancy, Has.Member(newCell));
                Assert.That(fixture.Occupancy, Has.No.Member(oldCell));
            }
        }

        [Test]
        public void ExplosionInsidePlayerRadius_UsesAuthoritativeFailureOnce()
        {
            using (var fixture = new Fixture(withGameManager: true))
            {
                var volatileEnemy = fixture.SpawnVolatile(new GridCoordinate(10, 10));
                var target = fixture.SpawnTarget(typeof(BasicBouncer), new GridCoordinate(11, 10));
                target.transform.position = volatileEnemy.transform.position;
                fixture.Player.transform.position = volatileEnemy.transform.position;
                PlayerFailureReason? reason = null;
                fixture.Game.PlayerFailed += value => reason = value;
                var before = fixture.Game.Lives;

                fixture.Manager.SimulateVolatileInteractions(.5f);
                var after = fixture.Game.Lives;
                fixture.Manager.SimulateVolatileInteractions(1f);

                Assert.That(reason, Is.EqualTo(PlayerFailureReason.VolatileExplosion));
                Assert.That(after, Is.EqualTo(before - 1));
                Assert.That(fixture.Game.Lives, Is.EqualTo(after));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
            }
        }

        [Test]
        public void ExplosionOutsidePlayerRadius_DoesNotRemoveLife()
        {
            using (var fixture = new Fixture(withGameManager: true))
            {
                var volatileEnemy = fixture.SpawnVolatile(new GridCoordinate(10, 10));
                var target = fixture.SpawnTarget(typeof(BasicBouncer), new GridCoordinate(11, 10));
                target.transform.position = volatileEnemy.transform.position;
                fixture.Player.transform.position = fixture.Board.GetWorldPosition(new GridCoordinate(40, 40));
                var before = fixture.Game.Lives;
                fixture.Manager.SimulateVolatileInteractions(.5f);
                Assert.That(fixture.Game.Lives, Is.EqualTo(before));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
            }
        }

        [Test]
        public void DirectPlayerContact_UsesEnemyContactOnceAndAbortsSameFrameDetonation()
        {
            using (var fixture = new Fixture(withGameManager: true))
            {
                var volatileEnemy = fixture.SpawnVolatile(new GridCoordinate(10, 10));
                var target = fixture.SpawnTarget(typeof(BasicBouncer), new GridCoordinate(11, 10));
                target.transform.position = volatileEnemy.transform.position;
                fixture.Player.transform.position = volatileEnemy.transform.position;
                var failures = 0;
                var respawns = 0;
                PlayerFailureReason? reason = null;
                fixture.Game.PlayerFailed += value => { failures++; reason = value; };
                fixture.Game.RespawnStarted += () => respawns++;
                var lives = fixture.Game.Lives;

                volatileEnemy.AdvanceMovement(0f);
                fixture.Manager.SimulateVolatileInteractions(.5f);

                Assert.That(reason, Is.EqualTo(PlayerFailureReason.EnemyContact));
                Assert.That(failures, Is.EqualTo(1));
                Assert.That(respawns, Is.EqualTo(1));
                Assert.That(fixture.Game.Lives, Is.EqualTo(lives - 1));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
                Assert.That(volatileEnemy.HasDetonated, Is.False);
                Assert.That(volatileEnemy.IsActiveEnemy, Is.True);
                Assert.That(target.IsActiveEnemy, Is.True);

                var respawnCell = fixture.Board.GetSafeRespawnCell();
                Assert.That(fixture.Board.IsValidRespawnCell(respawnCell), Is.True, respawnCell.ToString());
                Assert.That(fixture.CompleteRespawn(), Is.True);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeIdle));
                Assert.That(fixture.Board.IsValidRespawnCell(fixture.Board.PlayerCell), Is.True);
                fixture.Input.TrySelectDirection(fixture.Player.CurrentDirection);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("VolatileFixture");
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            public readonly BoardManager Board;
            public readonly EnemyManager Manager;
            public readonly PlayerController Player;
            public readonly GameManager Game;
            public readonly InputRouter Input;
            public readonly GameObject VolatilePrefab;
            public readonly EnemyDefinition VolatileDefinition;
            public GameObject LastTargetPrefab { get; private set; }
            public EnemyDefinition LastTargetDefinition { get; private set; }
            public List<GridCoordinate> Occupancy => (List<GridCoordinate>)GetField(Board, "enemySnapshot");

            public Fixture(bool withGameManager = false)
            {
                root.SetActive(false);
                Board = root.AddComponent<BoardManager>();
                Board.Initialize();
                var playerObject = new GameObject("Player"); owned.Add(playerObject);
                Player = playerObject.AddComponent<PlayerController>();
                Invoke(Player, "Awake");
                var pool = root.AddComponent<PoolService>();
                Game = withGameManager ? root.AddComponent<GameManager>() : null;
                Manager = root.AddComponent<EnemyManager>();
                SetField(Manager, "boardManager", Board); SetField(Manager, "playerController", Player);
                SetField(Manager, "poolService", pool); SetField(Manager, "gameManager", Game);
                if (Game != null)
                {
                    Input = root.AddComponent<InputRouter>();
                    SetField(Game, "inputRouter", Input); SetField(Game, "playerController", Player); SetField(Game, "boardManager", Board);
                }
                VolatilePrefab = Prefab<VolatileEnemy>("VolatilePrefab");
                VolatileDefinition = Definition(EnemyType.Volatile, 1f);
                VolatileDefinition.volatileSpawnProtection = .5f;
                VolatileDefinition.volatileCollisionRadius = .2f;
                VolatileDefinition.volatileBlastRadius = .4f;
                VolatileDefinition.volatileTerritoryRadiusCells = 2f;
                root.SetActive(true);
                Player.transform.position = Board.GetWorldPosition(new GridCoordinate(0, 1));
                if (Game != null) { Invoke(Game, "Awake"); Invoke(Game, "Start"); }
            }

            public bool CompleteRespawn()
            {
                var completed = (bool)InvokeResult(Game, "CompleteRespawn");
                if (!completed) return false;
                SetField(Game, "failureGateReleaseFrame", Time.frameCount - 1);
                Invoke(Game, "LateUpdate");
                return true;
            }

            public VolatileEnemy SpawnVolatile(GridCoordinate cell)
            {
                Assert.That(Manager.Spawn(VolatilePrefab, VolatileDefinition, cell, Vector2.right), Is.True);
                return (VolatileEnemy)Manager.ActiveEnemies[Manager.ActiveEnemies.Count - 1];
            }

            public EnemyController SpawnTarget(Type type, GridCoordinate cell)
            {
                var prefab = new GameObject(type.Name + "Prefab"); prefab.AddComponent(type); prefab.SetActive(false); owned.Add(prefab);
                var definition = Definition(type == typeof(LinearEnemy) ? EnemyType.Linear : type == typeof(UnstableEnemy) ? EnemyType.Unstable : EnemyType.BasicBouncer, 1f);
                LastTargetPrefab = prefab; LastTargetDefinition = definition;
                Assert.That(Manager.Spawn(prefab, definition, cell, Vector2.left), Is.True);
                return Manager.ActiveEnemies[Manager.ActiveEnemies.Count - 1];
            }

            private GameObject Prefab<T>(string name) where T : EnemyController
            {
                var prefab = new GameObject(name); prefab.AddComponent<T>(); prefab.SetActive(false); owned.Add(prefab); return prefab;
            }

            private EnemyDefinition Definition(EnemyType type, float speed)
            {
                var definition = ScriptableObject.CreateInstance<EnemyDefinition>(); definition.type = type; definition.moveSpeed = speed; owned.Add(definition); return definition;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private static object GetField(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        private static object InvokeResult(object target, string name) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    }
}
