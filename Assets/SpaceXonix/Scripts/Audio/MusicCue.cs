using UnityEngine;

namespace SpaceXonix.Audio
{
    /// <summary>
    /// Asks for a music track when this object wakes. The gameplay scene picks its track from the
    /// stage being loaded, but a scene like the menu has no such event, so it states its track here.
    /// </summary>
    public sealed class MusicCue : MonoBehaviour
    {
        [SerializeField] private MusicTrack track = MusicTrack.Menu;

        private void Start() => Play();

        /// <summary>Requests the track. Silent when no audio service exists. Public for tests.</summary>
        public bool Play()
        {
            var manager = AudioManager.Instance;
            if (manager == null) return false;
            manager.PlayMusic(track);
            return true;
        }
    }
}
