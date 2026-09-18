using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>Pure mapping from gameplay events to camera-shake force. Presentation only.</summary>
    public static class ShakeStrength
    {
        /// <summary>Captures shake in proportion to the area claimed, from a light tap to the large-capture punch.</summary>
        public static float ForCapture(float percentageGained, float smallForce, float largeForce, float largeCapturePercentage)
        {
            if (percentageGained <= 0f) return 0f;
            if (largeCapturePercentage <= 0f) return largeForce;
            var t = Mathf.Clamp01(percentageGained / largeCapturePercentage);
            return Mathf.Lerp(smallForce, largeForce, t);
        }

        /// <summary>Explosions shake with their blast size relative to a reference radius.</summary>
        public static float ForExplosion(float blastRadius, float referenceRadius, float force)
        {
            if (blastRadius <= 0f || referenceRadius <= 0f) return force;
            return force * Mathf.Clamp(blastRadius / referenceRadius, .5f, 2f);
        }
    }
}
