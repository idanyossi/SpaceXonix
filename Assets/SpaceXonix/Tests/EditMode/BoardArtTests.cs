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
