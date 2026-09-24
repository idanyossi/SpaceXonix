using System.Collections.Generic;
using System.IO;
using SpaceXonix.Audio;
using UnityEditor;
using UnityEngine;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Assigns the Kenney CC0 sound effects to the game's sounds. The choices were made from the
    /// clip names rather than by ear, so this is the one place to change when a sound is wrong.
    ///
    /// Two things stay on the generated placeholders: the direction-change blip, which has to be
    /// quieter and shorter than anything in the pack, and all three music loops, because the pack
    /// has no music at all.
    /// </summary>
    public static class ThirdPartyAudioApplier
    {
        private const string KenneyFolder = "Assets/SpaceXonix/Audio/ThirdParty/Kenney";
        private const string PlaceholderFolder = "Assets/SpaceXonix/Audio/Placeholder";
        private const string LibraryPath = "Assets/SpaceXonix/ScriptableObjects/Audio/SfxLibrary.asset";

        /// <summary>Several clips for a sound are picked at random, so repeats do not sound identical.</summary>
        private static readonly Dictionary<GameSfx, string[]> Mapping = new Dictionary<GameSfx, string[]>
        {
            { GameSfx.TrailStarted, new[] { "thrusterFire_000" } },
            { GameSfx.CaptureCompleted, new[] { "sfx_twoTone" } },
            { GameSfx.LargeCapture, new[] { "forceField_002" } },
            { GameSfx.PowerMeterFull, new[] { "sfx_zap" } },
            { GameSfx.PowerShot, new[] { "laserLarge_000" } },
            { GameSfx.PickupSpawned, new[] { "computerNoise_000" } },
            { GameSfx.PickupCollected, new[] { "computerNoise_001" } },
            { GameSfx.ShieldActivated, new[] { "sfx_shieldUp" } },
            { GameSfx.FreezeActivated, new[] { "forceField_003" } },
            { GameSfx.FreezeEnded, new[] { "sfx_shieldDown" } },
            { GameSfx.ArenaTilt, new[] { "spaceEngineLow_000" } },
            { GameSfx.EnemyDestroyed, new[] { "explosionCrunch_000", "explosionCrunch_001", "explosionCrunch_002" } },
            { GameSfx.VolatileExplosion, new[] { "lowFrequency_explosion_000" } },
            { GameSfx.PlayerHit, new[] { "sfx_lose" } },
            { GameSfx.LaserWarning, new[] { "forceField_000" } },
            { GameSfx.LaserFiring, new[] { "laserLarge_001" } },
            { GameSfx.BossProjectile, new[] { "laserSmall_000", "laserSmall_001", "laserSmall_002" } },
            { GameSfx.BossDestroyed, new[] { "lowFrequency_explosion_001" } },
            { GameSfx.UiInteraction, new[] { "computerNoise_002" } },
        };

        [MenuItem("SpaceXonix/Apply Kenney Audio")]
        public static void Apply()
        {
            ApplyImportSettings();
            var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogWarning($"Could not load {LibraryPath}.");
                return;
            }
            var assigned = 0;
            foreach (var definition in library.definitions)
            {
                if (definition == null || !Mapping.TryGetValue(definition.sfx, out var names)) continue;
                var clips = new List<AudioClip>();
                foreach (var name in names)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{KenneyFolder}/{name}.ogg");
                    if (clip != null) clips.Add(clip);
                    else Debug.LogWarning($"Missing Kenney clip '{name}' for {definition.sfx}.");
                }
                if (clips.Count == 0) continue;
                definition.clips = clips.ToArray();
                EditorUtility.SetDirty(definition);
                assigned++;
                DeletePlaceholder(definition.sfx);
            }
            library.Invalidate();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"Assigned Kenney audio to {assigned} sounds. The direction blip and the music remain placeholders.");
        }

        /// <summary>A replaced placeholder is removed so no one wonders which clip is live.</summary>
        private static void DeletePlaceholder(GameSfx sfx)
        {
            var path = $"{PlaceholderFolder}/Sfx_{sfx}.wav";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        }

        private static void ApplyImportSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { KenneyFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;
                var settings = importer.defaultSampleSettings;
                // Short effects are decompressed up front so playing one never stalls a frame.
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
        }
    }
}
