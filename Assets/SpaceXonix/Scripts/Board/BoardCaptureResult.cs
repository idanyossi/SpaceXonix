namespace SpaceXonix.Board
{
    public readonly struct BoardCaptureResult
    {
        public BoardCaptureResult(int regionCellsCaptured, int trailCellsCommitted, bool regionCaptured, float capturedPercentage,
            float percentageGained)
        {
            RegionCellsCaptured = regionCellsCaptured;
            TrailCellsCommitted = trailCellsCommitted;
            RegionCaptured = regionCaptured;
            CapturedPercentage = capturedPercentage;
            PercentageGained = percentageGained;
        }

        public int RegionCellsCaptured { get; }
        public int TrailCellsCommitted { get; }
        public bool RegionCaptured { get; }
        public float CapturedPercentage { get; }
        /// <summary>Playable area newly made safe by this capture (region plus committed trail).</summary>
        public float PercentageGained { get; }
    }
}
