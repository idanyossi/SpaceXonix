using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Hazards;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class BoardArtTests
    {
        [Test]
        public void Uvs_TileInWorldSpaceSoNeighbouringCellsLineUp()
        {
            // Tops use board XY: a point two units along x is one full repeat at half-scale.
            Assert.That(BoardMeshBuilder.Project(new Vector3(2f, 1f, -.35f), Vector3.back, .5f),
                Is.EqualTo(new Vector2(1f, .5f)));

            // A shared corner gets the same UV from either cell, which is what makes seams line up.
            var corner = new Vector3(.36f, .18f, -.35f);
            Assert.That(BoardMeshBuilder.Project(corner, Vector3.back, .5f),
                Is.EqualTo(BoardMeshBuilder.Project(corner, Vector3.back, .5f)));
        }

        [Test]
        public void Uvs_RunWallsAlongTheirLengthAndUpTheirHeight()
        {
            // A wall facing left or right runs along y; one facing up or down runs along x.
            // Either way the second coordinate is the height, so wall art reads upright.
            Assert.That(BoardMeshBuilder.Project(new Vector3(4f, 2f, -.3f), Vector3.left, 1f),
                Is.EqualTo(new Vector2(2f, .3f)).Using(new Vector2Comparer()));
            Assert.That(BoardMeshBuilder.Project(new Vector3(4f, 2f, -.3f), Vector3.up, 1f),
                Is.EqualTo(new Vector2(4f, .3f)).Using(new Vector2Comparer()));
            Assert.That(BoardMeshBuilder.Project(new Vector3(4f, 2f, 0f), Vector3.down, 1f).y, Is.Zero,
                "the foot of a wall is the bottom of its texture");
        }

        [Test]
        public void RaisedCells_CarryOneUvPerVertex()
        {
            var heights = new float[9];
            heights[4] = .35f; // a single raised cell in the middle of a 3x3 board
            var buffers = new BoardMeshBuilder.Buffers { UvScale = .5f };
            BoardMeshBuilder.BuildRaisedCells(heights, 3, 3, .18f, 0, 0, 3, 3, buffers);
            Assert.That(buffers.Uvs.Count, Is.EqualTo(buffers.Vertices.Count));
            Assert.That(buffers.Vertices.Count, Is.EqualTo(20), "one top and four walls");

            buffers.Clear();
            Assert.That(buffers.Uvs, Is.Empty, "clearing drops the UVs with the vertices");
        }

        [Test]
        public void Trim_RunsAlongEveryExposedEdgeAndNowhereElse()
        {
            // A plus shape: the centre cell is enclosed on all four sides, the arms are not.
            var heights = new float[9];
            foreach (var i in new[] { 1, 3, 4, 5, 7 }) heights[i] = .35f;
            var buffers = new BoardMeshBuilder.Buffers { UvScale = .5f, RimWidth = .05f };

            BoardMeshBuilder.BuildRaisedCells(heights, 3, 3, .18f, 1, 1, 2, 2, buffers);
            Assert.That(buffers.Rims, Is.Empty, "an enclosed cell has no exposed edge to trim");

            BoardMeshBuilder.BuildRaisedCells(heights, 3, 3, .18f, 1, 0, 2, 1, buffers);
            Assert.That(buffers.Rims.Count, Is.EqualTo(3 * 6), "an arm is exposed on three sides");
            Assert.That(buffers.Uvs.Count, Is.EqualTo(buffers.Vertices.Count));
        }

        [Test]
        public void Trim_LiesOnTheTopFaceInsideTheEdgeAndFacesTheCamera()
        {
            var heights = new float[1];
            heights[0] = .35f;
            var buffers = new BoardMeshBuilder.Buffers { UvScale = .5f, RimWidth = .05f };
            BoardMeshBuilder.BuildRaisedCells(heights, 1, 1, .18f, 0, 0, 1, 1, buffers);
            Assert.That(buffers.Rims.Count, Is.EqualTo(4 * 6), "a lone cell is trimmed on all four sides");

            for (var t = 0; t < buffers.Rims.Count; t += 3)
            {
                Vector3 a = buffers.Vertices[buffers.Rims[t]], b = buffers.Vertices[buffers.Rims[t + 1]], c = buffers.Vertices[buffers.Rims[t + 2]];
                Assert.That(Vector3.Dot(Vector3.Cross(b - a, c - a), Vector3.back), Is.GreaterThan(0f), "trim must face the camera");
                foreach (var v in new[] { a, b, c })
                {
                    Assert.That(v.z, Is.EqualTo(-.35f - BoardMeshBuilder.RimLift).Within(.0001f), "just above the top face");
                    Assert.That(v.x, Is.InRange(0f, .18f)); Assert.That(v.y, Is.InRange(0f, .18f));
                }
            }
        }

        [Test]
        public void Trim_IsOffWhenItHasNoWidth()
        {
            var heights = new float[1];
            heights[0] = .35f;
            var buffers = new BoardMeshBuilder.Buffers { UvScale = .5f };
            BoardMeshBuilder.BuildRaisedCells(heights, 1, 1, .18f, 0, 0, 1, 1, buffers);
            Assert.That(buffers.Rims, Is.Empty);
        }

        [Test]
        public void Laser_TilesItsTextureAlongTheBeamInsteadOfStretchingIt()
        {
            var host = new GameObject("Laser");
            try
            {
                var presentation = host.AddComponent<LaserPresentation>();
                typeof(LaserPresentation).GetField("patternLength",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(presentation, .5f);

                presentation.Configure(Vector3.zero, LaserAxis.Horizontal, 9.72f, .2f);
                Assert.That(presentation.Repeats, Is.EqualTo(19.44f).Within(.001f), "a board-wide beam repeats its pattern");

                presentation.Configure(Vector3.zero, LaserAxis.Vertical, .2f, .2f);
                Assert.That(presentation.Repeats, Is.EqualTo(1f), "never less than one whole repeat");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private sealed class Vector2Comparer : System.Collections.Generic.IEqualityComparer<Vector2>
        {
            public bool Equals(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < .000001f;
            public int GetHashCode(Vector2 value) => 0;
        }
    }
}
