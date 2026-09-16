using System;
using System.Collections.Generic;

namespace SpaceXonix.Board
{
    /// <summary>Authoritative grid state. The perimeter is structural and excluded from progress.</summary>
    public sealed class BoardModel
    {
        private static readonly GridCoordinate[] Neighbours =
        {
            new GridCoordinate(1, 0), new GridCoordinate(-1, 0), new GridCoordinate(0, 1), new GridCoordinate(0, -1)
        };

        private readonly BoardCellState[] cells;
        private readonly bool[] structuralCells;
        private readonly List<int> trail = new List<int>();
        private readonly bool[] trailMarks;
        private readonly int[] visitStamp;
        private readonly Queue<int> floodQueue = new Queue<int>();
        private int currentVisitStamp;
        private int capturedPlayableCells;

        public BoardModel(int width, int height)
        {
            if (width < 3 || height < 3) throw new ArgumentOutOfRangeException(nameof(width), "A board needs a one-cell perimeter and an interior.");
            Width = width;
            Height = height;
            cells = new BoardCellState[width * height];
            structuralCells = new bool[cells.Length];
            trailMarks = new bool[cells.Length];
            visitStamp = new int[cells.Length];
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    var index = ToIndex(x, y);
                    cells[index] = BoardCellState.Captured;
                    structuralCells[index] = true;
                }
            }
        }

        public int Width { get; }
        public int Height { get; }
        public int TotalPlayableCells => (Width - 2) * (Height - 2);
        public int CapturedPlayableCells => capturedPlayableCells;
        public int StructuralCapturedCells => (Width * Height) - TotalPlayableCells;
        public float CapturedPercentage => TotalPlayableCells == 0 ? 0f : capturedPlayableCells * 100f / TotalPlayableCells;
        public bool IsExposed => trail.Count > 0;
        public IReadOnlyList<int> ActiveTrail => trail;
        public event Action<BoardMoveResult> TrailStateChanged;
        public event Action<BoardCaptureResult> CaptureCompleted;
        public event Action<float> CapturedPercentageChanged;

        public bool IsInBounds(GridCoordinate cell) => cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;
        public BoardCellState GetCell(GridCoordinate cell) => IsInBounds(cell) ? cells[ToIndex(cell.X, cell.Y)] : throw new ArgumentOutOfRangeException(nameof(cell));
        public bool IsStructural(GridCoordinate cell) => IsInBounds(cell) && structuralCells[ToIndex(cell.X, cell.Y)];
        public GridCoordinate ToCoordinate(int index) => new GridCoordinate(index % Width, index / Width);

        public BoardMoveResult MoveTo(GridCoordinate cell, IReadOnlyCollection<GridCoordinate> activeEnemyCells = null)
        {
            if (!IsInBounds(cell)) return BoardMoveResult.OutOfBounds;
            var index = ToIndex(cell.X, cell.Y);
            var state = cells[index];
            if (!IsExposed)
            {
                if (state != BoardCellState.Uncaptured) return BoardMoveResult.SafeMove;
                SetTrail(index);
                TrailStateChanged?.Invoke(BoardMoveResult.TrailStarted);
                return BoardMoveResult.TrailStarted;
            }

            if (state == BoardCellState.Trail)
            {
                CancelTrail();
                TrailStateChanged?.Invoke(BoardMoveResult.TrailFailed);
                return BoardMoveResult.TrailFailed;
            }
            if (state == BoardCellState.Uncaptured)
            {
                SetTrail(index);
                TrailStateChanged?.Invoke(BoardMoveResult.TrailExtended);
                return BoardMoveResult.TrailExtended;
            }

            ResolveCapture(activeEnemyCells);
            return BoardMoveResult.Reconnected;
        }

        public void CancelTrail()
        {
            foreach (var index in trail) { cells[index] = BoardCellState.Uncaptured; trailMarks[index] = false; }
            trail.Clear();
        }

        public int RemoveCapturedWithinRadius(GridCoordinate center, float radius, GridCoordinate? protectedCell = null)
        {
            var radiusSquared = radius * radius;
            var removed = 0;
            for (var y = 1; y < Height - 1; y++) for (var x = 1; x < Width - 1; x++)
            {
                var dx = x - center.X; var dy = y - center.Y;
                if (dx * dx + dy * dy > radiusSquared) continue;
                if (protectedCell.HasValue && protectedCell.Value == new GridCoordinate(x, y)) continue;
                var index = ToIndex(x, y);
                if (cells[index] != BoardCellState.Captured) continue;
                cells[index] = BoardCellState.Uncaptured;
                capturedPlayableCells--; removed++;
            }
            if (removed > 0) CapturedPercentageChanged?.Invoke(CapturedPercentage);
            return removed;
        }

        private void ResolveCapture(IReadOnlyCollection<GridCoordinate> activeEnemyCells)
        {
            var enemyIndices = new HashSet<int>();
            if (activeEnemyCells != null) foreach (var enemy in activeEnemyCells) if (IsInBounds(enemy)) enemyIndices.Add(ToIndex(enemy.X, enemy.Y));
            var selected = FindSmallestEligibleRegionAdjacentToTrail(enemyIndices);
            var committedTrailCount = trail.Count;
            foreach (var index in trail) { cells[index] = BoardCellState.Captured; trailMarks[index] = false; capturedPlayableCells++; }
            trail.Clear();
            if (selected != null) foreach (var index in selected) { cells[index] = BoardCellState.Captured; capturedPlayableCells++; }
            var result = new BoardCaptureResult(selected?.Count ?? 0, committedTrailCount, selected != null, CapturedPercentage);
            CaptureCompleted?.Invoke(result);
            CapturedPercentageChanged?.Invoke(CapturedPercentage);
            TrailStateChanged?.Invoke(BoardMoveResult.Reconnected);
        }

        private List<int> FindSmallestEligibleRegionAdjacentToTrail(HashSet<int> enemyIndices)
        {
            if (currentVisitStamp == int.MaxValue) { Array.Clear(visitStamp, 0, visitStamp.Length); currentVisitStamp = 0; }
            currentVisitStamp++;
            List<int> best = null;
            for (var trailIndex = 0; trailIndex < trail.Count; trailIndex++)
            {
                var trailCell = ToCoordinate(trail[trailIndex]);
                foreach (var offset in Neighbours)
                {
                    var seed = new GridCoordinate(trailCell.X + offset.X, trailCell.Y + offset.Y);
                    if (!IsInBounds(seed)) continue;
                    var seedIndex = ToIndex(seed.X, seed.Y);
                    if (cells[seedIndex] != BoardCellState.Uncaptured || visitStamp[seedIndex] == currentVisitStamp) continue;
                    var component = FloodComponent(seedIndex, enemyIndices, out var containsEnemy);
                    if (!containsEnemy && (best == null || component.Count < best.Count)) best = component;
                }
            }
            return best;
        }

        private List<int> FloodComponent(int start, HashSet<int> enemyIndices, out bool containsEnemy)
        {
            var component = new List<int>(); containsEnemy = false;
            floodQueue.Enqueue(start); visitStamp[start] = currentVisitStamp;
            while (floodQueue.Count > 0)
            {
                var index = floodQueue.Dequeue(); component.Add(index); if (enemyIndices.Contains(index)) containsEnemy = true;
                var coordinate = ToCoordinate(index);
                foreach (var offset in Neighbours)
                {
                    var next = new GridCoordinate(coordinate.X + offset.X, coordinate.Y + offset.Y);
                    if (!IsInBounds(next)) continue;
                    var nextIndex = ToIndex(next.X, next.Y);
                    if (cells[nextIndex] != BoardCellState.Uncaptured || visitStamp[nextIndex] == currentVisitStamp) continue;
                    visitStamp[nextIndex] = currentVisitStamp; floodQueue.Enqueue(nextIndex);
                }
            }
            return component;
        }

        private void SetTrail(int index) { cells[index] = BoardCellState.Trail; trailMarks[index] = true; trail.Add(index); }
        private int ToIndex(int x, int y) => x + y * Width;
    }
}
