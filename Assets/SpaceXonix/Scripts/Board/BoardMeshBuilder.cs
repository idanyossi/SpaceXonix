using System.Collections.Generic;
using UnityEngine;

namespace SpaceXonix.Board
{
    /// <summary>
    /// Builds board presentation geometry in board-local space. The board lies on XY; height rises toward -Z (the camera).
    /// Submesh 0 holds slab tops, submesh 1 holds side walls, and submesh 2 holds the trim along
    /// every exposed top edge.
    ///
    /// UVs are in world space scaled by <see cref="Buffers.UvScale"/>, so a texture tiles at one fixed
    /// pixel density across every chunk, top and wall. Seams line up between neighbouring cells
    /// because each vertex's UV comes from its position, not from the cell it belongs to.
    /// </summary>
    public static class BoardMeshBuilder
    {
        public sealed class Buffers
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<int> Tops = new List<int>();
            public readonly List<int> Walls = new List<int>();
            public readonly List<int> Rims = new List<int>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            /// <summary>Texture repeats per world unit. 0.5 means one repeat every two units.</summary>
            public float UvScale = 1f;
            /// <summary>
            /// Width of the trim laid along a raised cell's top edge wherever it drops to a lower
            /// neighbour. 0 builds no trim. Its UVs run along the edge (u) and across it from the
            /// outer edge (v = 0) to the inner (v = 1), so a trim texture needs only a few rows.
            /// </summary>
            public float RimWidth;

            public void Clear()
            {
                Vertices.Clear(); Normals.Clear(); Tops.Clear(); Walls.Clear(); Rims.Clear(); Uvs.Clear();
            }
        }

        /// <summary>Adds raised-cell geometry for cells in [xMin, xMax) × [yMin, yMax). Walls appear only where a neighbour is lower.</summary>
        public static void BuildRaisedCells(float[] heights, int width, int height, float cellSize, int xMin, int yMin, int xMax, int yMax, Buffers buffers)
        {
            buffers.Clear();
            for (var y = yMin; y < yMax; y++) for (var x = xMin; x < xMax; x++)
            {
                var h = heights[x + y * width];
                if (h <= 0f) continue;
                float x0 = x * cellSize, x1 = (x + 1) * cellSize, y0 = y * cellSize, y1 = (y + 1) * cellSize;
                var top = -h;
                AddQuad(buffers, buffers.Tops, new Vector3(x0, y0, top), new Vector3(x1, y0, top), new Vector3(x0, y1, top), new Vector3(x1, y1, top), Vector3.back);

                var left = HeightAt(heights, width, height, x - 1, y);
                if (left < h)
                {
                    AddQuad(buffers, buffers.Walls, new Vector3(x0, y1, -left), new Vector3(x0, y0, -left), new Vector3(x0, y1, top), new Vector3(x0, y0, top), Vector3.left);
                    AddRim(buffers, new Vector3(x0, y0, top), new Vector3(x0, y1, top), Vector3.right);
                }
                var right = HeightAt(heights, width, height, x + 1, y);
                if (right < h)
                {
                    AddQuad(buffers, buffers.Walls, new Vector3(x1, y0, -right), new Vector3(x1, y1, -right), new Vector3(x1, y0, top), new Vector3(x1, y1, top), Vector3.right);
                    AddRim(buffers, new Vector3(x1, y0, top), new Vector3(x1, y1, top), Vector3.left);
                }
                var down = HeightAt(heights, width, height, x, y - 1);
                if (down < h)
                {
                    AddQuad(buffers, buffers.Walls, new Vector3(x0, y0, -down), new Vector3(x1, y0, -down), new Vector3(x0, y0, top), new Vector3(x1, y0, top), Vector3.down);
                    AddRim(buffers, new Vector3(x0, y0, top), new Vector3(x1, y0, top), Vector3.up);
                }
                var up = HeightAt(heights, width, height, x, y + 1);
                if (up < h)
                {
                    AddQuad(buffers, buffers.Walls, new Vector3(x1, y1, -up), new Vector3(x0, y1, -up), new Vector3(x1, y1, top), new Vector3(x0, y1, top), Vector3.up);
                    AddRim(buffers, new Vector3(x0, y1, top), new Vector3(x1, y1, top), Vector3.down);
                }
            }
        }

        /// <summary>Adds flat quads for the given cells at a fixed lift above the floor.</summary>
        public static void BuildFlatCells(IReadOnlyList<int> cellIndices, int width, float cellSize, float lift, Buffers buffers)
        {
            buffers.Clear();
            for (var i = 0; i < cellIndices.Count; i++)
            {
                var index = cellIndices[i];
                int x = index % width, y = index / width;
                float x0 = x * cellSize, x1 = (x + 1) * cellSize, y0 = y * cellSize, y1 = (y + 1) * cellSize;
                AddQuad(buffers, buffers.Tops, new Vector3(x0, y0, -lift), new Vector3(x1, y0, -lift), new Vector3(x0, y1, -lift), new Vector3(x1, y1, -lift), Vector3.back);
            }
        }

        public static void Apply(Mesh mesh, Buffers buffers)
        {
            mesh.Clear();
            mesh.subMeshCount = 3;
            mesh.SetVertices(buffers.Vertices);
            mesh.SetNormals(buffers.Normals);
            mesh.SetUVs(0, buffers.Uvs);
            mesh.SetTriangles(buffers.Tops, 0, false);
            mesh.SetTriangles(buffers.Walls, 1, false);
            mesh.SetTriangles(buffers.Rims, 2, false);
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Tops use the board's XY. Walls use the coordinate running along the wall and the height,
        /// so a wall's texture reads upright and continues around a corner.
        /// </summary>
        public static Vector2 Project(Vector3 vertex, Vector3 outward, float scale)
        {
            if (outward == Vector3.left || outward == Vector3.right) return new Vector2(vertex.y, -vertex.z) * scale;
            if (outward == Vector3.up || outward == Vector3.down) return new Vector2(vertex.x, -vertex.z) * scale;
            return new Vector2(vertex.x, vertex.y) * scale;
        }

        /// <summary>
        /// Lays a strip of trim on the top face along one edge, from <paramref name="start"/> to
        /// <paramref name="end"/>, extending <see cref="Buffers.RimWidth"/> inward. Without it a
        /// captured region stops mid-plate and looks cut out; with it every region ends on a
        /// finished edge. It sits a hair above the top so the two never fight for depth.
        /// </summary>
        private static void AddRim(Buffers buffers, Vector3 start, Vector3 end, Vector3 inward)
        {
            if (buffers.RimWidth <= 0f) return;
            var lift = new Vector3(0f, 0f, -RimLift);
            var offset = inward * buffers.RimWidth;
            Vector3 outerA = start + lift, outerB = end + lift, innerA = outerA + offset, innerB = outerB + offset;
            var startIndex = buffers.Vertices.Count;
            buffers.Vertices.Add(outerA); buffers.Vertices.Add(outerB); buffers.Vertices.Add(innerA); buffers.Vertices.Add(innerB);
            for (var i = 0; i < 4; i++) buffers.Normals.Add(Vector3.back);
            // Along the edge in world space, so the trim's pattern runs unbroken across cells.
            var along = end - start;
            float u0 = Vector3.Dot(start, along.normalized) * buffers.UvScale, u1 = Vector3.Dot(end, along.normalized) * buffers.UvScale;
            buffers.Uvs.Add(new Vector2(u0, 0f)); buffers.Uvs.Add(new Vector2(u1, 0f));
            buffers.Uvs.Add(new Vector2(u0, 1f)); buffers.Uvs.Add(new Vector2(u1, 1f));
            // Face the camera (-Z) whichever way the edge runs.
            if (Vector3.Dot(Vector3.Cross(innerA - outerA, outerB - outerA), Vector3.back) > 0f)
            {
                buffers.Rims.Add(startIndex); buffers.Rims.Add(startIndex + 2); buffers.Rims.Add(startIndex + 1);
                buffers.Rims.Add(startIndex + 2); buffers.Rims.Add(startIndex + 3); buffers.Rims.Add(startIndex + 1);
            }
            else
            {
                buffers.Rims.Add(startIndex); buffers.Rims.Add(startIndex + 1); buffers.Rims.Add(startIndex + 2);
                buffers.Rims.Add(startIndex + 2); buffers.Rims.Add(startIndex + 1); buffers.Rims.Add(startIndex + 3);
            }
        }

        public const float RimLift = .002f;

        private static float HeightAt(float[] heights, int width, int height, int x, int y) =>
            x < 0 || y < 0 || x >= width || y >= height ? 0f : heights[x + y * width];

        /// <summary>Corners are given as seen from outside the face: bottom-left, bottom-right, top-left, top-right.</summary>
        private static void AddQuad(Buffers buffers, List<int> indices, Vector3 bl, Vector3 br, Vector3 tl, Vector3 tr, Vector3 outward)
        {
            var start = buffers.Vertices.Count;
            buffers.Vertices.Add(bl); buffers.Vertices.Add(br); buffers.Vertices.Add(tl); buffers.Vertices.Add(tr);
            for (var i = 0; i < 4; i++) buffers.Normals.Add(outward);
            buffers.Uvs.Add(Project(bl, outward, buffers.UvScale));
            buffers.Uvs.Add(Project(br, outward, buffers.UvScale));
            buffers.Uvs.Add(Project(tl, outward, buffers.UvScale));
            buffers.Uvs.Add(Project(tr, outward, buffers.UvScale));
            // Unity front faces satisfy cross(b - a, c - a) pointing toward the viewer (outward).
            if (Vector3.Dot(Vector3.Cross(tl - bl, br - bl), outward) > 0f)
            {
                indices.Add(start); indices.Add(start + 2); indices.Add(start + 1);
                indices.Add(start + 2); indices.Add(start + 3); indices.Add(start + 1);
            }
            else
            {
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                indices.Add(start + 2); indices.Add(start + 1); indices.Add(start + 3);
            }
        }
    }
}
