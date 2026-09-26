using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Boss;
using SpaceXonix.Campaign;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using SpaceXonix.Hazards;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Power;
using SpaceXonix.PowerUps;
using SpaceXonix.Presentation;
using SpaceXonix.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpaceXonix.Tests.PlayMode
{
    /// <summary>
    /// The real game, running: each test boots through Boot and the Main Menu into Game.unity the way
    /// a player does, then drives it through the same input and campaign calls the UI uses.
    /// </summary>
    public sealed class GameFlowTests
    {
        private GameManager game;
        private CampaignManager campaign;
        private BoardManager board;
        private PlayerController player;
        private InputRouter input;
        private EnemyManager enemies;
        private PowerMeter meter;
        private PowerUpManager powerUps;

        // The game boots once per launch: Boot's persistent services outlive it, and loading Boot a
        // second time would only meet them as duplicates. Later tests start from the Main Menu.
        private static bool booted;

        [UnitySetUp]
        public IEnumerator BootIntoGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(booted ? SceneNames.MainMenu : SceneNames.Boot);
            booted = true;
            yield return Until(() => SceneManager.GetActiveScene().name == SceneNames.MainMenu && !ScreenFader.IsFading, 10f, "the Main Menu");
            SceneRouter.StartCampaign();
            yield return Until(() => SceneManager.GetActiveScene().name == SceneNames.Game && !ScreenFader.IsFading, 10f, "the Game scene");
            game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            campaign = UnityEngine.Object.FindFirstObjectByType<CampaignManager>();
            board = UnityEngine.Object.FindFirstObjectByType<BoardManager>();
            player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            input = UnityEngine.Object.FindFirstObjectByType<InputRouter>();
            enemies = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
            meter = UnityEngine.Object.FindFirstObjectByType<PowerMeter>();
            powerUps = UnityEngine.Object.FindFirstObjectByType<PowerUpManager>();
            yield return Until(() => campaign.Phase == CampaignPhase.Briefing, 5f, "the first briefing");
        }

        [TearDown]
        public void RestoreTime() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator Boot_ReachesTheFirstBriefingWithTheBoardAndHudReady()
        {
            Assert.That(campaign.Run.CurrentStageNumber, Is.EqualTo(1));
            Assert.That(board.Model, Is.Not.Null);
            Assert.That(board.CapturedPercentage, Is.Zero);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<GameHud>(), Is.Not.Null);
            Assert.That(game.Lives, Is.GreaterThan(0));
            yield break;
        }

        [UnityTest]
        public IEnumerator Swipes_SteerTheShipOutAndBack_AndTheCaptureRisesAndShows()
        {
            yield return StartStage();
            enemies.SetMovementSuspended(true);
            var start = board.PlayerCell;

            // Up the safe left edge, out across open space, and back down to the bottom edge.
            input.TrySelectLatchedDirection(CardinalDirection.Up);
            yield return Until(() => board.PlayerCell.Y >= start.Y + 10, 10f, "the ship to climb the edge");
            input.TrySelectLatchedDirection(CardinalDirection.Right);
            yield return Until(() => board.IsPlayerExposed, 5f, "the ship to leave safe territory");
            yield return Until(() => board.PlayerCell.X >= start.X + 6, 5f, "the trail to grow");
            Assert.That(board.Model.ActiveTrail.Count, Is.GreaterThan(0), "leaving safety draws a trail");
            input.TrySelectLatchedDirection(CardinalDirection.Down);
            yield return Until(() => !board.IsPlayerExposed, 10f, "the ship to reconnect");

            Assert.That(board.CapturedPercentage, Is.GreaterThan(0f), "closing the trail captures territory");
            Assert.That(board.Model.ActiveTrail.Count, Is.Zero);
            Assert.That(game.CurrentState, Is.EqualTo(GameplayState.Playing));
            yield return null;
            var hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
            var labels = hud.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            var shown = Array.Exists(labels, l => l.text == $"{board.CapturedPercentage:0.0}%");
            Assert.That(shown, Is.True, "the HUD shows the new capture percentage");
        }

        [UnityTest]
        public IEnumerator LosingALife_RespawnsTheShipSafelyAndPlayResumes()
        {
            yield return StartStage();
            yield return Until(() => !game.IsInvulnerable, 5f, "spawn protection to end");
            var lives = game.Lives;
            Assert.That(game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
            Assert.That(game.Lives, Is.EqualTo(lives - 1));
            Assert.That(game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.False, "one hit costs one life");
            yield return Until(() => game.CurrentState == GameplayState.Playing, 10f, "the respawn");
            Assert.That(board.IsPlayerExposed, Is.False, "the ship comes back on safe territory");
            Assert.That(board.Model.ActiveTrail.Count, Is.Zero);
            Assert.That(game.Lives, Is.EqualTo(lives - 1));
        }

        [UnityTest]
        public IEnumerator Pause_StopsTheGameUntilResumed()
        {
            yield return StartStage();
            input.TrySelectLatchedDirection(CardinalDirection.Up);
            yield return new WaitForSecondsRealtime(.2f);
            var pause = UnityEngine.Object.FindFirstObjectByType<PauseMenu>();
            Assert.That(pause.Open(), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            var frozenAt = player.transform.position;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(player.transform.position, Is.EqualTo(frozenAt), "nothing moves while paused");
            pause.Resume();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(player.transform.position, Is.Not.EqualTo(frozenAt), "and play carries on");
        }

        [UnityTest]
        public IEnumerator StoredShield_RunsForItsDurationThenEnds()
        {
            yield return StartStage();
            var slot = Field<PowerUpSlotModel>(powerUps, "slot");
            slot.Collect(PowerUpType.Shield);
            Assert.That(input.RequestAbility(), Is.True);
            Assert.That(powerUps.IsEffectActive(PowerUpType.Shield), Is.True);
            Assert.That(game.IsShieldActive, Is.True);
            var bubble = UnityEngine.Object.FindFirstObjectByType<ShieldBubble>();
            Assert.That(bubble, Is.Not.Null, "the bubble shows");
            var duration = powerUps.GetEffectRemaining(PowerUpType.Shield);
            yield return new WaitForSeconds(duration + .3f);
            Assert.That(powerUps.IsEffectActive(PowerUpType.Shield), Is.False);
            Assert.That(game.IsShieldActive, Is.False);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<ShieldBubble>(), Is.Null, "and hides when it ends");
        }

        [UnityTest]
        public IEnumerator PowerShot_DestroysTheAlienInFront_WithItsImpactEffects()
        {
            yield return StartStage();
            enemies.SetMovementSuspended(true);
            var target = enemies.ActiveEnemies[0];
            var ahead = player.transform.position + (Vector3)player.FacingDirection.ToVector2() * 3f;
            target.transform.position = ahead;
            typeof(EnemyController).GetField("movement", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, new EnemyMovementModel(ahead, Vector2.zero));
            Field<PowerMeterModel>(meter, "model").AddCapture(100f);
            Vector3? impact = null;
            meter.ShotImpact += at => impact = at;

            Assert.That(input.RequestPowerShot(), Is.True);
            yield return Until(() => !target.IsActiveEnemy, 3f, "the shot to hit");
            Assert.That(impact.HasValue, Is.True);
            var fx = UnityEngine.Object.FindFirstObjectByType<PowerShotPresenter>();
            Assert.That(fx.ShotsFired, Is.EqualTo(1));
            Assert.That(fx.Impacts, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "the hit-stop lets go");
        }

        [UnityTest]
        public IEnumerator Lasers_WarnThenFireThenCoolDown()
        {
            // Lasers join from a later stage; clear stages until one has them.
            SetCaptureTarget(0f);
            for (var guard = 0; guard < 6; guard++)
            {
                var laserManager = UnityEngine.Object.FindFirstObjectByType<LaserManager>();
                if (campaign.Phase == CampaignPhase.Briefing && laserManager != null && laserManager.ActiveEmitterCount > 0) break;
                yield return Advance();
            }
            var lasers = UnityEngine.Object.FindFirstObjectByType<LaserManager>();
            Assert.That(lasers.ActiveEmitterCount, Is.GreaterThan(0), "a stage with lasers was reached");
            SetCaptureTarget(75f);
            yield return StartStage();

            LaserEmitter emitter = null;
            foreach (var e in lasers.Emitters) if (e != null && e.isActiveAndEnabled) { emitter = e; break; }
            var warned = false; var fired = false; var cooled = false;
            emitter.WarningStarted += () => warned = true;
            emitter.FiringStarted += () => fired = warned;
            emitter.CooldownStarted += () => cooled = fired;
            game.SetShieldActive(true);
            yield return Until(() => cooled, 30f, "a full warning, firing and cooldown cycle");
            Assert.That(warned && fired && cooled, Is.True, "in that order");
        }

        [UnityTest]
        public IEnumerator FullCampaign_FiveStagesFourUpgradesTheBossDiesAndTheRunCompletes()
        {
            SetCaptureTarget(0f);
            var stagesPlayed = 0;
            var upgradesTaken = 0;
            var bossSequenceSeen = false;
            for (var guard = 0; guard < 40 && campaign.Phase != CampaignPhase.CampaignComplete; guard++)
            {
                switch (campaign.Phase)
                {
                    case CampaignPhase.Briefing:
                        stagesPlayed++;
                        Assert.That(campaign.StartStage(), Is.True);
                        yield return Until(() => campaign.Phase != CampaignPhase.Playing && campaign.Phase != CampaignPhase.Briefing, 5f, $"stage {stagesPlayed} to complete");
                        break;
                    case CampaignPhase.StageComplete:
                        if (campaign.IsBossStage)
                        {
                            var sequence = UnityEngine.Object.FindFirstObjectByType<BossDeathSequence>();
                            bossSequenceSeen = sequence.IsPlaying;
                            yield return Until(() => !sequence.IsPlaying, 5f, "the boss destruction sequence");
                            Assert.That(UnityEngine.Object.FindFirstObjectByType<BossController>().Body.gameObject.activeSelf, Is.False, "the core is gone");
                        }
                        Assert.That(campaign.ContinueAfterStageComplete(), Is.True);
                        yield return null;
                        break;
                    case CampaignPhase.UpgradeChoice:
                        Assert.That(campaign.ChooseUpgrade(0), Is.True);
                        upgradesTaken++;
                        yield return null;
                        break;
                    case CampaignPhase.GameOver:
                        Assert.Fail("the run ended in Game Over");
                        break;
                    default:
                        yield return null;
                        break;
                }
            }
            Assert.That(campaign.Phase, Is.EqualTo(CampaignPhase.CampaignComplete));
            Assert.That(stagesPlayed, Is.EqualTo(5), "four normal stages and the boss");
            Assert.That(upgradesTaken, Is.EqualTo(4), "one upgrade after each normal stage");
            Assert.That(bossSequenceSeen, Is.True, "the boss blew up on screen");
            var strip = UnityEngine.Object.FindFirstObjectByType<UpgradeStrip>();
            var upgrades = UnityEngine.Object.FindFirstObjectByType<UpgradeManager>();
            Assert.That(strip.BadgeCount, Is.EqualTo(upgrades.Run.Taken.Count), "every upgrade has a HUD badge");
        }

        // ---------------------------------------------------------------- helpers

        private IEnumerator StartStage()
        {
            Assert.That(campaign.StartStage(), Is.True);
            yield return Until(() => game.CurrentState == GameplayState.Playing, 5f, "the stage to start");
        }

        /// <summary>Moves the campaign one step: starts a briefed stage, continues a finished one, or takes an upgrade.</summary>
        private IEnumerator Advance()
        {
            switch (campaign.Phase)
            {
                case CampaignPhase.Briefing:
                    campaign.StartStage();
                    yield return Until(() => campaign.Phase != CampaignPhase.Playing && campaign.Phase != CampaignPhase.Briefing, 5f, "the stage to complete");
                    break;
                case CampaignPhase.StageComplete:
                    campaign.ContinueAfterStageComplete();
                    break;
                case CampaignPhase.UpgradeChoice:
                    campaign.ChooseUpgrade(0);
                    break;
            }
            yield return null;
        }

        private void SetCaptureTarget(float percentage) =>
            typeof(GameManager).GetField("captureTargetPercentage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, percentage);

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end)
                    Assert.Fail($"Timed out waiting for {what} (scene {SceneManager.GetActiveScene().name}, fading {ScreenFader.IsFading}, time scale {Time.timeScale}, frame {Time.frameCount}).");
                yield return null;
            }
        }
    }
}
