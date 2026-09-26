using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpaceXonix.UI
{
    /// <summary>
    /// Reports a horizontal swipe across a UI element: +1 for a drag to the right, -1 to the left.
    /// Works with a finger or a mouse drag, through the EventSystem, so it needs a raycast target.
    /// </summary>
    public sealed class HorizontalSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("How far, in screen pixels, a drag must travel sideways to count, as a fraction of the screen width.")]
        [SerializeField, Range(.01f, .5f)] private float threshold = .08f;

        private Vector2 start;

        public event Action<int> Swiped;

        public void OnBeginDrag(PointerEventData eventData) => start = eventData.position;

        // Needed so the EventSystem sends begin and end drag at all.
        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData) => Resolve(eventData.position - start, Screen.width);

        /// <summary>Turns a drag into a swipe when it is long enough and mostly sideways. Public for tests.</summary>
        public bool Resolve(Vector2 delta, float screenWidth)
        {
            if (Mathf.Abs(delta.x) < threshold * Mathf.Max(1f, screenWidth) || Mathf.Abs(delta.x) < Mathf.Abs(delta.y)) return false;
            Swiped?.Invoke(delta.x > 0f ? 1 : -1);
            return true;
        }
    }
}
