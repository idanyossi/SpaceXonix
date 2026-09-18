using System.Collections.Generic;
using UnityEngine;

namespace SpaceXonix.Board
{
    /// <summary>
    /// 2.5D board view: recessed floor, raised captured territory in chunked meshes, and a lifted trail strip.
    /// Mirrors BoardModel state; animation never delays or alters the logical transition.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public sealed class BoardRenderer : MonoBehaviour
    {
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material territoryTopMaterial;
        [SerializeField] private Material territoryWallMaterial;
        [SerializeField] private Material trailMaterial;
        [SerializeField, Min(.01f)] private float territoryHeight = .35f;
        [SerializeField, Min(.001f)] private float trailLift = .02f;
        [SerializeField, Min(.01f)] private float riseSeconds = .3f;
        [SerializeField, Min(1)] private int chunkSize = 9;

        private readonly BoardMeshBuilder.Buffers buffers = new BoardMeshBuilder.Buffers();
        private readonly List<int> trailCells = new List<int>();
        private BoardManager board;
        private int width, height, chunksX, chunksY;
        private float cellSize;
        private float[] heights;
        private float[] targetHeights;
        private bool[] trailFlags;
        private bool[] chunkDirty;
        private bool[] chunkAnimating;
        private Mesh[] chunkMeshes;
        private Mesh trailMesh;

        public float TerritoryHeight => territoryHeight;
        public int ChunkCount => chunkMeshes?.Length ?? 0;
        public bool IsAnimating { get; private set; }

        public float GetVisualHeight(GridCoordinate cell) => heights[cell.X + cell.Y * width];

        public void Initialize(BoardManager boardManager)
        {
            board = boardManager;
            width = board.Columns; height = board.Rows; cellSize = board.CellWorldSize;
            heights = new float[width * height];
            targetHeights = new float[width * height];
            trailFlags = new bool[width * height];
            chunksX = Mathf.CeilToInt(width / (float)chunkSize);
            chunksY = Mathf.CeilToInt(height / (float)chunkSize);
            chunkDirty = new bool[chunksX * chunksY];
            chunkAnimating = new bool[chunksX * chunksY];
            BuildFloor();
            BuildChunkObjects();
            Rebuild(board.Model);
        }

        /// <summary>Snaps the view to the model without animation (initialization and new stages).</summary>
        public void Rebuild(BoardModel model)
        {
            if (heights == null) return;
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var i = x + y * width;
                var target = model.GetCell(new GridCoordinate(x, y)) == BoardCellState.Captured ? territoryHeight : 0f;
                targetHeights[i] = heights[i] = target;
            }
            for (var c = 0; c < chunkDirty.Length; c++) { chunkDirty[c] = true; chunkAnimating[c] = false; }
            IsAnimating = false;
            RebuildDirtyChunks();
            RefreshTrail(model, force: true);
        }

        /// <summary>Picks up changed cells; newly captured cells rise and destroyed cells sink.</summary>
        public void Refresh(BoardModel model)
        {
            if (heights == null) return;
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var i = x + y * width;
                var target = model.GetCell(new GridCoordinate(x, y)) == BoardCellState.Captured ? territoryHeight : 0f;
                if (Mathf.Approximately(target, targetHeights[i])) continue;
                targetHeights[i] = target;
                chunkAnimating[ChunkOf(x, y)] = true;
                IsAnimating = true;
            }
            RefreshTrail(model, force: false);
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (!IsAnimating || heights == null) return;
            var step = territoryHeight / riseSeconds * Mathf.Max(0f, deltaTime);
            var anyAnimating = false;
            for (var cy = 0; cy < chunksY; cy++) for (var cx = 0; cx < chunksX; cx++)
            {
                var chunk = cx + cy * chunksX;
                if (!chunkAnimating[chunk]) continue;
                var stillAnimating = false;
                var touchesBorder = false;
                int xMin = cx * chunkSize, yMin = cy * chunkSize, xMax = Mathf.Min(width, xMin + chunkSize), yMax = Mathf.Min(height, yMin + chunkSize);
                for (var y = yMin; y < yMax; y++) for (var x = xMin; x < xMax; x++)
                {
                    var i = x + y * width;
                    if (Mathf.Approximately(heights[i], targetHeights[i])) continue;
                    heights[i] = Mathf.MoveTowards(heights[i], targetHeights[i], step);
                    if (!Mathf.Approximately(heights[i], targetHeights[i])) stillAnimating = true;
                    if (x == xMin || y == yMin || x == xMax - 1 || y == yMax - 1) touchesBorder = true;
                }
                chunkAnimating[chunk] = stillAnimating;
                anyAnimating |= stillAnimating;
                MarkDirty(cx, cy, touchesBorder);
            }
            IsAnimating = anyAnimating;
            RebuildDirtyChunks();
        }

        private void MarkDirty(int cx, int cy, bool includeNeighbours)
        {
            chunkDirty[cx + cy * chunksX] = true;
            if (!includeNeighbours) return;
            for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
            {
                int nx = cx + dx, ny = cy + dy;
                if (nx >= 0 && ny >= 0 && nx < chunksX && ny < chunksY) chunkDirty[nx + ny * chunksX] = true;
            }
        }

        private void RebuildDirtyChunks()
        {
            for (var cy = 0; cy < chunksY; cy++) for (var cx = 0; cx < chunksX; cx++)
            {
                var chunk = cx + cy * chunksX;
                if (!chunkDirty[chunk]) continue;
                chunkDirty[chunk] = false;
                int xMin = cx * chunkSize, yMin = cy * chunkSize;
                BoardMeshBuilder.BuildRaisedCells(heights, width, height, cellSize, xMin, yMin,
                    Mathf.Min(width, xMin + chunkSize), Mathf.Min(height, yMin + chunkSize), buffers);
                BoardMeshBuilder.Apply(chunkMeshes[chunk], buffers);
            }
        }

        private void RefreshTrail(BoardModel model, bool force)
        {
            var changed = force;
            var trail = model.ActiveTrail;
            if (!changed)
            {
                if (trail.Count != trailCells.Count) changed = true;
                else for (var i = 0; i < trail.Count && !changed; i++) if (!trailFlags[trail[i]]) changed = true;
            }
            if (!changed) return;
            for (var i = 0; i < trailCells.Count; i++) trailFlags[trailCells[i]] = false;
            trailCells.Clear();
            for (var i = 0; i < trail.Count; i++) { trailCells.Add(trail[i]); trailFlags[trail[i]] = true; }
            BoardMeshBuilder.BuildFlatCells(trailCells, width, cellSize, trailLift, buffers);
            BoardMeshBuilder.Apply(trailMesh, buffers);
        }

        private int ChunkOf(int x, int y) => x / chunkSize + (y / chunkSize) * chunksX;

        private void BuildFloor()
        {
            var mesh = new Mesh { name = "BoardFloor", hideFlags = HideFlags.DontSave };
            float w = width * cellSize, h = height * cellSize;
            mesh.vertices = new[] { Vector3.zero, new Vector3(w, 0f), new Vector3(0f, h), new Vector3(w, h) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            GetComponent<MeshRenderer>().sharedMaterial = floorMaterial != null ? floorMaterial : RuntimeMaterial(new Color(.02f, .03f, .08f));
        }

        private void BuildChunkObjects()
        {
            chunkMeshes = new Mesh[chunksX * chunksY];
            var top = territoryTopMaterial != null ? territoryTopMaterial : RuntimeMaterial(new Color(.08f, .55f, .78f));
            var wall = territoryWallMaterial != null ? territoryWallMaterial : RuntimeMaterial(new Color(.03f, .28f, .42f));
            for (var i = 0; i < chunkMeshes.Length; i++)
            {
                var chunk = new GameObject($"TerritoryChunk_{i % chunksX}_{i / chunksX}") { hideFlags = HideFlags.DontSave };
                chunk.transform.SetParent(transform, false);
                chunkMeshes[i] = new Mesh { name = chunk.name, hideFlags = HideFlags.DontSave };
                chunk.AddComponent<MeshFilter>().sharedMesh = chunkMeshes[i];
                chunk.AddComponent<MeshRenderer>().sharedMaterials = new[] { top, wall };
            }
            var trailObject = new GameObject("Trail") { hideFlags = HideFlags.DontSave };
            trailObject.transform.SetParent(transform, false);
            trailMesh = new Mesh { name = "Trail", hideFlags = HideFlags.DontSave };
            trailObject.AddComponent<MeshFilter>().sharedMesh = trailMesh;
            var trail = trailMaterial != null ? trailMaterial : RuntimeMaterial(new Color(1f, .67f, .12f));
            trailObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { trail, trail };
        }

        private static Material RuntimeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { hideFlags = HideFlags.DontSave, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }
    }
}
