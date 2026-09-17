using System;
using UnityEngine;

namespace SpaceXonix.Scoring
{
    [Serializable]
    public struct CaptureMultiplierTier
    {
        [Min(0f)] public float minimumPercentage;
        [Min(0f)] public float multiplier;

        public CaptureMultiplierTier(float minimumPercentage, float multiplier)
        {
            this.minimumPercentage = minimumPercentage;
            this.multiplier = multiplier;
        }
    }

    [CreateAssetMenu(menuName = "SpaceXonix/Scoring Definition")]
    public sealed class ScoringDefinition : ScriptableObject
    {
        [Min(0f)] public float pointsPerCapturedPercent = 100f;

        [Tooltip("The highest tier whose minimum is reached by a single capture applies. Captures below every tier use x1.")]
        public CaptureMultiplierTier[] largeCaptureTiers =
        {
            new CaptureMultiplierTier(5f, 1.5f),
            new CaptureMultiplierTier(10f, 2f),
            new CaptureMultiplierTier(15f, 3f)
        };
    }
}
