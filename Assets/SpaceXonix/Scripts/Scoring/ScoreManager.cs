using System;
using SpaceXonix.Board;
using UnityEngine;

namespace SpaceXonix.Scoring
{
    public sealed class ScoreManager : MonoBehaviour
    {
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private ScoringDefinition definition;

        private ScoreModel model;

        public int Score => model?.Score ?? 0;
        public float LargestCapturePercentage => model?.LargestCapturePercentage ?? 0f;
        public float StageLargestCapturePercentage => model?.StageLargestCapturePercentage ?? 0f;
        public float BonusMultiplier => model?.BonusMultiplier ?? 1f;
        public bool HasLastAward { get; private set; }
        public CaptureScoreAward LastAward { get; private set; }
        public event Action<int> ScoreChanged;
        public event Action<CaptureScoreAward> CaptureScored;

        private void Awake()
        {
            if (definition == null)
            {
                Debug.LogError("ScoreManager requires a ScoringDefinition.", this);
                enabled = false;
                return;
            }
            model = new ScoreModel(definition.pointsPerCapturedPercent, definition.largeCaptureTiers);
        }

        private void OnEnable()
        {
            if (boardManager != null) boardManager.CaptureCompleted += OnCaptureCompleted;
        }

        private void OnDisable()
        {
            if (boardManager != null) boardManager.CaptureCompleted -= OnCaptureCompleted;
        }

        public void SetBonusMultiplier(float multiplier) => model?.SetBonusMultiplier(multiplier);

        public void ResetStageStatistics()
        {
            model?.ResetStageStatistics();
            HasLastAward = false;
        }

        public void ResetScore()
        {
            if (model == null) return;
            model.Reset();
            HasLastAward = false;
            LastAward = default;
            ScoreChanged?.Invoke(model.Score);
        }

        private void OnCaptureCompleted(BoardCaptureResult result)
        {
            var award = model.AddCapture(result.PercentageGained);
            if (award.Points <= 0) return;
            LastAward = award;
            HasLastAward = true;
            CaptureScored?.Invoke(award);
            ScoreChanged?.Invoke(model.Score);
        }
    }
}
