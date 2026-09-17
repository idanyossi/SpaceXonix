using UnityEngine;

namespace SpaceXonix.Presentation
{
    public readonly struct ArenaFramingResult
    {
        public ArenaFramingResult(Vector3 position, Quaternion rotation, float distance, bool fits)
        {
            Position = position;
            Rotation = rotation;
            Distance = distance;
            Fits = fits;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float Distance { get; }
        public bool Fits { get; }
    }

    /// <summary>
    /// Solves a perspective diagonal-down camera that fits the XY-plane board inside viewport margins.
    /// Gameplay stays on the board plane; "up" off the board is world -Z.
    /// </summary>
    public static class ArenaFraming
    {
        private const float MinDistance = 1f;
        private const float MaxDistance = 400f;

        public static ArenaFramingResult Solve(Rect board, float heightAboveBoard, float pitchDegrees, float verticalFov, float aspect,
            float sideMargin, float bottomMargin, float topMargin)
        {
            var rotation = Quaternion.Euler(-pitchDegrees, 0f, 0f);
            var center = new Vector3(board.center.x, board.center.y, 0f);
            var targetMid = (bottomMargin + topMargin) * .5f;
            float low = MinDistance, high = MaxDistance;
            var best = default(ArenaFramingResult);
            var found = false;
            for (var i = 0; i < 40; i++)
            {
                var distance = (low + high) * .5f;
                var candidate = Frame(board, heightAboveBoard, rotation, center, distance, verticalFov, aspect, targetMid, out var minX, out var maxX, out var minY, out var maxY);
                var fits = minX >= sideMargin && maxX <= 1f - sideMargin && minY >= bottomMargin && maxY <= topMargin;
                if (fits)
                {
                    best = new ArenaFramingResult(candidate, rotation, distance, true);
                    found = true;
                    high = distance;
                }
                else low = distance;
            }
            if (found) return best;
            var fallback = Frame(board, heightAboveBoard, rotation, center, MaxDistance, verticalFov, aspect, targetMid, out _, out _, out _, out _);
            return new ArenaFramingResult(fallback, rotation, MaxDistance, false);
        }

        public static Vector2 WorldToViewport(Vector3 point, Vector3 cameraPosition, Quaternion cameraRotation, float verticalFov, float aspect)
        {
            var local = Quaternion.Inverse(cameraRotation) * (point - cameraPosition);
            var tanV = Mathf.Tan(verticalFov * .5f * Mathf.Deg2Rad);
            var tanH = tanV * aspect;
            return new Vector2(local.x / (local.z * tanH) * .5f + .5f, local.y / (local.z * tanV) * .5f + .5f);
        }

        private static Vector3 Frame(Rect board, float height, Quaternion rotation, Vector3 center, float distance, float fov, float aspect,
            float targetMid, out float minX, out float maxX, out float minY, out float maxY)
        {
            var forward = rotation * Vector3.forward;
            var up = rotation * Vector3.up;
            var shift = 0f;
            var position = center - forward * distance;
            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;
            for (var pass = 0; pass < 6; pass++)
            {
                position = center - forward * distance + up * shift;
                Bounds(board, height, position, rotation, fov, aspect, out minX, out maxX, out minY, out maxY);
                var mid = (minY + maxY) * .5f;
                var tanV = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
                shift += (mid - targetMid) * 2f * tanV * distance;
            }
            position = center - forward * distance + up * shift;
            Bounds(board, height, position, rotation, fov, aspect, out minX, out maxX, out minY, out maxY);
            return position;
        }

        private static void Bounds(Rect board, float height, Vector3 position, Quaternion rotation, float fov, float aspect,
            out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;
            for (var i = 0; i < 8; i++)
            {
                var point = new Vector3((i & 1) == 0 ? board.xMin : board.xMax, (i & 2) == 0 ? board.yMin : board.yMax, (i & 4) == 0 ? 0f : -height);
                var viewport = WorldToViewport(point, position, rotation, fov, aspect);
                minX = Mathf.Min(minX, viewport.x); maxX = Mathf.Max(maxX, viewport.x);
                minY = Mathf.Min(minY, viewport.y); maxY = Mathf.Max(maxY, viewport.y);
            }
        }
    }
}
