using System.Collections.Generic;

namespace SpaceXonix.Hazards
{
    /// <summary>
    /// Chooses the row or column a laser fires along. Each warning moves the laser somewhere new, so
    /// the player cannot learn a safe lane. Lines keep a margin from the board's edges, where a beam
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
        public int Pick(int lineCount, int edgeMargin, int separation, IReadOnlyList<int> occupied)
        {
            var min = edgeMargin;
            var max = lineCount - 1 - edgeMargin;
            if (max < min) return lineCount / 2;
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
