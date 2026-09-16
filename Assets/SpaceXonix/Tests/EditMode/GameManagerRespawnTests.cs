using System;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Enemies;
using SpaceXonix.Pooling;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class GameManagerRespawnTests
    {
        [Test]
        public void FailureLifecycle_ClearsTrailRespawnsAtSafeCellAndRestoresControl()
        {
            using (var fixture = new Fixture())
            {
                var safeCell = new GridCoordinate(0, 8);
                fixture.Board.ResetPlayerTracking(fixture.Board.GetWorldPosition(safeCell));
                fixture.Board.Model.MoveTo(new GridCoordinate(1, 8));
                fixture.Player.transform.position = fixture.Board.GetWorldPosition(new GridCoordinate(10, 10));

                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Input.GameplayInputEnabled, Is.False);
                Assert.That(fixture.Player.MovementEnabled, Is.False);

                Assert.That(fixture.CompleteRespawn(), Is.True);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Board.WorldToGrid(fixture.Player.transform.position), Is.EqualTo(safeCell));
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
                Assert.That(fixture.Player.CurrentDirection, Is.EqualTo(CardinalDirection.Right));
                Assert.That(fixture.Input.CurrentDirection, Is.EqualTo(CardinalDirection.Right));
                Assert.That(fixture.Input.GameplayInputEnabled, Is.True);
                Assert.That(fixture.Player.MovementEnabled, Is.True);

                var respawnPosition = fixture.Player.transform.position;
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                Assert.That(fixture.Player.transform.position, Is.Not.EqualTo(respawnPosition));
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(fixture.Board.WorldToGrid(fixture.Player.transform.position)));
            }
        }

        [Test]
        public void RespawnOnRightEdge_SelectsInBoundsDirectionAndMovesOnNextStep()
        {
            using (var fixture = new Fixture())
            {
                var safeCell = new GridCoordinate(fixture.Board.Columns - 1, 5);
                fixture.Board.ResetPlayerTracking(fixture.Board.GetWorldPosition(safeCell));
                fixture.Game.ReportPlayerFailure(PlayerFailureReason.Laser);
                fixture.CompleteRespawn();
                var respawnPosition = fixture.Player.transform.position;

                Assert.That(fixture.Board.WorldToGrid(respawnPosition), Is.EqualTo(safeCell));
                Assert.That(fixture.Player.CurrentDirection, Is.Not.EqualTo(CardinalDirection.Right));
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                Assert.That(fixture.Player.transform.position, Is.Not.EqualTo(respawnPosition));
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(fixture.Board.WorldToGrid(fixture.Player.transform.position)));
            }
        }

        [Test]
        public void InvalidLastSafeCell_UsesDeterministicSafeFallback()
        {
            using (var fixture = new Fixture())
            {
                Fixture.Set(fixture.Board, "lastSafeCell", new GridCoordinate(2, 2));
                Fixture.Set(fixture.Board, "hasLastSafeCell", true);

                Assert.That(fixture.Board.GetSafeRespawnCell(), Is.EqualTo(new GridCoordinate(0, 1)));
                Assert.That(fixture.Board.Model.GetCell(fixture.Board.GetSafeRespawnCell()), Is.EqualTo(BoardCellState.Captured));
            }
        }

        [Test]
        public void EnemyMovingIntoActiveTrail_ReportsFailureBeforeVelocityReflection()
        {
            using (var fixture = new Fixture())
            {
                var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                var enemyObject = new GameObject("TrailHitEnemy");
                var enemy = enemyObject.AddComponent<BasicBouncer>();
                try
                {
                    definition.moveSpeed = 1.2f;
                    fixture.Board.Model.MoveTo(new GridCoordinate(1, 3));
                    enemy.Activate(
                        definition,
                        fixture.Board,
                        fixture.Game,
                        fixture.Board.GetWorldPosition(new GridCoordinate(2, 3)),
                        Vector2.left);

                    enemy.AdvanceMovement(.2f);

                    Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                    Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
                    Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                    Assert.That(fixture.Board.IsPlayerExposed, Is.False);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(enemyObject);
                    UnityEngine.Object.DestroyImmediate(definition);
                }
            }
        }

        [Test]
        public void EnemyCrossingIntermediateTrailCell_ReportsTrailHit()
        {
            using (var fixture = new Fixture())
            using (var enemy = new EnemyFixture<BasicBouncer>(fixture, new GridCoordinate(4, 3), Vector2.left, 3.6f))
            {
                fixture.Board.Model.MoveTo(new GridCoordinate(3, 3));

                enemy.Controller.AdvanceMovement(.15f);

                Assert.That(enemy.Controller.LastTraversedCells, Is.EqualTo(new[]
                {
                    new GridCoordinate(4, 3), new GridCoordinate(3, 3),
                    new GridCoordinate(2, 3), new GridCoordinate(1, 3)
                }));
                Assert.That(enemy.Controller.LastTrailHitAccepted, Is.True);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
            }
        }

        [Test]
        public void EnemyPassingAdjacentToTrail_DoesNotReportFailure()
        {
            using (var fixture = new Fixture())
            using (var enemy = new EnemyFixture<BasicBouncer>(fixture, new GridCoordinate(4, 4), Vector2.left, 3.6f))
            {
                fixture.Board.Model.MoveTo(new GridCoordinate(3, 3));

                enemy.Controller.AdvanceMovement(.15f);

                Assert.That(enemy.Controller.LastTraversedCells, Has.No.Member(new GridCoordinate(3, 3)));
                Assert.That(enemy.Controller.LastTrailHitAccepted, Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(3));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Board.Model.ActiveTrail.Count, Is.EqualTo(1));
            }
        }

        [TestCase(typeof(BasicBouncer))]
        [TestCase(typeof(LinearEnemy))]
        [TestCase(typeof(UnstableEnemy))]
        public void StandardEnemyTypes_UseSharedTrailTraversal(Type enemyType)
        {
            using (var fixture = new Fixture())
            using (var enemy = new EnemyFixture(fixture, enemyType, new GridCoordinate(4, 3), Vector2.left, 3.6f))
            {
                fixture.Board.Model.MoveTo(new GridCoordinate(3, 3));

                enemy.Controller.AdvanceMovement(.15f);

                Assert.That(enemy.Controller.LastTrailHitAccepted, Is.True);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
            }
        }

        [Test]
        public void ReusedEnemy_CanReportOneTrailHitAgain()
        {
            using (var fixture = new Fixture(startingLives: 3))
            using (var enemy = new EnemyFixture<BasicBouncer>(fixture, new GridCoordinate(2, 3), Vector2.left, 1.2f))
            {
                fixture.Board.Model.MoveTo(new GridCoordinate(1, 3));
                enemy.Controller.AdvanceMovement(.2f);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));

                fixture.CompleteRespawn();
                enemy.Controller.Deactivate();
                enemy.Activate(new GridCoordinate(2, 4), Vector2.left, 1.2f);
                fixture.Board.Model.MoveTo(new GridCoordinate(1, 4));
                enemy.Controller.AdvanceMovement(.2f);

                Assert.That(enemy.Controller.LastTrailHitAccepted, Is.True);
                Assert.That(fixture.Game.Lives, Is.EqualTo(1));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
            }
        }

        [Test]
        public void PooledEnemy_ReportsTrailHitAfterReleaseAndReacquire()
        {
            using (var fixture = new Fixture(startingLives: 3))
            {
                var poolHost = new GameObject("TrailHitPool");
                var prefab = new GameObject("TrailHitPrefab");
                prefab.AddComponent<BasicBouncer>();
                prefab.SetActive(false);
                var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                definition.moveSpeed = 1.2f;
                try
                {
                    var pool = poolHost.AddComponent<PoolService>();
                    var firstObject = pool.Acquire(prefab, poolHost.transform);
                    var first = firstObject.GetComponent<BasicBouncer>();
                    fixture.Board.Model.MoveTo(new GridCoordinate(1, 3));
                    first.Activate(definition, fixture.Board, fixture.Game, fixture.Board.GetWorldPosition(new GridCoordinate(2, 3)), Vector2.left);
                    first.AdvanceMovement(.2f);
                    Assert.That(first.LastTrailHitAccepted, Is.True);
                    fixture.CompleteRespawn();

                    first.Deactivate();
                    pool.Release(prefab, firstObject);
                    var reusedObject = pool.Acquire(prefab, poolHost.transform);
                    var reused = reusedObject.GetComponent<BasicBouncer>();
                    fixture.Board.Model.MoveTo(new GridCoordinate(1, 4));
                    reused.Activate(definition, fixture.Board, fixture.Game, fixture.Board.GetWorldPosition(new GridCoordinate(2, 4)), Vector2.left);
                    reused.AdvanceMovement(.2f);

                    Assert.That(reusedObject, Is.SameAs(firstObject));
                    Assert.That(reused.LastTrailHitAccepted, Is.True);
                    Assert.That(fixture.Game.Lives, Is.EqualTo(1));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(poolHost);
                    UnityEngine.Object.DestroyImmediate(prefab);
                    UnityEngine.Object.DestroyImmediate(definition);
                }
            }
        }

        [Test]
        public void DuplicateTrailHitDuringRespawn_DoesNotConsumeAnotherLife()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.TrailHit), Is.True);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.TrailHit), Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
            }
        }

        [Test]
        public void MultiCellSelfIntersection_StopsTrackingBeforeASecondTrailCanStart()
        {
            using (var fixture = new Fixture())
            {
                fixture.Board.Model.MoveTo(new GridCoordinate(1, 3));
                fixture.Board.Model.MoveTo(new GridCoordinate(2, 3));
                var from = fixture.Board.GetWorldPosition(new GridCoordinate(3, 3));
                var target = fixture.Board.GetWorldPosition(new GridCoordinate(1, 3));
                fixture.Board.ResetPlayerTracking(from);

                var result = fixture.Board.TrackPlayerWorldPosition(from, target);

                Assert.That(result, Is.EqualTo(BoardMoveResult.TrailFailed));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);
            }
        }

        [Test]
        public void RepeatedRespawns_ClearStaleStateAndAlwaysProduceAMovementStep()
        {
            using (var fixture = new Fixture(startingLives: 21))
            {
                for (var index = 0; index < 20; index++)
                {
                    var safeCell = index % 4 == 0 ? new GridCoordinate(0, 0) :
                        index % 4 == 1 ? new GridCoordinate(fixture.Board.Columns - 1, 0) :
                        index % 4 == 2 ? new GridCoordinate(fixture.Board.Columns - 1, fixture.Board.Rows - 1) :
                        new GridCoordinate(0, fixture.Board.Rows - 1);
                    fixture.Player.RespawnAt(fixture.Board.GetWorldPosition(safeCell), CardinalDirection.Left);
                    fixture.Board.ResetPlayerTracking(fixture.Board.GetWorldPosition(safeCell));

                    Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True, $"failure {index}");
                    Assert.That(fixture.CompleteRespawn(), Is.True, $"respawn {index}");
                    Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty, $"trail {index}");
                    Assert.That(fixture.Board.IsLegalPlayerStep(NextCell(fixture.Board.PlayerCell, fixture.Player.CurrentDirection)), Is.True, $"direction {index}");
                    Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True, $"movement {index}");
                    Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position), $"sync {index}");
                }

                Assert.That(fixture.Game.Lives, Is.EqualTo(1));
            }
        }

        private static GridCoordinate NextCell(GridCoordinate cell, CardinalDirection direction)
        {
            var offset = direction.ToVector2();
            return new GridCoordinate(cell.X + (int)offset.x, cell.Y + (int)offset.y);
        }

        [Test]
        public void NormalDeath_DoesNotEraseCapturedTerritory()
        {
            using (var fixture = new Fixture())
            {
                fixture.Board.Model.MoveTo(new GridCoordinate(1, 1));
                fixture.Board.Model.MoveTo(new GridCoordinate(2, 1));
                fixture.Board.Model.MoveTo(new GridCoordinate(3, 1));
                fixture.Board.Model.MoveTo(new GridCoordinate(4, 1));
                fixture.Board.Model.MoveTo(new GridCoordinate(5, 1));
                var capturedBefore = fixture.Board.CapturedPercentage;

                fixture.Game.ReportPlayerFailure(PlayerFailureReason.TrailHit);
                fixture.CompleteRespawn();

                Assert.That(capturedBefore, Is.GreaterThan(0f));
                Assert.That(fixture.Board.CapturedPercentage, Is.EqualTo(capturedBefore));
            }
        }

        [Test]
        public void EveryFailureReason_IsIgnoredDuringRespawning()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
                foreach (PlayerFailureReason reason in Enum.GetValues(typeof(PlayerFailureReason)))
                {
                    Assert.That(fixture.Game.ReportPlayerFailure(reason), Is.False, reason.ToString());
                }

                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
            }
        }

        [Test]
        public void LaterFailureAfterCompletedRespawn_ConsumesExactlyOneMoreLife()
        {
            using (var fixture = new Fixture())
            {
                fixture.Game.ReportPlayerFailure(PlayerFailureReason.Laser);
                fixture.CompleteRespawn();
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.VolatileExplosion), Is.True);

                Assert.That(fixture.Game.Lives, Is.EqualTo(1));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
            }
        }

        [Test]
        public void FinalLife_EntersGameOverWithoutRespawnAndRejectsDuplicates()
        {
            using (var fixture = new Fixture(startingLives: 1))
            {
                var positionBefore = fixture.Player.transform.position;
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.Laser), Is.True);

                Assert.That(fixture.Game.Lives, Is.Zero);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.GameOver));
                Assert.That(fixture.Input.GameplayInputEnabled, Is.False);
                Assert.That(fixture.Player.MovementEnabled, Is.False);
                Assert.That(fixture.CompleteRespawn(), Is.False);
                Assert.That(fixture.Player.transform.position, Is.EqualTo(positionBefore));
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.False);
                Assert.That(fixture.Game.Lives, Is.Zero);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("RespawnFixture");
            private readonly GameObject playerObject = new GameObject("Player");
            public readonly BoardManager Board;
            public readonly InputRouter Input;
            public readonly PlayerController Player;
            public readonly GameManager Game;

            public Fixture(int startingLives = 3)
            {
                root.SetActive(false);
                Board = root.AddComponent<BoardManager>();
                Set(Board, "columns", 6);
                Set(Board, "rows", 10);
                Board.Initialize();
                Input = root.AddComponent<InputRouter>();
                Player = playerObject.AddComponent<PlayerController>();
                Invoke(Player, "Awake");
                Game = root.AddComponent<GameManager>();
                Set(Game, "inputRouter", Input);
                Set(Game, "playerController", Player);
                Set(Game, "boardManager", Board);
                Set(Game, "startingLives", startingLives);
                Invoke(Game, "Awake");
                root.SetActive(true);
                Invoke(Game, "Start");
            }

            public bool CompleteRespawn()
            {
                return (bool)Invoke(Game, "CompleteRespawn");
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(playerObject);
            }

            public static void Set(object target, string name, object value)
            {
                target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
            }

            private static object Invoke(object target, string name)
            {
                return target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
            }
        }

        private class EnemyFixture : IDisposable
        {
            private readonly GameObject enemyObject;
            private readonly EnemyDefinition definition;
            private readonly Fixture fixture;
            public EnemyController Controller { get; }

            public EnemyFixture(Fixture fixture, Type enemyType, GridCoordinate cell, Vector2 direction, float speed)
            {
                this.fixture = fixture;
                enemyObject = new GameObject(enemyType.Name);
                Controller = (EnemyController)enemyObject.AddComponent(enemyType);
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                Activate(cell, direction, speed);
            }

            public void Activate(GridCoordinate cell, Vector2 direction, float speed)
            {
                definition.moveSpeed = speed;
                definition.linearAxis = Mathf.Abs(direction.x) > 0f ? EnemyAxis.Horizontal : EnemyAxis.Vertical;
                Controller.Activate(definition, fixture.Board, fixture.Game, fixture.Board.GetWorldPosition(cell), direction);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private sealed class EnemyFixture<T> : EnemyFixture where T : EnemyController
        {
            public EnemyFixture(Fixture fixture, GridCoordinate cell, Vector2 direction, float speed)
                : base(fixture, typeof(T), cell, direction, speed) { }
        }
    }
}
