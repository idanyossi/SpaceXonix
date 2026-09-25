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
        public void ProtectedVolatile_ArmsThenTurnsNormalEnemyIntoHybrid(Type targetType)
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
                Assert.That(target.IsActiveEnemy, Is.True, "caught aliens are no longer destroyed");
                Assert.That(target.IsHybrid, Is.True);
                Assert.That(target.GetType(), Is.EqualTo(targetType), "a hybrid keeps its own movement");
                Assert.That(fixture.Manager.ActiveEnemies, Has.Exactly(1).SameAs(target));
                Assert.That(fixture.Occupancy, Has.Member(target.LogicalCell));
            }
        }

        [Test]
        public void Explosion_FiresOnceConvertsNearEnemyAndLeavesDistantEnemy()
        {
            using (var fixture = new Fixture())
            {
                var volatileEnemy = fixture.SpawnVolatile(new GridCoordinate(10, 10));
                var near = fixture.SpawnTarget(typeof(BasicBouncer), new GridCoordinate(11, 10));
                var far = fixture.SpawnTarget(typeof(BasicBouncer), new GridCoordinate(20, 20));
                near.transform.position = volatileEnemy.transform.position;
                var explosions = 0;
                fixture.Manager.ExplosionOccurred += (_, __, ___) => explosions++;

                fixture.Manager.SimulateVolatileInteractions(.5f);
                fixture.Manager.SimulateVolatileInteractions(10f);

                Assert.That(explosions, Is.EqualTo(1), "the permanent border never sets a hybrid off");
                Assert.That(near.IsActiveEnemy, Is.True);
                Assert.That(near.IsHybrid, Is.True);
                Assert.That(far.IsHybrid, Is.False, "outside the blast");
                Assert.That(fixture.Manager.ActiveEnemies, Has.Count.EqualTo(2));
                Assert.That(fixture.Occupancy, Has.Member(far.LogicalCell));
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
        public void ExplosionDuringInvulnerability_PreservesOccupiedCapturedCellAndValidPlayerState()
        {
            using (var fixture = new Fixture(withGameManager: true))
            {
                for (var x = 1; x < fixture.Board.Columns - 1; x++)
                    fixture.Board.Model.MoveTo(new GridCoordinate(x, 48));
                fixture.Board.Model.MoveTo(new GridCoordinate(fixture.Board.Columns - 1, 48));
                var playerCell = new GridCoordinate(10, 70);
                Assert.That(fixture.Player.RestoreSafeManualState(playerCell, CardinalDirection.Right), Is.True);
                var volatileEnemy = fixture.SpawnVolatile(new GridCoordinate(10, 10));
                volatileEnemy.Activate(fixture.VolatileDefinition, fixture.Board, fixture.Game,
                    fixture.Board.GetWorldPosition(playerCell), Vector2.right);
                volatileEnemy.AdvanceSpawnProtection(.5f);
                SetField(fixture.Game, "invulnerabilityRemaining", 2f);
                var livesBefore = fixture.Game.Lives;

                Assert.That(fixture.Manager.ResolveVolatileExplosion(volatileEnemy), Is.True);

                Assert.That(fixture.Game.Lives, Is.EqualTo(livesBefore));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(playerCell));
                Assert.That(fixture.Board.Model.GetCell(playerCell), Is.EqualTo(BoardCellState.Captured));
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeIdle));
                Assert.That(fixture.Player.HasValidPlayingState(), Is.True);
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

        [Test]
        public void Hybrid_BlowsAHoleInBuiltTerritoryButIgnoresTheBorder()
        {
            using (var fixture = new Fixture())
            {
                fixture.CaptureColumn(30);
                Assert.That(fixture.Board.Model.GetCell(new GridCoordinate(40, 20)), Is.EqualTo(BoardCellState.Captured));

                var volatileEnemy = fixture.SpawnVolatile(new GridCoordinate(10, 10));
                var hybrid = fixture.SpawnTarget(typeof(BasicBouncer), new GridCoordinate(11, 10));
                var prefab = fixture.LastTargetPrefab; var definition = fixture.LastTargetDefinition;
                var bystander = fixture.SpawnTarget(typeof(BasicBouncer), new GridCoordinate(20, 40));
                hybrid.transform.position = volatileEnemy.transform.position;
                var explosions = 0;
                fixture.Manager.ExplosionOccurred += (_, __, ___) => explosions++;
                fixture.Manager.SimulateVolatileInteractions(.5f);
                Assert.That(hybrid.IsHybrid, Is.True);

                // Right against the permanent border, armed: nothing happens.
                hybrid.Relocate(fixture.Board.GetWorldPosition(new GridCoordinate(1, 20)));
                fixture.Manager.SimulateVolatileInteractions(1f);
                Assert.That(hybrid.IsActiveEnemy, Is.True);
                Assert.That(explosions, Is.EqualTo(1));

                // Touching territory the player built: it goes off and takes a bite out of it.
                var edge = fixture.Board.GetWorldPosition(new GridCoordinate(29, 20));
                hybrid.Relocate(edge + Vector3.right * fixture.Board.CellWorldSize * .5f);
                fixture.Manager.SimulateVolatileInteractions(.01f);
                Assert.That(explosions, Is.EqualTo(2));
                Assert.That(hybrid.IsActiveEnemy, Is.False, "the charge is used up");
                Assert.That(fixture.Board.Model.GetCell(new GridCoordinate(30, 20)), Is.EqualTo(BoardCellState.Uncaptured));
                Assert.That(fixture.Board.Model.GetCell(new GridCoordinate(32, 20)), Is.EqualTo(BoardCellState.Uncaptured),
                    "the hole is centred on the territory it hit, so it reaches the full radius in, not just the edge");
                Assert.That(fixture.Board.Model.GetCell(new GridCoordinate(33, 20)), Is.EqualTo(BoardCellState.Captured), "and no further");
                Assert.That(bystander.IsActiveEnemy && !bystander.IsHybrid, Is.True, "hybrid blasts never touch other aliens");

                // Pooled aliens come back as ordinary ones.
                Assert.That(fixture.Manager.Spawn(prefab, definition, new GridCoordinate(12, 12), Vector2.up), Is.True);
                Assert.That(hybrid.IsActiveEnemy, Is.True);
                Assert.That(hybrid.IsHybrid, Is.False);
            }
        }

        [Test]
        public void Volatile_IsAbsorbedByAHybrid_DoublingItsChargeUpToTheCap()
        {
            using (var fixture = new Fixture())
            {
                fixture.CaptureColumn(30);
                var first = fixture.SpawnVolatile(new GridCoordinate(10, 10));
                var hybrid = fixture.SpawnTarget(typeof(BasicBouncer), new GridCoordinate(11, 10));
                hybrid.transform.position = first.transform.position;
                fixture.Manager.SimulateVolatileInteractions(.5f);
                Assert.That(hybrid.IsHybrid, Is.True);
                Assert.That(hybrid.HybridCharge, Is.EqualTo(1f));
                var supercharged = 0;
                fixture.Manager.HybridSupercharged += _ => supercharged++;

                foreach (var expected in new[] { 2f, 4f })
                {
                    var volatileEnemy = fixture.SpawnVolatile(new GridCoordinate(20, 20));
                    fixture.Manager.SimulateVolatileInteractions(.5f);
                    volatileEnemy.transform.position = hybrid.transform.position;
                    fixture.Manager.SimulateVolatileInteractions(.01f);
                    Assert.That(volatileEnemy.IsActiveEnemy, Is.False, "the Volatile is absorbed");
                    Assert.That(volatileEnemy.HasDetonated, Is.False, "absorbed, not exploded");
                    Assert.That(hybrid.HybridCharge, Is.EqualTo(expected));
                }
                Assert.That(supercharged, Is.EqualTo(2));

                var extra = fixture.SpawnVolatile(new GridCoordinate(20, 20));
                fixture.Manager.SimulateVolatileInteractions(.5f);
                extra.transform.position = hybrid.transform.position;
                fixture.Manager.SimulateVolatileInteractions(.01f);
                Assert.That(extra.IsActiveEnemy, Is.True, "at the cap, a Volatile just passes");
                Assert.That(hybrid.HybridCharge, Is.EqualTo(4f));
                fixture.Manager.Despawn(extra);

                // A x4 charge blows a hole four times the radius: 8 cells instead of 2.
                var edge = fixture.Board.GetWorldPosition(new GridCoordinate(29, 40));
                hybrid.Relocate(edge + Vector3.right * fixture.Board.CellWorldSize * .5f);
                fixture.Manager.SimulateVolatileInteractions(.01f);
                Assert.That(hybrid.IsActiveEnemy, Is.False);
                Assert.That(fixture.Board.Model.GetCell(new GridCoordinate(38, 40)), Is.EqualTo(BoardCellState.Uncaptured));
                Assert.That(fixture.Board.Model.GetCell(new GridCoordinate(39, 40)), Is.EqualTo(BoardCellState.Captured));
                Assert.That(fixture.Manager.LastExplosionScale, Is.EqualTo(4f), "the ring and shake grow with it");
            }
        }

        [Test]
        public void EachHybrid_BringsARegularReinforcementAwayFromTheShip()
        {
            using (var fixture = new Fixture())
            {
                var reinforcement = fixture.UseReinforcement(typeof(UnstableEnemy));
                var volatileEnemy = fixture.SpawnVolatile(new GridCoordinate(20, 40));
                var a = fixture.SpawnTarget(typeof(BasicBouncer), new GridCoordinate(21, 40));
                var b = fixture.SpawnTarget(typeof(LinearEnemy), new GridCoordinate(19, 40));
                a.transform.position = volatileEnemy.transform.position;
                b.transform.position = volatileEnemy.transform.position;

                fixture.Manager.SimulateVolatileInteractions(.5f);

                Assert.That(a.IsHybrid && b.IsHybrid, Is.True);
                var fresh = new List<EnemyController>();
                foreach (var enemy in fixture.Manager.ActiveEnemies) if (enemy != a && enemy != b) fresh.Add(enemy);
                Assert.That(fresh, Has.Count.EqualTo(2), "one regular alien per hybrid, so the Volatile never thins the stage");
                var ship = fixture.Board.WorldToGrid(fixture.Player.transform.position);
                foreach (var enemy in fresh)
                {
                    Assert.That(enemy, Is.InstanceOf<UnstableEnemy>());
                    Assert.That(enemy.IsHybrid, Is.False);
                    Assert.That(enemy.Definition, Is.SameAs(reinforcement));
                    var dx = enemy.LogicalCell.X - ship.X; var dy = enemy.LogicalCell.Y - ship.Y;
                    Assert.That(dx * dx + dy * dy, Is.GreaterThanOrEqualTo(100), "not dropped on the ship");
                }
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

            /// <summary>Cuts a full column and reconnects, a real capture that fills the smaller side.</summary>
            public void CaptureColumn(int column)
            {
                for (var row = 1; row < Board.Rows - 1; row++) Board.Model.MoveTo(new GridCoordinate(column, row));
                Board.Model.MoveTo(new GridCoordinate(column, Board.Rows - 1));
            }

            /// <summary>Configures the regular alien that arrives for each hybrid.</summary>
            public EnemyDefinition UseReinforcement(Type type)
            {
                var prefab = new GameObject(type.Name + "Reinforcement"); prefab.AddComponent(type); prefab.SetActive(false); owned.Add(prefab);
                var definition = Definition(type == typeof(UnstableEnemy) ? EnemyType.Unstable : EnemyType.BasicBouncer, 1f);
                SetField(Manager, "reinforcements", new[] { new EnemySpawnRequest { prefab = prefab, definition = definition } });
                SetField(Manager, "randomSeed", 99);
                return definition;
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
