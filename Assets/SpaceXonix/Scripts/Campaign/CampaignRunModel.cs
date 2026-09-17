using System;

namespace SpaceXonix.Campaign
{
    /// <summary>Run progression through the normal stages. Stage numbers are 1-based.</summary>
    public sealed class CampaignRunModel
    {
        public CampaignRunModel(int normalStageCount)
        {
            if (normalStageCount < 1) throw new ArgumentOutOfRangeException(nameof(normalStageCount));
            NormalStageCount = normalStageCount;
            Reset();
        }

        public int NormalStageCount { get; }
        public int CurrentStageIndex { get; private set; }
        public int CurrentStageNumber => CurrentStageIndex + 1;
        public int HighestStageReached { get; private set; }
        public bool NormalStagesCleared { get; private set; }
        public bool HasNextNormalStage => CurrentStageIndex + 1 < NormalStageCount;

        /// <summary>Records clearing the current stage and moves to the next one when it exists.</summary>
        public bool CompleteCurrentStage()
        {
            if (NormalStagesCleared) return false;
            if (!HasNextNormalStage)
            {
                NormalStagesCleared = true;
                return false;
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
        }
    }
}
