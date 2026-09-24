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
        /// <summary>
        /// In-plane rotation of the visual in degrees, 0 pointing up the board. Lets a sprite such as
        /// the ship face its direction of travel while still billboarding toward the camera.
        /// </summary>
        public float HeadingDegrees { get; set; }
        public bool FaceCamera => faceCamera;
        public Transform Visual => visual;
        public Transform Shadow => shadow;

        public void ConnectBoard(BoardManager board) => boardManager = board;

        private void Awake()
        {
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (boardManager == null) boardManager = FindFirstObjectByType<BoardManager>();
            // A flattened sphere reads as a round blob without needing a texture. It is sized to the
            // actor, so a tiny Volatile alien does not sit on a shadow three times its own width.
            if (shadow != null)
            {
                var size = shadowScale * VisualWidth();
                shadow.localScale = new Vector3(size, size, size * .06f);
            }
        }

        /// <summary>The visual's drawn width in world units, whether it is a sprite or a mesh.</summary>
        public float VisualWidth()
        {
            if (visual == null) return 1f;
            var sprite = visual.GetComponent<SpriteRenderer>();
            if (sprite != null && sprite.sprite != null) return sprite.sprite.bounds.size.x * visual.localScale.x;
            var filter = visual.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) return filter.sharedMesh.bounds.size.x * visual.localScale.x;
            return 1f;
        }

        private void LateUpdate() => Apply(cameraTransform != null ? cameraTransform.rotation : ResolveCameraRotation());

        /// <summary>Places the hovering visual and its shadow for the actor's current logical position.</summary>
        public void Apply(Quaternion cameraRotation)
        {
            var origin = transform.position;
            if (visual != null)
            {
                visual.position = new Vector3(origin.x, origin.y, origin.z - hoverHeight);
                var heading = Quaternion.Euler(0f, 0f, HeadingDegrees);
                // Heading is applied inside the facing, so a billboard turns on screen rather than tilting away.
                if (faceCamera) visual.rotation = cameraRotation * heading;
                else if (HeadingDegrees != 0f) visual.rotation = heading;
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
