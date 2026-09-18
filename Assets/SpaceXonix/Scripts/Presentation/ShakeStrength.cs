using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>Pure mapping from gameplay events to camera-shake force. Presentation only.</summary>
    public static class ShakeStrength
    {
        /// <summary>
        /// Ordinary captures never shake: the arena stays steady like AirXonix's fixed camera.
        /// Only captures at or above the large-capture threshold shake, ramping to full force.
        /// </summary>
        public static float ForCapture(float percentageGained, float minimumPercentage, float fullPercentage, float force)
        {
            if (percentageGained < minimumPercentage) return 0f;
            if (fullPercentage <= minimumPercentage) return force;
            var t = Mathf.Clamp01((percentageGained - minimumPercentage) / (fullPercentage - minimumPercentage));
            return Mathf.Lerp(force * .5f, force, t);
        }

        /// <summary>Explosions shake with their blast size relative to a reference radius.</summary>
        public static float ForExplosion(float blastRadius, float referenceRadius, float force)
        {
            if (blastRadius <= 0f || referenceRadius <= 0f) return force;
            return force * Mathf.Clamp(blastRadius / referenceRadius, .5f, 2f);
        }

        /// <summary>
        /// Death shakes hardest and never the same way twice: a random in-plane direction and a random
        /// magnitude between the two forces, so repeated deaths do not feel like one canned effect.
        /// </summary>
        public static Vector3 ForDeath(float minForce, float maxForce, float angleTurns, float magnitudeRoll)
        {
            var angle = Mathf.Repeat(angleTurns, 1f) * Mathf.PI * 2f;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            var magnitude = Mathf.Lerp(Mathf.Min(minForce, maxForce), Mathf.Max(minForce, maxForce), Mathf.Clamp01(magnitudeRoll));
            return direction * magnitude;
        }
    }
}
