using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Audio;
using SpaceXonix.Settings;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class AudioTests
    {
        [Test]
        public void Definition_WithNoClipsYetPicksNothingRatherThanThrowing()
        {
            var definition = ScriptableObject.CreateInstance<SfxDefinition>();
            try
            {
                // The audio assets arrive in a later phase; until then every sound is simply empty.
                definition.clips = new AudioClip[0];
                Assert.That(definition.HasClip, Is.False);
                Assert.That(definition.PickClip(new System.Random(1)), Is.Null);

                definition.clips = null;
                Assert.That(definition.HasClip, Is.False);
                Assert.That(definition.PickClip(new System.Random(1)), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Definition_VariesPitchWithinItsConfiguredSpread()
        {
            var definition = ScriptableObject.CreateInstance<SfxDefinition>();
            try
            {
                definition.pitchVariance = 0f;
                Assert.That(definition.PickPitch(new System.Random(1)), Is.EqualTo(1f), "no spread means an identical sound every time");

                definition.pitchVariance = .1f;
                var random = new System.Random(7);
                for (var i = 0; i < 50; i++)
                {
                    var pitch = definition.PickPitch(random);
                    Assert.That(pitch, Is.InRange(.9f, 1.1f));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Library_FindsEverySoundAndKeepsTheFirstOfAnyDuplicate()
        {
            var owned = new List<UnityEngine.Object>();
            try
            {
                var first = Own(owned, Definition(GameSfx.PowerShot));
                var duplicate = Own(owned, Definition(GameSfx.PowerShot));
                var other = Own(owned, Definition(GameSfx.PlayerHit));
                var library = Own(owned, ScriptableObject.CreateInstance<SfxLibrary>());
                library.definitions = new[] { first, duplicate, other, null };

                Assert.That(library.Find(GameSfx.PowerShot), Is.SameAs(first), "a duplicate entry is a data mistake, not a crash");
                Assert.That(library.Find(GameSfx.PlayerHit), Is.SameAs(other));
                Assert.That(library.Find(GameSfx.BossDestroyed), Is.Null, "an unconfigured sound simply has no definition");
            }
            finally
            {
                Destroy(owned);
            }
        }

        [Test]
        public void Library_MapsEachMusicTrackAndNoneToSilence()
        {
            var owned = new List<UnityEngine.Object>();
            try
            {
                var library = Own(owned, ScriptableObject.CreateInstance<SfxLibrary>());
                library.menuTrack = Own(owned, Clip("menu"));
                library.gameplayTrack = Own(owned, Clip("gameplay"));
                library.bossTrack = Own(owned, Clip("boss"));

                Assert.That(library.TrackFor(MusicTrack.Menu), Is.SameAs(library.menuTrack));
                Assert.That(library.TrackFor(MusicTrack.Gameplay), Is.SameAs(library.gameplayTrack));
                Assert.That(library.TrackFor(MusicTrack.Boss), Is.SameAs(library.bossTrack));
                Assert.That(library.TrackFor(MusicTrack.None), Is.Null);
            }
            finally
            {
                Destroy(owned);
            }
        }

        [Test]
        public void Manager_PlaysAConfiguredSoundAndSkipsAnEmptyOne()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Manager.Play(GameSfx.BossDestroyed), Is.False, "a sound with no definition is silent");
                Assert.That(fixture.Manager.Play(GameSfx.PlayerHit), Is.False, "a definition with no clip yet is silent");

                Assert.That(fixture.Manager.Play(GameSfx.PowerShot), Is.True);
                Assert.That(fixture.Manager.PlayedCount, Is.EqualTo(1));
                Assert.That(fixture.Manager.LastPlayed, Is.EqualTo(GameSfx.PowerShot));
            }
        }

        [Test]
        public void Manager_RefusesToMachineGunASoundInsideItsMinimumInterval()
        {
            using (var fixture = new Fixture())
            {
                fixture.WithClip.minimumInterval = 5f;
                Assert.That(fixture.Manager.Play(GameSfx.PowerShot), Is.True);
                Assert.That(fixture.Manager.Play(GameSfx.PowerShot), Is.False, "the repeat lands inside the interval");
                Assert.That(fixture.Manager.PlayedCount, Is.EqualTo(1));

                // A different sound is unaffected by another's interval.
                fixture.Spare.clips = fixture.WithClip.clips;
                Assert.That(fixture.Manager.Play(GameSfx.CaptureCompleted), Is.True);
            }
        }

        [Test]
        public void Manager_StaysSilentWhenThePlayerHasMutedEffects()
        {
            using (var fixture = new Fixture())
            {
                fixture.Settings.SfxVolume = 0f;
                Assert.That(fixture.Manager.Play(GameSfx.PowerShot), Is.False);

                fixture.Settings.SfxVolume = 1f;
                fixture.Settings.MasterVolume = 0f;
                Assert.That(fixture.Manager.Play(GameSfx.PowerShot), Is.False, "muting the master mutes effects too");

                fixture.Settings.MasterVolume = 1f;
                Assert.That(fixture.Manager.Play(GameSfx.PowerShot), Is.True);
            }
        }

        [Test]
        public void Manager_SwitchesTracksButLeavesTheSameOnePlaying()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Manager.CurrentTrack, Is.EqualTo(MusicTrack.None));

                fixture.Manager.PlayMusic(MusicTrack.Gameplay);
                Assert.That(fixture.Manager.CurrentTrack, Is.EqualTo(MusicTrack.Gameplay));
                var source = fixture.MusicSource;
                var clip = source.clip;

                // Loading another normal stage must not restart the music from the top.
                fixture.Manager.PlayMusic(MusicTrack.Gameplay);
                Assert.That(source.clip, Is.SameAs(clip));

                fixture.Manager.PlayMusic(MusicTrack.Boss);
                Assert.That(fixture.Manager.CurrentTrack, Is.EqualTo(MusicTrack.Boss));
                Assert.That(source.clip, Is.SameAs(fixture.Library.bossTrack));

                fixture.Manager.StopMusic();
                Assert.That(fixture.Manager.CurrentTrack, Is.EqualTo(MusicTrack.None));
                Assert.That(source.clip, Is.Null);
            }
        }

        [Test]
        public void MusicCue_AsksForItsTrackAndIsSilentWithoutAService()
        {
            var host = new GameObject("MusicCue");
            try
            {
                var cue = host.AddComponent<MusicCue>();
                typeof(MusicCue).GetField("track", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(cue, MusicTrack.Menu);

                // No AudioManager exists here, which is exactly the case that must not throw.
                Assert.That(cue.Play(), Is.False);

                using (var fixture = new Fixture())
                {
                    fixture.Library.menuTrack = AudioClip.Create("menu", 64, 1, 8000, false);
                    Assert.That(cue.Play(), Is.True);
                    Assert.That(fixture.Manager.CurrentTrack, Is.EqualTo(MusicTrack.Menu));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static SfxDefinition Definition(GameSfx sfx)
        {
            var definition = ScriptableObject.CreateInstance<SfxDefinition>();
            definition.sfx = sfx;
            return definition;
        }

        private static AudioClip Clip(string name) => AudioClip.Create(name, 64, 1, 8000, false);

        private static T Own<T>(List<UnityEngine.Object> owned, T item) where T : UnityEngine.Object
        {
            owned.Add(item);
            return item;
        }

        private static void Destroy(List<UnityEngine.Object> owned)
        {
            foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("AudioFixture");
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            public readonly AudioManager Manager;
            public readonly SfxLibrary Library;
            public readonly GameSettingsModel Settings = new GameSettingsModel();
            public readonly SfxDefinition WithClip;
            public readonly SfxDefinition Empty;
            public readonly SfxDefinition Spare;

            public Fixture()
            {
                root.SetActive(false);
                Library = Own(ScriptableObject.CreateInstance<SfxLibrary>());
                WithClip = Own(Definition(GameSfx.PowerShot));
                WithClip.clips = new[] { Own(Clip("shot")) };
                Empty = Own(Definition(GameSfx.PlayerHit));
                Empty.clips = new AudioClip[0];
                Spare = Own(Definition(GameSfx.CaptureCompleted));
                Spare.clips = new AudioClip[0];
                Library.definitions = new[] { WithClip, Empty, Spare };
                Library.gameplayTrack = Own(Clip("gameplay"));
                Library.bossTrack = Own(Clip("boss"));

                Manager = root.AddComponent<AudioManager>();
                typeof(AudioManager).GetField("library", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(Manager, Library);
                root.SetActive(true);
                // EditMode does not run Awake, which builds the voices and binds the settings.
                typeof(AudioManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Manager, null);
                Manager.BindSettings(Settings);
            }

            public AudioSource MusicSource =>
                (AudioSource)typeof(AudioManager).GetField("musicSource", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(Manager);

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }

            private T Own<T>(T item) where T : UnityEngine.Object
            {
                owned.Add(item);
                return item;
            }
        }
    }
}
