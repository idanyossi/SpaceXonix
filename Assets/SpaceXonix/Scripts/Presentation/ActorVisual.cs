using SpaceXonix.Board;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Lifts an actor's visual off the board and drops a shadow on the surface below it.
    /// The logical root stays on the board plane; only the child visuals move.
    /// </summary>
    public sealed class ActorVisual : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Transform shadow;
        [SerializeField] private BoardManager boardManager;
        [Tooltip("World height above the board floor. The board rises toward -Z, so this offsets the visual along -Z.")]
        [SerializeField, Min(0f)] private float hoverHeight = .55f;
        [SerializeField] private bool faceCamera = true;
        [SerializeField, Min(0f)] private float shadowLift = .01f;
        [SerializeField, Min(0f)] private float shadowScale = .8f;

        private Transform cameraTransform;

        public float HoverHeight => hoverHeight;
        public Transform Visual => visual;
        public Transform Shadow => shadow;

        public void ConnectBoard(BoardManager board) => boardManager = board;

        private void Awake()
        {
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (boardManager == null) boardManager = FindFirstObjectByType<BoardManager>();
            // A flattened sphere reads as a round blob without needing a texture.
            if (shadow != null) shadow.localScale = new Vector3(shadowScale, shadowScale, shadowScale * .06f);
        }

        private void LateUpdate() => Apply(cameraTransform != null ? cameraTransform.rotation : ResolveCameraRotation());

        /// <summary>Places the hovering visual and its shadow for the actor's current logical position.</summary>
        public void Apply(Quaternion cameraRotation)
        {
            var origin = transform.position;
            if (visual != null)
            {
                visual.position = new Vector3(origin.x, origin.y, origin.z - hoverHeight);
                if (faceCamera) visual.rotation = cameraRotation;
            }
            if (shadow == null) return;
            var surface = boardManager != null ? boardManager.GetVisualSurfaceHeight(origin) : 0f;
            shadow.position = new Vector3(origin.x, origin.y, -surface - shadowLift);
            shadow.rotation = Quaternion.identity;
        }

        private Quaternion ResolveCameraRotation()
        {
            var main = Camera.main;
            if (main == null) return Quaternion.identity;
            cameraTransform = main.transform;
            return cameraTransform.rotation;
        }
    }
}
