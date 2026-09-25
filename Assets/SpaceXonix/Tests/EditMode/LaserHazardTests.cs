using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using SpaceXonix.Hazards;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class LaserHazardTests
    {
        [Test]
        public void Cycle_UsesConfiguredWarningFiringCooldownAndRepeats()
        {
            var cycle = new LaserCycleModel(.75f, .25f, 2f);
            Assert.That(cycle.State, Is.EqualTo(LaserState.Cooldown));
            cycle.Advance(1.99f); Assert.That(cycle.State, Is.EqualTo(LaserState.Cooldown));
            cycle.Advance(.01f); Assert.That(cycle.State, Is.EqualTo(LaserState.Warning));
            cycle.Advance(.74f); Assert.That(cycle.State, Is.EqualTo(LaserState.Warning));
            cycle.Advance(.01f); Assert.That(cycle.State, Is.EqualTo(LaserState.Firing));
            cycle.Advance(.25f); Assert.That(cycle.State, Is.EqualTo(LaserState.Cooldown));
            cycle.Advance(2f); Assert.That(cycle.State, Is.EqualTo(LaserState.Warning));
        }

        [Test]
        public void Cycle_LargeDeterministicStepPreservesOverflow()
        {
            var cycle = new LaserCycleModel(.75f, .25f, 2f);
            cycle.Advance(3.1f);
            Assert.That(cycle.State, Is.EqualTo(LaserState.Cooldown));
            Assert.That(cycle.TimeRemaining, Is.EqualTo(1.9f).Within(.0001f));
        }

        [TestCase(LaserAxis.Horizontal)]
        [TestCase(LaserAxis.Vertical)]
        public void Emitter_PathIsAxisAligned(LaserAxis axis)
        {
            using (var fixture = new Fixture(axis))
            {
                var center = fixture.Board.GetWorldPosition(new GridCoordinate(10, 10));
                fixture.Emitter.transform.position = center;
                var onAxis = axis == LaserAxis.Horizontal ? center + Vector3.right : center + Vector3.up;
                var offAxis = axis == LaserAxis.Horizontal ? center + Vector3.up : center + Vector3.right;
                Assert.That(fixture.Emitter.Axis, Is.EqualTo(axis));
                Assert.That(fixture.Emitter.ContainsPoint(onAxis), Is.True);
                Assert.That(fixture.Emitter.ContainsPoint(offAxis), Is.False);
            }
        }

        [Test]
        public void WarningAndBeamPresentations_AreReleasedAndReused()
        {
            using (var fixture = new Fixture(LaserAxis.Horizontal))
            {
                fixture.Emitter.Tick(2f);
                var warning = ActivePresentation(fixture.Emitter.transform);
                Assert.That(warning, Is.Not.Null);
                fixture.Emitter.Tick(.75f);
                var beam = ActivePresentation(fixture.Emitter.transform);
                Assert.That(beam, Is.Not.Null.And.Not.SameAs(warning));
                fixture.Emitter.Tick(.25f);
                Assert.That(ActivePresentation(fixture.Emitter.transform), Is.Null);
                fixture.Emitter.Tick(2f);
                Assert.That(ActivePresentation(fixture.Emitter.transform), Is.SameAs(warning));
            }
        }

        [Test]
        public void WarningDoesNotDamageButFiringReportsOneAuthoritativeLaserFailure()
        {
            using (var fixture = new Fixture(LaserAxis.Horizontal, withGame: true))
            {
                fixture.Player.transform.position = fixture.Emitter.transform.position;
                PlayerFailureReason? reason = null; fixture.Game.PlayerFailed += value => reason = value;
                var before = fixture.Game.Lives;
                fixture.Emitter.Tick(2f);
                Assert.That(fixture.Game.Lives, Is.EqualTo(before));
                fixture.Emitter.Tick(.75f);
                Assert.That(reason, Is.EqualTo(PlayerFailureReason.Laser));
                Assert.That(fixture.Game.Lives, Is.EqualTo(before - 1));
                fixture.Emitter.Tick(.01f);
                Assert.That(fixture.Game.Lives, Is.EqualTo(before - 1));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SafeTerrainLaserHit_RestoresSafeIdleAndMovesOnFreshInput(bool startMoving)
        {
            using (var fixture = new Fixture(LaserAxis.Horizontal, withGame: true))
            {
                var laserCell = fixture.Board.WorldToGrid(fixture.Emitter.transform.position);
                var safeCell = new GridCoordinate(0, laserCell.Y);
                var safePosition = fixture.Board.GetWorldPosition(safeCell);
                fixture.Player.RespawnAt(safePosition, CardinalDirection.Right);
                fixture.Board.ResetPlayerTracking(safePosition);
                if (startMoving) fixture.Input.TrySelectDirection(CardinalDirection.Right);
                var before = fixture.Game.Lives;
                Assert.That(fixture.Emitter.ContainsPoint(fixture.Player.transform.position), Is.True);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Player.ControlState, Is.EqualTo(startMoving
                    ? PlayerControlState.SafeMoving
                    : PlayerControlState.SafeIdle));

                fixture.Emitter.Tick(2f);
                fixture.Emitter.Tick(.75f);
                Assert.That(fixture.Game.Lives, Is.EqualTo(before - 1));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.Respawning));
                Assert.That(fixture.Input.IsDirectionHeld, Is.False);
                Assert.That(CompleteRespawn(fixture.Game), Is.True);
                var respawnPosition = fixture.Player.transform.position;

                fixture.Emitter.Tick(.01f);

                Assert.That(fixture.Game.Lives, Is.EqualTo(before - 1));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Player.MovementEnabled, Is.True);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeIdle));
                Assert.That(fixture.Board.IsPlayerExposed, Is.False);
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Player.IsAwaitingDirectionInput, Is.True);
                Assert.That(fixture.Input.IsDirectionHeld, Is.False);
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(fixture.Board.WorldToGrid(fixture.Player.transform.position)));
                Assert.That(fixture.Board.IsLegalPlayerStep(new GridCoordinate(safeCell.X + 1, safeCell.Y)), Is.True);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.False);
                fixture.Input.TrySelectDirection(CardinalDirection.Right);
                Assert.That(fixture.Player.AdvanceMovement(.02f), Is.True);
                Assert.That(fixture.Player.transform.position, Is.Not.EqualTo(respawnPosition));
                Assert.That(fixture.Player.LogicalPosition, Is.EqualTo((Vector2)fixture.Player.transform.position));
            }
        }

        [Test]
        public void FiringHit_IsNotSkippedByAFrameCrossingTheWholeFiringWindow()
        {
            using (var fixture = new Fixture(LaserAxis.Horizontal, withGame: true))
            {
                fixture.Player.transform.position = fixture.Emitter.transform.position;
                var before = fixture.Game.Lives;
                fixture.Emitter.Tick(3f);
                Assert.That(fixture.Emitter.State, Is.EqualTo(LaserState.Cooldown));
                Assert.That(fixture.Game.Lives, Is.EqualTo(before - 1));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
            }
        }

        [Test]
        public void ExposedPlayer_FiringUsesFailurePipelineAndClearsTrail()
        {
            using (var fixture = new Fixture(LaserAxis.Horizontal, withGame: true))
            {
                fixture.Board.Model.MoveTo(new GridCoordinate(1, 3));
                fixture.Player.transform.position = fixture.Emitter.transform.position;
                PlayerFailureReason? reason = null; fixture.Game.PlayerFailed += value => reason = value;
                var before = fixture.Game.Lives;
                fixture.Emitter.Tick(2.75f);
                Assert.That(reason, Is.EqualTo(PlayerFailureReason.Laser));
                Assert.That(fixture.Game.Lives, Is.EqualTo(before - 1));
                Assert.That(fixture.Board.Model.IsExposed, Is.False);
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
            }
        }

        [Test]
        public void PlayerOutsideBeamIsUnaffected()
        {
            using (var fixture = new Fixture(LaserAxis.Horizontal, withGame: true))
            {
                fixture.Player.transform.position = fixture.Emitter.transform.position + Vector3.up;
                var before = fixture.Game.Lives;
                fixture.Emitter.Tick(2.75f);
                Assert.That(fixture.Game.Lives, Is.EqualTo(before));
            }
        }

        [Test]
        public void FiringDoesNotAffectEnemiesVolatileOrBoardState()
        {
            using (var fixture = new Fixture(LaserAxis.Horizontal))
            {
                var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                var basicObject = new GameObject("Basic"); var basic = basicObject.AddComponent<BasicBouncer>();
                var volatileObject = new GameObject("Volatile"); var volatileEnemy = volatileObject.AddComponent<VolatileEnemy>();
                try
                {
                    basic.Activate(definition, fixture.Board, null, fixture.Emitter.transform.position, Vector2.right);
                    volatileEnemy.Activate(definition, fixture.Board, null, fixture.Emitter.transform.position, Vector2.left);
                    fixture.Board.Model.MoveTo(new GridCoordinate(1, 3));
                    var trailCount = fixture.Board.Model.ActiveTrail.Count; var percentage = fixture.Board.CapturedPercentage;
                    fixture.Emitter.Tick(2.75f);
                    Assert.That(basic.IsActiveEnemy, Is.True);
                    Assert.That(volatileEnemy.IsActiveEnemy, Is.True);
                    Assert.That(volatileEnemy.HasDetonated, Is.False);
                    Assert.That(fixture.Board.Model.ActiveTrail.Count, Is.EqualTo(trailCount));
                    Assert.That(fixture.Board.CapturedPercentage, Is.EqualTo(percentage));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(basicObject); UnityEngine.Object.DestroyImmediate(volatileObject);
                    UnityEngine.Object.DestroyImmediate(definition);
                }
            }
        }

        private static GameObject ActivePresentation(Transform parent)
        {
            for (var i = 0; i < parent.childCount; i++) if (parent.GetChild(i).gameObject.activeSelf) return parent.GetChild(i).gameObject;
            return null;
        }

        [Test]
        public void RandomLines_MoveAtEachWarningAndTheBeamFollows()
        {
            using (var fixture = new Fixture(LaserAxis.Horizontal))
            {
                var lines = new Queue<int>(new[] { 30, 55 });
                fixture.Emitter.SetLinePicker(_ => lines.Dequeue());

                fixture.Emitter.Tick(2.01f);
                Assert.That(fixture.Emitter.State, Is.EqualTo(LaserState.Warning));
                Assert.That(fixture.Emitter.Line, Is.EqualTo(30), "the warning appears on the newly picked row");
                fixture.Emitter.Tick(.8f);
                Assert.That(fixture.Emitter.State, Is.EqualTo(LaserState.Firing));
                Assert.That(fixture.Emitter.ContainsPoint(fixture.Board.GetWorldPosition(new GridCoordinate(5, 30))), Is.True, "the beam fires where it warned");
                Assert.That(fixture.Emitter.ContainsPoint(fixture.Board.GetWorldPosition(new GridCoordinate(5, 10))), Is.False, "the placed row is no longer dangerous");

                fixture.Emitter.Tick(.3f + 2.01f);
                Assert.That(fixture.Emitter.Line, Is.EqualTo(55), "the next shot comes from somewhere new");
            }
        }

        [Test]
        public void LinePicker_KeepsClearOfEdgesAndOtherLasers()
        {
            var picker = new LaserLinePicker(1234);
            var occupied = new List<int> { 40 };
            for (var i = 0; i < 200; i++)
            {
                var line = picker.Pick(96, 4, 8, occupied);
                Assert.That(line, Is.InRange(4, 91), "never on the captured border");
                Assert.That(Math.Abs(line - 40), Is.GreaterThanOrEqualTo(8), "kept apart from the other laser when there is room");
            }
            Assert.That(picker.Pick(5, 4, 8, null), Is.EqualTo(2), "a board too small for the margin falls back to the middle");

            for (var i = 0; i < 200; i++)
            {
                var line = picker.Pick(96, 4, 8, null, 50, 12);
                Assert.That(line, Is.InRange(38, 62), "near the ship");
            }
            var seen = new HashSet<int>();
            for (var i = 0; i < 200; i++) seen.Add(picker.Pick(96, 4, 8, null, 50, 12));
            Assert.That(seen.Count, Is.GreaterThan(10), "still random within that window");
            for (var i = 0; i < 50; i++)
                Assert.That(picker.Pick(96, 4, 8, null, 1, 12), Is.InRange(4, 16), "a ship on the border still gets lines inside the margin");
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("LaserFixture");
            private readonly GameObject playerObject = new GameObject("Player");
            private readonly GameObject warningPrefab = new GameObject("WarningPrefab");
            private readonly GameObject beamPrefab = new GameObject("BeamPrefab");
            private readonly LaserDefinition definition;
            public readonly BoardManager Board;
            public readonly LaserEmitter Emitter;
            public readonly PlayerController Player;
            public readonly GameManager Game;
            public readonly InputRouter Input;

            public Fixture(LaserAxis axis, bool withGame = false)
            {
                root.SetActive(false); warningPrefab.AddComponent<LaserPresentation>(); beamPrefab.AddComponent<LaserPresentation>(); warningPrefab.SetActive(false); beamPrefab.SetActive(false);
                Board = root.AddComponent<BoardManager>(); Board.Initialize(); Player = playerObject.AddComponent<PlayerController>(); Invoke(Player, "Awake");
                var pool = root.AddComponent<PoolService>(); Game = withGame ? root.AddComponent<GameManager>() : null;
                if (Game != null)
                {
                    Input = root.AddComponent<InputRouter>(); Set(Game, "inputRouter", Input); Set(Game, "playerController", Player); Set(Game, "boardManager", Board);
                }
                definition = ScriptableObject.CreateInstance<LaserDefinition>(); definition.axis = axis; definition.warningDuration = .75f; definition.firingDuration = .25f; definition.cooldownDuration = 2f; definition.beamWidth = .12f;
                Emitter = new GameObject("Emitter").AddComponent<LaserEmitter>(); Emitter.transform.position = Board.GetWorldPosition(new GridCoordinate(10, 10)); Set(Emitter, "definition", definition);
                root.SetActive(true); if (Game != null) { Invoke(Game, "Awake"); Invoke(Game, "Start"); } Emitter.Initialize(Board, Game, pool, warningPrefab, beamPrefab);
            }

            public void Dispose()
            {
                Emitter.Shutdown(); UnityEngine.Object.DestroyImmediate(Emitter.gameObject); UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(warningPrefab); UnityEngine.Object.DestroyImmediate(beamPrefab); UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        private static bool CompleteRespawn(GameManager game) => (bool)game.GetType().GetMethod("CompleteRespawn", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
    }
}
