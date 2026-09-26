using System.Collections.Generic;
using UnityEngine;

namespace SpaceXonix.Board
{
    /// <summary>
    /// A raised metal frame round the arena, standing a little taller than captured territory, with a
    /// glowing lip along its inner edge that slowly breathes. It sits outside the playable grid, so
    /// it never covers a cell. Built once by <see cref="BoardRenderer"/>; presentation only.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ArenaRim : MonoBehaviour
    {
        public const int TopSubmesh = 0, WallSubmesh = 1, LipSubmesh = 2;

        [SerializeField] private Material topMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material lipMaterial;
        [Tooltip("Width of the frame's top, in world units. A cell is 0.18.")]
        [SerializeField, Min(.01f)] private float frameWidth = .3f;
        [Tooltip("Height of the frame above the floor. Captured territory stands 0.35.")]
        [SerializeField, Min(.01f)] private float frameHeight = .55f;
        [Tooltip("How far the outer wall drops below the floor, so the frame reads as a solid slab.")]
        [SerializeField, Min(0f)] private float outerDepth = .6f;
        [SerializeField, Min(.005f)] private float lipWidth = .05f;
        [Tooltip("World length of one repeat of the top texture along the frame.")]
        [SerializeField, Min(.01f)] private float textureLength = 2f;
        [Tooltip("World size of one repeat of the wall texture, matching the territory walls.")]
        [SerializeField, Min(.01f)] private float wallTextureSize = 2f;

        [Header("Glow")]
        [SerializeField] private Color lipColor = new Color(.5f, .95f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float lipPulse = .3f;
        [SerializeField, Min(.1f)] private float pulseSeconds = 2.6f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;
        private Mesh mesh;

        public Mesh Mesh => mesh;
        public float FrameWidth => frameWidth;
        public float FrameHeight => frameHeight;

        /// <summary>Builds the frame round a board of the given world size, with its corner at this object's origin.</summary>
        public void Build(float boardWidth, float boardHeight)
        {
            if (mesh == null) mesh = new Mesh { name = "ArenaRim", hideFlags = HideFlags.DontSave };
            BuildMesh(mesh, boardWidth, boardHeight, frameWidth, frameHeight, outerDepth, lipWidth, textureLength, wallTextureSize);
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = new[] { topMaterial, wallMaterial, lipMaterial };
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            ApplyLip(0f);
        }

        private void Update() => ApplyLip(Time.time);

        private void ApplyLip(float time)
        {
            if (lipMaterial == null) return;
            var meshRenderer = GetComponent<MeshRenderer>();
            block ??= new MaterialPropertyBlock();
            var breath = 1f - lipPulse * (.5f + .5f * Mathf.Sin(time * Mathf.PI * 2f / pulseSeconds));
            var colour = lipColor * breath;
            colour.a = lipColor.a;
            meshRenderer.GetPropertyBlock(block, LipSubmesh);
            block.SetColor(BaseColorId, colour);
            meshRenderer.SetPropertyBlock(block, LipSubmesh);
        }

        /// <summary>
        /// Fills <paramref name="target"/> with the frame: submesh 0 the top (UV u along the frame,
        /// v 0 at the inner edge to 1 at the outer), 1 the inner and outer walls, 2 the glowing lip.
        /// "Up" off the board is world -Z.
        /// </summary>
        public static void BuildMesh(Mesh target, float w, float h, float frame, float height, float depth, float lip,
            float textureLength, float wallTextureSize)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tops = new List<int>();
            var walls = new List<int>();
            var lips = new List<int>();
            var top = -height;
            var u = 1f / textureLength;

            // Top: the long strips run the full width and cover the corners; the sides fit between them.
            Quad(vertices, normals, uvs, tops, Vector3.back,
                new Vector3(-frame, -frame, top), new Vector3(w + frame, -frame, top), new Vector3(-frame, 0f, top), new Vector3(w + frame, 0f, top),
                new Vector2(-frame * u, 1f), new Vector2((w + frame) * u, 1f), new Vector2(-frame * u, 0f), new Vector2((w + frame) * u, 0f));
            Quad(vertices, normals, uvs, tops, Vector3.back,
                new Vector3(-frame, h, top), new Vector3(w + frame, h, top), new Vector3(-frame, h + frame, top), new Vector3(w + frame, h + frame, top),
                new Vector2(-frame * u, 0f), new Vector2((w + frame) * u, 0f), new Vector2(-frame * u, 1f), new Vector2((w + frame) * u, 1f));
            Quad(vertices, normals, uvs, tops, Vector3.back,
                new Vector3(-frame, 0f, top), new Vector3(0f, 0f, top), new Vector3(-frame, h, top), new Vector3(0f, h, top),
                new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(h * u, 1f), new Vector2(h * u, 0f));
            Quad(vertices, normals, uvs, tops, Vector3.back,
                new Vector3(w, 0f, top), new Vector3(w + frame, 0f, top), new Vector3(w, h, top), new Vector3(w + frame, h, top),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(h * u, 0f), new Vector2(h * u, 1f));

            // Inner walls face into the arena, from the frame top down to the floor.
            var s = 1f / wallTextureSize;
            Wall(vertices, normals, uvs, walls, new Vector3(0f, 0f, 0f), new Vector3(w, 0f, 0f), top, 0f, Vector3.up, s);
            Wall(vertices, normals, uvs, walls, new Vector3(0f, h, 0f), new Vector3(w, h, 0f), top, 0f, Vector3.down, s);
            Wall(vertices, normals, uvs, walls, new Vector3(0f, 0f, 0f), new Vector3(0f, h, 0f), top, 0f, Vector3.right, s);
            Wall(vertices, normals, uvs, walls, new Vector3(w, 0f, 0f), new Vector3(w, h, 0f), top, 0f, Vector3.left, s);
            // Outer walls face away, dropping below the floor.
            Wall(vertices, normals, uvs, walls, new Vector3(-frame, -frame, 0f), new Vector3(w + frame, -frame, 0f), top, depth, Vector3.down, s);
            Wall(vertices, normals, uvs, walls, new Vector3(-frame, h + frame, 0f), new Vector3(w + frame, h + frame, 0f), top, depth, Vector3.up, s);
            Wall(vertices, normals, uvs, walls, new Vector3(-frame, -frame, 0f), new Vector3(-frame, h + frame, 0f), top, depth, Vector3.left, s);
            Wall(vertices, normals, uvs, walls, new Vector3(w + frame, -frame, 0f), new Vector3(w + frame, h + frame, 0f), top, depth, Vector3.right, s);

            // The lip: a thin bright band on the top along the inner edge, a hair above it so it never flickers.
            var z = top - .004f;
            Quad(vertices, normals, uvs, lips, Vector3.back,
                new Vector3(-lip, -lip, z), new Vector3(w + lip, -lip, z), new Vector3(-lip, 0f, z), new Vector3(w + lip, 0f, z));
            Quad(vertices, normals, uvs, lips, Vector3.back,
                new Vector3(-lip, h, z), new Vector3(w + lip, h, z), new Vector3(-lip, h + lip, z), new Vector3(w + lip, h + lip, z));
            Quad(vertices, normals, uvs, lips, Vector3.back,
                new Vector3(-lip, 0f, z), new Vector3(0f, 0f, z), new Vector3(-lip, h, z), new Vector3(0f, h, z));
            Quad(vertices, normals, uvs, lips, Vector3.back,
                new Vector3(w, 0f, z), new Vector3(w + lip, 0f, z), new Vector3(w, h, z), new Vector3(w + lip, h, z));

            target.Clear();
            target.SetVertices(vertices);
            target.SetNormals(normals);
            target.SetUVs(0, uvs);
            target.subMeshCount = 3;
            target.SetTriangles(tops, TopSubmesh);
            target.SetTriangles(walls, WallSubmesh);
            target.SetTriangles(lips, LipSubmesh);
            target.RecalculateBounds();
        }

        private static void Wall(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3 from, Vector3 to, float top, float bottom, Vector3 facing, float scale)
        {
            var length = Vector3.Distance(from, to);
            Quad(vertices, normals, uvs, triangles, facing,
                new Vector3(from.x, from.y, bottom), new Vector3(to.x, to.y, bottom), new Vector3(from.x, from.y, top), new Vector3(to.x, to.y, top),
                new Vector2(0f, -bottom * scale), new Vector2(length * scale, -bottom * scale), new Vector2(0f, -top * scale), new Vector2(length * scale, -top * scale));
        }

        private static void Quad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, Vector3 facing,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Quad(vertices, normals, uvs, triangles, facing, a, b, c, d, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f));
        }

        /// <summary>Adds a quad (a, b along one edge, c, d along the other) wound to face <paramref name="facing"/>.</summary>
        private static void Quad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, Vector3 facing,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            var start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (var i = 0; i < 4; i++) normals.Add(facing);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            // Unity treats clockwise-as-seen as the front, where cross(b - a, c - a) points away from the viewer.
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            {
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start + 2); triangles.Add(start + 3); triangles.Add(start + 1);
            }
            else
            {
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start + 1); triangles.Add(start + 3); triangles.Add(start + 2);
            }
        }
    }
}
