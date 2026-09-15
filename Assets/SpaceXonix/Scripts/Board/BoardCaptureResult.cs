namespace SpaceXonix.Board
{
    public readonly struct BoardCaptureResult
    {
        public BoardCaptureResult(int regionCellsCaptured, int trailCellsCommitted, bool regionCaptured, float capturedPercentage)
        {
            RegionCellsCaptured = regionCellsCaptured;
            TrailCellsCommitted = trailCellsCommitted;
            RegionCaptured = regionCaptured;
            CapturedPercentage = capturedPercentage;
        }

        public int RegionCellsCaptured { get; }
        public int TrailCellsCommitted { get; }
        public bool RegionCaptured { get; }
        public float CapturedPercentage { get; }
    }
}
