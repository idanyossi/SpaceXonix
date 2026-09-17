using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceXonix.Scoring
{
    public readonly struct CaptureScoreAward
    {
        public CaptureScoreAward(float percentageGained, float captureMultiplier, float bonusMultiplier, int points)
        {
            PercentageGained = percentageGained;
            CaptureMultiplier = captureMultiplier;
            BonusMultiplier = bonusMultiplier;
            Points = points;
        }

        public float PercentageGained { get; }
        public float CaptureMultiplier { get; }
        public float BonusMultiplier { get; }
        public int Points { get; }
    }

    /// <summary>Run score: 100 points per captured percent, scaled by the single-capture tier and any stage bonus.</summary>
    public sealed class ScoreModel
    {
        private readonly float pointsPerPercent;
        private readonly CaptureMultiplierTier[] tiers;

        public ScoreModel(float pointsPerPercent, IReadOnlyList<CaptureMultiplierTier> largeCaptureTiers)
        {
            if (pointsPerPercent < 0f) throw new ArgumentOutOfRangeException(nameof(pointsPerPercent));
            this.pointsPerPercent = pointsPerPercent;
            tiers = new CaptureMultiplierTier[largeCaptureTiers?.Count ?? 0];
            for (var i = 0; i < tiers.Length; i++) tiers[i] = largeCaptureTiers[i];
            Array.Sort(tiers, (a, b) => a.minimumPercentage.CompareTo(b.minimumPercentage));
        }

        public int Score { get; private set; }
        public float LargestCapturePercentage { get; private set; }
        public float BonusMultiplier { get; private set; } = 1f;

        public float GetCaptureMultiplier(float percentageGained)
        {
            var multiplier = 1f;
            foreach (var tier in tiers)
            {
                if (percentageGained < tier.minimumPercentage) break;
                multiplier = tier.multiplier;
            }
            return multiplier;
        }

        public void SetBonusMultiplier(float multiplier)
        {
            if (multiplier <= 0f) throw new ArgumentOutOfRangeException(nameof(multiplier));
            BonusMultiplier = multiplier;
        }

        public CaptureScoreAward AddCapture(float percentageGained)
        {
            if (percentageGained <= 0f) return new CaptureScoreAward(0f, 1f, BonusMultiplier, 0);
            var captureMultiplier = GetCaptureMultiplier(percentageGained);
            var points = Mathf.RoundToInt(pointsPerPercent * percentageGained * captureMultiplier * BonusMultiplier);
            Score += points;
            if (percentageGained > LargestCapturePercentage) LargestCapturePercentage = percentageGained;
            return new CaptureScoreAward(percentageGained, captureMultiplier, BonusMultiplier, points);
        }

        public void Reset()
        {
            Score = 0;
            LargestCapturePercentage = 0f;
            BonusMultiplier = 1f;
        }
    }
}
