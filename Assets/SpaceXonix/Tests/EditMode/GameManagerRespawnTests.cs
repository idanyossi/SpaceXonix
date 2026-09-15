using System;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Input;
using SpaceXonix.Player;
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
                Player.ConnectInput(Input);
                Player.ConnectBoard(Board);
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
    }
}
