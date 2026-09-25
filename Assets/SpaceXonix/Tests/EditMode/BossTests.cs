using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Boss;
using SpaceXonix.Campaign;
using SpaceXonix.Core;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class BossTests
    {
        [Test]
        public void Run_PlaysTheBossAfterTheNormalStagesAndThenCompletesTheCampaign()
        {
            var run = new CampaignRunModel(4, hasBossStage: true);
            Assert.That(run.TotalStageCount, Is.EqualTo(5));
            Assert.That(run.IsBossStage, Is.False);

            for (var stage = 1; stage < 4; stage++) Assert.That(run.CompleteCurrentStage(), Is.True);
            Assert.That(run.CurrentStageNumber, Is.EqualTo(4));
            Assert.That(run.NormalStagesCleared, Is.False);

            Assert.That(run.CompleteCurrentStage(), Is.True, "clearing stage 4 loads the boss stage");
            Assert.That(run.IsBossStage, Is.True);
            Assert.That(run.NormalStagesCleared, Is.True);
            Assert.That(run.CurrentStageNumber, Is.EqualTo(5));
            Assert.That(run.HighestStageReached, Is.EqualTo(5));
            Assert.That(run.CampaignComplete, Is.False);

            Assert.That(run.CompleteCurrentStage(), Is.False, "beating the boss ends the run rather than loading a stage");
            Assert.That(run.CampaignComplete, Is.True);
            Assert.That(run.IsBossStage, Is.False);
            Assert.That(run.CurrentStageNumber, Is.EqualTo(5), "a finished run still reads as the boss stage, not stage 4");
            Assert.That(run.CompleteCurrentStage(), Is.False, "a finished campaign stays finished");

            run.Reset();
            Assert.That(run.CampaignComplete, Is.False);
            Assert.That(run.IsBossStage, Is.False);
            Assert.That(run.CurrentStageNumber, Is.EqualTo(1));
        }

        [Test]
        public void Run_WithoutABossStillEndsOnNormalStagesCleared()
        {
            var run = new CampaignRunModel(4);
            Assert.That(run.TotalStageCount, Is.EqualTo(4));
            for (var stage = 1; stage < 4; stage++) run.CompleteCurrentStage();
            Assert.That(run.CompleteCurrentStage(), Is.False);
            Assert.That(run.NormalStagesCleared, Is.True);
            Assert.That(run.IsBossStage, Is.False);
            Assert.That(run.CampaignComplete, Is.False);
        }

        [Test]
        public void Attack_FiresOnTheIntervalAndAPowerShotInterruptStopsTheCycle()
        {
            var attack = new BossAttackModel(2f);
            var interrupts = 0;
            var recoveries = 0;
            attack.InterruptStarted += () => interrupts++;
            attack.InterruptEnded += () => recoveries++;

            Assert.That(attack.Tick(1.99f), Is.False);
            Assert.That(attack.Tick(.01f), Is.True, "the first volley lands exactly on the interval");
            Assert.That(attack.Tick(1.99f), Is.False);
            Assert.That(attack.Tick(.01f), Is.True, "and the cycle repeats");

            attack.Interrupt(1.5f);
            Assert.That(attack.IsInterrupted, Is.True);
            Assert.That(interrupts, Is.EqualTo(1));
            Assert.That(attack.Tick(3f), Is.False, "no volley escapes while the core is stunned");
            Assert.That(attack.IsInterrupted, Is.False);
            Assert.That(recoveries, Is.EqualTo(1));

            Assert.That(attack.Tick(1.99f), Is.False, "recovery restarts a full interval rather than firing instantly");
            Assert.That(attack.Tick(.01f), Is.True);

            attack.Interrupt(1f);
            attack.Interrupt(3f);
            Assert.That(attack.InterruptTimeRemaining, Is.EqualTo(3f).Within(.0001f), "the longer stun wins");
            Assert.That(interrupts, Is.EqualTo(2), "an interrupt during a stun is not a second interrupt");

            attack.Reset();
            Assert.That(attack.IsInterrupted, Is.False);
            Assert.That(attack.TimeUntilNextVolley, Is.EqualTo(2f).Within(.0001f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossAttackModel(0f));
        }

        [Test]
        public void Volley_AimsAtTheShipAndSpreadsAroundThatDirection()
        {
            using (var fixture = new Fixture())
            {
                fixture.StartPlaying();
                // Well inside the arena, so nothing is released for leaving the board before it can be inspected.
                fixture.Boss.transform.position = fixture.Board.GetWorldPosition(new GridCoordinate(27, 70));
                fixture.Player.transform.position = fixture.Boss.transform.position + Vector3.down * 5f;
                fixture.Boss.Tick(fixture.Definition.fireInterval - .01f);
                fixture.Boss.Tick(.01f);

                Assert.That(fixture.Boss.ActiveProjectiles.Count, Is.EqualTo(3));
                var angles = new List<float>();
                foreach (var projectile in fixture.Boss.ActiveProjectiles)
                {
                    Assert.That(projectile.Velocity.magnitude, Is.EqualTo(fixture.Definition.projectileSpeed).Within(.001f));
                    angles.Add(Vector2.SignedAngle(Vector2.down, projectile.Velocity));
                }
                foreach (var projectile in fixture.Boss.ActiveProjectiles)
                {
                    var middle = Mathf.Abs(Vector2.SignedAngle(Vector2.down, projectile.Velocity)) < .01f;
                    Assert.That(projectile.BreaksTerritory, Is.EqualTo(middle), "only the middle shot breaks territory");
                }
                angles.Sort();
                Assert.That(angles[0], Is.EqualTo(-12f).Within(.01f));
                Assert.That(angles[1], Is.EqualTo(0f).Within(.01f), "the middle shot goes straight at the ship");
                Assert.That(angles[2], Is.EqualTo(12f).Within(.01f));
            }
        }

        [Test]
        public void MiddleShot_BreaksASmallPatchOfBuiltTerritoryAndIsSpent()
        {
            using (var fixture = new Fixture())
            {
                fixture.StartPlaying();
                fixture.CaptureColumn(30);
                var captured = fixture.Board.CapturedPercentage;
                var broken = new List<int>();
                fixture.Boss.TerritoryBroken += (_, cells) => broken.Add(cells);

                fixture.FireAtCell(new GridCoordinate(32, 40), 2.5f);
                for (var i = 0; i < 20 && fixture.Boss.ActiveProjectiles.Count > 0; i++) fixture.Boss.Tick(.02f);

                Assert.That(fixture.Boss.ActiveProjectiles, Is.Empty, "the shot is spent on the territory");
                Assert.That(broken, Has.Count.EqualTo(1));
                Assert.That(broken[0], Is.GreaterThan(5));
                Assert.That(fixture.Board.Model.GetCell(new GridCoordinate(30, 40)), Is.EqualTo(BoardCellState.Uncaptured), "it breaks where it lands");
                Assert.That(fixture.Board.Model.GetCell(new GridCoordinate(34, 40)), Is.EqualTo(BoardCellState.Captured), "and only a small patch");
                Assert.That(fixture.Board.CapturedPercentage, Is.LessThan(captured));
            }
        }

        [Test]
        public void SideShots_StillFlyOverTerritory()
        {
            using (var fixture = new Fixture())
            {
                fixture.StartPlaying();
                fixture.CaptureColumn(30);
                var captured = fixture.Board.CapturedPercentage;

                fixture.FireAtCell(new GridCoordinate(32, 40));
                for (var i = 0; i < 20; i++) fixture.Boss.Tick(.02f);

                Assert.That(fixture.Board.CapturedPercentage, Is.EqualTo(captured));
                Assert.That(fixture.Board.Model.GetCell(new GridCoordinate(30, 40)), Is.EqualTo(BoardCellState.Captured));
            }
        }

        [Test]
        public void Projectile_KillsTheShipAndTheShieldBlocksThatButNotATrailHit()
        {
            using (var fixture = new Fixture())
            {
                fixture.StartPlaying();
                // Shield on: a projectile driven into the ship is absorbed, not fatal.
                fixture.Game.SetShieldActive(true);
                var lives = fixture.Game.Lives;
                fixture.FireAtPlayer();
                fixture.Boss.Tick(.5f);
                Assert.That(fixture.Game.Lives, Is.EqualTo(lives), "Shield blocks boss projectiles, as the GDD requires");
                Assert.That(fixture.Boss.ActiveProjectiles, Is.Empty, "the projectile is still spent on the shield");

                // The trail stays vulnerable while shielded, exactly like every other hazard.
                fixture.Board.Model.MoveTo(new GridCoordinate(20, 40));
                fixture.Board.Model.MoveTo(new GridCoordinate(20, 41));
                PlayerFailureReason? reason = null;
                fixture.Game.PlayerFailed += value => reason = value;
                fixture.FireAtCell(new GridCoordinate(20, 41));
                fixture.Boss.Tick(.5f);
                Assert.That(reason, Is.EqualTo(PlayerFailureReason.TrailHit));
                Assert.That(fixture.Game.Lives, Is.EqualTo(lives - 1));
            }
        }

        [Test]
        public void Projectile_WithoutAShieldKillsTheShipAndReportsABossProjectileFailure()
        {
            using (var fixture = new Fixture())
            {
                fixture.StartPlaying();
                PlayerFailureReason? reason = null;
                fixture.Game.PlayerFailed += value => reason = value;
                var lives = fixture.Game.Lives;

                fixture.FireAtPlayer();
                fixture.Boss.Tick(.5f);

                Assert.That(reason, Is.EqualTo(PlayerFailureReason.BossProjectile));
                Assert.That(fixture.Game.Lives, Is.EqualTo(lives - 1));
                Assert.That(fixture.Boss.ActiveProjectiles, Is.Empty);
            }
        }

        [Test]
        public void PowerShot_InterruptsTheCoreInsteadOfDestroyingIt()
        {
            using (var fixture = new Fixture())
            {
                fixture.StartPlaying();
                var origin = (Vector2)fixture.Boss.transform.position;
                var interrupts = 0;
                fixture.Boss.Interrupted += () => interrupts++;

                Assert.That(fixture.Boss.TryInterceptShot(origin + Vector2.left * 40f, origin + Vector2.left * 30f, .1f), Is.False,
                    "a shot that never reaches the core passes by");

                Assert.That(fixture.Boss.TryInterceptShot(origin + Vector2.down * 6f, origin + Vector2.up * 6f, .1f), Is.True);
                Assert.That(interrupts, Is.EqualTo(1));
                Assert.That(fixture.Boss.IsInterrupted, Is.True);
                Assert.That(fixture.Boss.IsActive, Is.True, "the core survives: shooting it never reduces its health");

                fixture.Boss.transform.position = fixture.Board.GetWorldPosition(new GridCoordinate(27, 70));
                fixture.Player.transform.position = fixture.Boss.transform.position + Vector3.down * 5f;
                fixture.Boss.Tick(fixture.Definition.fireInterval - .01f);
                fixture.Boss.Tick(.01f);
                Assert.That(fixture.Boss.ActiveProjectiles, Is.Empty, "a stunned core fires nothing");
            }
        }

        [Test]
        public void Core_IsHiddenOnEveryStageExceptTheBossStage()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Body.gameObject.activeSelf, Is.False,
                    "the core sits in the scene for every stage, so it must start hidden");

                // A normal stage load passes no boss definition.
                fixture.Boss.Activate(null);
                Assert.That(fixture.Boss.IsActive, Is.False);
                Assert.That(fixture.Body.gameObject.activeSelf, Is.False, "a normal stage must not show the core");

                fixture.Boss.Activate(fixture.Definition);
                Assert.That(fixture.Boss.IsActive, Is.True);
                Assert.That(fixture.Body.gameObject.activeSelf, Is.True);

                fixture.Boss.Deactivate();
                Assert.That(fixture.Body.gameObject.activeSelf, Is.False, "beating it hides it again");
            }
        }

        [Test]
        public void Modifiers_ScaleTheFiringCycleAndProjectileSpeedWithoutTouchingTheDefinition()
        {
            using (var fixture = new Fixture())
            {
                // Overdriven Core: the cycle is set before the stage activates the boss, as the campaign does it.
                fixture.Boss.SetFireIntervalMultiplier(.5f);
                fixture.Boss.SetProjectileSpeedMultiplier(2f);
                fixture.StartPlaying();
                fixture.Boss.transform.position = fixture.Board.GetWorldPosition(new GridCoordinate(27, 70));
                fixture.Player.transform.position = fixture.Boss.transform.position + Vector3.down * 5f;

                fixture.Boss.Tick(.99f);
                Assert.That(fixture.Boss.ActiveProjectiles, Is.Empty);
                fixture.Boss.Tick(.01f);
                Assert.That(fixture.Boss.ActiveProjectiles.Count, Is.EqualTo(3), "a halved interval fires at 1s, not 2s");
                Assert.That(fixture.Boss.ActiveProjectiles[0].Velocity.magnitude,
                    Is.EqualTo(fixture.Definition.projectileSpeed * 2f).Within(.001f));
                Assert.That(fixture.Definition.fireInterval, Is.EqualTo(2f), "definitions stay immutable");
                Assert.That(fixture.Definition.projectileSpeed, Is.EqualTo(5f));
            }
        }

        [Test]
        public void Core_TakesVisibleCaptureDamageAndDiesWhenTheStageIsCleared()
        {
            using (var fixture = new Fixture())
            {
                fixture.StartPlaying();
                var stages = new List<int>();
                fixture.Boss.DamageStageChanged += stage => stages.Add(stage);
                Assert.That(fixture.Boss.DamageStage, Is.Zero);
                var fullScale = fixture.Body.localScale.x;

                // The core's damage tracks progress toward the stage's capture target, not the whole board.
                fixture.ReportCapturedPercentage(fixture.Game.CaptureTargetPercentage * .5f);
                Assert.That(fixture.Boss.DamageStage, Is.EqualTo(2));
                Assert.That(fixture.Body.localScale.x, Is.LessThan(fullScale), "the core visibly shrinks as it is damaged");

                fixture.ReportCapturedPercentage(fixture.Game.CaptureTargetPercentage * .75f);
                Assert.That(fixture.Boss.DamageStage, Is.EqualTo(3));
                Assert.That(stages, Is.EqualTo(new[] { 2, 3 }));

                var defeated = 0;
                fixture.Boss.Defeated += () => defeated++;
                fixture.Game.SetShieldActive(false);
                fixture.CompleteStage();

                Assert.That(defeated, Is.EqualTo(1));
                Assert.That(fixture.Boss.IsActive, Is.False);
                Assert.That(fixture.Boss.ActiveProjectiles, Is.Empty, "in-flight projectiles die with the core");
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("BossFixture");
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            private readonly GameObject projectilePrefab;
            public readonly BoardManager Board;
            public readonly InputRouter Input;
            public readonly PlayerController Player;
            public readonly GameManager Game;
            public readonly BossController Boss;
            public readonly BossDefinition Definition;
            public readonly Transform Body;

            public Fixture()
            {
                root.SetActive(false);
                Board = root.AddComponent<BoardManager>();
                Board.Initialize();
                Input = root.AddComponent<InputRouter>();
                Player = Own(new GameObject("Player")).AddComponent<PlayerController>();
                Invoke(Player, "Awake");
                Game = root.AddComponent<GameManager>();
                Set(Game, "inputRouter", Input); Set(Game, "playerController", Player); Set(Game, "boardManager", Board);
                Invoke(Game, "Awake");

                projectilePrefab = Own(new GameObject("BossProjectilePrefab"));
                projectilePrefab.AddComponent<BossProjectile>();
                projectilePrefab.SetActive(false);

                Definition = Own(ScriptableObject.CreateInstance<BossDefinition>());
                Definition.fireInterval = 2f;
                Definition.projectileSpeed = 5f;
                Definition.projectilesPerVolley = 3;
                Definition.volleySpreadDegrees = 24f;
                Definition.projectileRadius = .3f;
                Definition.interruptDuration = 2f;
                Definition.damageStages = 4;
                Definition.bodyRadius = 1.5f;

                var bossObject = Own(new GameObject("AlienCore"));
                // Inactive while the references are set, so Awake sees them exactly as it does in the saved scene.
                bossObject.SetActive(false);
                Body = new GameObject("Body").transform;
                Body.SetParent(bossObject.transform, false);
                Boss = bossObject.AddComponent<BossController>();
                Set(Boss, "boardManager", Board); Set(Boss, "gameManager", Game);
                Set(Boss, "poolService", root.AddComponent<PoolService>());
                Set(Boss, "projectilePrefab", projectilePrefab);
                Set(Boss, "bodyVisual", Body);
                bossObject.SetActive(true);
                // EditMode does not run Awake for us, so the hidden-by-default state is applied by hand.
                Invoke(Boss, "Awake");
                root.SetActive(true);
            }

            public void StartPlaying()
            {
                Invoke(Game, "Start");
                Boss.Activate(Definition);
                // OnEnable already ran when the fixture root activated, so the capture subscription is live.
            }

            /// <summary>Puts one projectile just short of the ship, travelling into it.</summary>
            public void FireAtPlayer() => FireAt(Player.transform.position);

            public void FireAtCell(GridCoordinate cell, float territoryRadiusCells = 0f) => FireAt(Board.GetWorldPosition(cell), territoryRadiusCells);

            /// <summary>Cuts a full column and reconnects, a real capture that fills the smaller side.</summary>
            public void CaptureColumn(int column)
            {
                for (var row = 1; row < Board.Rows - 1; row++) Board.Model.MoveTo(new GridCoordinate(column, row));
                Board.Model.MoveTo(new GridCoordinate(column, Board.Rows - 1));
            }

            private void FireAt(Vector3 target, float territoryRadiusCells = 0f)
            {
                var direction = Vector2.right;
                var origin = target - (Vector3)(direction * 1f);
                var instance = UnityEngine.Object.Instantiate(projectilePrefab, Boss.transform);
                instance.SetActive(true);
                var projectile = instance.GetComponent<BossProjectile>();
                projectile.Launch(origin, direction * 4f, Definition.projectileRadius, territoryRadiusCells);
                var active = (List<BossProjectile>)typeof(BossController)
                    .GetField("activeProjectiles", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Boss);
                active.Clear();
                active.Add(projectile);
            }

            public void ReportCapturedPercentage(float percentage) =>
                typeof(BossController).GetMethod("OnCapturedPercentageChanged", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(Boss, new object[] { percentage });

            public void CompleteStage()
            {
                FireAtPlayer();
                typeof(BossController).GetMethod("OnStageCompleted", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(Boss, null);
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
