using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Campaign;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using SpaceXonix.Hazards;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using SpaceXonix.Scoring;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class CampaignTests
    {
        [Test]
        public void RunModel_AdvancesThroughNormalStagesThenMarksCleared()
        {
            var run = new CampaignRunModel(4);
            Assert.That(run.CurrentStageNumber, Is.EqualTo(1));
            Assert.That(run.HighestStageReached, Is.EqualTo(1));
            for (var expected = 2; expected <= 4; expected++)
            {
                Assert.That(run.CompleteCurrentStage(), Is.True);
                Assert.That(run.CurrentStageNumber, Is.EqualTo(expected));
                Assert.That(run.HighestStageReached, Is.EqualTo(expected));
            }
            Assert.That(run.HasNextNormalStage, Is.False);
            Assert.That(run.CompleteCurrentStage(), Is.False);
            Assert.That(run.NormalStagesCleared, Is.True);
            Assert.That(run.CompleteCurrentStage(), Is.False);
        }

        [Test]
        public void RunModel_ResetReturnsToStageOne()
        {
            var run = new CampaignRunModel(4);
            run.CompleteCurrentStage();
            run.CompleteCurrentStage();
            run.Reset();
            Assert.That(run.CurrentStageNumber, Is.EqualTo(1));
            Assert.That(run.HighestStageReached, Is.EqualTo(1));
            Assert.That(run.NormalStagesCleared, Is.False);
        }

        [Test]
        public void ScoreModel_StageLargestCaptureResetsWithoutClearingRunScore()
        {
            var model = new ScoreModel(100f, null);
            model.AddCapture(12f);
            model.ResetStageStatistics();
            model.AddCapture(3f);
            Assert.That(model.StageLargestCapturePercentage, Is.EqualTo(3f));
            Assert.That(model.LargestCapturePercentage, Is.EqualTo(12f));
            Assert.That(model.Score, Is.EqualTo(1500));
        }

        [Test]
        public void StageDefinition_ListsDistinctEnemyTypesInOrder()
        {
            using (var fixture = new Fixture())
            {
                var types = fixture.Stages[3].GetEnemyTypes(new List<EnemyType>());
                Assert.That(types, Is.EqualTo(new[] { EnemyType.BasicBouncer, EnemyType.Volatile }));
            }
        }

        [Test]
        public void LoadingStage_EntersBriefingWithFreshBoardEnemiesAndLasers()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Campaign.Phase, Is.EqualTo(CampaignPhase.Briefing));
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Briefing));
                Assert.That(fixture.Game.Lives, Is.EqualTo(3));
                Assert.That(fixture.Input.GameplayInputEnabled, Is.False);
                Assert.That(fixture.Player.MovementEnabled, Is.False);
                Assert.That(fixture.Board.CapturedPercentage, Is.Zero);
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(new GridCoordinate(0, 1)));
                Assert.That(fixture.Enemies.ActiveEnemies.Count, Is.EqualTo(1));
                Assert.That(fixture.Lasers.ActiveEmitterCount, Is.Zero);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.False);

                var enemy = fixture.Enemies.ActiveEnemies[0];
                var before = enemy.transform.position;
                enemy.AdvanceMovement(.5f);
                Assert.That(enemy.transform.position, Is.EqualTo(before), "enemies hold still during the briefing");
            }
        }

        [Test]
        public void StartStage_EnablesPlayOnlyFromBriefing()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Campaign.StartStage(), Is.True);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Input.GameplayInputEnabled, Is.True);
                Assert.That(fixture.Player.ControlState, Is.EqualTo(PlayerControlState.SafeIdle));
                Assert.That(fixture.Campaign.StartStage(), Is.False);
            }
        }

        [Test]
        public void CompletingStage_ContinuesToNextStageWithResetBoardLivesAndCarriedScore()
        {
            using (var fixture = new Fixture())
            {
                fixture.Campaign.StartStage();
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.Laser), Is.True);
                fixture.CompleteRespawn();
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
                fixture.CompleteCurrentStageByCapture();
                var score = fixture.Score.Score;
                Assert.That(score, Is.GreaterThan(0));
                Assert.That(fixture.Campaign.Phase, Is.EqualTo(CampaignPhase.StageComplete));
                Assert.That(fixture.Campaign.StageCompletedPercentage, Is.GreaterThanOrEqualTo(20f));
                Assert.That(fixture.Score.StageLargestCapturePercentage, Is.GreaterThan(0f));

                Assert.That(fixture.Campaign.ContinueAfterStageComplete(), Is.True);
                Assert.That(fixture.Campaign.Run.CurrentStageNumber, Is.EqualTo(2));
                Assert.That(fixture.Campaign.CurrentStage, Is.SameAs(fixture.Stages[1]));
                Assert.That(fixture.Campaign.Phase, Is.EqualTo(CampaignPhase.Briefing));
                Assert.That(fixture.Game.Lives, Is.EqualTo(3));
                Assert.That(fixture.Board.CapturedPercentage, Is.Zero);
                Assert.That(fixture.Board.Model.ActiveTrail, Is.Empty);
                Assert.That(fixture.Board.PlayerCell, Is.EqualTo(new GridCoordinate(0, 1)));
                Assert.That(fixture.Score.Score, Is.EqualTo(score));
                Assert.That(fixture.Score.StageLargestCapturePercentage, Is.Zero);
                Assert.That(fixture.Enemies.ActiveEnemies.Count, Is.EqualTo(2));
                Assert.That(fixture.Lasers.ActiveEmitterCount, Is.EqualTo(1));
                var emitter = fixture.Lasers.Emitters[0];
                Assert.That(emitter.Axis, Is.EqualTo(LaserAxis.Horizontal));
                Assert.That(fixture.Board.WorldToGrid(emitter.transform.position).Y, Is.EqualTo(40));

                Assert.That(fixture.Campaign.StartStage(), Is.True);
                Assert.That(fixture.Player.AdvanceMovement(0f), Is.False);
                fixture.Input.TrySelectDirection(CardinalDirection.Up);
                Assert.That(fixture.Player.AdvanceMovement(.05f), Is.True, "player moves normally on the new stage");
            }
        }

        [Test]
        public void StageWithFewerLasers_HidesUnusedEmitters()
        {
            using (var fixture = new Fixture())
            {
                fixture.AdvanceToStage(3);
                Assert.That(fixture.Lasers.ActiveEmitterCount, Is.EqualTo(2));
                fixture.CompleteCurrentStageByCapture();
                fixture.Campaign.ContinueAfterStageComplete();
                Assert.That(fixture.Campaign.Run.CurrentStageNumber, Is.EqualTo(4));
                Assert.That(fixture.Lasers.ActiveEmitterCount, Is.Zero);
                Assert.That(fixture.Lasers.Emitters.Length, Is.EqualTo(2));
            }
        }

        [Test]
        public void ClearingFinalNormalStage_MarksNormalStagesCleared()
        {
            using (var fixture = new Fixture())
            {
                var cleared = 0;
                fixture.Campaign.NormalStagesCleared += () => cleared++;
                fixture.AdvanceToStage(4);
                fixture.CompleteCurrentStageByCapture();
                Assert.That(fixture.Campaign.ContinueAfterStageComplete(), Is.True);
                Assert.That(cleared, Is.EqualTo(1));
                Assert.That(fixture.Campaign.Phase, Is.EqualTo(CampaignPhase.NormalStagesCleared));
                Assert.That(fixture.Campaign.ContinueAfterStageComplete(), Is.False);
            }
        }

        [Test]
        public void GameOver_RetryRestartsStageOneWithZeroScore()
        {
            using (var fixture = new Fixture())
            {
                fixture.AdvanceToStage(2);
                Assert.That(fixture.Score.Score, Is.GreaterThan(0));
                Assert.That(fixture.Campaign.StartStage(), Is.True);
                for (var i = 0; i < 3; i++)
                {
                    Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
                    if (fixture.Game.CurrentState == GameplayState.Respawning) fixture.CompleteRespawn();
                }
                Assert.That(fixture.Campaign.Phase, Is.EqualTo(CampaignPhase.GameOver));
                Assert.That(fixture.Campaign.Run.HighestStageReached, Is.EqualTo(2));

                fixture.Campaign.RetryCampaign();
                Assert.That(fixture.Campaign.Run.CurrentStageNumber, Is.EqualTo(1));
                Assert.That(fixture.Campaign.Phase, Is.EqualTo(CampaignPhase.Briefing));
                Assert.That(fixture.Score.Score, Is.Zero);
                Assert.That(fixture.Game.Lives, Is.EqualTo(3));
                Assert.That(fixture.Enemies.ActiveEnemies.Count, Is.EqualTo(1));
                Assert.That(fixture.Campaign.StartStage(), Is.True);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
            }
        }

        [Test]
        public void LasersDoNotCycleDuringBriefing()
        {
            using (var fixture = new Fixture())
            {
                fixture.AdvanceToStage(2);
                var emitter = fixture.Lasers.Emitters[0];
                var remaining = emitter.TimeRemaining;
                fixture.Lasers.Tick(1f);
                Assert.That(emitter.TimeRemaining, Is.EqualTo(remaining));
                fixture.Campaign.StartStage();
                fixture.Lasers.Tick(.5f);
                Assert.That(emitter.TimeRemaining, Is.LessThan(remaining));
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("CampaignFixture");
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            public readonly BoardManager Board;
            public readonly InputRouter Input;
            public readonly PlayerController Player;
            public readonly GameManager Game;
            public readonly EnemyManager Enemies;
            public readonly LaserManager Lasers;
            public readonly ScoreManager Score;
            public readonly CampaignManager Campaign;
            public readonly StageDefinition[] Stages = new StageDefinition[4];

            public Fixture()
            {
                root.SetActive(false);
                Board = root.AddComponent<BoardManager>();
                Board.Initialize();
                Input = root.AddComponent<InputRouter>();
                var playerObject = Own(new GameObject("Player"));
                Player = playerObject.AddComponent<PlayerController>();
                Invoke(Player, "Awake");
                Game = root.AddComponent<GameManager>();
                Set(Game, "inputRouter", Input); Set(Game, "playerController", Player); Set(Game, "boardManager", Board);
                var pool = root.AddComponent<PoolService>();
                Enemies = root.AddComponent<EnemyManager>();
                Set(Enemies, "boardManager", Board); Set(Enemies, "gameManager", Game);
                Set(Enemies, "playerController", Player); Set(Enemies, "poolService", pool);
                Lasers = root.AddComponent<LaserManager>();
                Set(Lasers, "boardManager", Board); Set(Lasers, "gameManager", Game); Set(Lasers, "poolService", pool);
                Score = root.AddComponent<ScoreManager>();
                var scoring = Own(ScriptableObject.CreateInstance<ScoringDefinition>());
                Set(Score, "boardManager", Board); Set(Score, "definition", scoring);

                var basicPrefab = Own(new GameObject("BasicPrefab")); basicPrefab.AddComponent<BasicBouncer>(); basicPrefab.SetActive(false);
                var volatilePrefab = Own(new GameObject("VolatilePrefab")); volatilePrefab.AddComponent<VolatileEnemy>(); volatilePrefab.SetActive(false);
                var basic = EnemyDefinitionOf(EnemyType.BasicBouncer);
                var volatileDefinition = EnemyDefinitionOf(EnemyType.Volatile);
                var horizontal = LaserDefinitionOf(LaserAxis.Horizontal);
                var vertical = LaserDefinitionOf(LaserAxis.Vertical);

                Stages[0] = Stage(1, new[] { Spawn(basicPrefab, basic, 20, 50) }, new LaserPlacement[0]);
                Stages[1] = Stage(2, new[] { Spawn(basicPrefab, basic, 20, 50), Spawn(basicPrefab, basic, 30, 70) },
                    new[] { new LaserPlacement { definition = horizontal, line = 40 } });
                Stages[2] = Stage(3, new[] { Spawn(basicPrefab, basic, 20, 50) },
                    new[] { new LaserPlacement { definition = horizontal, line = 30 }, new LaserPlacement { definition = vertical, line = 26 } });
                Stages[3] = Stage(4, new[] { Spawn(basicPrefab, basic, 20, 50), Spawn(volatilePrefab, volatileDefinition, 35, 80), Spawn(basicPrefab, basic, 10, 60) },
                    new LaserPlacement[0]);
                var campaign = Own(ScriptableObject.CreateInstance<CampaignDefinition>());
                campaign.normalStages = Stages;

                Campaign = root.AddComponent<CampaignManager>();
                Set(Campaign, "campaign", campaign); Set(Campaign, "gameManager", Game); Set(Campaign, "boardManager", Board);
                Set(Campaign, "enemyManager", Enemies); Set(Campaign, "laserManager", Lasers); Set(Campaign, "scoreManager", Score);

                Invoke(Game, "Awake");
                root.SetActive(true);
                Invoke(Score, "Awake");
                Invoke(Score, "OnEnable");
                Invoke(Game, "Start");
                Invoke(Campaign, "Start");
            }

            public void AdvanceToStage(int stageNumber)
            {
                while (Campaign.Run.CurrentStageNumber < stageNumber)
                {
                    CompleteCurrentStageByCapture();
                    Campaign.ContinueAfterStageComplete();
                }
                Assert.That(Campaign.Run.CurrentStageNumber, Is.EqualTo(stageNumber));
                Assert.That(Campaign.Phase, Is.EqualTo(CampaignPhase.Briefing));
            }

            public void CompleteCurrentStageByCapture()
            {
                if (Game.CurrentState == GameplayState.Briefing) Campaign.StartStage();
                Set(Game, "captureTargetPercentage", 20f);
                Input.TrySelectDirection(CardinalDirection.Up);
                for (var i = 0; i < 2000 && Board.PlayerCell.Y < 50; i++) Player.AdvanceMovement(.02f);
                Input.TrySelectDirection(CardinalDirection.Right);
                var exposed = false;
                for (var i = 0; i < 4000; i++)
                {
                    Player.AdvanceMovement(.02f);
                    if (Board.IsPlayerExposed && !exposed) { exposed = true; Input.ReleaseDirection(); }
                    if (exposed && !Board.IsPlayerExposed) break;
                }
                Input.ReleaseDirection();
                Assert.That(Board.CapturedPercentage, Is.GreaterThanOrEqualTo(20f));
                Assert.That(Game.TryCompleteStage(), Is.True);
            }

            public void CompleteRespawn()
            {
                Assert.That((bool)Invoke(Game, "CompleteRespawn"), Is.True);
                Set(Game, "failureGateReleaseFrame", Time.frameCount - 1);
                Invoke(Game, "LateUpdate");
                Game.AdvanceLifecycle(5f);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }

            private StageDefinition Stage(int number, EnemySpawnRequest[] spawns, LaserPlacement[] lasers)
            {
                var stage = Own(ScriptableObject.CreateInstance<StageDefinition>());
                stage.stageNumber = number; stage.stageName = "Test " + number; stage.enemySpawns = spawns; stage.lasers = lasers;
                return stage;
            }

            private static EnemySpawnRequest Spawn(GameObject prefab, EnemyDefinition definition, int column, int row) =>
                new EnemySpawnRequest { prefab = prefab, definition = definition, column = column, row = row, direction = Vector2.one };

            private EnemyDefinition EnemyDefinitionOf(EnemyType type)
            {
                var definition = Own(ScriptableObject.CreateInstance<EnemyDefinition>());
                definition.type = type;
                return definition;
            }

            private LaserDefinition LaserDefinitionOf(LaserAxis axis)
            {
                var definition = Own(ScriptableObject.CreateInstance<LaserDefinition>());
                definition.axis = axis;
                return definition;
            }

            private T Own<T>(T item) where T : UnityEngine.Object
            {
                owned.Add(item);
                return item;
            }

            private static void Set(object target, string name, object value) => SetField(target, name, value);

            private static void SetField(object target, string name, object value) =>
                target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

            private static object Invoke(object target, string name) =>
                target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        }
    }
}
