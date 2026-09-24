using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// The deep-space scene behind the arena: layered pixel art that faces the camera and drifts, so
    /// the ship's deck reads as floating in a living sky rather than on a flat grey fill.
    ///
    /// Every layer faces the camera and is re-fitted each frame, so it covers the view at any aspect,
    /// portrait phone or landscape editor. Layers sit at a fraction of the camera's far plane, which
    /// the rig changes with the framing, so they always render behind the board and never get clipped.
    /// The illusion of depth comes from drift speed: nearer layers move faster.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class SpaceBackdrop : MonoBehaviour
    {
        private static readonly int BaseMapTiling = Shader.PropertyToID("_BaseMap_ST");

        public enum LayerFit
        {
            /// <summary>Covers the whole view, tiling sideways; for the backdrop, stars and planet field.</summary>
            FillView,
            /// <summary>A single object at a point in the view; for the feature planets.</summary>
            Feature
        }

        [System.Serializable]
        public sealed class Layer
        {
            public string name = "Layer";
            public Material material;
            public LayerFit fit = LayerFit.FillView;
            [Tooltip("Where along the camera's far plane the layer sits. Larger is further back and drawn earlier.")]
            [Range(.5f, .98f)] public float depth = .9f;
            [Tooltip("How many times the texture spans the view's height. 1 fills it exactly.")]
            [Min(.1f)] public float verticalRepeats = 1f;
            [Tooltip("Texture repeats per second the layer drifts, for FillView layers.")]
            public Vector2 drift;
            [Tooltip("Viewport position of a Feature layer's centre.")]
            public Vector2 viewportPosition = new Vector2(.5f, .5f);
            [Tooltip("A Feature layer's height as a fraction of the view's height.")]
            [Range(.01f, 1f)] public float heightFraction = .3f;
            [Tooltip("How far a Feature layer bobs, as a fraction of the view's height, and how fast.")]
            [Range(0f, .1f)] public float bobAmount = .01f;
            [Min(0f)] public float bobSpeed = .2f;

            [System.NonSerialized] public Transform quad;
            [System.NonSerialized] public Renderer renderer;
            [System.NonSerialized] public Vector2 offset;
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Layer[] layers;

        private MaterialPropertyBlock block;
        private Mesh quadMesh;

        public Layer[] Layers => layers;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            block = new MaterialPropertyBlock();
            quadMesh = BuildQuad();
            if (layers == null) return;
            foreach (var layer in layers) CreateQuad(layer);
        }

        private void OnDestroy()
        {
            if (quadMesh != null) Destroy(quadMesh);
        }

        private void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null || layers == null) return;
            Fit(targetCamera, Time.unscaledDeltaTime, Time.unscaledTime);
        }

        /// <summary>Places and sizes every layer for a camera. Public so tests and renders can drive it.</summary>
        public void Fit(Camera camera, float deltaTime, float time)
        {
            if (layers == null) return;
            var cameraTransform = camera.transform;
            foreach (var layer in layers)
            {
                if (layer.quad == null) continue;
                var distance = camera.farClipPlane * layer.depth;
                var viewHeight = 2f * distance * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
                var viewWidth = viewHeight * camera.aspect;
                var texture = layer.material != null ? layer.material.mainTexture : null;
                var textureAspect = texture != null ? (float)texture.width / texture.height : 1f;

                if (layer.fit == LayerFit.FillView)
                {
                    layer.quad.position = cameraTransform.position + cameraTransform.forward * distance;
                    layer.quad.rotation = cameraTransform.rotation;
                    layer.quad.localScale = new Vector3(viewWidth, viewHeight, 1f);
                    // Keep texels square: the sideways repeat follows the view's shape, not the texture's.
                    var repeatsY = layer.verticalRepeats;
                    var repeatsX = repeatsY * (viewWidth / viewHeight) / textureAspect;
                    layer.offset += layer.drift * deltaTime;
                    layer.offset.x = Mathf.Repeat(layer.offset.x, 1f);
                    layer.offset.y = Mathf.Repeat(layer.offset.y, 1f);
                    // Centre the tiling on the view, so a portrait crop keeps the middle of the art.
                    var centre = new Vector2(.5f - repeatsX * .5f, .5f - repeatsY * .5f);
                    SetTiling(layer.renderer, new Vector4(repeatsX, repeatsY, centre.x + layer.offset.x, centre.y + layer.offset.y));
                }
                else
                {
                    var height = viewHeight * layer.heightFraction;
                    var bob = Mathf.Sin(time * layer.bobSpeed * Mathf.PI * 2f) * layer.bobAmount * viewHeight;
                    var local = new Vector3((layer.viewportPosition.x - .5f) * viewWidth, (layer.viewportPosition.y - .5f) * viewHeight + bob, distance);
                    layer.quad.position = cameraTransform.TransformPoint(local);
                    layer.quad.rotation = cameraTransform.rotation;
                    layer.quad.localScale = new Vector3(height * textureAspect, height, 1f);
                    SetTiling(layer.renderer, new Vector4(1f, 1f, 0f, 0f));
                }
            }
        }

        private void SetTiling(Renderer target, Vector4 tiling)
        {
            if (target == null) return;
            block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetVector(BaseMapTiling, tiling);
            target.SetPropertyBlock(block);
        }

        private void CreateQuad(Layer layer)
        {
            var go = new GameObject($"Backdrop_{layer.name}") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = layer.material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            layer.quad = go.transform;
            layer.renderer = meshRenderer;
        }

        /// <summary>A unit quad facing -Z, which is toward a camera looking down +Z.</summary>
        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "BackdropQuad", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return mesh;
        }
    }
}
