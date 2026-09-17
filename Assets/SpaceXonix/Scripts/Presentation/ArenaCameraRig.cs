using SpaceXonix.Board;
using Unity.Cinemachine;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Frames the board with a perspective diagonal-down camera and owns presentation roll (Arena Tilt).
    /// Positive roll lowers the right side of the arena on screen.
    /// </summary>
    public sealed class ArenaCameraRig : MonoBehaviour
    {
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [Tooltip("Camera whose aspect ratio is framed. Falls back to the screen.")]
        [SerializeField] private Camera outputCamera;
        [SerializeField, Range(0f, 80f)] private float pitchDegrees = 35f;
        [SerializeField, Range(5f, 90f)] private float verticalFieldOfView = 30f;
        [Tooltip("Height above the board that must stay in frame (hovering actors).")]
        [SerializeField, Min(0f)] private float framedHeight = .6f;
        [SerializeField, Range(0f, .3f)] private float sideMargin = .03f;
        [SerializeField, Range(0f, .4f)] private float bottomMargin = .08f;
        [SerializeField, Range(.5f, 1f)] private float topMargin = .86f;
        [SerializeField, Min(.01f)] private float rollBlendSeconds = .25f;

        private float framedAspect = -1f;
        private float rollVelocity;

        public float PitchDegrees => pitchDegrees;
        public float VerticalFieldOfView => verticalFieldOfView;
        public float CurrentRoll { get; private set; }
        public float TargetRoll { get; private set; }
        public ArenaFramingResult LastFraming { get; private set; }

        private void Start() => Reframe();

        private void LateUpdate()
        {
            if (!Mathf.Approximately(CurrentAspect(), framedAspect)) Reframe();
            if (Mathf.Approximately(CurrentRoll, TargetRoll)) return;
            CurrentRoll = Mathf.SmoothDamp(CurrentRoll, TargetRoll, ref rollVelocity, rollBlendSeconds * .5f);
            if (Mathf.Abs(CurrentRoll - TargetRoll) < .01f) CurrentRoll = TargetRoll;
            ApplyRoll();
        }

        public void SetRoll(float degrees)
        {
            TargetRoll = degrees;
            if (Application.isPlaying) return;
            CurrentRoll = degrees;
            rollVelocity = 0f;
            ApplyRoll();
        }

        public void Reframe()
        {
            framedAspect = CurrentAspect();
            if (boardManager != null)
            {
                var board = new Rect(boardManager.transform.position.x, boardManager.transform.position.y,
                    boardManager.Columns * boardManager.CellWorldSize, boardManager.Rows * boardManager.CellWorldSize);
                LastFraming = ArenaFraming.Solve(board, framedHeight, pitchDegrees, verticalFieldOfView, framedAspect, sideMargin, bottomMargin, topMargin);
                transform.position = LastFraming.Position;
            }
            if (cinemachineCamera != null)
            {
                var lens = cinemachineCamera.Lens;
                lens.ModeOverride = LensSettings.OverrideModes.Perspective;
                lens.FieldOfView = verticalFieldOfView;
                lens.NearClipPlane = .1f;
                lens.FarClipPlane = Mathf.Max(100f, LastFraming.Distance * 3f);
                cinemachineCamera.Lens = lens;
            }
            ApplyRoll();
        }

        private void ApplyRoll()
        {
            var pitch = Quaternion.Euler(-pitchDegrees, 0f, 0f);
            if (cinemachineCamera != null)
            {
                transform.rotation = pitch;
                var lens = cinemachineCamera.Lens;
                lens.Dutch = CurrentRoll;
                cinemachineCamera.Lens = lens;
                return;
            }
            transform.rotation = pitch * Quaternion.Euler(0f, 0f, CurrentRoll);
        }

        private float CurrentAspect()
        {
            if (outputCamera != null && outputCamera.pixelHeight > 0) return (float)outputCamera.pixelWidth / outputCamera.pixelHeight;
            return Screen.height > 0 ? (float)Screen.width / Screen.height : 9f / 16f;
        }
    }
}
