using System;

namespace SpaceXonix.Campaign
{
    /// <summary>Run progression through the normal stages and the optional boss stage. Stage numbers are 1-based.</summary>
    public sealed class CampaignRunModel
    {
        public CampaignRunModel(int normalStageCount, bool hasBossStage = false)
        {
            if (normalStageCount < 1) throw new ArgumentOutOfRangeException(nameof(normalStageCount));
            NormalStageCount = normalStageCount;
            HasBossStage = hasBossStage;
            Reset();
        }

        public int NormalStageCount { get; }
        public bool HasBossStage { get; }
        public int CurrentStageIndex { get; private set; }
        /// <summary>
        /// The boss is numbered one past the normal stages, so a 4-stage campaign ends on stage 5.
        /// A finished run keeps reporting that last number rather than falling back to the stage index.
        /// </summary>
        public int CurrentStageNumber =>
            IsBossStage || (CampaignComplete && HasBossStage) ? NormalStageCount + 1 : CurrentStageIndex + 1;
        public int TotalStageCount => NormalStageCount + (HasBossStage ? 1 : 0);
        public int HighestStageReached { get; private set; }
        public bool NormalStagesCleared { get; private set; }
        public bool IsBossStage { get; private set; }
        public bool CampaignComplete { get; private set; }
        public bool HasNextNormalStage => CurrentStageIndex + 1 < NormalStageCount;

        /// <summary>Records clearing the current stage and moves to the next one when it exists.</summary>
        public bool CompleteCurrentStage()
        {
            if (CampaignComplete) return false;
            if (IsBossStage)
            {
                IsBossStage = false;
                CampaignComplete = true;
                return false;
            }
            if (NormalStagesCleared) return false;
            if (!HasNextNormalStage)
            {
                NormalStagesCleared = true;
                if (!HasBossStage) return false;
                IsBossStage = true;
                HighestStageReached = Math.Max(HighestStageReached, CurrentStageNumber);
                return true;
            }
            CurrentStageIndex++;
            HighestStageReached = Math.Max(HighestStageReached, CurrentStageNumber);
            return true;
        }

        public void Reset()
        {
            CurrentStageIndex = 0;
            HighestStageReached = 1;
            NormalStagesCleared = false;
            IsBossStage = false;
            CampaignComplete = false;
        }
    }
}
