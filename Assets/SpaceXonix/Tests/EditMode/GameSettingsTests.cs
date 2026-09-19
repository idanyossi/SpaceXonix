using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpaceXonix.Settings;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class GameSettingsTests
    {
        [Test]
        public void Model_ClampsVolumesAndRaisesChangeOnlyOnRealChanges()
        {
            var model = new GameSettingsModel();
            var changes = 0;
            model.Changed += () => changes++;

            Assert.That(model.MasterVolume, Is.EqualTo(GameSettingsModel.DefaultMasterVolume));
            Assert.That(model.MusicVolume, Is.EqualTo(GameSettingsModel.DefaultMusicVolume));
            Assert.That(model.VibrationEnabled, Is.True);
            Assert.That(model.CameraShakeEnabled, Is.True);

            model.MasterVolume = 2.5f;
            Assert.That(model.MasterVolume, Is.EqualTo(1f), "volumes clamp to 0-1");
            Assert.That(changes, Is.Zero, "clamping to the value it already had is not a change");

            model.MusicVolume = -3f;
            Assert.That(model.MusicVolume, Is.Zero);
            Assert.That(changes, Is.EqualTo(1));

            model.MusicVolume = 0f;
            Assert.That(changes, Is.EqualTo(1), "setting the same value again raises nothing");

            model.CameraShakeEnabled = false;
            model.VibrationEnabled = false;
            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void Model_CombinesMasterWithEachChannel()
        {
            var model = new GameSettingsModel { MasterVolume = .5f, MusicVolume = .4f, SfxVolume = .8f };
            Assert.That(model.EffectiveMusicVolume, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(model.EffectiveSfxVolume, Is.EqualTo(.4f).Within(.0001f));

            model.MasterVolume = 0f;
            Assert.That(model.EffectiveMusicVolume, Is.Zero, "muting the master mutes everything");
            Assert.That(model.EffectiveSfxVolume, Is.Zero);
        }

        [Test]
        public void Model_KeepsOnlyTheBestScoreAndSurvivesASettingsReset()
        {
            var model = new GameSettingsModel();
            var changes = 0;
            model.Changed += () => changes++;

            Assert.That(model.CampaignHighScore, Is.Zero);
            Assert.That(model.TrySetHighScore(1200), Is.True);
            Assert.That(model.CampaignHighScore, Is.EqualTo(1200));
            Assert.That(changes, Is.EqualTo(1));

            Assert.That(model.TrySetHighScore(900), Is.False, "a worse run is not a record");
            Assert.That(model.TrySetHighScore(1200), Is.False, "matching the record is not beating it");
            Assert.That(model.CampaignHighScore, Is.EqualTo(1200));
            Assert.That(changes, Is.EqualTo(1));

            model.MasterVolume = .25f;
            model.ResetToDefaults();
            Assert.That(model.MasterVolume, Is.EqualTo(GameSettingsModel.DefaultMasterVolume));
            Assert.That(model.CampaignHighScore, Is.EqualTo(1200), "a record is not a preference and survives a reset");
        }

        [Test]
        public void Service_LoadsDefaultsThenPersistsEveryChange()
        {
            var store = new MemoryStore();
            using (var fixture = new Fixture(store))
            {
                var model = fixture.Settings.Model;
                Assert.That(model.MasterVolume, Is.EqualTo(GameSettingsModel.DefaultMasterVolume));
                Assert.That(store.Writes, Is.Zero, "an empty store is read, never written, on first load");

                model.SfxVolume = .3f;
                model.CameraShakeEnabled = false;
                Assert.That(store.Saves, Is.EqualTo(2), "each change is written through");

                // A fresh service over the same store must see what the first one wrote.
                using (var reloaded = new Fixture(store))
                {
                    Assert.That(reloaded.Settings.Model.SfxVolume, Is.EqualTo(.3f).Within(.0001f));
                    Assert.That(reloaded.Settings.Model.CameraShakeEnabled, Is.False);
                    Assert.That(reloaded.Settings.Model.MusicVolume, Is.EqualTo(GameSettingsModel.DefaultMusicVolume),
                        "untouched settings keep their defaults");
                }
            }
        }

        [Test]
        public void Service_PersistsTheHighScoreAndReportsOnlyRealRecords()
        {
            var store = new MemoryStore();
            using (var fixture = new Fixture(store))
            {
                Assert.That(fixture.Settings.ReportCampaignScore(5000), Is.True);
                Assert.That(fixture.Settings.ReportCampaignScore(4000), Is.False);
                Assert.That(fixture.Settings.Model.CampaignHighScore, Is.EqualTo(5000));
            }
            using (var reloaded = new Fixture(store))
            {
                Assert.That(reloaded.Settings.Model.CampaignHighScore, Is.EqualTo(5000));
                Assert.That(reloaded.Settings.ReportCampaignScore(5000), Is.False,
                    "a reloaded record still has to be beaten, not matched");
                Assert.That(reloaded.Settings.ReportCampaignScore(5001), Is.True);
            }
        }

        [Test]
        public void Shaker_FollowsTheCameraShakeSettingIncludingChangesMidRun()
        {
            var store = new MemoryStore();
            using (var fixture = new Fixture(store))
            {
                fixture.Settings.Model.CameraShakeEnabled = false;

                var shakerObject = new GameObject("Shaker");
                shakerObject.SetActive(false);
                var shaker = shakerObject.AddComponent<SpaceXonix.Presentation.ArenaShaker>();
                try
                {
                    shakerObject.SetActive(true);
                    // EditMode does not run OnEnable for us, so the subscription is made by hand.
                    typeof(SpaceXonix.Presentation.ArenaShaker).GetMethod("OnEnable",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(shaker, null);
                    Assert.That(shaker.ShakeEnabled, Is.False, "the shaker adopts the stored preference when it wakes");

                    fixture.Settings.Model.CameraShakeEnabled = true;
                    Assert.That(shaker.ShakeEnabled, Is.True, "and follows it when changed from the pause menu");

                    fixture.Settings.Model.CameraShakeEnabled = false;
                    Assert.That(shaker.ShakeEnabled, Is.False);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(shakerObject);
                }
            }
        }

        /// <summary>An in-memory store, so tests never touch the editor's shared PlayerPrefs.</summary>
        private sealed class MemoryStore : ISettingsStore
        {
            private readonly Dictionary<string, float> floats = new Dictionary<string, float>();
            private readonly Dictionary<string, int> ints = new Dictionary<string, int>();

            public int Writes { get; private set; }
            public int Saves { get; private set; }

            public float GetFloat(string key, float defaultValue) => floats.TryGetValue(key, out var value) ? value : defaultValue;
            public void SetFloat(string key, float value) { floats[key] = value; Writes++; }
            public int GetInt(string key, int defaultValue) => ints.TryGetValue(key, out var value) ? value : defaultValue;
            public void SetInt(string key, int value) { ints[key] = value; Writes++; }
            public void Save() => Saves++;
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            public readonly GameSettings Settings;

            public Fixture(ISettingsStore store)
            {
                root = new GameObject("SettingsFixture");
                root.SetActive(false);
                Settings = root.AddComponent<GameSettings>();
                // Scene-local, so each fixture owns its own service rather than a persistent singleton.
                typeof(GameSettings).GetField("persistAcrossScenes",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(Settings, false);
                root.SetActive(true);
                typeof(GameSettings).GetMethod("Awake",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(Settings, null);
                Settings.Initialize(store);
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
