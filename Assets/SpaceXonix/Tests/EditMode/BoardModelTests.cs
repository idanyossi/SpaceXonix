using System.Collections.Generic;
using NUnit.Framework;
using SpaceXonix.Board;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class BoardModelTests
    {
        [Test]
        public void InitialBoard_HasStructuralPerimeterAndZeroProgress()
        {
            var board = new BoardModel(8, 8);
            Assert.That(board.GetCell(new GridCoordinate(0, 0)), Is.EqualTo(BoardCellState.Captured));
            Assert.That(board.GetCell(new GridCoordinate(7, 5)), Is.EqualTo(BoardCellState.Captured));
            Assert.That(board.GetCell(new GridCoordinate(1, 1)), Is.EqualTo(BoardCellState.Uncaptured));
            Assert.That(board.CapturedPlayableCells, Is.Zero);
            Assert.That(board.CapturedPercentage, Is.Zero);
        }

        [Test]
        public void SafeMovement_DoesNotStartTrail()
        {
            var board = new BoardModel(8, 8);
            Assert.That(board.MoveTo(new GridCoordinate(0, 2)), Is.EqualTo(BoardMoveResult.SafeMove));
            Assert.That(board.IsExposed, Is.False);
        }

        [Test]
        public void LeavingSafeSpace_StartsAndExtendsCardinalTrail()
        {
            var board = new BoardModel(8, 8);
            Assert.That(board.MoveTo(new GridCoordinate(1, 3)), Is.EqualTo(BoardMoveResult.TrailStarted));
            Assert.That(board.MoveTo(new GridCoordinate(2, 3)), Is.EqualTo(BoardMoveResult.TrailExtended));
            Assert.That(board.ActiveTrail.Count, Is.EqualTo(2));
            Assert.That(board.GetCell(new GridCoordinate(2, 3)), Is.EqualTo(BoardCellState.Trail));
        }

        [Test]
        public void SelfIntersection_CancelsTrailAndRestoresUncapturedCells()
        {
            var board = new BoardModel(8, 8);
            board.MoveTo(new GridCoordinate(1, 3));
            board.MoveTo(new GridCoordinate(2, 3));
            Assert.That(board.MoveTo(new GridCoordinate(1, 3)), Is.EqualTo(BoardMoveResult.TrailFailed));
            Assert.That(board.IsExposed, Is.False);
            Assert.That(board.GetCell(new GridCoordinate(1, 3)), Is.EqualTo(BoardCellState.Uncaptured));
        }

        [Test]
        public void CancelTrail_CleansAnOddlyShapedRouteWithoutChangingCapturedProgress()
        {
            var board = new BoardModel(8, 8);
            board.MoveTo(new GridCoordinate(1, 3));
            board.MoveTo(new GridCoordinate(2, 3));
            board.MoveTo(new GridCoordinate(2, 4));
            board.MoveTo(new GridCoordinate(3, 4));
            board.MoveTo(new GridCoordinate(3, 5));
            board.CancelTrail();
            Assert.That(board.IsExposed, Is.False);
            Assert.That(board.CapturedPlayableCells, Is.Zero);
            Assert.That(board.CapturedPercentage, Is.Zero);
            Assert.That(board.GetCell(new GridCoordinate(3, 5)), Is.EqualTo(BoardCellState.Uncaptured));
            Assert.That(board.GetCell(new GridCoordinate(0, 3)), Is.EqualTo(BoardCellState.Captured));
        }

        [Test]
        public void Reconnection_CapturesSmallestEligibleRegionAndTrail()
        {
            var board = CompleteHorizontalCut(new BoardModel(8, 8));
            Assert.That(board.IsExposed, Is.False);
            Assert.That(board.GetCell(new GridCoordinate(2, 1)), Is.EqualTo(BoardCellState.Captured));
            Assert.That(board.GetCell(new GridCoordinate(2, 5)), Is.EqualTo(BoardCellState.Uncaptured));
            Assert.That(board.CapturedPlayableCells, Is.EqualTo(18));
            Assert.That(board.CapturedPercentage, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void EnemyInSmallestRegion_SelectsOtherEligibleRegion()
        {
            var board = new BoardModel(8, 8);
            CompleteHorizontalCut(board, new[] { new GridCoordinate(2, 1) });
            Assert.That(board.GetCell(new GridCoordinate(2, 1)), Is.EqualTo(BoardCellState.Uncaptured));
            Assert.That(board.GetCell(new GridCoordinate(2, 5)), Is.EqualTo(BoardCellState.Captured));
        }

        [Test]
        public void EnemyInEveryRegion_CommitsTrailButCapturesNoRegion()
        {
            var board = new BoardModel(8, 8);
            CompleteHorizontalCut(board, new[] { new GridCoordinate(2, 1), new GridCoordinate(2, 5) });
            Assert.That(board.GetCell(new GridCoordinate(2, 1)), Is.EqualTo(BoardCellState.Uncaptured));
            Assert.That(board.GetCell(new GridCoordinate(2, 5)), Is.EqualTo(BoardCellState.Uncaptured));
            Assert.That(board.GetCell(new GridCoordinate(2, 3)), Is.EqualTo(BoardCellState.Captured));
            Assert.That(board.CapturedPlayableCells, Is.EqualTo(6));
        }

        [Test]
        public void TerritoryRemoval_OnlyRemovesInteriorCapturedCellsAndUpdatesProgress()
        {
            var board = CompleteHorizontalCut(new BoardModel(8, 8));
            var removed = board.RemoveCapturedWithinRadius(new GridCoordinate(2, 1), 0f);
            Assert.That(removed, Is.EqualTo(1));
            Assert.That(board.GetCell(new GridCoordinate(2, 1)), Is.EqualTo(BoardCellState.Uncaptured));
            Assert.That(board.GetCell(new GridCoordinate(0, 1)), Is.EqualTo(BoardCellState.Captured));
            Assert.That(board.CapturedPlayableCells, Is.EqualTo(17));
        }

        [Test]
        public void SequentialCaptures_AccumulateProgressWithoutLeakingTrailState()
        {
            var board = CompleteHorizontalCut(new BoardModel(8, 8));
            var before = board.CapturedPlayableCells;
            for (var y = 4; y < 7; y++) board.MoveTo(new GridCoordinate(3, y));
            board.MoveTo(new GridCoordinate(3, 7));
            Assert.That(board.IsExposed, Is.False);
            Assert.That(board.CapturedPlayableCells, Is.GreaterThan(before));
            Assert.That(board.GetCell(new GridCoordinate(1, 5)), Is.EqualTo(BoardCellState.Captured));
            Assert.That(board.GetCell(new GridCoordinate(5, 5)), Is.EqualTo(BoardCellState.Uncaptured));
        }

        [Test]
        public void CaptureResult_ReportsRegionAndCommittedTrailSeparately()
        {
            var board = new BoardModel(8, 8);
            BoardCaptureResult result = default;
            board.CaptureCompleted += capture => result = capture;
            CompleteHorizontalCut(board);
            Assert.That(result.RegionCaptured, Is.True);
            Assert.That(result.RegionCellsCaptured, Is.EqualTo(12));
            Assert.That(result.TrailCellsCommitted, Is.EqualTo(6));
        }

        [Test]
        public void TerritoryRemoval_DoesNotModifyAnActiveTrail()
        {
            var board = CompleteHorizontalCut(new BoardModel(8, 8));
            board.MoveTo(new GridCoordinate(3, 4));
            board.RemoveCapturedWithinRadius(new GridCoordinate(2, 1), 2f);
            Assert.That(board.IsExposed, Is.True);
            Assert.That(board.GetCell(new GridCoordinate(3, 4)), Is.EqualTo(BoardCellState.Trail));
        }

        [Test]
        public void TerritoryRemoval_UpdatesPercentagePreservesPerimeterAndAllowsLaterCapture()
        {
            var board = CompleteHorizontalCut(new BoardModel(8, 8));
            var before = board.CapturedPercentage;
            var removed = board.RemoveCapturedWithinRadius(new GridCoordinate(2, 1), 1f);
            Assert.That(removed, Is.GreaterThan(0));
            Assert.That(board.CapturedPercentage, Is.LessThan(before));
            Assert.That(board.GetCell(new GridCoordinate(0, 1)), Is.EqualTo(BoardCellState.Captured));
            for (var y = 4; y < 7; y++) board.MoveTo(new GridCoordinate(3, y));
            Assert.That(board.MoveTo(new GridCoordinate(3, 7)), Is.EqualTo(BoardMoveResult.Reconnected));
            Assert.That(board.IsExposed, Is.False);
        }

        [Test]
        public void PercentageChanged_IsNotInflatedByStructuralBoundary()
        {
            var board = new BoardModel(8, 8);
            var received = -1f;
            board.CapturedPercentageChanged += value => received = value;
            board.MoveTo(new GridCoordinate(1, 3));
            board.MoveTo(new GridCoordinate(2, 3));
            board.CancelTrail();
            Assert.That(received, Is.EqualTo(-1f));
            CompleteHorizontalCut(board);
            Assert.That(received, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void Bounds_AreRejectedWithoutChangingBoard()
        {
            var board = new BoardModel(8, 8);
            Assert.That(board.MoveTo(new GridCoordinate(-1, 1)), Is.EqualTo(BoardMoveResult.OutOfBounds));
            Assert.That(board.CapturedPlayableCells, Is.Zero);
        }

        [Test]
        public void GridWorldMapping_UsesCellCentersAndClampsEdges()
        {
            var host = new GameObject("Board");
            var manager = host.AddComponent<BoardManager>();
            try
            {
                var cell = manager.WorldToGrid(manager.GetWorldPosition(new GridCoordinate(0, 1)));
                Assert.That(cell, Is.EqualTo(new GridCoordinate(0, 1)));
                Assert.That(manager.WorldToGrid(manager.ClampToBoard(new Vector3(-20f, 200f))), Is.EqualTo(new GridCoordinate(0, manager.Rows - 1)));
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static BoardModel CompleteHorizontalCut(BoardModel board, IReadOnlyCollection<GridCoordinate> enemies = null)
        {
            for (var x = 1; x < 7; x++) board.MoveTo(new GridCoordinate(x, 3));
            board.MoveTo(new GridCoordinate(7, 3), enemies);
            return board;
        }
    }
}
