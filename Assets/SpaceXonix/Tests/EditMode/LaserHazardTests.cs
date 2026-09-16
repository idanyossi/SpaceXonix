using System;
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

        [Test]
        public void RespawnDuringSameFiringPhase_IsNotHitAgainAndMovesOnNextTick()
        {
            using (var fixture = new Fixture(LaserAxis.Horizontal, withGame: true))
            {
                var laserCell = fixture.Board.WorldToGrid(fixture.Emitter.transform.position);
                var safeCell = new GridCoordinate(0, laserCell.Y);
                var safePosition = fixture.Board.GetWorldPosition(safeCell);
                fixture.Player.RespawnAt(safePosition, CardinalDirection.Right);
                fixture.Board.ResetPlayerTracking(safePosition);
                var before = fixture.Game.Lives;
                Assert.That(fixture.Emitter.ContainsPoint(fixture.Player.transform.position), Is.True);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));

                fixture.Emitter.Tick(2f);
                fixture.Emitter.Tick(.75f);
                Assert.That(fixture.Game.Lives, Is.EqualTo(before - 1));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Respawning));
                Assert.That(CompleteRespawn(fixture.Game), Is.True);
                var respawnPosition = fixture.Player.transform.position;

                fixture.Emitter.Tick(.01f);

                Assert.That(fixture.Game.Lives, Is.EqualTo(before - 1));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Player.MovementEnabled, Is.True);
                Assert.That(fixture.Board.IsLegalPlayerStep(new GridCoordinate(safeCell.X + 1, safeCell.Y)), Is.True);
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

            public Fixture(LaserAxis axis, bool withGame = false)
            {
                root.SetActive(false); warningPrefab.AddComponent<LaserPresentation>(); beamPrefab.AddComponent<LaserPresentation>(); warningPrefab.SetActive(false); beamPrefab.SetActive(false);
                Board = root.AddComponent<BoardManager>(); Board.Initialize(); Player = playerObject.AddComponent<PlayerController>(); Invoke(Player, "Awake");
                var pool = root.AddComponent<PoolService>(); Game = withGame ? root.AddComponent<GameManager>() : null;
                if (Game != null)
                {
                    var input = root.AddComponent<InputRouter>(); Set(Game, "inputRouter", input); Set(Game, "playerController", Player); Set(Game, "boardManager", Board);
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
