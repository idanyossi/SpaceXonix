using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>Builds a flat annulus (ring) in the XY plane facing the camera along -Z.</summary>
    public static class RingMeshBuilder
    {
        public static Mesh Create(float innerRadius, float outerRadius, int segments, string name = "Ring")
        {
            if (outerRadius <= innerRadius) throw new System.ArgumentException("outerRadius must exceed innerRadius");
            segments = Mathf.Max(8, segments);
            var vertices = new Vector3[segments * 2];
            var normals = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices[i * 2] = direction * innerRadius;
                vertices[i * 2 + 1] = direction * outerRadius;
                normals[i * 2] = Vector3.back;
                normals[i * 2 + 1] = Vector3.back;

                var next = (i + 1) % segments;
                var t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = next * 2;
                triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2 + 1;
                triangles[t + 4] = next * 2;
                triangles[t + 5] = next * 2 + 1;
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
