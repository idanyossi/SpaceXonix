using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Scoring;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class ScoreModelTests
    {
        private static ScoreModel CreateDefault() => new ScoreModel(100f, new[]
        {
            new CaptureMultiplierTier(10f, 2f),
            new CaptureMultiplierTier(5f, 1.5f),
            new CaptureMultiplierTier(15f, 3f)
        });

        [TestCase(0.01f, 1f)]
        [TestCase(4.99f, 1f)]
        [TestCase(5f, 1.5f)]
        [TestCase(9.99f, 1.5f)]
        [TestCase(10f, 2f)]
        [TestCase(14.99f, 2f)]
        [TestCase(15f, 3f)]
        [TestCase(60f, 3f)]
        public void CaptureMultiplier_MatchesGddTiersAtBoundaries(float percentage, float expected)
        {
            Assert.That(CreateDefault().GetCaptureMultiplier(percentage), Is.EqualTo(expected));
        }

        [TestCase(2f, 200)]
        [TestCase(5f, 750)]
        [TestCase(12f, 2400)]
        [TestCase(20f, 6000)]
        public void AddCapture_Awards100PointsPerPercentTimesMultiplier(float percentage, int expectedPoints)
        {
            var model = CreateDefault();
            var award = model.AddCapture(percentage);
            Assert.That(award.Points, Is.EqualTo(expectedPoints));
            Assert.That(model.Score, Is.EqualTo(expectedPoints));
        }

        [Test]
        public void SequentialCaptures_AccumulateAndTrackLargestSingleCapture()
        {
            var model = CreateDefault();
            model.AddCapture(3f);
            model.AddCapture(11f);
            model.AddCapture(1f);
            Assert.That(model.Score, Is.EqualTo(300 + 2200 + 100));
            Assert.That(model.LargestCapturePercentage, Is.EqualTo(11f));
        }

        [Test]
        public void ManySmallCaptures_ScoreLessThanOneEqualLargeCapture()
        {
            var trimming = CreateDefault();
            for (var i = 0; i < 5; i++) trimming.AddCapture(3f);
            var bold = CreateDefault();
            bold.AddCapture(15f);
            Assert.That(bold.Score, Is.GreaterThan(trimming.Score));
        }

        [Test]
        public void BonusMultiplier_StacksWithCaptureMultiplier()
        {
            var model = CreateDefault();
            model.SetBonusMultiplier(1.2f);
            var award = model.AddCapture(10f);
            Assert.That(award.CaptureMultiplier, Is.EqualTo(2f));
            Assert.That(award.BonusMultiplier, Is.EqualTo(1.2f));
            Assert.That(award.Points, Is.EqualTo(2400));
        }

        [Test]
        public void NonPositiveCapture_AwardsNothing()
        {
            var model = CreateDefault();
            Assert.That(model.AddCapture(0f).Points, Is.Zero);
            Assert.That(model.AddCapture(-3f).Points, Is.Zero);
            Assert.That(model.Score, Is.Zero);
            Assert.That(model.LargestCapturePercentage, Is.Zero);
        }

        [Test]
        public void Reset_ClearsScoreLargestCaptureAndBonus()
        {
            var model = CreateDefault();
            model.SetBonusMultiplier(1.15f);
            model.AddCapture(8f);
            model.Reset();
            Assert.That(model.Score, Is.Zero);
            Assert.That(model.LargestCapturePercentage, Is.Zero);
            Assert.That(model.BonusMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void NoTiers_AlwaysUsesBaseMultiplier()
        {
            var model = new ScoreModel(100f, null);
            Assert.That(model.AddCapture(40f).Points, Is.EqualTo(4000));
        }

        [Test]
        public void BoardCapture_ReportsPercentageGainedIncludingCommittedTrail()
        {
            var board = new BoardModel(8, 8);
            BoardCaptureResult result = default;
            board.CaptureCompleted += capture => result = capture;
            for (var x = 1; x < 7; x++) board.MoveTo(new GridCoordinate(x, 3));
            board.MoveTo(new GridCoordinate(7, 3));
            var expected = (result.RegionCellsCaptured + result.TrailCellsCommitted) * 100f / board.TotalPlayableCells;
            Assert.That(result.PercentageGained, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(result.PercentageGained, Is.EqualTo(board.CapturedPercentage).Within(0.0001f));
        }

        [Test]
        public void TrailOnlyCommit_ReportsOnlyTrailPercentage()
        {
            var board = new BoardModel(8, 8);
            BoardCaptureResult result = default;
            board.CaptureCompleted += capture => result = capture;
            var enemies = new[] { new GridCoordinate(2, 1), new GridCoordinate(2, 6) };
            for (var x = 1; x < 7; x++) board.MoveTo(new GridCoordinate(x, 3), enemies);
            board.MoveTo(new GridCoordinate(7, 3), enemies);
            Assert.That(result.RegionCaptured, Is.False);
            Assert.That(result.PercentageGained, Is.EqualTo(6 * 100f / board.TotalPlayableCells).Within(0.0001f));
        }
    }
}
