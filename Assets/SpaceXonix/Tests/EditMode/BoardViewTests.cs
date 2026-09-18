using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpaceXonix.Board;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class BoardViewTests
    {
        private const float Cell = .18f;

        [Test]
        public void SingleRaisedCell_HasTopAndFourOutwardWalls()
        {
            var heights = new float[3 * 3];
            heights[4] = .15f;
            var buffers = new BoardMeshBuilder.Buffers();
            BoardMeshBuilder.BuildRaisedCells(heights, 3, 3, Cell, 0, 0, 3, 3, buffers);
            Assert.That(buffers.Tops.Count, Is.EqualTo(6));
            Assert.That(buffers.Walls.Count, Is.EqualTo(24));
            AssertTrianglesFaceTheirNormals(buffers, buffers.Tops);
            AssertTrianglesFaceTheirNormals(buffers, buffers.Walls);
            foreach (var index in buffers.Tops) Assert.That(buffers.Vertices[index].z, Is.EqualTo(-.15f).Within(.0001f), "tops rise toward the camera (-Z)");
        }

        [Test]
        public void AdjacentRaisedCells_ShareNoInnerWallAndPartialWallsSpanTheHeightDifference()
        {
            var heights = new float[3 * 1];
            heights[0] = .15f; heights[1] = .15f; heights[2] = .05f;
            var buffers = new BoardMeshBuilder.Buffers();
            BoardMeshBuilder.BuildRaisedCells(heights, 3, 1, Cell, 0, 0, 3, 1, buffers);
            // cell 0: left, down, up; cell 1: right (partial), down, up; cell 2: left none (higher neighbour), right, down, up.
            Assert.That(buffers.Walls.Count / 6, Is.EqualTo(3 + 3 + 3));
            var partial = false;
            for (var v = 0; v < buffers.Vertices.Count; v++)
                if (buffers.Normals[v] == Vector3.right && Mathf.Abs(buffers.Vertices[v].x - 2 * Cell) < .0001f && Mathf.Abs(buffers.Vertices[v].z + .05f) < .0001f) partial = true;
            Assert.That(partial, Is.True, "the wall between heights .15 and .05 starts at the lower neighbour's top");
        }

        [Test]
        public void FlatZeroHeightBoard_ProducesNoTerritoryGeometry()
        {
            var buffers = new BoardMeshBuilder.Buffers();
            BoardMeshBuilder.BuildRaisedCells(new float[16], 4, 4, Cell, 0, 0, 4, 4, buffers);
            Assert.That(buffers.Vertices, Is.Empty);
        }

        [Test]
        public void Renderer_SnapsInitialPerimeterThenAnimatesCaptureWithoutChangingModel()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Renderer.ChunkCount, Is.EqualTo(6 * 11));
                Assert.That(fixture.Renderer.GetVisualHeight(new GridCoordinate(0, 0)), Is.EqualTo(fixture.Renderer.TerritoryHeight), "perimeter starts raised");
                Assert.That(fixture.Renderer.GetVisualHeight(new GridCoordinate(5, 5)), Is.Zero);

                for (var x = 1; x < 53; x++) fixture.Board.Model.MoveTo(new GridCoordinate(x, 3));
                fixture.Board.Model.MoveTo(new GridCoordinate(53, 3));
                var captured = new GridCoordinate(10, 2);
                Assert.That(fixture.Board.Model.GetCell(captured), Is.EqualTo(BoardCellState.Captured), "logic is committed immediately");
                Assert.That(fixture.Renderer.IsAnimating, Is.True);
                Assert.That(fixture.Renderer.GetVisualHeight(captured), Is.Zero, "presentation rises after the logical capture");

                fixture.Renderer.Tick(.15f);
                Assert.That(fixture.Renderer.GetVisualHeight(captured), Is.InRange(.01f, fixture.Renderer.TerritoryHeight - .01f));
                fixture.Renderer.Tick(.2f);
                Assert.That(fixture.Renderer.GetVisualHeight(captured), Is.EqualTo(fixture.Renderer.TerritoryHeight).Within(.0001f));
                Assert.That(fixture.Renderer.IsAnimating, Is.False);
            }
        }

        [Test]
        public void Renderer_SinksDestroyedTerritoryAndSnapsOnStageReset()
        {
            using (var fixture = new Fixture())
            {
                for (var x = 1; x < 53; x++) fixture.Board.Model.MoveTo(new GridCoordinate(x, 3));
                fixture.Board.Model.MoveTo(new GridCoordinate(53, 3));
                fixture.Renderer.Tick(1f);
                var cell = new GridCoordinate(10, 2);
                Assert.That(fixture.Board.RemoveCapturedWithinRadius(cell, 0f), Is.EqualTo(1));
                Assert.That(fixture.Renderer.GetVisualHeight(cell), Is.EqualTo(fixture.Renderer.TerritoryHeight), "sinks after the logical removal");
                fixture.Renderer.Tick(1f);
                Assert.That(fixture.Renderer.GetVisualHeight(cell), Is.Zero);

                fixture.Board.ResetBoard();
                Assert.That(fixture.Renderer.IsAnimating, Is.False);
                Assert.That(fixture.Renderer.GetVisualHeight(new GridCoordinate(10, 1)), Is.Zero, "new stage snaps flat");
                Assert.That(fixture.Renderer.GetVisualHeight(new GridCoordinate(0, 50)), Is.EqualTo(fixture.Renderer.TerritoryHeight));
            }
        }

        [Test]
        public void Renderer_SkipsDrawingChunksWithNoTerritory()
        {
            using (var fixture = new Fixture())
            {
                var perimeterOnly = fixture.Renderer.VisibleChunkCount;
                Assert.That(perimeterOnly, Is.LessThan(fixture.Renderer.ChunkCount), "interior chunks start empty and are not drawn");

                for (var x = 1; x < 53; x++) fixture.Board.Model.MoveTo(new GridCoordinate(x, 40));
                fixture.Board.Model.MoveTo(new GridCoordinate(53, 40));
                fixture.Renderer.Tick(1f);
                Assert.That(fixture.Renderer.VisibleChunkCount, Is.GreaterThan(perimeterOnly), "captured chunks start drawing");
                Assert.That(fixture.Renderer.VisibleChunkCount, Is.LessThanOrEqualTo(fixture.Renderer.ChunkCount));
            }
        }

        [Test]
        public void Renderer_TrailMeshFollowsActiveTrail()
        {
            using (var fixture = new Fixture())
            {
                var trail = fixture.Renderer.transform.Find("Trail").GetComponent<MeshFilter>().sharedMesh;
                fixture.Board.Model.MoveTo(new GridCoordinate(1, 10));
                fixture.Board.Model.MoveTo(new GridCoordinate(2, 10));
                Assert.That(trail.vertexCount, Is.EqualTo(8));
                fixture.Board.CancelActiveTrail();
                Assert.That(trail.vertexCount, Is.Zero);
            }
        }

        private static void AssertTrianglesFaceTheirNormals(BoardMeshBuilder.Buffers buffers, List<int> indices)
        {
            for (var t = 0; t < indices.Count; t += 3)
            {
                var a = buffers.Vertices[indices[t]]; var b = buffers.Vertices[indices[t + 1]]; var c = buffers.Vertices[indices[t + 2]];
                Assert.That(Vector3.Dot(Vector3.Cross(b - a, c - a), buffers.Normals[indices[t]]), Is.GreaterThan(0f), "front face points outward");
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("BoardViewFixture");
            public readonly BoardManager Board;
            public readonly BoardRenderer Renderer;

            public Fixture()
            {
                root.SetActive(false);
                Board = root.AddComponent<BoardManager>();
                var view = new GameObject("BoardView");
                view.transform.SetParent(root.transform, false);
                Renderer = view.AddComponent<BoardRenderer>();
                typeof(BoardManager).GetField("boardRenderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(Board, Renderer);
                Board.Initialize();
                Renderer.Initialize(Board);
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
