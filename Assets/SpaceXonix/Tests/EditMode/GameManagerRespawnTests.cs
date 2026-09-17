using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Enemies;
using SpaceXonix.Pooling;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class GameManagerRespawnTests
    {
        [Test]
        public void GameManager_KeepsRespawnLifecycleRunningWhenWindowLosesFocus()
        {
            var previous = Application.runInBackground;
            try
            {
                Application.runInBackground = false;
                using (var fixture = new Fixture())
                {
                    Assert.That(Application.runInBackground, Is.True);
                    Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
                    Assert.That(fixture.CompleteRespawn(), Is.True);
                    Assert.That(fixture.Player.HasValidPlayingState(), Is.True);
                }
            }
            finally
            {
                Application.runInBackground = previous;
            }
        }

        [Test]
        public void Startup_WaitsForFirstDirectionCommandWithoutCreatingTrailOrCapture()
        {
            using (var fixture = new Fixture())
            {
                var start = fixture.Player.transform.position;

                Assert.That(fixture.Player.AdvanceMovement(.1f), Is.False);
                Assert.That(fixture.Player.transform.position, Is.EqualTo(start));
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(new GridCoordinate(0, 1)));
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);
                Assert.That(fixture.Board.CapturedPercentage, Is.Zero);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeIdle));

                Assert.That(fixture.Input.TrySelectDirection(CardinalDirection.Right), Is.True);
                Assert.That(fixture.Player.PendingDirection, Is.EqualTo(CardinalDirection.Right));
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeMoving));
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
            }
        }

        [Test]
        public void SafeTerritory_HeldDirectionContinuesUntilReleased()
        {
            using (var fixture = new Fixture())
            {
                fixture.Input.TrySelectDirection(CardinalDirection.Up);

                Assert.That(fixture.Player.AdvanceMovement(.04f), Is.True);
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(new GridCoordinate(0, 2)));
                Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.False);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeMoving));
                Assert.That(fixture.Player.AdvanceMovement(.04f), Is.True);
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(new GridCoordinate(0, 3)));

                fixture.Input.ReleaseDirection();
                Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.True);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeIdle));
                var stoppedPosition = fixture.Player.transform.position;

                Assert.That(fixture.Player.AdvanceMovement(.2f), Is.False);
                Assert.That(fixture.Player.transform.position, Is.EqualTo(stoppedPosition));
            }
        }

        [TestCase(CardinalDirection.Left, CardinalDirection.Right, 3, 0)]
        [TestCase(CardinalDirection.Right, CardinalDirection.Left, 2, 0)]
        [TestCase(CardinalDirection.Up, CardinalDirection.Down, 0, 4)]
        public void SafeTerritory_OppositeDirectionReversesNormally(
            CardinalDirection first, CardinalDirection opposite, int x, int y)
        {
            using (var fixture = new Fixture())
            {
                fixture.Player.RespawnAt(fixture.Board.GetWorldPosition(new GridCoordinate(x, y)), first);
                fixture.Input.TrySelectDirection(first);
                Assert.That(fixture.Player.AdvanceMovement(.005f), Is.True);
                var beforeReverse = fixture.Player.transform.position;

                fixture.Input.TrySelectDirection(opposite);

                Assert.That(fixture.Player.AdvanceMovement(.005f), Is.True);
                Assert.That(fixture.Player.CurrentDirection, Is.EqualTo(opposite));
                Assert.That(fixture.Game.Lives, Is.EqualTo(3));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
                Assert.That(fixture.Player.transform.position, Is.Not.EqualTo(beforeReverse));
            }
        }

        [TestCase(CardinalDirection.Left, CardinalDirection.Right, 3, 0)]
        [TestCase(CardinalDirection.Down, CardinalDirection.Up, 0, 4)]
        public void SafeTerritory_RapidOppositeAlternationNeverStallsOrDesynchronizes(
            CardinalDirection first, CardinalDirection second, int x, int y)
        {
            using (var fixture = new Fixture())
            {
                fixture.Player.RespawnAt(fixture.Board.GetWorldPosition(new GridCoordinate(x, y)), first);
                for (var index = 0; index < 100; index++)
                {
                    var direction = index % 2 == 0 ? first : second;
                    fixture.Input.TrySelectDirection(direction);
                    Assert.That(fixture.Player.AdvanceMovement(.001f), Is.True, $"movement {index}");
                    Assert.That(fixture.Player.CurrentDirection, Is.EqualTo(direction), $"direction {index}");
                    Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position), $"sync {index}");
                    Assert.That(fixture.Board.PlayerCell, Is.EqualTo(fixture.Board.WorldToGrid(fixture.Player.transform.position)), $"cell {index}");
                    Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing), $"state {index}");
                }

                var beforeProofStep = fixture.Player.transform.position;
                fixture.Input.TrySelectDirection(second);
                Assert.That(fixture.Player.AdvanceMovement(.005f), Is.True);
                Assert.That(fixture.Player.transform.position, Is.Not.EqualTo(beforeProofStep));
                Assert.That(fixture.Game.Lives, Is.EqualTo(3));
            }
        }

        [Test]
        public void EnteringUncapturedTerritory_ContinuesWithoutAdditionalInput()
        {
            using (var fixture = new Fixture())
            {
                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                Assert.That(fixture.Player.AdvanceMovement(.04f), Is.True);
                Assert.That(fixture.Board.IsPlayerExposed, Is.True);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.ExposedMoving));
                fixture.Input.ReleaseDirection();
                var exposedPosition = fixture.Player.transform.position;

                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                Assert.That(fixture.Player.transform.position.x, Is.GreaterThan(exposedPosition.x));
                Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.False);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.ExposedMoving));
            }
        }

        [Test]
        public void DirectionChangeWhileExposed_PersistsUntilReconnect()
        {
            using (var fixture = new Fixture())
            {
                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Input.TrySelectDirection(CardinalDirection.Up);
                fixture.Player.AdvanceMovement(.04f);
                var afterTurn = fixture.Player.transform.position;

                Assert.That(fixture.Board.IsPlayerExposed, Is.True);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                Assert.That(fixture.Player.CurrentDirection, Is.EqualTo(CardinalDirection.Up));
                Assert.That(fixture.Player.transform.position.y, Is.GreaterThan(afterTurn.y));
            }
        }

        [Test]
        public void Reconnect_StopsAtCaptureCellUntilFreshInput()
        {
            using (var fixture = new Fixture())
            {
                fixture.Board.SetEnemyCells(new[] { new GridCoordinate(4, 5) });
                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Input.TrySelectDirection(CardinalDirection.Up);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Input.TrySelectDirection(CardinalDirection.Left);
                fixture.Input.ReleaseDirection();

                Assert.That(fixture.Player.AdvanceMovement(.04f), Is.True);
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Game.Lives, Is.EqualTo(3));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Board.CapturedPercentage, Is.GreaterThan(0f));
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(new GridCoordinate(0, 2)));
                Assert.That(fixture.Board.GetSafeRespawnCell(), Is.EqualTo(fixture.Board.PlayerCell));
                Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.True);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeIdle));
                var capturePosition = fixture.Player.transform.position;

                Assert.That(fixture.Player.AdvanceMovement(.2f), Is.False);
                Assert.That(fixture.Player.transform.position, Is.EqualTo(capturePosition));
            }
        }

        [Test]
        public void TwoCaptures_DoNotLeakPersistentDirectionBetweenThem()
        {
            using (var fixture = new Fixture())
            {
                fixture.Board.SetEnemyCells(new[] { new GridCoordinate(4, 5) });
                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Input.TrySelectDirection(CardinalDirection.Up);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Input.TrySelectDirection(CardinalDirection.Left);
                fixture.Input.ReleaseDirection();
                fixture.Player.AdvanceMovement(.04f);
                Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.True);

                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Player.AdvanceMovement(.04f);
                Assert.That(fixture.Board.IsPlayerExposed, Is.True);
                fixture.Input.TrySelectDirection(CardinalDirection.Down);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Input.TrySelectDirection(CardinalDirection.Left);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Input.ReleaseDirection();
                fixture.Player.AdvanceMovement(.04f);

                Assert.That(fixture.Board.IsPlayerExposed, Is.False);
                Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.True);
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(new GridCoordinate(1, 1)));
                Assert.That(fixture.Player.AdvanceMovement(.2f), Is.False);
            }
        }

        [Test]
        public void MultipleInputsBeforeMovement_KeepLatestLegalDirection()
        {
            using (var fixture = new Fixture())
            {
                var cell = new GridCoordinate(0, 5);
                fixture.Player.RespawnAt(fixture.Board.GetWorldPosition(cell), CardinalDirection.Right);

                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                fixture.Input.TrySelectDirection(CardinalDirection.Up);
                fixture.Input.TrySelectDirection(CardinalDirection.Left);

                Assert.That(fixture.Input.CurrentDirection, Is.EqualTo(CardinalDirection.Left));
                Assert.That(fixture.Player.PendingDirection, Is.EqualTo(CardinalDirection.Up));
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                Assert.That(fixture.Player.CurrentDirection, Is.EqualTo(CardinalDirection.Up));
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
            }
        }

        [Test]
        public void InvalidEdgeDirectionFollowedByValidDirection_DoesNotStall()
        {
            using (var fixture = new Fixture())
            {
                var cell = new GridCoordinate(0, 5);
                fixture.Player.RespawnAt(fixture.Board.GetWorldPosition(cell), CardinalDirection.Up);
                var before = fixture.Player.transform.position;

                fixture.Input.TrySelectDirection(CardinalDirection.Left);
                fixture.Input.TrySelectDirection(CardinalDirection.Right);

                Assert.That(fixture.Player.PendingDirection, Is.EqualTo(CardinalDirection.Right));
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                Assert.That(fixture.Player.transform.position.x, Is.GreaterThan(before.x));
            }
        }

        [Test]
        public void RapidDirectionChangesAfterRespawn_StaySynchronizedAndMove()
        {
            using (var fixture = new Fixture())
            {
                fixture.Game.ReportPlayerFailure(PlayerFailureReason.Laser);
                fixture.CompleteRespawn();
                fixture.Input.TrySelectDirection(CardinalDirection.Up);
                fixture.Input.TrySelectDirection(CardinalDirection.Down);
                fixture.Input.TrySelectDirection(CardinalDirection.Right);

                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                Assert.That(fixture.Player.CurrentDirection, Is.EqualTo(CardinalDirection.Right));
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(fixture.Board.WorldToGrid(fixture.Player.transform.position)));
            }
        }

        [Test]
        public void RapidDirectionChangesWhileExposed_ContinueWithoutDiagonalState()
        {
            using (var fixture = new Fixture())
            {
                var cell = new GridCoordinate(0, 4);
                fixture.Player.RespawnAt(fixture.Board.GetWorldPosition(cell), CardinalDirection.Right);
                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                fixture.Player.AdvanceMovement(.04f);
                Assert.That(fixture.Board.IsPlayerExposed, Is.True);
                fixture.Input.TrySelectDirection(CardinalDirection.Up);
                fixture.Input.TrySelectDirection(CardinalDirection.Down);
                var before = fixture.Player.transform.position;

                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                Assert.That(fixture.Player.CurrentDirection, Is.EqualTo(CardinalDirection.Up));
                Assert.That(fixture.Player.transform.position.x, Is.EqualTo(before.x).Within(.0001f));
                Assert.That(fixture.Player.transform.position.y, Is.GreaterThan(before.y));
                Assert.That(fixture.Player.PendingDirection, Is.Null);
                Assert.That(fixture.Game.Lives, Is.EqualTo(3));
            }
        }

        [Test]
        public void OppositeDirectionWhileExposed_IsIgnoredWithoutFailure()
        {
            using (var fixture = new Fixture())
            {
                fixture.Player.RespawnAt(fixture.Board.GetWorldPosition(new GridCoordinate(0, 4)), CardinalDirection.Right);
                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Input.TrySelectDirection(CardinalDirection.Up);
                fixture.Player.AdvanceMovement(.04f);
                var beforeReverse = fixture.Player.transform.position;

                Assert.That(fixture.Input.TrySelectDirection(CardinalDirection.Down), Is.True);
                Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.False);
                Assert.That(fixture.Player.AdvanceMovement(.005f), Is.True);
                Assert.That(fixture.Player.CurrentDirection, Is.EqualTo(CardinalDirection.Up));
                Assert.That(fixture.Player.transform.position.y, Is.GreaterThan(beforeReverse.y));

                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                Assert.That(fixture.Game.Lives, Is.EqualTo(3));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Board.IsPlayerExposed, Is.True);
                Assert.That(fixture.Player.PendingDirection, Is.Null);
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
            }
        }

        [Test]
        public void HorizontalOppositeDirectionWhileExposed_IsIgnoredAndKeepsMoving()
        {
            using (var fixture = new Fixture())
            {
                fixture.Player.RespawnAt(fixture.Board.GetWorldPosition(new GridCoordinate(0, 4)), CardinalDirection.Right);
                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                fixture.Player.AdvanceMovement(.04f);
                var beforeReverseInput = fixture.Player.transform.position;

                fixture.Input.TrySelectDirection(CardinalDirection.Left);

                Assert.That(fixture.Player.AdvanceMovement(.01f), Is.True);
                Assert.That(fixture.Player.CurrentDirection, Is.EqualTo(CardinalDirection.Right));
                Assert.That(fixture.Player.transform.position.x, Is.GreaterThan(beforeReverseInput.x));
                Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(3));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Board.IsPlayerExposed, Is.True);
            }
        }

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
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.Respawning));

                Assert.That(fixture.CompleteRespawn(), Is.True);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Board.WorldToGrid(fixture.Player.transform.position), Is.EqualTo(safeCell));
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
                Assert.That(fixture.Player.CurrentDirection, Is.EqualTo(CardinalDirection.Right));
                Assert.That(fixture.Input.CurrentDirection, Is.EqualTo(CardinalDirection.Right));
                Assert.That(fixture.Input.GameplayInputEnabled, Is.True);
                Assert.That(fixture.Player.MovementEnabled, Is.True);
                Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.True);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeIdle));

                var respawnPosition = fixture.Player.transform.position;
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.False);
                fixture.Input.TrySelectDirection(fixture.Player.CurrentDirection);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                Assert.That(fixture.Player.transform.position, Is.Not.EqualTo(respawnPosition));
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(fixture.Board.WorldToGrid(fixture.Player.transform.position)));
            }
        }

        [Test]
        public void VolatilePlayerContact_UsesEnemyFailureAndMovesAfterRespawn()
        {
            using (var fixture = new Fixture())
            {
                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                fixture.Player.AdvanceMovement(.04f);
                Assert.That(fixture.Board.IsPlayerExposed, Is.True);

                var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                var enemyObject = new GameObject("VolatileContact");
                var enemy = enemyObject.AddComponent<VolatileEnemy>();
                try
                {
                    definition.moveSpeed = 0f;
                    PlayerFailureReason? failureReason = null;
                    fixture.Game.PlayerFailed += reason => failureReason = reason;
                    enemy.Activate(definition, fixture.Board, fixture.Game, fixture.Player.transform.position, Vector2.right);

                    enemy.AdvanceMovement(0f);

                    Assert.That(failureReason, Is.EqualTo(PlayerFailureReason.EnemyContact));
                    Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                    Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
                    Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                    Assert.That(fixture.CompleteRespawn(), Is.True);
                    Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.True);
                    Assert.That(fixture.Player.AdvanceMovement(.02f), Is.False);
                    fixture.Input.TrySelectDirection(fixture.Player.CurrentDirection);
                    Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(enemyObject);
                    UnityEngine.Object.DestroyImmediate(definition);
                }
            }
        }

        [Test]
        public void DirectEnemyContactOnSafeTerrain_UsesSharedRespawnAndRestoresMovement()
        {
            using (var fixture = new Fixture())
            using (var enemy = new EnemyFixture(fixture, typeof(BasicBouncer), fixture.Board.PlayerCell, Vector2.zero, 0f))
            {
                var safeCell = fixture.Board.PlayerCell;
                enemy.Controller.AdvanceMovement(0f);

                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
                Assert.That(fixture.Game.PlayerLifecycleGeneration, Is.EqualTo(1));
                Assert.That(fixture.CompleteRespawn(), Is.True);
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(safeCell));
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeIdle));
                fixture.Input.TrySelectDirection(fixture.Player.CurrentDirection);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
            }
        }

        [TestCase(PlayerFailureReason.EnemyContact)]
        [TestCase(PlayerFailureReason.TrailHit)]
        [TestCase(PlayerFailureReason.VolatileExplosion)]
        [TestCase(PlayerFailureReason.Laser)]
        [TestCase(PlayerFailureReason.TrailSelfIntersection)]
        public void EveryNonFatalRespawn_ReturnsToSynchronizedSafeManualMovement(PlayerFailureReason reason)
        {
            using (var fixture = new Fixture())
            {
                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                fixture.Player.AdvanceMovement(.04f);
                Assert.That(fixture.Board.IsPlayerExposed, Is.True);

                Assert.That(fixture.Game.ReportPlayerFailure(reason), Is.True);
                Assert.That(fixture.CompleteRespawn(), Is.True);

                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Board.Model.GetCell(fixture.Board.PlayerCell), Is.EqualTo(BoardCellState.Captured));
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(fixture.Board.WorldToGrid(fixture.Player.transform.position)));
                Assert.That(fixture.Input.IsDirectionHeld, Is.False);
                Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.True);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.False);
                fixture.Input.TrySelectDirection(fixture.Player.CurrentDirection);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
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
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.False);
                fixture.Input.TrySelectDirection(fixture.Player.CurrentDirection);
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

                var fallback = fixture.Board.GetSafeRespawnCell();
                Assert.That(fallback, Is.EqualTo(new GridCoordinate(2, 0)));
                Assert.That(fixture.Board.IsValidRespawnCell(fallback), Is.True);
            }
        }

        [Test]
        public void LargeCaptureThenInvalidatedHistoricalSafeCell_UsesCurrentSafeCellWithExit()
        {
            using (var fixture = new Fixture())
            {
                for (var x = 1; x < fixture.Board.Columns - 1; x++)
                    fixture.Board.Model.MoveTo(new GridCoordinate(x, 4));
                fixture.Board.Model.MoveTo(new GridCoordinate(fixture.Board.Columns - 1, 4));
                Assert.That(fixture.Board.CapturedPercentage, Is.GreaterThan(25f));

                var historicalCell = new GridCoordinate(2, 2);
                Assert.That(fixture.Board.Model.GetCell(historicalCell), Is.EqualTo(BoardCellState.Captured));
                fixture.Board.ResetPlayerTracking(fixture.Board.GetWorldPosition(historicalCell));
                Assert.That(fixture.Board.Model.RemoveCapturedWithinRadius(historicalCell, 0f), Is.EqualTo(1));
                Assert.That(fixture.Board.Model.GetCell(historicalCell), Is.EqualTo(BoardCellState.Uncaptured));

                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
                Assert.That(fixture.CompleteRespawn(), Is.True);

                var respawnCell = fixture.Board.PlayerCell;
                Assert.That(respawnCell, Is.Not.EqualTo(historicalCell));
                Assert.That(fixture.Board.IsValidRespawnCell(respawnCell), Is.True);
                Assert.That(fixture.Board.Model.GetCell(respawnCell), Is.EqualTo(BoardCellState.Captured));
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
                Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.True);
                fixture.Input.TrySelectDirection(fixture.Player.CurrentDirection);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
            }
        }

        [Test]
        public void PlayingOnUncapturedCellWithoutTrail_IsRepairedToSynchronizedSafeIdle()
        {
            using (var fixture = new Fixture())
            {
                var invalidCell = new GridCoordinate(2, 2);
                var invalidPosition = fixture.Board.GetWorldPosition(invalidCell);
                fixture.Board.ResetPlayerTracking(invalidPosition);
                fixture.Player.transform.position = invalidPosition;
                var movement = (PlayerMovementModel)Fixture.Get(fixture.Player, "movementModel");
                movement.SetPosition(invalidPosition);
                var livesBefore = fixture.Game.Lives;
                var generationBefore = fixture.Game.PlayerLifecycleGeneration;

                Assert.That(fixture.Board.Model.GetCell(invalidCell), Is.EqualTo(BoardCellState.Uncaptured));
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);

                LogAssert.Expect(LogType.Error,
                    new Regex("^=== SPACEXONIX PLAYER LIFECYCLE INVALID STATE ==="));
                fixture.EnsureValidPlayingPlayerState();

                Assert.That(fixture.Game.Lives, Is.EqualTo(livesBefore));
                Assert.That(fixture.Game.PlayerLifecycleGeneration, Is.EqualTo(generationBefore + 1));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Board.Model.GetCell(fixture.Board.PlayerCell), Is.EqualTo(BoardCellState.Captured));
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeIdle));
                Assert.That(fixture.Player.HasValidPlayingState(), Is.True);
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(fixture.Board.WorldToGrid(fixture.Player.transform.position)));
            }
        }

        [Test]
        public void RespawnCompletion_RejectsInvalidatedCellAndRestoresFullSafeInvariant()
        {
            using (var fixture = new Fixture())
            {
                for (var x = 1; x < fixture.Board.Columns - 1; x++)
                    fixture.Board.Model.MoveTo(new GridCoordinate(x, 4));
                fixture.Board.Model.MoveTo(new GridCoordinate(fixture.Board.Columns - 1, 4));
                var staleCell = new GridCoordinate(2, 2);
                fixture.Board.ResetPlayerTracking(fixture.Board.GetWorldPosition(staleCell));
                fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact);
                fixture.Board.Model.RemoveCapturedWithinRadius(staleCell, 0f);

                Assert.That(fixture.CompleteRespawn(), Is.True);
                Assert.That(fixture.Board.PlayerCell, Is.Not.EqualTo(staleCell));
                Assert.That(fixture.Player.HasValidPlayingState(), Is.True);
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeIdle));
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
        public void MultiCellEnemyTrailHit_AtomicallyRespawnsAndRestoresMovement()
        {
            using (var fixture = new Fixture())
            using (var enemy = new EnemyFixture<BasicBouncer>(fixture, new GridCoordinate(4, 1), Vector2.left, 3.6f))
            {
                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Input.TrySelectDirection(CardinalDirection.Up);
                fixture.Player.AdvanceMovement(.04f);
                Assert.That(fixture.Board.IsPlayerExposed, Is.True);

                var failures = 0;
                var reentrantInputAccepted = true;
                var reentrantMovementAdvanced = true;
                PlayerFailureReason? acceptedReason = null;
                fixture.Game.PlayerFailed += reason =>
                {
                    failures++;
                    acceptedReason = reason;
                    reentrantInputAccepted = fixture.Input.TrySelectDirection(CardinalDirection.Right);
                    reentrantMovementAdvanced = fixture.Player.AdvanceMovement(.04f);
                };
                var enemyPosition = enemy.Controller.transform.position;

                enemy.Controller.AdvanceMovement(.15f);

                Assert.That(enemy.Controller.LastTraversedCells, Is.EqualTo(new[]
                {
                    new GridCoordinate(4, 1), new GridCoordinate(3, 1),
                    new GridCoordinate(2, 1), new GridCoordinate(1, 1)
                }));
                Assert.That(enemy.Controller.LastTrailHitAccepted, Is.True);
                Assert.That(enemy.Controller.transform.position, Is.EqualTo(enemyPosition), "accepted failure must abort enemy traversal");
                Assert.That(acceptedReason, Is.EqualTo(PlayerFailureReason.TrailHit));
                Assert.That(failures, Is.EqualTo(1));
                Assert.That(reentrantInputAccepted, Is.False, "input must be disabled before failure callbacks run");
                Assert.That(reentrantMovementAdvanced, Is.False, "player traversal must be disabled before failure callbacks run");
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));

                Assert.That(fixture.CompleteRespawn(), Is.True);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Board.IsValidRespawnCell(fixture.Board.PlayerCell), Is.True);
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);
                Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.True);
                Assert.That(fixture.Player.PendingDirection, Is.Null);
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
                fixture.Input.TrySelectDirection(fixture.Player.CurrentDirection);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
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
        public void TwoEnemyHitsInSameFrame_StartOneFailureAndOneRespawnFlow()
        {
            using (var fixture = new Fixture())
            {
                var respawnStarts = 0;
                fixture.Game.RespawnStarted += () => respawnStarts++;

                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
                var respawnOperation = Fixture.Get(fixture.Game, "respawnCoroutine");
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.False);

                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                Assert.That(fixture.Game.IsFailureInProgress, Is.True);
                Assert.That(respawnStarts, Is.EqualTo(1));
                Assert.That(respawnOperation, Is.Not.Null.And.SameAs(Fixture.Get(fixture.Game, "respawnCoroutine")));
            }
        }

        [TestCase(PlayerFailureReason.EnemyContact, PlayerFailureReason.Laser)]
        [TestCase(PlayerFailureReason.EnemyContact, PlayerFailureReason.VolatileExplosion)]
        [TestCase(PlayerFailureReason.TrailHit, PlayerFailureReason.EnemyContact)]
        [TestCase(PlayerFailureReason.Laser, PlayerFailureReason.VolatileExplosion)]
        public void OverlappingDifferentFailures_AcceptOnlyTheFirst(PlayerFailureReason first, PlayerFailureReason second)
        {
            using (var fixture = new Fixture())
            {
                var failedEvents = 0;
                var respawnStarts = 0;
                fixture.Game.PlayerFailed += _ => failedEvents++;
                fixture.Game.RespawnStarted += () => respawnStarts++;

                Assert.That(fixture.Game.ReportPlayerFailure(first), Is.True);
                Assert.That(fixture.Game.ReportPlayerFailure(second), Is.False);

                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                Assert.That(failedEvents, Is.EqualTo(1));
                Assert.That(respawnStarts, Is.EqualTo(1));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
                Assert.That(fixture.Game.PlayerLifecycleGeneration, Is.EqualTo(1));
            }
        }

        [TestCase(PlayerFailureReason.EnemyContact, PlayerFailureReason.EnemyContact)]
        [TestCase(PlayerFailureReason.EnemyContact, PlayerFailureReason.Laser)]
        [TestCase(PlayerFailureReason.EnemyContact, PlayerFailureReason.VolatileExplosion)]
        [TestCase(PlayerFailureReason.TrailHit, PlayerFailureReason.EnemyContact)]
        [TestCase(PlayerFailureReason.Laser, PlayerFailureReason.VolatileExplosion)]
        public void SimultaneousFailures_RunOneRespawnAndRejectCompletionFrameCallbacks(
            PlayerFailureReason first, PlayerFailureReason overlapping)
        {
            using (var fixture = new Fixture())
            {
                var failures = 0;
                var respawnStarts = 0;
                var respawnCompletions = 0;
                var callbackAccepted = true;
                fixture.Game.PlayerFailed += _ => failures++;
                fixture.Game.RespawnStarted += () => respawnStarts++;
                fixture.Game.PlayerRespawned += () =>
                {
                    respawnCompletions++;
                    callbackAccepted = fixture.Game.ReportPlayerFailure(overlapping);
                };

                Assert.That(fixture.Game.ReportPlayerFailure(first), Is.True);
                Assert.That(fixture.Game.ReportPlayerFailure(overlapping), Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                Assert.That(failures, Is.EqualTo(1));
                Assert.That(respawnStarts, Is.EqualTo(1));

                Assert.That(fixture.CompleteRespawnWithoutAdvancingFrame(), Is.True);
                Assert.That(callbackAccepted, Is.False, "callbacks queued in the completion frame must remain rejected");
                Assert.That(fixture.Game.ReportPlayerFailure(overlapping), Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                Assert.That(respawnCompletions, Is.EqualTo(1));
                Assert.That(fixture.Game.IsFailureInProgress, Is.True);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Board.IsValidRespawnCell(fixture.Board.PlayerCell), Is.True);
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);

                Assert.That(fixture.AdvancePastFailureFrame(), Is.True);
                fixture.Input.TrySelectDirection(fixture.Player.CurrentDirection);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
            }
        }

        [Test]
        public void AtomicFailure_RecoversMovementThenAllowsOneLaterDeath()
        {
            using (var fixture = new Fixture())
            {
                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                fixture.Player.AdvanceMovement(.04f);

                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.Laser), Is.False);
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.CompleteRespawn(), Is.True);
                Assert.That(fixture.Game.IsFailureInProgress, Is.False);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Board.IsValidRespawnCell(fixture.Board.PlayerCell), Is.True);
                fixture.Input.TrySelectDirection(fixture.Player.CurrentDirection);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);

                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.VolatileExplosion), Is.True);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.TrailHit), Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(1));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
            }
        }

        [Test]
        public void RespawnInvulnerability_BlocksDamageForTwoSecondsWithoutBlockingMovement()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.Laser), Is.True);
                Assert.That(fixture.CompleteRespawnWithoutAdvancingFrame(), Is.True);
                Assert.That(fixture.AdvancePastFailureFrame(), Is.True);
                Assert.That(fixture.Game.IsInvulnerable, Is.True);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeIdle));

                fixture.Input.TrySelectDirection(fixture.Player.CurrentDirection);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                var lives = fixture.Game.Lives;
                var position = fixture.Player.transform.position;
                var trailCount = fixture.Board.Model.ActiveTrail.Count;

                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(lives));
                Assert.That(fixture.Player.transform.position, Is.EqualTo(position));
                Assert.That(fixture.Board.Model.ActiveTrail.Count, Is.EqualTo(trailCount));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));

                fixture.Game.AdvanceLifecycle(1.99f);
                Assert.That(fixture.Game.IsInvulnerable, Is.True);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.VolatileExplosion), Is.False);
                fixture.Game.AdvanceLifecycle(.01f);
                Assert.That(fixture.Game.IsInvulnerable, Is.False);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.VolatileExplosion), Is.True);
                Assert.That(fixture.Game.Lives, Is.EqualTo(lives - 1));
            }
        }

        [Test]
        public void InvulnerableSelfIntersection_LeavesTrailAndPlayerTraversalUnchanged()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
                Assert.That(fixture.CompleteRespawnWithoutAdvancingFrame(), Is.True);
                Assert.That(fixture.AdvancePastFailureFrame(), Is.True);
                Assert.That(fixture.Game.IsInvulnerable, Is.True);
                fixture.Board.Model.MoveTo(new GridCoordinate(1, 3));
                fixture.Board.Model.MoveTo(new GridCoordinate(2, 3));
                fixture.Board.Model.MoveTo(new GridCoordinate(3, 3));
                var head = new GridCoordinate(3, 3);
                var from = fixture.Board.GetWorldPosition(head);
                fixture.Board.ResetPlayerTracking(from);
                fixture.Player.transform.position = from;
                var movement = (PlayerMovementModel)Fixture.Get(fixture.Player, "movementModel");
                movement.SetPosition(from);
                movement.SetDirection(CardinalDirection.Left);
                Fixture.Set(fixture.Player, "controlState", PlayerControlState.ExposedMoving);
                var lives = fixture.Game.Lives;

                var movedIntoTrail = fixture.Player.AdvanceMovement(.04f);

                Assert.That(movedIntoTrail, Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(lives));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(head));
                Assert.That(fixture.Board.IsPlayerExposed, Is.True);
                Assert.That(fixture.Board.Model.ActiveTrail.Count, Is.EqualTo(3));
                Assert.That(fixture.Board.Model.GetCell(head), Is.EqualTo(BoardCellState.Trail));
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));

                fixture.Input.TrySelectDirection(CardinalDirection.Up);
                Assert.That(fixture.Player.AdvanceMovement(.04f), Is.True);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.ExposedMoving));
                Assert.That(fixture.Board.Model.ActiveTrail.Count, Is.EqualTo(4));
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
                var target = fixture.Board.GetWorldPosition(new GridCoordinate(0, 3));
                fixture.Board.ResetPlayerTracking(from);

                var result = fixture.Board.TrackPlayerWorldPosition(from, target);

                Assert.That(result, Is.EqualTo(BoardMoveResult.TrailFailed));
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(new GridCoordinate(2, 3)), "cells after the first intersection must not be processed");
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);
            }
        }

        [Test]
        public void PlayerCrossingOwnActiveTrail_LosesOneLifeClearsTrailAndRespawns()
        {
            using (var fixture = new Fixture())
            {
                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Input.TrySelectDirection(CardinalDirection.Up);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Input.TrySelectDirection(CardinalDirection.Left);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Player.AdvanceMovement(.04f);
                fixture.Input.TrySelectDirection(CardinalDirection.Down);

                Assert.That(fixture.Player.AdvanceMovement(.04f), Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.TrailSelfIntersection), Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));

                Assert.That(fixture.CompleteRespawn(), Is.True);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Input.GameplayInputEnabled, Is.True);
                Assert.That(fixture.Player.MovementEnabled, Is.True);
                Assert.That(fixture.Board.Model.GetCell(fixture.Board.PlayerCell), Is.EqualTo(BoardCellState.Captured));
                fixture.Input.TrySelectDirection(fixture.Player.CurrentDirection);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
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
                    Assert.That(fixture.Player.AdvanceMovement(.02f), Is.False, $"manual wait {index}");
                    fixture.Input.TrySelectDirection(fixture.Player.CurrentDirection);
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
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.GameOver));
                Assert.That(fixture.CompleteRespawn(), Is.False);
                Assert.That(fixture.Player.transform.position, Is.EqualTo(positionBefore));
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.False);
                Assert.That(fixture.Game.Lives, Is.Zero);
            }
        }

        [Test]
        public void CaptureBelowTarget_KeepsPlaying()
        {
            using (var fixture = new Fixture())
            {
                CaptureBottomBand(fixture);
                Assert.That(fixture.Board.CapturedPercentage, Is.LessThan(fixture.Game.CaptureTargetPercentage));
                Assert.That(fixture.Game.TryCompleteStage(), Is.False);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
            }
        }

        [Test]
        public void CaptureReachingTarget_CompletesStageOnceAndLocksPlayer()
        {
            using (var fixture = new Fixture())
            {
                Fixture.Set(fixture.Game, "captureTargetPercentage", 20f);
                var completions = 0;
                fixture.Game.StageCompleted += () => completions++;
                CaptureBottomBand(fixture);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing), "completion waits for the end-of-frame check");

                Assert.That(fixture.Game.TryCompleteStage(), Is.True);
                Assert.That(fixture.Game.TryCompleteStage(), Is.False);
                Assert.That(completions, Is.EqualTo(1));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.StageComplete));
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.StageComplete));
                Assert.That(fixture.Input.GameplayInputEnabled, Is.False);
                Assert.That(fixture.Input.TrySelectDirection(CardinalDirection.Up), Is.False);
                var position = fixture.Player.transform.position;
                Assert.That(fixture.Player.AdvanceMovement(.2f), Is.False);
                Assert.That(fixture.Player.transform.position, Is.EqualTo(position));
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.False);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.Laser), Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(3));
            }
        }

        [Test]
        public void StageCompletion_IsDetectedByEndOfFrameCheck()
        {
            using (var fixture = new Fixture())
            {
                Fixture.Set(fixture.Game, "captureTargetPercentage", 20f);
                CaptureBottomBand(fixture);
                fixture.AdvancePastFailureFrame();
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.StageComplete));
            }
        }

        [Test]
        public void StageCompletion_IsNotTriggeredWhileRespawningOrGameOver()
        {
            using (var fixture = new Fixture(startingLives: 2))
            {
                CaptureBottomBand(fixture);
                Fixture.Set(fixture.Game, "captureTargetPercentage", 1f);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
                Assert.That(fixture.Game.TryCompleteStage(), Is.False);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
            }
            using (var fixture = new Fixture(startingLives: 1))
            {
                CaptureBottomBand(fixture);
                Fixture.Set(fixture.Game, "captureTargetPercentage", 1f);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.Laser), Is.True);
                Assert.That(fixture.Game.TryCompleteStage(), Is.False);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.GameOver));
            }
        }

        [Test]
        public void StageCompletion_IsNotTriggeredWhileExposed()
        {
            using (var fixture = new Fixture())
            {
                CaptureBottomBand(fixture);
                Fixture.Set(fixture.Game, "captureTargetPercentage", 1f);
                fixture.Input.TrySelectDirection(CardinalDirection.Up);
                for (var i = 0; i < 200 && fixture.Board.PlayerCell.Y < 3; i++) fixture.Player.AdvanceMovement(.02f);
                fixture.Input.TrySelectDirection(CardinalDirection.Left);
                for (var i = 0; i < 200 && !fixture.Board.IsPlayerExposed; i++) fixture.Player.AdvanceMovement(.02f);
                Assert.That(fixture.Board.IsPlayerExposed, Is.True);
                Assert.That(fixture.Game.TryCompleteStage(), Is.False);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
            }
        }

        [Test]
        public void EnemyManager_SuspendsEnemyMovementWhenStageCompletes()
        {
            using (var fixture = new Fixture())
            {
                var managers = new GameObject("StageCompleteManagers");
                try
                {
                    var enemyManager = managers.AddComponent<EnemyManager>();
                    Fixture.Set(enemyManager, "boardManager", fixture.Board);
                    Fixture.Set(enemyManager, "gameManager", fixture.Game);
                    Fixture.Set(enemyManager, "playerController", fixture.Player);
                    typeof(EnemyManager).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(enemyManager, null);
                    using (var enemy = new EnemyFixture<BasicBouncer>(fixture, new GridCoordinate(3, 7), Vector2.right, 1f))
                    {
                        CaptureBottomBand(fixture);
                        Assert.That(enemyManager.Spawn(enemy.Controller, enemy.Controller.Definition, new GridCoordinate(3, 7), Vector2.right), Is.True);
                        Assert.That(enemy.Controller.MovementEnabled, Is.True);
                        Fixture.Set(fixture.Game, "captureTargetPercentage", 20f);
                        Assert.That(fixture.Game.TryCompleteStage(), Is.True);
                        Assert.That(enemy.Controller.MovementEnabled, Is.False);
                        var before = enemy.Controller.transform.position;
                        enemy.Controller.AdvanceMovement(.5f);
                        Assert.That(enemy.Controller.transform.position, Is.EqualTo(before));
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(managers);
                }
            }
        }

        private static void CaptureBottomBand(Fixture fixture)
        {
            fixture.Input.TrySelectDirection(CardinalDirection.Up);
            for (var i = 0; i < 200 && fixture.Board.PlayerCell.Y < 2; i++) fixture.Player.AdvanceMovement(.02f);
            fixture.Input.TrySelectDirection(CardinalDirection.Right);
            var exposed = false;
            for (var i = 0; i < 500; i++)
            {
                fixture.Player.AdvanceMovement(.02f);
                if (fixture.Board.IsPlayerExposed && !exposed) { exposed = true; fixture.Input.ReleaseDirection(); }
                if (exposed && !fixture.Board.IsPlayerExposed) break;
            }
            fixture.Input.ReleaseDirection();
            Assert.That(exposed, Is.True);
            Assert.That(fixture.Board.IsPlayerExposed, Is.False);
            Assert.That(fixture.Board.CapturedPercentage, Is.GreaterThan(0f));
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
                var completed = CompleteRespawnWithoutAdvancingFrame();
                if (completed)
                {
                    AdvancePastFailureFrame();
                    Game.AdvanceLifecycle(2f);
                }
                return completed;
            }

            public bool CompleteRespawnWithoutAdvancingFrame()
            {
                return (bool)Invoke(Game, "CompleteRespawn");
            }

            public bool AdvancePastFailureFrame()
            {
                Set(Game, "failureGateReleaseFrame", Time.frameCount - 1);
                Invoke(Game, "LateUpdate");
                return !Game.IsFailureInProgress;
            }

            public void EnsureValidPlayingPlayerState()
            {
                Invoke(Game, "EnsureValidPlayingPlayerState");
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

            public static object Get(object target, string name)
            {
                return target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
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
