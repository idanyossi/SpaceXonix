using System;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using SpaceXonix.Hazards;
using SpaceXonix.Power;
using SpaceXonix.PowerUps;
using SpaceXonix.Scoring;
using UnityEngine;

namespace SpaceXonix.Campaign
{
    public enum CampaignPhase
    {
        Briefing,
        Playing,
        StageComplete,
        UpgradeChoice,
        GameOver,
        NormalStagesCleared,
        CampaignComplete
    }

    /// <summary>Drives the normal stages and the final boss stage inside Game.unity by resetting the scene's gameplay systems in place.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class CampaignManager : MonoBehaviour
    {
        [SerializeField] private CampaignDefinition campaign;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private LaserManager laserManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private PowerMeter powerMeter;
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private UpgradeManager upgradeManager;
        [SerializeField] private StageModifierManager modifierManager;
        [SerializeField] private SpaceXonix.Boss.BossController bossController;

        private CampaignRunModel run;
        private bool runStarted;

        public CampaignRunModel Run => run;
        public StageDefinition CurrentStage => run == null
            ? null
            : run.IsBossStage ? campaign.bossStage : campaign.normalStages[run.CurrentStageIndex];
        public bool IsBossStage => run != null && run.IsBossStage;
        public float StageCompletedPercentage { get; private set; }
        public StageModifierDefinition CurrentModifier => modifierManager != null ? modifierManager.Current : null;
        public float ModifierScoreMultiplier => modifierManager != null ? modifierManager.ScoreMultiplier : 1f;
        public string DescribeModifier() => modifierManager != null ? modifierManager.Describe() : "none";
        public CampaignPhase Phase
        {
            get
            {
                if (upgradeManager != null && upgradeManager.HasOffer) return CampaignPhase.UpgradeChoice;
                if (run != null && run.CampaignComplete) return CampaignPhase.CampaignComplete;
                // Once the normal stages are cleared the run either moves on to the boss or ends here.
                if (run != null && run.NormalStagesCleared && !run.IsBossStage) return CampaignPhase.NormalStagesCleared;
                switch (gameManager.CurrentState)
                {
                    case GameplayState.Briefing: return CampaignPhase.Briefing;
                    case GameplayState.StageComplete: return CampaignPhase.StageComplete;
                    case GameplayState.GameOver: return CampaignPhase.GameOver;
                    default: return CampaignPhase.Playing;
                }
            }
        }
        public event Action<StageDefinition> StageLoaded;
        public event Action NormalStagesCleared;
        public event Action CampaignCompleted;

        private void Start()
        {
            if (!Initialize()) return;
            LoadCurrentStage();
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.StageCompleted -= OnStageCompleted;
        }

        public bool Initialize()
        {
            if (run != null) return true;
            if (campaign == null || campaign.normalStages == null || campaign.normalStages.Length == 0 || gameManager == null)
            {
                Debug.LogError("CampaignManager requires a CampaignDefinition with at least one stage and a GameManager.", this);
                enabled = false;
                return false;
            }
            run = new CampaignRunModel(campaign.normalStages.Length, campaign.HasBossStage);
            gameManager.StageCompleted += OnStageCompleted;
            return true;
        }

        public void LoadCurrentStage()
        {
            if (run == null) return;
            var stage = CurrentStage;
            StageCompletedPercentage = 0f;
            if (powerUpManager != null) powerUpManager.PrepareForStage();
            if (powerMeter != null) powerMeter.PrepareForStage();
            if (enemyManager != null) enemyManager.DespawnAll();
            if (bossController != null) bossController.Deactivate();
            // Lives carry across stages; only the first stage of a run starts from the configured total.
            gameManager.BeginStage(runStarted ? Mathf.Max(1, gameManager.Lives) : 0);
            runStarted = true;
            // The modifier is rolled before the stage is built so its multipliers reach the lasers and aliens as they spawn.
            if (modifierManager != null) modifierManager.SelectFor(stage);
            if (laserManager != null) laserManager.ConfigureStage(stage.lasers);
            if (enemyManager != null) enemyManager.SpawnAll(stage.enemySpawns);
            if (modifierManager != null) modifierManager.SpawnExtras(stage);
            // The boss stage has no standard aliens, so Arena Tilt would have nothing to act on.
            if (powerUpManager != null) powerUpManager.SetTypeExcluded(PowerUps.PowerUpType.ArenaTilt, stage.IsBossStage);
            if (bossController != null && stage.IsBossStage) bossController.Activate(stage.boss);
            if (upgradeManager != null) upgradeManager.ApplyToSystems();
            if (scoreManager != null) scoreManager.ResetStageStatistics();
            StageLoaded?.Invoke(stage);
        }

        public bool StartStage() => Phase == CampaignPhase.Briefing && gameManager.StartStagePlay();

        /// <summary>Leaves Stage Complete: offers the run upgrade choice when one is available, otherwise continues.</summary>
        public bool ContinueAfterStageComplete()
        {
            if (Phase != CampaignPhase.StageComplete) return false;
            // Beating the boss ends the run, so there is no next stage to spend an upgrade on.
            if (!run.IsBossStage && upgradeManager != null && upgradeManager.BuildOffer()) return true;
            return AdvancePastCompletedStage();
        }

        /// <summary>Takes one of the offered upgrades and moves on to the next stage.</summary>
        public bool ChooseUpgrade(int index)
        {
            if (Phase != CampaignPhase.UpgradeChoice || upgradeManager == null) return false;
            if (!upgradeManager.Take(index)) return false;
            return AdvancePastCompletedStage();
        }

        private bool AdvancePastCompletedStage()
        {
            var wasBossStage = run.IsBossStage;
            if (run.CompleteCurrentStage())
            {
                LoadCurrentStage();
                return true;
            }
            if (wasBossStage) CampaignCompleted?.Invoke();
            else NormalStagesCleared?.Invoke();
            return true;
        }

        public void RetryCampaign()
        {
            if (run == null) return;
            run.Reset();
            runStarted = false;
            if (upgradeManager != null) upgradeManager.ResetRun();
            if (scoreManager != null) scoreManager.ResetScore();
            if (powerMeter != null) powerMeter.ResetMeter();
            if (powerUpManager != null) { powerUpManager.ResetForCampaign(); powerUpManager.ClearExcludedTypes(); }
            LoadCurrentStage();
        }

        private void OnStageCompleted() => StageCompletedPercentage = boardManager != null ? boardManager.CapturedPercentage : 0f;
    }
}
