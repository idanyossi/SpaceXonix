using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceXonix.Board
{
    public sealed class BoardManager : MonoBehaviour
    {
        private static readonly GridCoordinate[] CardinalOffsets =
        {
            new GridCoordinate(0, 1),
            new GridCoordinate(0, -1),
            new GridCoordinate(-1, 0),
            new GridCoordinate(1, 0)
        };

        [SerializeField, Min(3)] private int columns = 54;
        [SerializeField, Min(3)] private int rows = 96;
        [SerializeField, Min(0.1f)] private float cellWorldSize = 0.18f;
        [SerializeField] private BoardRenderer boardRenderer;

        private readonly List<GridCoordinate> enemySnapshot = new List<GridCoordinate>();
        private GridCoordinate playerCell;
        private GridCoordinate lastSafeCell;
        private bool hasPlayerCell;
        private bool hasLastSafeCell;

        public BoardModel Model { get; private set; }
        public int Columns => columns;
        public int Rows => rows;
        public float CellWorldSize => cellWorldSize;
        public bool IsPlayerExposed => Model != null && Model.IsExposed;
        public GridCoordinate PlayerCell => playerCell;
        public float CapturedPercentage => Model != null ? Model.CapturedPercentage : 0f;
        public event Action<BoardMoveResult> TrailStateChanged;
        public event Func<bool> TrailFailureRequested;
        public event Action<BoardCaptureResult> CaptureCompleted;
        public event Action<float> CapturedPercentageChanged;
        public event Action<int> TerritoryDestroyed;

        private void Awake() => Initialize();

        public void Initialize()
        {
            Model = new BoardModel(columns, rows);
            Model.TrailStateChanged += result => { boardRenderer?.Refresh(Model); TrailStateChanged?.Invoke(result); };
            Model.CaptureCompleted += result => CaptureCompleted?.Invoke(result);
            Model.CapturedPercentageChanged += value => CapturedPercentageChanged?.Invoke(value);
            if (Application.isPlaying && boardRenderer != null) boardRenderer.Initialize(this);
        }

        public void SetEnemyCells(IEnumerable<GridCoordinate> enemyCells)
        {
            enemySnapshot.Clear();
            if (enemyCells == null) return;
            foreach (var cell in enemyCells) if (Model.IsInBounds(cell)) enemySnapshot.Add(cell);
        }

        public Vector3 GetWorldPosition(GridCoordinate cell)
        {
            return transform.TransformPoint(new Vector3((cell.X + 0.5f) * cellWorldSize, (cell.Y + 0.5f) * cellWorldSize, 0f));
        }

        public GridCoordinate WorldToGrid(Vector3 worldPosition)
        {
            var local = transform.InverseTransformPoint(worldPosition);
            return new GridCoordinate(Mathf.FloorToInt(local.x / cellWorldSize), Mathf.FloorToInt(local.y / cellWorldSize));
        }

        public Vector3 ClampToBoard(Vector3 worldPosition)
        {
            var local = transform.InverseTransformPoint(worldPosition);
            local.x = Mathf.Clamp(local.x, 0.001f, columns * cellWorldSize - 0.001f);
            local.y = Mathf.Clamp(local.y, 0.001f, rows * cellWorldSize - 0.001f);
            return transform.TransformPoint(local);
        }

        public Vector3 GetDefaultSpawnPosition() => GetWorldPosition(new GridCoordinate(0, 1));

        public void CancelActiveTrail()
        {
            if (!Model.IsExposed) return;
            Model.CancelTrail();
            boardRenderer?.Refresh(Model);
        }

        public GridCoordinate GetSafeRespawnCell()
        {
            if (hasLastSafeCell && IsValidRespawnCell(lastSafeCell)) return lastSafeCell;

            var origin = hasLastSafeCell ? lastSafeCell : new GridCoordinate(0, 1);
            var fallback = new GridCoordinate(0, 1);
            var bestDistance = int.MaxValue;
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    var cell = new GridCoordinate(x, y);
                    if (!IsValidRespawnCell(cell)) continue;
                    var distance = Mathf.Abs(cell.X - origin.X) + Mathf.Abs(cell.Y - origin.Y);
                    if (distance >= bestDistance) continue;
                    fallback = cell;
                    bestDistance = distance;
                }
            }

            return fallback;
        }

        public bool IsValidRespawnCell(GridCoordinate cell)
        {
            if (!IsSafeRespawnCell(cell)) return false;
            for (var i = 0; i < CardinalOffsets.Length; i++)
            {
                var offset = CardinalOffsets[i];
                if (IsLegalPlayerStep(new GridCoordinate(cell.X + offset.X, cell.Y + offset.Y))) return true;
            }
            return false;
        }

        public bool HasValidPlayerBoardState()
        {
            if (Model == null || !hasPlayerCell || !Model.IsInBounds(playerCell)) return false;
            var trail = Model.ActiveTrail;
            var state = Model.GetCell(playerCell);
            if (!Model.IsExposed)
                return trail.Count == 0 && state == BoardCellState.Captured;

            if (trail.Count == 0 || state != BoardCellState.Trail || Model.ToCoordinate(trail[trail.Count - 1]) != playerCell)
                return false;

            var first = Model.ToCoordinate(trail[0]);
            var connectsToSafe = false;
            for (var i = 0; i < CardinalOffsets.Length; i++)
            {
                var offset = CardinalOffsets[i];
                var neighbour = new GridCoordinate(first.X + offset.X, first.Y + offset.Y);
                if (IsSafeRespawnCell(neighbour))
                {
                    connectsToSafe = true;
                    break;
                }
            }
            if (!connectsToSafe) return false;

            for (var i = 0; i < trail.Count; i++)
            {
                var cell = Model.ToCoordinate(trail[i]);
                if (Model.GetCell(cell) != BoardCellState.Trail) return false;
                if (i == 0) continue;
                var previous = Model.ToCoordinate(trail[i - 1]);
                if (Mathf.Abs(cell.X - previous.X) + Mathf.Abs(cell.Y - previous.Y) != 1) return false;
            }
            return true;
        }

        public Vector3 GetSafeRespawnPosition() => GetWorldPosition(GetSafeRespawnCell());

        public bool IsInBounds(GridCoordinate cell) => Model != null && Model.IsInBounds(cell);

        public bool IsLegalPlayerStep(GridCoordinate cell)
        {
            return IsInBounds(cell);
        }

        public void GetTraversedCells(Vector3 startWorldPosition, Vector3 endWorldPosition, List<GridCoordinate> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            cells.Clear();

            var start = transform.InverseTransformPoint(startWorldPosition) / cellWorldSize;
            var end = transform.InverseTransformPoint(endWorldPosition) / cellWorldSize;
            var cell = new GridCoordinate(Mathf.FloorToInt(start.x), Mathf.FloorToInt(start.y));
            var endCell = new GridCoordinate(Mathf.FloorToInt(end.x), Mathf.FloorToInt(end.y));
            AddTraversedCell(cell, cells);

            var delta = (Vector2)end - (Vector2)start;
            var stepX = Math.Sign(delta.x);
            var stepY = Math.Sign(delta.y);
            var tDeltaX = stepX == 0 ? float.PositiveInfinity : Mathf.Abs(1f / delta.x);
            var tDeltaY = stepY == 0 ? float.PositiveInfinity : Mathf.Abs(1f / delta.y);
            var nextBoundaryX = stepX > 0 ? cell.X + 1f : cell.X;
            var nextBoundaryY = stepY > 0 ? cell.Y + 1f : cell.Y;
            var tMaxX = stepX == 0 ? float.PositiveInfinity : (nextBoundaryX - start.x) / delta.x;
            var tMaxY = stepY == 0 ? float.PositiveInfinity : (nextBoundaryY - start.y) / delta.y;

            while (cell != endCell)
            {
                if (tMaxX < tMaxY)
                {
                    cell = new GridCoordinate(cell.X + stepX, cell.Y);
                    tMaxX += tDeltaX;
                }
                else if (tMaxY < tMaxX)
                {
                    cell = new GridCoordinate(cell.X, cell.Y + stepY);
                    tMaxY += tDeltaY;
                }
                else
                {
                    cell = new GridCoordinate(cell.X + stepX, cell.Y + stepY);
                    tMaxX += tDeltaX;
                    tMaxY += tDeltaY;
                }
                AddTraversedCell(cell, cells);
            }
        }

        private void AddTraversedCell(GridCoordinate cell, List<GridCoordinate> cells)
        {
            if (Model.IsInBounds(cell)) cells.Add(cell);
        }

        public BoardMoveResult TrackPlayerWorldPosition(Vector3 previousWorldPosition, Vector3 currentWorldPosition)
        {
            var clamped = ClampToBoard(currentWorldPosition);
            var target = WorldToGrid(clamped);
            if (!hasPlayerCell)
            {
                playerCell = WorldToGrid(ClampToBoard(previousWorldPosition)); hasPlayerCell = true;
            }
            var result = BoardMoveResult.Ignored;
            while (playerCell != target)
            {
                var previousCell = playerCell;
                var deltaX = target.X - playerCell.X;
                var deltaY = target.Y - playerCell.Y;
                // The controller is cardinal; this fallback only protects against external teleports.
                if (deltaX != 0) playerCell = new GridCoordinate(playerCell.X + Math.Sign(deltaX), playerCell.Y);
                else playerCell = new GridCoordinate(playerCell.X, playerCell.Y + Math.Sign(deltaY));
                result = Model.MoveTo(playerCell, enemySnapshot, TryAcceptTrailFailure);
                if (result == BoardMoveResult.TrailFailed && Model.IsExposed)
                    playerCell = previousCell;
                if (!Model.IsExposed && Model.GetCell(playerCell) == BoardCellState.Captured)
                {
                    lastSafeCell = playerCell;
                    hasLastSafeCell = true;
                }
                if (result == BoardMoveResult.TrailFailed || result == BoardMoveResult.SafeMove || result == BoardMoveResult.Reconnected) break;
            }
            return result;
        }

        public void ResetPlayerTracking(Vector3 playerWorldPosition)
        {
            playerCell = WorldToGrid(ClampToBoard(playerWorldPosition)); hasPlayerCell = true;
            if (IsSafeRespawnCell(playerCell))
            {
                lastSafeCell = playerCell;
                hasLastSafeCell = true;
            }
        }

        public int RemoveCapturedWithinRadius(GridCoordinate center, float radius)
        {
            var protectedPlayerCell = hasPlayerCell ? playerCell : (GridCoordinate?)null;
            var removed = Model.RemoveCapturedWithinRadius(center, radius, protectedPlayerCell);
            if (removed <= 0) return 0;
            boardRenderer?.Refresh(Model);
            TerritoryDestroyed?.Invoke(removed);
            return removed;
        }

        private bool IsSafeRespawnCell(GridCoordinate cell) => Model.IsInBounds(cell) && Model.GetCell(cell) == BoardCellState.Captured;

        private bool TryAcceptTrailFailure() => TrailFailureRequested == null || TrailFailureRequested.Invoke();
    }
}
