using UnityEngine;

namespace SpaceXonix.Hazards
{
    public sealed class LaserPresentation : MonoBehaviour
    {
        private static readonly int BaseMapTiling = Shader.PropertyToID("_BaseMap_ST");

        [Tooltip("World units covered by one repeat of the texture along the beam, so pixel art tiles rather than stretching across the whole board.")]
        [SerializeField, Min(.01f)] private float patternLength = .5f;
        [Tooltip("Texture repeats per second the pattern flows along the beam. 0 holds it still.")]
        [SerializeField] private float scrollSpeed;

        private MaterialPropertyBlock block;
        private Renderer beamRenderer;
        private float repeats = 1f;
        private float offset;

        public float Repeats => repeats;

        /// <summary>Height lifts the presentation off the board floor toward the camera (-Z).</summary>
        public void Configure(Vector3 center, LaserAxis axis, float length, float width, float height = 0f)
        {
            center.z -= height;
            transform.position = center;
            transform.rotation = Quaternion.Euler(0f, 0f, axis == LaserAxis.Horizontal ? 0f : 90f);
            transform.localScale = new Vector3(length, width, width);
            repeats = Mathf.Max(1f, length / patternLength);
            ApplyTiling();
        }

        private void Update()
        {
            if (scrollSpeed == 0f) return;
            // Unscaled, so a paused game does not stop the beam from reading as live.
            offset = Mathf.Repeat(offset + scrollSpeed * Time.unscaledDeltaTime, 1f);
            ApplyTiling();
        }

        // A property block tiles each pooled beam without creating a material per instance.
        private void ApplyTiling()
        {
            if (beamRenderer == null) beamRenderer = GetComponentInChildren<Renderer>();
            if (beamRenderer == null) return;
            block ??= new MaterialPropertyBlock();
            beamRenderer.GetPropertyBlock(block);
            block.SetVector(BaseMapTiling, new Vector4(repeats, 1f, offset, 0f));
            beamRenderer.SetPropertyBlock(block);
        }
    }
}
