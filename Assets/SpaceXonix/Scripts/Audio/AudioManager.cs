using System.Collections.Generic;
using SpaceXonix.Settings;
using UnityEngine;

namespace SpaceXonix.Audio
{
    /// <summary>
    /// Plays the game's sounds and music at the volumes the player chose. Survives scene loads, so
    /// the menu and the gameplay scene share one mixer state and music can continue across a load.
    /// Sounds with no clip yet are simply skipped, so the game is silent rather than broken while
    /// the audio assets are still being sourced.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private SfxLibrary library;
        [SerializeField, Min(1)] private int voiceCount = 12;
        [Tooltip("Seconds to fade between two music tracks.")]
        [SerializeField, Min(0f)] private float musicCrossfade = .75f;
        [SerializeField] private int randomSeed;

        private static AudioManager instance;
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private readonly Dictionary<GameSfx, float> lastPlayedAt = new Dictionary<GameSfx, float>();
        private AudioSource musicSource;
        private GameSettingsModel settings;
        private System.Random random;
        private int nextVoice;
        private float musicTargetVolume = 1f;

        public static AudioManager Instance => instance;
        public MusicTrack CurrentTrack { get; private set; } = MusicTrack.None;
        /// <summary>Sounds actually started, so tests can assert without listening.</summary>
        public int PlayedCount { get; private set; }
        public GameSfx? LastPlayed { get; private set; }

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (instance != null && instance != this)
                {
                    Destroy(gameObject);
                    return;
                }
                if (transform.parent == null) DontDestroyOnLoad(gameObject);
            }
            instance = this;
            // Nothing is heard without a listener, and the menu had none: only the gameplay camera
            // carried one. The audio is all 2D, so where the listener sits does not matter, and
            // owning it here means every scene is heard, with exactly one listener.
            if (GetComponent<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            random = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
            BuildVoices();
            BindSettings(GameSettings.Current);
        }

        private void OnDestroy()
        {
            if (settings != null) settings.Changed -= ApplyVolumes;
            if (instance == this) instance = null;
        }

        /// <summary>Binds to the settings so volume changes are heard immediately. Public for tests.</summary>
        public void BindSettings(GameSettingsModel model)
        {
            if (settings != null) settings.Changed -= ApplyVolumes;
            settings = model;
            if (settings != null) settings.Changed += ApplyVolumes;
            ApplyVolumes();
        }

        public void SetLibrary(SfxLibrary value) => library = value;

        /// <summary>
        /// Plays a sound. Returns false when it was skipped, which happens when it has no clip yet,
        /// when it repeated inside its minimum interval, or when the player has muted everything.
        /// </summary>
        public bool Play(GameSfx sfx)
        {
            if (library == null) return false;
            var definition = library.Find(sfx);
            if (definition == null || !definition.HasClip) return false;
            if (settings != null && settings.EffectiveSfxVolume <= 0f) return false;

            // Frequent events like trail steps would otherwise stack into a buzz.
            if (definition.minimumInterval > 0f &&
                lastPlayedAt.TryGetValue(sfx, out var last) &&
                Time.unscaledTime - last < definition.minimumInterval) return false;

            var clip = definition.PickClip(random);
            if (clip == null) return false;
            var voice = NextVoice();
            if (voice == null) return false;
            voice.clip = clip;
            voice.pitch = definition.PickPitch(random);
            voice.volume = definition.volume * (settings != null ? settings.EffectiveSfxVolume : 1f);
            voice.Play();

            lastPlayedAt[sfx] = Time.unscaledTime;
            PlayedCount++;
            LastPlayed = sfx;
            return true;
        }

        /// <summary>Switches music. The same track twice is left alone so it does not restart.</summary>
        public void PlayMusic(MusicTrack track)
        {
            if (CurrentTrack == track) return;
            CurrentTrack = track;
            if (musicSource == null) return;
            var clip = library != null ? library.TrackFor(track) : null;
            if (clip == null)
            {
                musicSource.Stop();
                musicSource.clip = null;
                return;
            }
            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.volume = 0f;
            musicSource.Play();
            ApplyVolumes();
        }

        public void StopMusic() => PlayMusic(MusicTrack.None);

        private void Update()
        {
            if (musicSource == null || !musicSource.isPlaying) return;
            // Unscaled, so music keeps fading normally while the game is paused.
            musicSource.volume = musicCrossfade <= 0f
                ? musicTargetVolume
                : Mathf.MoveTowards(musicSource.volume, musicTargetVolume, Time.unscaledDeltaTime / musicCrossfade);
        }

        private void ApplyVolumes()
        {
            musicTargetVolume = settings != null ? settings.EffectiveMusicVolume : 1f;
            if (musicSource != null && musicCrossfade <= 0f) musicSource.volume = musicTargetVolume;
        }

        private void BuildVoices()
        {
            if (voices.Count > 0) return;
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            // Music and effects are 2D: nothing in a top-down arena benefits from panning by position.
            musicSource.spatialBlend = 0f;
            for (var i = 0; i < voiceCount; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
                voices.Add(voice);
            }
        }

        /// <summary>Round-robins the voices, preferring one that is not already busy.</summary>
        private AudioSource NextVoice()
        {
            if (voices.Count == 0) return null;
            for (var i = 0; i < voices.Count; i++)
            {
                var candidate = voices[(nextVoice + i) % voices.Count];
                if (candidate.isPlaying) continue;
                nextVoice = (nextVoice + i + 1) % voices.Count;
                return candidate;
            }
            // Everything is busy: steal the oldest slot rather than dropping the sound.
            var stolen = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;
            return stolen;
        }
    }
}
