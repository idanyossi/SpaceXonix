using NUnit.Framework;
using SpaceXonix.Presentation;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class RingMeshTests
    {
        [Test]
        public void Ring_SpansInnerAndOuterRadiusFacingTheCamera()
        {
            var mesh = RingMeshBuilder.Create(.45f, .6f, 32);
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(64));
                Assert.That(mesh.triangles.Length, Is.EqualTo(32 * 6));
                var min = float.MaxValue;
                var max = float.MinValue;
                foreach (var vertex in mesh.vertices)
                {
                    Assert.That(vertex.z, Is.Zero, "the ring is flat");
                    var radius = new Vector2(vertex.x, vertex.y).magnitude;
                    min = Mathf.Min(min, radius);
                    max = Mathf.Max(max, radius);
                }
                Assert.That(min, Is.EqualTo(.45f).Within(.0001f));
                Assert.That(max, Is.EqualTo(.6f).Within(.0001f));
                foreach (var normal in mesh.normals) Assert.That(normal, Is.EqualTo(Vector3.back), "faces the camera");

                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                for (var t = 0; t < triangles.Length; t += 3)
                {
                    var a = vertices[triangles[t]]; var b = vertices[triangles[t + 1]]; var c = vertices[triangles[t + 2]];
                    Assert.That(Vector3.Cross(b - a, c - a).z, Is.LessThan(0f), "front faces point toward the camera");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Ring_RejectsInvalidRadiiAndClampsSegments()
        {
            Assert.That(() => RingMeshBuilder.Create(.6f, .4f, 32), Throws.ArgumentException);
            var mesh = RingMeshBuilder.Create(.1f, .2f, 2);
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(16), "segments clamp to a usable minimum");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
