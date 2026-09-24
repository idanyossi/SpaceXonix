using System;
using System.Collections.Generic;
using System.IO;
using SpaceXonix.Audio;
using UnityEditor;
using UnityEngine;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Synthesises a placeholder clip for every sound and music track, so the game is audible while
    /// the licensed audio is still being sourced. These are generated here rather than downloaded,
    /// so they carry no licence obligations and can be deleted the moment real assets land.
    /// </summary>
    public static class PlaceholderAudioGenerator
    {
        private const string OutputFolder = "Assets/SpaceXonix/Audio/Placeholder";
        private const string LibraryPath = "Assets/SpaceXonix/ScriptableObjects/Audio/SfxLibrary.asset";
        private const int SampleRate = 44100;

        private enum Wave { Sine, Square, Saw, Noise }

        /// <summary>One sound's synthesis recipe: a swept tone, optionally roughened with noise.</summary>
        private struct Recipe
        {
            public Wave wave;
            public float startHz;
            public float endHz;
            public float seconds;
            public float noiseMix;
            public float attack;
            public float gain;

            public Recipe(Wave wave, float startHz, float endHz, float seconds,
                float noiseMix = 0f, float attack = .01f, float gain = .5f)
            {
                this.wave = wave; this.startHz = startHz; this.endHz = endHz; this.seconds = seconds;
                this.noiseMix = noiseMix; this.attack = attack; this.gain = gain;
            }
        }

        [MenuItem("SpaceXonix/Generate Placeholder Audio")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);
            var recipes = BuildRecipes();
            var random = new System.Random(20260924);

            foreach (var pair in recipes)
            {
                var samples = Synthesise(pair.Value, random);
                File.WriteAllBytes(Path.Combine(OutputFolder, $"Sfx_{pair.Key}.wav"), EncodeWav(samples));
            }
            foreach (var pair in BuildMusic(random))
                File.WriteAllBytes(Path.Combine(OutputFolder, $"Music_{pair.Key}.wav"), EncodeWav(pair.Value));

            AssetDatabase.Refresh();
            ApplyImportSettings();
            AssignToLibrary();
            Debug.Log($"Generated {recipes.Count} placeholder sounds and 3 music loops in {OutputFolder}.");
        }

        [MenuItem("SpaceXonix/Delete Placeholder Audio")]
        public static void DeletePlaceholders()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                Debug.Log("No placeholder audio to delete.");
                return;
            }
            AssetDatabase.DeleteAsset(OutputFolder);
            AssetDatabase.Refresh();
            Debug.Log("Deleted the placeholder audio. Assign the real clips in the SfxLibrary.");
        }

        /// <summary>
        /// Each sound gets a shape that matches what it represents, so they stay distinguishable
        /// while playtesting: rising for gains, falling for losses, noise for destruction.
        /// </summary>
        private static Dictionary<GameSfx, Recipe> BuildRecipes()
        {
            return new Dictionary<GameSfx, Recipe>
            {
                { GameSfx.DirectionChanged, new Recipe(Wave.Square, 440f, 500f, .045f, 0f, .004f, .22f) },
                { GameSfx.TrailStarted, new Recipe(Wave.Saw, 300f, 420f, .08f, 0f, .006f, .28f) },
                { GameSfx.CaptureCompleted, new Recipe(Wave.Sine, 520f, 820f, .26f, 0f, .01f, .45f) },
                { GameSfx.LargeCapture, new Recipe(Wave.Sine, 420f, 980f, .55f, .05f, .012f, .6f) },
                { GameSfx.PowerMeterFull, new Recipe(Wave.Sine, 660f, 1050f, .38f, 0f, .01f, .5f) },
                { GameSfx.PowerShot, new Recipe(Wave.Square, 950f, 220f, .18f, .08f, .004f, .4f) },
                { GameSfx.PickupSpawned, new Recipe(Wave.Sine, 700f, 1000f, .14f, 0f, .008f, .32f) },
                { GameSfx.PickupCollected, new Recipe(Wave.Sine, 820f, 1240f, .16f, 0f, .006f, .42f) },
                { GameSfx.ShieldActivated, new Recipe(Wave.Sine, 300f, 720f, .32f, 0f, .015f, .45f) },
                { GameSfx.FreezeActivated, new Recipe(Wave.Sine, 920f, 380f, .36f, 0f, .012f, .45f) },
                { GameSfx.FreezeEnded, new Recipe(Wave.Sine, 380f, 900f, .26f, 0f, .01f, .38f) },
                { GameSfx.ArenaTilt, new Recipe(Wave.Saw, 180f, 300f, .42f, .04f, .02f, .35f) },
                { GameSfx.EnemyDestroyed, new Recipe(Wave.Square, 620f, 140f, .2f, .45f, .004f, .4f) },
                { GameSfx.VolatileExplosion, new Recipe(Wave.Noise, 260f, 60f, .55f, .9f, .005f, .6f) },
                { GameSfx.PlayerHit, new Recipe(Wave.Square, 420f, 70f, .38f, .25f, .004f, .55f) },
                { GameSfx.LaserWarning, new Recipe(Wave.Square, 880f, 880f, .22f, 0f, .01f, .3f) },
                { GameSfx.LaserFiring, new Recipe(Wave.Saw, 1200f, 320f, .3f, .3f, .004f, .45f) },
                { GameSfx.BossProjectile, new Recipe(Wave.Square, 520f, 300f, .16f, .15f, .005f, .35f) },
                { GameSfx.BossDestroyed, new Recipe(Wave.Noise, 320f, 40f, 1.3f, .85f, .02f, .7f) },
                { GameSfx.UiInteraction, new Recipe(Wave.Square, 620f, 620f, .05f, 0f, .003f, .25f) },
            };
        }

        /// <summary>Three short looping arpeggios, so each context is recognisable without being music.</summary>
        private static Dictionary<MusicTrack, float[]> BuildMusic(System.Random random)
        {
            return new Dictionary<MusicTrack, float[]>
            {
                { MusicTrack.Menu, Arpeggio(new[] { 261.6f, 329.6f, 392f, 329.6f }, .55f, 8f, .18f, random) },
                { MusicTrack.Gameplay, Arpeggio(new[] { 220f, 277.2f, 329.6f, 277.2f }, .4f, 8f, .16f, random) },
                { MusicTrack.Boss, Arpeggio(new[] { 146.8f, 174.6f, 138.6f, 174.6f }, .35f, 8f, .2f, random) },
            };
        }

        private static float[] Arpeggio(float[] notes, float noteSeconds, float totalSeconds, float gain, System.Random random)
        {
            var samples = new float[Mathf.RoundToInt(totalSeconds * SampleRate)];
            var noteSamples = Mathf.RoundToInt(noteSeconds * SampleRate);
            for (var i = 0; i < samples.Length; i++)
            {
                var noteIndex = (i / noteSamples) % notes.Length;
                var withinNote = i % noteSamples;
                var t = (float)i / SampleRate;
                // A short pluck envelope per note keeps the loop from droning.
                var envelope = Mathf.Exp(-4f * withinNote / noteSamples);
                var value = Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * t);
                value += .3f * Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * 2f * t);
                samples[i] = value * envelope * gain;
            }
            FadeEdges(samples, Mathf.RoundToInt(.02f * SampleRate));
            return samples;
        }

        private static float[] Synthesise(Recipe recipe, System.Random random)
        {
            var count = Mathf.Max(1, Mathf.RoundToInt(recipe.seconds * SampleRate));
            var samples = new float[count];
            var attackSamples = Mathf.Max(1, Mathf.RoundToInt(recipe.attack * SampleRate));
            var phase = 0f;

            for (var i = 0; i < count; i++)
            {
                var progress = (float)i / count;
                var hz = Mathf.Lerp(recipe.startHz, recipe.endHz, progress);
                phase += 2f * Mathf.PI * hz / SampleRate;
                if (phase > 2f * Mathf.PI) phase -= 2f * Mathf.PI;

                var tone = Oscillate(recipe.wave, phase, random);
                if (recipe.noiseMix > 0f)
                    tone = Mathf.Lerp(tone, (float)(random.NextDouble() * 2d - 1d), recipe.noiseMix);

                // Quick attack, exponential decay: the shape of almost every game sound effect.
                var attack = i < attackSamples ? (float)i / attackSamples : 1f;
                var decay = Mathf.Exp(-3.5f * progress);
                samples[i] = tone * attack * decay * recipe.gain;
            }
            FadeEdges(samples, Mathf.Min(64, count / 4));
            return samples;
        }

        private static float Oscillate(Wave wave, float phase, System.Random random)
        {
            switch (wave)
            {
                case Wave.Square: return Mathf.Sin(phase) >= 0f ? 1f : -1f;
                case Wave.Saw: return phase / Mathf.PI - 1f;
                case Wave.Noise: return (float)(random.NextDouble() * 2d - 1d);
                default: return Mathf.Sin(phase);
            }
        }

        /// <summary>Silences the very start and end, so nothing clicks on play or on loop.</summary>
        private static void FadeEdges(float[] samples, int fadeSamples)
        {
            if (fadeSamples <= 0) return;
            for (var i = 0; i < fadeSamples && i < samples.Length; i++)
            {
                var factor = (float)i / fadeSamples;
                samples[i] *= factor;
                samples[samples.Length - 1 - i] *= factor;
            }
        }

        /// <summary>Encodes mono 16-bit PCM, which every platform imports without fuss.</summary>
        private static byte[] EncodeWav(float[] samples)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var dataBytes = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataBytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);   // PCM
                writer.Write((short)1);   // mono
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);
                foreach (var sample in samples)
                    writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>Short effects decompress into memory; the music streams instead of being held.</summary>
        private static void ApplyImportSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { OutputFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;
                var isMusic = Path.GetFileName(path).StartsWith("Music_", StringComparison.Ordinal);
                var settings = importer.defaultSampleSettings;
                settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = isMusic ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.loadInBackground = isMusic;
                importer.SaveAndReimport();
            }
        }

        private static void AssignToLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogWarning($"Could not load {LibraryPath}; the clips were generated but not assigned.");
                return;
            }
            foreach (var definition in library.definitions)
            {
                if (definition == null) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{OutputFolder}/Sfx_{definition.sfx}.wav");
                if (clip == null) continue;
                definition.clips = new[] { clip };
                EditorUtility.SetDirty(definition);
            }
            library.menuTrack = AssetDatabase.LoadAssetAtPath<AudioClip>($"{OutputFolder}/Music_{MusicTrack.Menu}.wav");
            library.gameplayTrack = AssetDatabase.LoadAssetAtPath<AudioClip>($"{OutputFolder}/Music_{MusicTrack.Gameplay}.wav");
            library.bossTrack = AssetDatabase.LoadAssetAtPath<AudioClip>($"{OutputFolder}/Music_{MusicTrack.Boss}.wav");
            library.Invalidate();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
    }
}
