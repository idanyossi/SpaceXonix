using SpaceXonix.Player;
using UnityEngine;

namespace SpaceXonix.Input
{
    /// <summary>
    /// Turns a drag into a cardinal direction. Pure so the swipe rules can be tested without a
    /// device: a swipe picks whichever axis moved further, and anything shorter than the threshold
    /// is ignored so a tap or a shaky finger never steers the ship.
    /// </summary>
    public static class SwipeModel
    {
        /// <summary>
        /// Resolves a drag from <paramref name="start"/> to <paramref name="end"/>.
        /// Returns false when the movement is too small to count as a deliberate swipe.
        /// </summary>
        public static bool TryResolve(Vector2 start, Vector2 end, float minimumDistance, out CardinalDirection direction)
        {
            direction = CardinalDirection.Right;
            var delta = end - start;
            var threshold = Mathf.Max(0f, minimumDistance);
            if (delta.sqrMagnitude < threshold * threshold) return false;

            // The dominant axis wins; a perfectly diagonal swipe resolves horizontally.
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                direction = delta.x >= 0f ? CardinalDirection.Right : CardinalDirection.Left;
            else
                direction = delta.y >= 0f ? CardinalDirection.Up : CardinalDirection.Down;
            return true;
        }

        /// <summary>
        /// The swipe threshold in pixels for a screen, expressed as a fraction of its shorter edge
        /// so the gesture feels the same on a phone and on a tablet.
        /// </summary>
        public static float ThresholdPixels(float fractionOfShortEdge, int screenWidth, int screenHeight)
        {
            var shortEdge = Mathf.Min(Mathf.Max(screenWidth, 1), Mathf.Max(screenHeight, 1));
            return Mathf.Max(1f, shortEdge * Mathf.Max(0f, fractionOfShortEdge));
        }
    }
}
