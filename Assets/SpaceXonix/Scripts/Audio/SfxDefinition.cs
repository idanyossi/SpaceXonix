using UnityEngine;

namespace SpaceXonix.Audio
{
    /// <summary>
    /// One sound's tuning. The clip may be empty while the audio assets are still being sourced;
    /// the manager then simply plays nothing rather than warning on every shot fired.
    /// </summary>
    [CreateAssetMenu(menuName = "SpaceXonix/Sfx Definition")]
    public sealed class SfxDefinition : ScriptableObject
    {
        public GameSfx sfx;
        [Tooltip("Picked at random, so a repeated sound does not become mechanical. May be empty.")]
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random pitch spread around 1, which keeps frequent sounds from grating.")]
        [Range(0f, .5f)] public float pitchVariance = .05f;
        [Tooltip("Shortest gap between two plays of this sound. Stops rapid events machine-gunning.")]
        [Min(0f)] public float minimumInterval;

        public bool HasClip => clips != null && clips.Length > 0;

        /// <summary>Picks a clip at random, or null when none are assigned yet.</summary>
        public AudioClip PickClip(System.Random random)
        {
            if (!HasClip) return null;
            if (clips.Length == 1) return clips[0];
            return clips[(random ?? new System.Random()).Next(clips.Length)];
        }

        public float PickPitch(System.Random random)
        {
            if (pitchVariance <= 0f) return 1f;
            var roll = (float)(random ?? new System.Random()).NextDouble() * 2f - 1f;
            return 1f + roll * pitchVariance;
        }
    }
}
