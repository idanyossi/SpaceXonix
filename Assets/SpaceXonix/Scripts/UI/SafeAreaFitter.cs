using UnityEngine;

namespace SpaceXonix.UI
{
    /// <summary>
    /// Keeps a RectTransform inside the device's safe area, so notches and gesture bars never
    /// cover the HUD on Android. The anchor maths is static and pure so it can be unit tested
    /// without a device.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [Tooltip("Apply the horizontal insets. Portrait phones rarely need this.")]
        [SerializeField] private bool applyHorizontal = true;
        [Tooltip("Apply the vertical insets, which cover notches and the gesture bar.")]
        [SerializeField] private bool applyVertical = true;

        private RectTransform target;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        public Vector2 AppliedAnchorMin { get; private set; } = Vector2.zero;
        public Vector2 AppliedAnchorMax { get; private set; } = Vector2.one;

        private void Awake() => target = GetComponent<RectTransform>();

        private void OnEnable() => Apply(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));

        // The safe area changes on rotation and on some foldables, so it is cheap-checked each frame.
        private void Update()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea == lastSafeArea && screenSize == lastScreenSize) return;
            Apply(Screen.safeArea, screenSize);
        }

        /// <summary>Applies one safe area. Public so tests and the editor can drive it directly.</summary>
        public void Apply(Rect safeArea, Vector2Int screenSize)
        {
            lastSafeArea = safeArea;
            lastScreenSize = screenSize;
            CalculateAnchors(safeArea, screenSize, applyHorizontal, applyVertical, out var min, out var max);
            AppliedAnchorMin = min;
            AppliedAnchorMax = max;
            if (target == null) target = GetComponent<RectTransform>();
            if (target == null) return;
            target.anchorMin = min;
            target.anchorMax = max;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Converts a pixel safe area into normalised anchors. A zero-sized screen falls back to
        /// the full rect rather than dividing by zero, which happens during some editor reloads.
        /// </summary>
        public static void CalculateAnchors(Rect safeArea, Vector2Int screenSize, bool applyHorizontal, bool applyVertical,
            out Vector2 anchorMin, out Vector2 anchorMax)
        {
            anchorMin = Vector2.zero;
            anchorMax = Vector2.one;
            if (screenSize.x <= 0 || screenSize.y <= 0) return;

            var min = new Vector2(safeArea.xMin / screenSize.x, safeArea.yMin / screenSize.y);
            var max = new Vector2(safeArea.xMax / screenSize.x, safeArea.yMax / screenSize.y);
            if (applyHorizontal)
            {
                anchorMin.x = Mathf.Clamp01(min.x);
                anchorMax.x = Mathf.Clamp01(max.x);
            }
            if (applyVertical)
            {
                anchorMin.y = Mathf.Clamp01(min.y);
                anchorMax.y = Mathf.Clamp01(max.y);
            }
            // A degenerate area (bad platform data) would collapse the panel; keep it usable instead.
            if (anchorMax.x <= anchorMin.x) { anchorMin.x = 0f; anchorMax.x = 1f; }
            if (anchorMax.y <= anchorMin.y) { anchorMin.y = 0f; anchorMax.y = 1f; }
        }
    }
}
