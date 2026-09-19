using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SpaceXonix.Input
{
    /// <summary>
    /// Feeds swipes into the same <see cref="InputRouter"/> the keyboard uses, so Android and PC
    /// share one input abstraction. A drag steers as soon as it passes the threshold and then
    /// re-anchors, letting one continuous finger movement turn the ship more than once.
    /// </summary>
    public sealed class TouchInputSource : MonoBehaviour
    {
        [SerializeField] private InputRouter inputRouter;
        [Tooltip("Swipe distance needed to steer, as a fraction of the screen's shorter edge.")]
        [SerializeField, Range(.005f, .2f)] private float swipeThresholdFraction = .03f;
        [Tooltip("Also treat mouse drags as swipes, so the gesture can be tried in the editor.")]
        [SerializeField] private bool allowMouseSwipe = true;

        private Vector2 swipeOrigin;
        private bool tracking;
        private bool startedOverUi;

        public bool IsTracking => tracking;
        public float ThresholdPixels => SwipeModel.ThresholdPixels(swipeThresholdFraction, Screen.width, Screen.height);

        private void Update()
        {
            var pointer = ActivePointer();
            if (pointer == null)
            {
                tracking = false;
                return;
            }
            if (pointer.press.wasPressedThisFrame) BeginSwipe(pointer.position.ReadValue(), pointer.deviceId);
            else if (pointer.press.isPressed) ContinueSwipe(pointer.position.ReadValue());
            else tracking = false;
        }

        /// <summary>Touch wins when present; the mouse is only a convenience for editor testing.</summary>
        private Pointer ActivePointer()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) return Touchscreen.current;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame) return Touchscreen.current;
            if (allowMouseSwipe && Mouse.current != null) return Mouse.current;
            return Touchscreen.current;
        }

        private void BeginSwipe(Vector2 position, int deviceId)
        {
            swipeOrigin = position;
            // A press that lands on a button belongs to the UI, never to the ship.
            startedOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(deviceId);
            tracking = !startedOverUi;
        }

        private void ContinueSwipe(Vector2 position)
        {
            if (!tracking || inputRouter == null) return;
            if (!SwipeModel.TryResolve(swipeOrigin, position, ThresholdPixels, out var direction)) return;
            inputRouter.TrySelectDirection(direction);
            // Re-anchor so a long drag can steer again rather than firing once per touch.
            swipeOrigin = position;
        }

        /// <summary>Drives one swipe directly. Used by the on-screen controls and by tests.</summary>
        public bool ResolveSwipe(Vector2 start, Vector2 end)
        {
            if (inputRouter == null) return false;
            if (!SwipeModel.TryResolve(start, end, ThresholdPixels, out var direction)) return false;
            return inputRouter.TrySelectDirection(direction);
        }
    }
}
