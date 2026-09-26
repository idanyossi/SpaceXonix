using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SpaceXonix.Audio;
using UnityEditor;
using UnityEngine;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Pick every sound by ear. The previous pass chose clips from their file names, and they were
    /// wrong; this window lists a shortlist per game sound from Juhani Junkala's 512 retro effects
    /// (CC0) so each one can be heard and chosen in place.
    ///
    /// SpaceXonix > Import Junkala Audio copies the shortlist and the music in from AssetSources and
    /// assigns the first candidate of each as a default. SpaceXonix > Sound Audition opens the window.
    /// </summary>
    public sealed class SoundAudition : EditorWindow
    {
        private const string SourceRoot = "AssetSources";
        private const string SfxFolder = "Assets/SpaceXonix/Audio/ThirdParty/Junkala/Sfx";
        private const string MusicFolder = "Assets/SpaceXonix/Audio/ThirdParty/Junkala/Music";
        private const string LibraryPath = "Assets/SpaceXonix/ScriptableObjects/Audio/SfxLibrary.asset";

        /// <summary>Three candidates per sound, best guess first. The first is the default.</summary>
        private static readonly Dictionary<GameSfx, string[]> Shortlist = new Dictionary<GameSfx, string[]>
        {
            // DirectionChanged is left out on purpose: turning is silent, see GameplayAudioBinder.
            // Trail start, button clicks and the laser warning are picked for being short, soft and
            // steady in pitch: sweeps read as squirmy, and a long flat buzz grates when it repeats.
            { GameSfx.TrailStarted, new[] { "sfx_menu_move4", "sfx_damage_hit2", "sfx_sounds_Blip7" } },
            { GameSfx.CaptureCompleted, new[] { "sfx_coin_double1", "sfx_coin_double3", "sfx_sounds_powerup2" } },
            { GameSfx.LargeCapture, new[] { "sfx_coin_cluster3", "sfx_sounds_fanfare1", "sfx_coin_cluster6" } },
            { GameSfx.PowerMeterFull, new[] { "sfx_sounds_powerup4", "sfx_sounds_powerup10", "sfx_sounds_powerup16" } },
            { GameSfx.PowerShot, new[] { "sfx_wpn_laser1", "sfx_wpn_laser5", "sfx_wpn_laser9" } },
            { GameSfx.PickupSpawned, new[] { "sfx_sounds_Blip8", "sfx_sound_neutral1", "sfx_sounds_high3" } },
            { GameSfx.PickupCollected, new[] { "sfx_coin_single1", "sfx_coin_single4", "sfx_sounds_powerup6" } },
            { GameSfx.ShieldActivated, new[] { "sfx_sounds_powerup1", "sfx_movement_portal3", "sfx_sounds_powerup12" } },
            { GameSfx.FreezeActivated, new[] { "sfx_sound_neutral5", "sfx_sounds_falling2", "sfx_movement_portal5" } },
            { GameSfx.FreezeEnded, new[] { "sfx_sounds_high5", "sfx_sound_neutral8", "sfx_sounds_Blip10" } },
            { GameSfx.ArenaTilt, new[] { "sfx_sound_mechanicalnoise1", "sfx_sound_mechanicalnoise3", "sfx_sounds_falling6" } },
            { GameSfx.EnemyDestroyed, new[] { "sfx_exp_short_hard1", "sfx_exp_shortest_hard3", "sfx_exp_short_hard8" } },
            { GameSfx.VolatileExplosion, new[] { "sfx_exp_medium1", "sfx_exp_cluster2", "sfx_exp_medium7" } },
            { GameSfx.PlayerHit, new[] { "sfx_sounds_damage1", "sfx_damage_hit3", "sfx_deathscream_android1" } },
            { GameSfx.LaserWarning, new[] { "sfx_alarm_loop6", "sfx_sounds_powerup13", "sfx_sounds_error3" } },
            { GameSfx.LaserFiring, new[] { "sfx_wpn_laser3", "sfx_wpn_laser7", "sfx_wpn_laser11" } },
            { GameSfx.BossProjectile, new[] { "sfx_weapon_singleshot1", "sfx_weapon_singleshot7", "sfx_weapon_singleshot14" } },
            { GameSfx.BossDestroyed, new[] { "sfx_exp_long1", "sfx_exp_long4", "sfx_exp_cluster9" } },
            { GameSfx.UiInteraction, new[] { "sfx_menu_move1", "sfx_sounds_Blip10", "sfx_sounds_Blip4" } },
        };

        /// <summary>
        /// Four of the five tracks. Level 2 is left out to keep 13 MB of WAV out of a public repository;
        /// the gameplay track is shared by every normal stage.
        /// </summary>
        private static readonly (MusicTrack Track, string Source, string Name)[] Music =
        {
            (MusicTrack.Menu, "Title Screen", "Music_TitleScreen"),
            (MusicTrack.Gameplay, "Level 1", "Music_Level1"),
            (MusicTrack.Boss, "Level 3", "Music_Level3"),
            (MusicTrack.Victory, "Ending", "Music_Ending"),
        };

        private Vector2 scroll;

        [MenuItem("SpaceXonix/Sound Audition")]
        public static void Open() => GetWindow<SoundAudition>("Sound Audition");

        [MenuItem("SpaceXonix/Import Junkala Audio")]
        public static void Import()
        {
            var sfxSource = Path.Combine(SourceRoot, "junkala_512_sfx");
            var musicSource = Path.Combine(SourceRoot, "junkala_5_chiptunes");
            if (!Directory.Exists(sfxSource) || !Directory.Exists(musicSource))
            {
                Debug.LogWarning("Download the Junkala packs into AssetSources first; see Assets/ThirdParty/ATTRIBUTION.md.");
                return;
            }
            Directory.CreateDirectory(SfxFolder);
            Directory.CreateDirectory(MusicFolder);

            var byName = Directory.GetFiles(sfxSource, "*.wav", SearchOption.AllDirectories)
                .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var copied = 0;
            foreach (var name in Shortlist.Values.SelectMany(names => names).Distinct())
            {
                if (!byName.TryGetValue(name, out var path)) { Debug.LogWarning($"Missing Junkala sound '{name}'."); continue; }
                File.Copy(path, Path.Combine(SfxFolder, name + ".wav"), true);
                copied++;
            }
            foreach (var (_, source, name) in Music)
            {
                var path = Directory.GetFiles(musicSource, $"*{source}.wav").FirstOrDefault();
                if (path == null) { Debug.LogWarning($"Missing Junkala track '{source}'."); continue; }
                File.Copy(path, Path.Combine(MusicFolder, name + ".wav"), true);
            }
            File.WriteAllText(Path.Combine(SfxFolder, "..", "LICENSE.txt"),
                "Juhani Junkala - The Essential Retro Video Game Sound Effects Collection [512 sounds]\n" +
                "Juhani Junkala - 5 Chiptunes (Action)\n\n" +
                "Both released under CC0 1.0 Universal: http://creativecommons.org/publicdomain/zero/1.0/\n" +
                "Sources: https://opengameart.org/content/512-sound-effects-8-bit-style\n" +
                "         https://opengameart.org/content/5-chiptunes-action\n");

            AssetDatabase.Refresh();
            ApplyImportSettings();
            AssignDefaults();
            Debug.Log($"Imported {copied} candidate sounds and {Music.Length} music tracks. Open SpaceXonix > Sound Audition to choose by ear.");
        }

        private static void ApplyImportSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SfxFolder, MusicFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;
                var isMusic = path.StartsWith(MusicFolder);
                var settings = importer.defaultSampleSettings;
                // Effects are decompressed up front so playing one never stalls; music streams.
                settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = isMusic ? .6f : .8f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = !isMusic;
                importer.loadInBackground = isMusic;
                importer.SaveAndReimport();
            }
        }

        private static void AssignDefaults()
        {
            var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
            if (library == null) return;
            foreach (var definition in library.definitions)
            {
                if (definition == null || !Shortlist.TryGetValue(definition.sfx, out var names)) continue;
                var clip = LoadCandidate(names[0]);
                if (clip == null) continue;
                definition.clips = new[] { clip };
                EditorUtility.SetDirty(definition);
            }
            foreach (var (track, _, name) in Music)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicFolder}/{name}.wav");
                switch (track)
                {
                    case MusicTrack.Menu: library.menuTrack = clip; break;
                    case MusicTrack.Gameplay: library.gameplayTrack = clip; break;
                    case MusicTrack.Boss: library.bossTrack = clip; break;
                    case MusicTrack.Victory: library.victoryTrack = clip; break;
                }
            }
            library.Invalidate();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        private static AudioClip LoadCandidate(string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/{name}.wav");

        private void OnGUI()
        {
            var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
            if (library == null)
            {
                EditorGUILayout.HelpBox($"Could not load {LibraryPath}.", MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox(
                "▶ plays a candidate. Use makes it the sound; + adds it as a random variant alongside the current one. " +
                "Changes apply to the next Play Mode session.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("■ Stop")) StopAll();
                if (GUILayout.Button("Remove unused candidates")) RemoveUnused(library);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var pair in Shortlist)
            {
                var definition = library.Find(pair.Key);
                if (definition == null) continue;
                EditorGUILayout.Space(6f);
                var current = definition.HasClip ? string.Join(", ", definition.clips.Where(c => c != null).Select(c => c.name)) : "(none)";
                EditorGUILayout.LabelField($"{pair.Key}", current, EditorStyles.boldLabel);

                foreach (var name in pair.Value)
                {
                    var clip = LoadCandidate(name);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(clip == null))
                        {
                            if (GUILayout.Button("▶", GUILayout.Width(28f))) Play(clip);
                        }
                        var inUse = clip != null && definition.clips != null && definition.clips.Contains(clip);
                        EditorGUILayout.LabelField((inUse ? "● " : "   ") + name + (clip == null ? "  (removed)" : ""));
                        using (new EditorGUI.DisabledScope(clip == null))
                        {
                            if (GUILayout.Button("Use", GUILayout.Width(44f))) SetClips(definition, new[] { clip });
                            using (new EditorGUI.DisabledScope(inUse))
                            {
                                if (GUILayout.Button("+", GUILayout.Width(24f)))
                                    SetClips(definition, (definition.clips ?? new AudioClip[0]).Where(c => c != null).Append(clip).ToArray());
                            }
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void SetClips(SfxDefinition definition, AudioClip[] clips)
        {
            definition.clips = clips;
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Deletes candidates nobody chose, once the choices are final, to keep the repository lean.</summary>
        private static void RemoveUnused(SfxLibrary library)
        {
            var used = new HashSet<AudioClip>(library.definitions.Where(d => d != null && d.clips != null).SelectMany(d => d.clips));
            var removed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SfxFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (used.Contains(AssetDatabase.LoadAssetAtPath<AudioClip>(path))) continue;
                AssetDatabase.DeleteAsset(path);
                removed++;
            }
            Debug.Log($"Removed {removed} unused candidate sounds.");
        }

        // Unity has no public editor preview API; AudioUtil is internal, so failures are reported, not thrown.
        private static void Play(AudioClip clip)
        {
            StopAll();
            InvokeAudioUtil("PlayPreviewClip", clip, 0, false);
        }

        private static void StopAll() => InvokeAudioUtil("StopAllPreviewClips");

        private static void InvokeAudioUtil(string method, params object[] arguments)
        {
            var type = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
            var info = type?.GetMethod(method, BindingFlags.Static | BindingFlags.Public);
            if (info == null)
            {
                Debug.LogWarning($"AudioUtil.{method} is unavailable in this Unity version; select the clip in the Project window to preview it instead.");
                return;
            }
            info.Invoke(null, arguments);
        }
    }
}
