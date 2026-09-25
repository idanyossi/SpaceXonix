using UnityEngine;

namespace SpaceXonix.UI
{
    /// <summary>
    /// Shrinks a fixed-size panel evenly until it fits inside its parent, keeping its layout. The
    /// hangar is designed for a portrait phone; in a landscape window, or on a squat screen, it
    /// would otherwise run off the top and bottom. It never scales a panel up past its design size.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UniformFit : MonoBehaviour
    {
        [Tooltip("Space kept free around the panel, in canvas units.")]
        [SerializeField, Min(0f)] private float margin = 24f;

        public float Scale { get; private set; } = 1f;

        private void OnEnable() => Fit();

        private void OnRectTransformDimensionsChange() => Fit();

        private void Update() => Fit();

        /// <summary>Recomputes the scale. Public so tests can check a given parent size.</summary>
        public void Fit()
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null) return;
            var size = rect.rect.size;
            var room = parent.rect.size - Vector2.one * (margin * 2f);
            if (size.x <= 0f || size.y <= 0f || room.x <= 0f || room.y <= 0f) return;
            var scale = Mathf.Min(1f, room.x / size.x, room.y / size.y);
            if (Mathf.Approximately(scale, Scale) && Mathf.Approximately(rect.localScale.x, scale)) return;
            Scale = scale;
            rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
