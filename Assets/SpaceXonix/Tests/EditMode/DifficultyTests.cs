using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Settings;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class DifficultyTests
    {
        [Test]
        public void BossStage_FiresLasersOnlyOnHard()
        {
            var stage = UnityEditor.AssetDatabase.LoadAssetAtPath<SpaceXonix.Campaign.StageDefinition>(
                "Assets/SpaceXonix/ScriptableObjects/Stages/Stage5_AlienCore.asset");
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.IsBossStage, Is.True);
            Assert.That(stage.LasersFor(DifficultyMode.Easy), Is.Empty, "Easy keeps the boss fight as it was");
            var hard = stage.LasersFor(DifficultyMode.Hard);
            Assert.That(hard, Has.Count.EqualTo(2));
            foreach (var laser in hard) Assert.That(laser.definition, Is.Not.Null, "every Hard laser resolves to a real laser");
            Assert.That(hard[0].definition.axis, Is.Not.EqualTo(hard[1].definition.axis), "one each way");
        }

        [Test]
        public void HardLasers_AddToAStagesOwn()
        {
            var stage = UnityEngine.ScriptableObject.CreateInstance<SpaceXonix.Campaign.StageDefinition>();
            try
            {
                stage.lasers = new[] { new SpaceXonix.Hazards.LaserPlacement() };
                stage.hardModeLasers = new[] { new SpaceXonix.Hazards.LaserPlacement(), new SpaceXonix.Hazards.LaserPlacement() };
                Assert.That(stage.LasersFor(DifficultyMode.Easy), Has.Count.EqualTo(1));
                Assert.That(stage.LasersFor(DifficultyMode.Hard), Has.Count.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void Mode_EasyDropsModifiersAndAddsALifeWhileHardIsTheFullGame()
        {
            Assert.That(DifficultyMode.Easy.UsesStageModifiers(), Is.False);
            Assert.That(DifficultyMode.Hard.UsesStageModifiers(), Is.True);
            Assert.That(DifficultyMode.Easy.BonusStartingLives(), Is.EqualTo(1));
            Assert.That(DifficultyMode.Hard.BonusStartingLives(), Is.Zero);
            Assert.That(DifficultyMode.Easy.DisplayName(), Is.EqualTo("Easy"));
            Assert.That(DifficultyMode.Hard.DisplayName(), Is.EqualTo("Hard"));
        }

        [Test]
        public void Model_KeepsASeparateRecordPerDifficulty()
        {
            var model = new GameSettingsModel();
            Assert.That(model.Difficulty, Is.EqualTo(DifficultyMode.Hard), "the full game is the default");

            Assert.That(model.TrySetHighScore(DifficultyMode.Hard, 9000), Is.True);
            Assert.That(model.GetHighScore(DifficultyMode.Easy), Is.Zero,
                "a Hard record must not fill in the Easy one, whose runs score lower");

            // An Easy run far below the Hard record is still that mode's first record.
            Assert.That(model.TrySetHighScore(DifficultyMode.Easy, 100), Is.True);
            Assert.That(model.GetHighScore(DifficultyMode.Easy), Is.EqualTo(100));
            Assert.That(model.GetHighScore(DifficultyMode.Hard), Is.EqualTo(9000));

            // The parameterless overload follows the selected mode.
            model.Difficulty = DifficultyMode.Easy;
            Assert.That(model.CampaignHighScore, Is.EqualTo(100));
            Assert.That(model.TrySetHighScore(50), Is.False);
            Assert.That(model.TrySetHighScore(150), Is.True);
            Assert.That(model.GetHighScore(DifficultyMode.Easy), Is.EqualTo(150));
            Assert.That(model.GetHighScore(DifficultyMode.Hard), Is.EqualTo(9000), "the other record is untouched");
        }

        [Test]
        public void Model_DifficultyIsAChoiceNotAPreferenceSoAResetLeavesIt()
        {
            var model = new GameSettingsModel();
            var changes = 0;
            model.Changed += () => changes++;

            model.Difficulty = DifficultyMode.Easy;
            Assert.That(changes, Is.EqualTo(1));
            model.Difficulty = DifficultyMode.Easy;
            Assert.That(changes, Is.EqualTo(1), "choosing the same mode again changes nothing");

            model.TrySetHighScore(DifficultyMode.Easy, 400);
            model.ResetToDefaults();
            Assert.That(model.Difficulty, Is.EqualTo(DifficultyMode.Easy), "a settings reset does not reselect the mode");
            Assert.That(model.GetHighScore(DifficultyMode.Easy), Is.EqualTo(400));
        }

        [Test]
        public void Service_PersistsBothRecordsAndTheSelectedMode()
        {
            var store = new MemoryStore();
            using (var fixture = new Fixture(store))
            {
                fixture.Settings.Model.Difficulty = DifficultyMode.Easy;
                fixture.Settings.Model.TrySetHighScore(DifficultyMode.Easy, 1234);
                fixture.Settings.Model.TrySetHighScore(DifficultyMode.Hard, 8765);
            }
            using (var reloaded = new Fixture(store))
            {
                Assert.That(reloaded.Settings.Model.Difficulty, Is.EqualTo(DifficultyMode.Easy), "the mode is remembered");
                Assert.That(reloaded.Settings.Model.GetHighScore(DifficultyMode.Easy), Is.EqualTo(1234));
                Assert.That(reloaded.Settings.Model.GetHighScore(DifficultyMode.Hard), Is.EqualTo(8765));
                Assert.That(reloaded.Settings.Model.CampaignHighScore, Is.EqualTo(1234), "and selects that mode's record");
            }
        }

        [Test]
        public void Service_ReadsAPreDifficultyRecordAsTheHardRecord()
        {
            // Saves written before difficulty existed always had modifiers on, so that score is Hard's.
            var store = new MemoryStore();
            store.SetInt("spacexonix.campaign.highscore", 13960);
            using (var fixture = new Fixture(store))
            {
                Assert.That(fixture.Settings.Model.GetHighScore(DifficultyMode.Hard), Is.EqualTo(13960));
                Assert.That(fixture.Settings.Model.GetHighScore(DifficultyMode.Easy), Is.Zero);
                Assert.That(fixture.Settings.Model.Difficulty, Is.EqualTo(DifficultyMode.Hard));
            }
        }

        private sealed class MemoryStore : ISettingsStore
        {
            private readonly Dictionary<string, float> floats = new Dictionary<string, float>();
            private readonly Dictionary<string, int> ints = new Dictionary<string, int>();

            public float GetFloat(string key, float defaultValue) => floats.TryGetValue(key, out var value) ? value : defaultValue;
            public void SetFloat(string key, float value) => floats[key] = value;
            public int GetInt(string key, int defaultValue) => ints.TryGetValue(key, out var value) ? value : defaultValue;
            public void SetInt(string key, int value) => ints[key] = value;
            public void Save() { }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            public readonly GameSettings Settings;

            public Fixture(ISettingsStore store)
            {
                root = new GameObject("DifficultyFixture");
                root.SetActive(false);
                Settings = root.AddComponent<GameSettings>();
                typeof(GameSettings).GetField("persistAcrossScenes", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(Settings, false);
                root.SetActive(true);
                typeof(GameSettings).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Settings, null);
                Settings.Initialize(store);
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
