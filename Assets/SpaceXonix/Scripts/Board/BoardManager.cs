using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceXonix.Board
{
    public sealed class BoardManager : MonoBehaviour
    {
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
        public float CapturedPercentage => Model != null ? Model.CapturedPercentage : 0f;
        public event Action<BoardMoveResult> TrailStateChanged;
        public event Action<BoardCaptureResult> CaptureCompleted;
        public event Action<float> CapturedPercentageChanged;

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

        public Vector3 GetSafeRespawnPosition()
        {
            if (hasLastSafeCell && IsSafeRespawnCell(lastSafeCell)) return GetWorldPosition(lastSafeCell);
            return GetDefaultSpawnPosition();
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
                var deltaX = target.X - playerCell.X;
                var deltaY = target.Y - playerCell.Y;
                // The controller is cardinal; this fallback only protects against external teleports.
                if (deltaX != 0) playerCell = new GridCoordinate(playerCell.X + Math.Sign(deltaX), playerCell.Y);
                else playerCell = new GridCoordinate(playerCell.X, playerCell.Y + Math.Sign(deltaY));
                result = Model.MoveTo(playerCell, enemySnapshot);
                if (!Model.IsExposed && Model.GetCell(playerCell) == BoardCellState.Captured)
                {
                    lastSafeCell = playerCell;
                    hasLastSafeCell = true;
                }
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

        public int RemoveCapturedWithinRadius(GridCoordinate center, float radius) => Model.RemoveCapturedWithinRadius(center, radius);

        private bool IsSafeRespawnCell(GridCoordinate cell) => Model.IsInBounds(cell) && Model.GetCell(cell) == BoardCellState.Captured;
    }
}
