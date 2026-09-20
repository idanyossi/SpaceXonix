using System.Collections.Generic;
using UnityEngine;

namespace SpaceXonix.Audio
{
    /// <summary>
    /// Every sound definition, looked up by <see cref="GameSfx"/>. In its own file, like every other
    /// ScriptableObject here: a type sharing a file with another is saved without a script reference
    /// and loads as null after a domain reload.
    /// </summary>
    [CreateAssetMenu(menuName = "SpaceXonix/Sfx Library")]
    public sealed class SfxLibrary : ScriptableObject
    {
        public SfxDefinition[] definitions;

        [Header("Music")]
        public AudioClip menuTrack;
        public AudioClip gameplayTrack;
        public AudioClip bossTrack;

        private Dictionary<GameSfx, SfxDefinition> lookup;

        /// <summary>Returns the definition for a sound, or null when it has none configured.</summary>
        public SfxDefinition Find(GameSfx sfx)
        {
            EnsureLookup();
            return lookup.TryGetValue(sfx, out var definition) ? definition : null;
        }

        public AudioClip TrackFor(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Menu: return menuTrack;
                case MusicTrack.Gameplay: return gameplayTrack;
                case MusicTrack.Boss: return bossTrack;
                default: return null;
            }
        }

        /// <summary>Rebuilds the lookup, for the editor after the definitions are edited.</summary>
        public void Invalidate() => lookup = null;

        private void EnsureLookup()
        {
            if (lookup != null) return;
            lookup = new Dictionary<GameSfx, SfxDefinition>();
            if (definitions == null) return;
            foreach (var definition in definitions)
            {
                if (definition == null) continue;
                // A duplicate entry is a data mistake; the first one wins rather than throwing.
                if (!lookup.ContainsKey(definition.sfx)) lookup.Add(definition.sfx, definition);
            }
        }
    }
}
