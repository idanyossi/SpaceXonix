using System.Collections.Generic;

namespace SpaceXonix.Hazards
{
    /// <summary>
    /// Chooses the row or column a laser fires along. Each warning moves the laser somewhere new near
    /// the ship, so the player can neither learn a safe lane nor sit far from every laser. Lines keep a margin from the board's edges, where a beam
    /// would only ever cross captured border, and prefer a gap from other lasers on the same axis so
    /// two beams do not stack into one.
    /// </summary>
    public sealed class LaserLinePicker
    {
        private const int Attempts = 8;
        private readonly System.Random random;

        public LaserLinePicker(int seed = 0) => random = seed != 0 ? new System.Random(seed) : new System.Random();

        /// <param name="lineCount">Rows for a horizontal laser, columns for a vertical one.</param>
        /// <param name="occupied">Lines other lasers on the same axis are using.</param>
        /// <param name="focus">A line to stay near, usually the ship's row or column; ignored when negative.</param>
        /// <param name="vicinity">How many lines either side of <paramref name="focus"/> are allowed; 0 allows anywhere.</param>
        public int Pick(int lineCount, int edgeMargin, int separation, IReadOnlyList<int> occupied, int focus = -1, int vicinity = 0)
        {
            var min = edgeMargin;
            var max = lineCount - 1 - edgeMargin;
            if (max < min) return lineCount / 2;
            if (focus >= 0 && vicinity > 0)
            {
                // Near the ship, but still random within the window, so it cannot just sit still.
                var clampedFocus = System.Math.Min(max, System.Math.Max(min, focus));
                min = System.Math.Max(min, clampedFocus - vicinity);
                max = System.Math.Min(max, clampedFocus + vicinity);
            }
            var line = random.Next(min, max + 1);
            for (var attempt = 1; attempt < Attempts && TooClose(line, separation, occupied); attempt++)
                line = random.Next(min, max + 1);
            return line;
        }

        private static bool TooClose(int line, int separation, IReadOnlyList<int> occupied)
        {
            if (occupied == null) return false;
            for (var i = 0; i < occupied.Count; i++)
                if (System.Math.Abs(occupied[i] - line) < separation) return true;
            return false;
        }
    }
}
