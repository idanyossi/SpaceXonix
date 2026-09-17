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
        GameOver,
        NormalStagesCleared
    }

    /// <summary>Drives stages 1–4 inside Game.unity by resetting the scene's gameplay systems in place.</summary>
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

        private CampaignRunModel run;

        public CampaignRunModel Run => run;
        public StageDefinition CurrentStage => run != null ? campaign.normalStages[run.CurrentStageIndex] : null;
        public float StageCompletedPercentage { get; private set; }
        public CampaignPhase Phase
        {
            get
            {
                if (run != null && run.NormalStagesCleared) return CampaignPhase.NormalStagesCleared;
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
            run = new CampaignRunModel(campaign.normalStages.Length);
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
            gameManager.BeginStage();
            if (laserManager != null) laserManager.ConfigureStage(stage.lasers);
            if (enemyManager != null) enemyManager.SpawnAll(stage.enemySpawns);
            if (scoreManager != null) scoreManager.ResetStageStatistics();
            StageLoaded?.Invoke(stage);
        }

        public bool StartStage() => Phase == CampaignPhase.Briefing && gameManager.StartStagePlay();

        public bool ContinueAfterStageComplete()
        {
            if (Phase != CampaignPhase.StageComplete) return false;
            if (run.CompleteCurrentStage())
            {
                LoadCurrentStage();
                return true;
            }
            NormalStagesCleared?.Invoke();
            return true;
        }

        public void RetryCampaign()
        {
            if (run == null) return;
            run.Reset();
            if (scoreManager != null) scoreManager.ResetScore();
            if (powerMeter != null) powerMeter.ResetMeter();
            if (powerUpManager != null) powerUpManager.ResetForCampaign();
            LoadCurrentStage();
        }

        private void OnStageCompleted() => StageCompletedPercentage = boardManager != null ? boardManager.CapturedPercentage : 0f;
    }
}
