using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Presentation;
using SpaceXonix.Settings;
using SpaceXonix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class ShipSkinTests
    {
        private const string LibraryPath = "Assets/SpaceXonix/ScriptableObjects/Skins/ShipSkinLibrary.asset";

        [Test]
        public void Library_HasAtLeastFiveDistinctAnimatedShipsOnTheSameGrid()
        {
            var library = AssetDatabase.LoadAssetAtPath<ShipSkinLibrary>(LibraryPath);
            Assert.That(library, Is.Not.Null);
            Assert.That(library.skins, Has.Length.GreaterThanOrEqualTo(5));
            var ids = new HashSet<string>();
            foreach (var skin in library.skins)
            {
                Assert.That(skin, Is.Not.Null);
                Assert.That(ids.Add(skin.id), Is.True, $"duplicate id {skin.id}");
                Assert.That(skin.frames, Has.Length.GreaterThanOrEqualTo(2), $"{skin.id} animates");
                foreach (var frame in skin.frames)
                {
                    Assert.That(frame, Is.Not.Null, skin.id);
                    // Same 16-pixel width and density as the default ship, so skins swap without resizing the art.
                    Assert.That(frame.rect.width, Is.EqualTo(16f), skin.id);
                    Assert.That(frame.pixelsPerUnit, Is.EqualTo(16f), skin.id);
                    Assert.That(frame.texture.filterMode, Is.EqualTo(FilterMode.Point), skin.id);
                }
            }
            Assert.That(library.Default.id, Is.EqualTo("crimson-vanguard"), "the original ship stays the default");
        }

        [Test]
        public void Library_FallsBackToTheDefaultForAnUnknownOrEmptyChoice()
        {
            var library = AssetDatabase.LoadAssetAtPath<ShipSkinLibrary>(LibraryPath);
            Assert.That(library.Find(""), Is.SameAs(library.Default));
            Assert.That(library.Find("a-skin-that-was-removed"), Is.SameAs(library.Default));
            Assert.That(library.Find(library.skins[2].id), Is.SameAs(library.skins[2]));
        }

        [Test]
        public void PlayerSkin_KeepsTheShipExactlyItsHitboxWidth()
        {
            var library = AssetDatabase.LoadAssetAtPath<ShipSkinLibrary>(LibraryPath);
            var ship = new GameObject("Ship");
            try
            {
                var visual = new GameObject("Visual").transform;
                visual.SetParent(ship.transform, false);
                var renderer = visual.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = library.Default.Preview;
                visual.localScale = Vector3.one * .75f;
                var actor = ship.AddComponent<ActorVisual>();
                Set(actor, "visual", visual);
                var skin = ship.AddComponent<PlayerShipSkin>();
                Set(skin, "actorVisual", actor);
                var width = actor.VisualWidth();

                foreach (var candidate in library.skins)
                {
                    skin.Apply(candidate);
                    Assert.That(skin.Current, Is.SameAs(candidate));
                    Assert.That(renderer.sprite, Is.SameAs(candidate.Preview));
                    Assert.That(actor.VisualWidth(), Is.EqualTo(width).Within(.0001f), $"{candidate.id} changed the ship's size");
                }
            }
            finally
            {
                Object.DestroyImmediate(ship);
            }
        }

        [Test]
        public void Hangar_BrowsesAnEndlessCarouselAndEquipsWhatIsOnShow()
        {
            var library = AssetDatabase.LoadAssetAtPath<ShipSkinLibrary>(LibraryPath);
            var root = new GameObject("Hangar");
            try
            {
                root.SetActive(false);
                var panel = root.AddComponent<SkinSelectPanel>();
                Set(panel, "root", root);
                Set(panel, "library", library);
                var nameLabel = new GameObject("Name", typeof(RectTransform)).AddComponent<Text>();
                nameLabel.transform.SetParent(root.transform, false);
                Set(panel, "nameLabel", nameLabel);

                var settings = new GameSettingsModel { ShipSkin = library.skins[2].id };
                panel.Show(settings);
                Assert.That(panel.CurrentIndex, Is.EqualTo(2), "it opens on the equipped ship");
                Assert.That(nameLabel.text, Is.EqualTo(library.skins[2].displayName.ToUpperInvariant()));

                panel.Next();
                Assert.That(settings.ShipSkin, Is.EqualTo(library.skins[3].id), "the ship on show is equipped and saved");

                panel.Equip(library.skins.Length - 1);
                panel.Next();
                Assert.That(panel.CurrentIndex, Is.Zero, "past the last ship it wraps to the first");
                panel.Previous();
                Assert.That(panel.CurrentIndex, Is.EqualTo(library.skins.Length - 1), "and back again");
                Assert.That(settings.ShipSkin, Is.EqualTo(library.skins[library.skins.Length - 1].id));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Swipe_NeedsASidewaysDragOfSomeLength()
        {
            var go = new GameObject("Swipe");
            try
            {
                var swipe = go.AddComponent<HorizontalSwipe>();
                var seen = new List<int>();
                swipe.Swiped += seen.Add;
                Assert.That(swipe.Resolve(new Vector2(-300f, 20f), 1080f), Is.True);
                Assert.That(swipe.Resolve(new Vector2(250f, -30f), 1080f), Is.True);
                Assert.That(swipe.Resolve(new Vector2(40f, 0f), 1080f), Is.False, "a tap or a nudge is not a swipe");
                Assert.That(swipe.Resolve(new Vector2(200f, 400f), 1080f), Is.False, "a mostly vertical drag is not a swipe");
                Assert.That(seen, Is.EqualTo(new[] { -1, 1 }));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EveryShipButTheDefault_TradesAStrengthForAWeakness()
        {
            var library = AssetDatabase.LoadAssetAtPath<ShipSkinLibrary>(LibraryPath);
            var neutral = library.Default.stats;
            Assert.That(Multipliers(neutral), Is.All.EqualTo(1f), "the default ship is the balanced baseline");
            Assert.That(neutral.extraLives, Is.Zero);
            for (var i = 1; i < library.skins.Length; i++)
            {
                var stats = library.skins[i].stats;
                var name = library.skins[i].displayName;
                Assert.That(stats.perk, Is.Not.Empty, name);
                Assert.That(stats.drawback, Is.Not.Empty, name);
                var multipliers = Multipliers(stats);
                Assert.That(multipliers.Exists(value => value > 1f) || stats.extraLives > 0, Is.True, $"{name} has no strength");
                Assert.That(multipliers.Exists(value => value < 1f), Is.True, $"{name} has no weakness");
            }
        }

        [Test]
        public void Hangar_LaunchesTheRunInTheShipOnShow()
        {
            var library = AssetDatabase.LoadAssetAtPath<ShipSkinLibrary>(LibraryPath);
            var root = new GameObject("Hangar");
            try
            {
                root.SetActive(false);
                var panel = root.AddComponent<SkinSelectPanel>();
                Set(panel, "root", root);
                Set(panel, "library", library);
                var launched = 0;
                panel.OpenForLaunch(() => launched++, new GameSettingsModel());
                Assert.That(panel.IsShown, Is.True, "the difficulty choice leads to the hangar");
                Assert.That(launched, Is.Zero, "nothing starts until the player launches");
                panel.Launch();
                Assert.That(launched, Is.EqualTo(1));
                panel.Hide();
                Assert.That(panel.IsShown, Is.False, "back returns to the difficulty choice");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static List<float> Multipliers(ShipStats stats) => new List<float>
        {
            stats.speed, stats.safeSpeed, stats.exposedSpeed, stats.powerCharge, stats.shotSpeed, stats.pickupChance, stats.abilityDuration
        };

        [Test]
        public void SkinChoice_SurvivesASettingsReset()
        {
            var settings = new GameSettingsModel { ShipSkin = "viper" };
            settings.ResetToDefaults();
            Assert.That(settings.ShipSkin, Is.EqualTo("viper"), "a skin is a choice, not a preference");
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
