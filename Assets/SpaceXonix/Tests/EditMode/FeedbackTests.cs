using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Campaign;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using SpaceXonix.Power;
using SpaceXonix.Presentation;
using SpaceXonix.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.Tests.EditMode
{
    /// <summary>The shield bubble, the arena rim, the Power Shot's effects, and the run's upgrades on screen.</summary>
    public sealed class FeedbackTests
    {
        // ---------------------------------------------------------------- arena rim

        [Test]
        public void Rim_FramesTheBoardOutsideThePlayableGridAndFacesTheCamera()
        {
            var mesh = new Mesh();
            try
            {
                const float w = 9.72f, h = 17.28f, frame = .3f, height = .55f;
                ArenaRim.BuildMesh(mesh, w, h, frame, height, .6f, .05f, 2f, 2f);
                Assert.That(mesh.subMeshCount, Is.EqualTo(3));
                for (var s = 0; s < 3; s++) Assert.That(mesh.GetTriangles(s), Is.Not.Empty, $"submesh {s}");

                var vertices = mesh.vertices;
                var normals = mesh.normals;
                foreach (var v in vertices)
                {
                    // Nothing of the frame lies over a playable cell.
                    var inside = v.x > 0f + 1e-4f && v.x < w - 1e-4f && v.y > 0f + 1e-4f && v.y < h - 1e-4f;
                    Assert.That(inside, Is.False, $"vertex {v} is over the board");
                }
                Assert.That(mesh.bounds.min.x, Is.EqualTo(-frame).Within(1e-4f));
                Assert.That(mesh.bounds.max.y, Is.EqualTo(h + frame).Within(1e-4f));
                // The top stands above captured territory (0.35), "up" being -Z.
                Assert.That(mesh.bounds.min.z, Is.LessThan(-.35f));

                // Every triangle is wound to face the way its normal points, so none is culled from view.
                for (var s = 0; s < 3; s++)
                {
                    var triangles = mesh.GetTriangles(s);
                    for (var i = 0; i < triangles.Length; i += 3)
                    {
                        Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                        var facing = Vector3.Cross(b - a, c - a);
                        Assert.That(Vector3.Dot(facing, normals[triangles[i]]), Is.GreaterThan(0f), $"submesh {s} triangle {i / 3}");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        // ---------------------------------------------------------------- shield

        [Test]
        public void ShieldBubble_PopsInFlashesOnAHitAndMarksTheCoveredTrail()
        {
            var root = new GameObject("Shield");
            var cameraObject = new GameObject("Camera");
            try
            {
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<Camera>();
                cameraObject.transform.rotation = Quaternion.Euler(-35f, 0f, 0f);
                root.SetActive(false);
                var bubble = new GameObject("Bubble").AddComponent<SpriteRenderer>();
                bubble.transform.SetParent(root.transform, false);
                bubble.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 4f);
                var glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
                glow.transform.SetParent(bubble.transform, false);
                var footprint = GameObject.CreatePrimitive(PrimitiveType.Quad);
                footprint.transform.SetParent(root.transform, false);
                var presenter = root.AddComponent<ShieldBubble>();
                Set(presenter, "bubble", bubble);
                Set(presenter, "hitGlow", glow);
                Set(presenter, "footprint", footprint.GetComponent<Renderer>());
                Set(presenter, "cameraTransform", cameraObject.transform);
                root.transform.position = new Vector3(2f, 3f, -.55f);
                root.SetActive(true);

                presenter.Tick(0f, 0f);
                Assert.That(presenter.BubbleScale, Is.LessThan(.6f), "it starts small");
                presenter.Tick(1f, 0f);
                Assert.That(presenter.BubbleScale, Is.EqualTo(1f).Within(.001f), "and settles at full size");
                Assert.That(Quaternion.Angle(bubble.transform.rotation, cameraObject.transform.rotation), Is.LessThan(.01f), "facing the camera");
                Assert.That(glow.enabled, Is.False);

                // The footprint lies flat on the floor under the ship, at the bubble's reach over the trail.
                var floor = footprint.transform.position;
                Assert.That(new Vector2(floor.x, floor.y), Is.EqualTo(new Vector2(2f, 3f)));
                Assert.That(floor.z, Is.GreaterThan(-.1f), "on the floor, not at hover height");
                Assert.That(footprint.transform.localScale.x, Is.EqualTo(.75f / .88f).Within(.001f));

                typeof(ShieldBubble).GetMethod("OnShielded", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(presenter, new object[] { SpaceXonix.Core.PlayerFailureReason.EnemyContact });
                presenter.Tick(.01f, 0f);
                Assert.That(presenter.HitFlash, Is.GreaterThan(.9f));
                Assert.That(glow.enabled, Is.True, "a blocked hit flashes the bubble");
                Assert.That(presenter.BubbleScale, Is.GreaterThan(1.1f), "and swells it");
                presenter.Tick(1f, 0f);
                Assert.That(presenter.HitFlash, Is.Zero);
                Assert.That(glow.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
            }
        }

        // ---------------------------------------------------------------- power shot

        [TestCase(CardinalDirection.Up, 0f)]
        [TestCase(CardinalDirection.Right, -90f)]
        [TestCase(CardinalDirection.Down, 180f)]
        [TestCase(CardinalDirection.Left, 90f)]
        public void PowerShot_TurnsItsBoltToTheDirectionOfTravelWithoutStretchingIt(CardinalDirection direction, float heading)
        {
            var shot = new GameObject("Shot");
            try
            {
                var visual = new GameObject("Visual").AddComponent<SpriteRenderer>();
                visual.transform.SetParent(shot.transform, false);
                var streak = visual.gameObject.AddComponent<TrailRenderer>();
                var actor = shot.AddComponent<ActorVisual>();
                Set(actor, "visual", visual.transform);
                var projectile = shot.AddComponent<PowerShotProjectile>();
                Set(projectile, "streak", streak);

                projectile.Launch(new Vector3(1f, 2f, 0f), direction, 14f);
                Assert.That(actor.HeadingDegrees, Is.EqualTo(heading));
                Assert.That(visual.transform.localScale, Is.EqualTo(Vector3.one), "a sprite bolt keeps its drawn shape");
                Assert.That(streak.positionCount, Is.Zero, "the streak starts fresh at the ship");
                Assert.That(visual.transform.position, Is.EqualTo(new Vector3(1f, 2f, -actor.HoverHeight)));
            }
            finally
            {
                Object.DestroyImmediate(shot);
            }
        }

        [Test]
        public void PowerShot_FlashesOnFireAndShockwavesOnImpact()
        {
            var root = new GameObject("Fx");
            var burstPrefab = new GameObject("Burst");
            var ringPrefab = new GameObject("Ring");
            var shotObject = new GameObject("Shot");
            try
            {
                var frames = new Sprite[3];
                for (var i = 0; i < frames.Length; i++) frames[i] = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
                Set(burstPrefab.AddComponent<SpriteBurst>(), "frames", frames);
                burstPrefab.SetActive(false);
                ringPrefab.AddComponent<ExplosionRing>();
                ringPrefab.SetActive(false);
                var pool = root.AddComponent<PoolService>();

                var muzzles = new GameObject("Muzzles").AddComponent<SpriteBurstPresenter>();
                muzzles.transform.SetParent(root.transform);
                Set(muzzles, "poolService", pool); Set(muzzles, "explosionPrefab", burstPrefab);
                var explosions = new GameObject("Explosions").AddComponent<SpriteBurstPresenter>();
                explosions.transform.SetParent(root.transform);
                Set(explosions, "poolService", pool); Set(explosions, "explosionPrefab", burstPrefab);
                var shockwaves = new GameObject("Shockwaves").AddComponent<ExplosionRingPresenter>();
                shockwaves.transform.SetParent(root.transform);
                Set(shockwaves, "poolService", pool); Set(shockwaves, "ringPrefab", ringPrefab);
                var presenter = root.AddComponent<PowerShotPresenter>();
                Set(presenter, "muzzleFlashes", muzzles);
                Set(presenter, "explosions", explosions);
                Set(presenter, "shockwaves", shockwaves);

                var shot = shotObject.AddComponent<PowerShotProjectile>();
                shot.Launch(Vector3.zero, CardinalDirection.Up, 10f);
                Invoke(presenter, "OnShotFired", shot);
                Assert.That(presenter.ShotsFired, Is.EqualTo(1));
                Assert.That(muzzles.Active.Count, Is.EqualTo(1), "a muzzle flash off the nose");
                Assert.That(muzzles.Active[0].transform.position.y, Is.GreaterThan(0f), "ahead of the ship");

                // An alien kill already has its own explosion: the impact adds only the shockwave.
                Invoke(presenter, "OnEnemyDestroyed", new object[] { null });
                Invoke(presenter, "OnShotImpact", new Vector3(1f, 1f, 0f));
                Assert.That(shockwaves.ActiveRings.Count, Is.EqualTo(1));
                Assert.That(explosions.Active.Count, Is.Zero);

                // The boss is only stunned, so its hit gets an explosion here.
                Invoke(presenter, "OnShotImpact", new Vector3(2f, 2f, 0f));
                Assert.That(shockwaves.ActiveRings.Count, Is.EqualTo(2));
                Assert.That(explosions.Active.Count, Is.EqualTo(1));
                Assert.That(presenter.Impacts, Is.EqualTo(2));
                Assert.That(presenter.IsHitStopping, Is.False, "no hit-stop outside play mode");
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(burstPrefab);
                Object.DestroyImmediate(ringPrefab);
                Object.DestroyImmediate(shotObject);
            }
        }

        // ---------------------------------------------------------------- upgrades

        [Test]
        public void RunUpgrades_RememberEachUpgradeOnceInTheOrderTaken()
        {
            var thrusters = Upgrade(UpgradeType.ImprovedThrusters);
            var hull = Upgrade(UpgradeType.ReinforcedHull);
            try
            {
                var run = new RunUpgradeModel();
                run.Take(hull);
                run.Take(thrusters);
                run.Take(hull);
                Assert.That(run.Taken, Is.EqualTo(new[] { hull, thrusters }));
                Assert.That(run.GetStacks(UpgradeType.ReinforcedHull), Is.EqualTo(2));
                run.Reset();
                Assert.That(run.Taken, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(thrusters);
                Object.DestroyImmediate(hull);
            }
        }

        [Test]
        public void UpgradeHud_ShowsABadgePerUpgradeAndThePauseListDescribesThem()
        {
            var thrusters = Upgrade(UpgradeType.ImprovedThrusters);
            var hull = Upgrade(UpgradeType.ReinforcedHull);
            var set = ScriptableObject.CreateInstance<UpgradeSetDefinition>();
            var root = new GameObject("Hud", typeof(RectTransform));
            try
            {
                set.upgrades = new[] { thrusters, hull };
                root.SetActive(false);
                var manager = root.AddComponent<UpgradeManager>();
                Set(manager, "upgradeSet", set);
                Set(manager, "offerCount", 2);
                manager.SetRandom(new System.Random(3));

                var badge = NewRect("Badge", root.transform);
                badge.sizeDelta = new Vector2(72f, 72f);
                badge.gameObject.AddComponent<Image>();
                NewRect("Icon", badge).gameObject.AddComponent<Image>();
                NewRect("Count", badge).gameObject.AddComponent<Text>();
                var strip = root.AddComponent<UpgradeStrip>();
                Set(strip, "upgradeManager", manager);
                Set(strip, "badgeTemplate", badge);

                var panel = NewRect("Panel", root.transform);
                var row = NewRect("Row", panel);
                NewRect("Icon", row).gameObject.AddComponent<Image>();
                foreach (var name in new[] { "Name", "Effect", "Count" }) NewRect(name, row).gameObject.AddComponent<Text>();
                var empty = NewRect("Empty", panel).gameObject.AddComponent<Text>();
                var listHost = new GameObject("List");
                listHost.transform.SetParent(root.transform);
                var list = listHost.AddComponent<UpgradeList>();
                Set(list, "upgradeManager", manager);
                Set(list, "panel", panel);
                Set(list, "rowTemplate", row);
                Set(list, "emptyLabel", empty);
                root.SetActive(true);
                // Edit mode runs no OnEnable, which is where both views subscribe.
                Invoke(strip, "OnEnable");
                Invoke(list, "OnEnable");

                Assert.That(strip.BadgeCount, Is.Zero);
                Assert.That(list.RowCount, Is.Zero);
                Assert.That(empty.gameObject.activeSelf, Is.True, "an empty run says so");
                var emptyHeight = panel.sizeDelta.y;

                TakeUpgrade(manager, hull);
                TakeUpgrade(manager, thrusters);
                TakeUpgrade(manager, hull);

                Assert.That(strip.BadgeCount, Is.EqualTo(2));
                var first = strip.Badges[0];
                Assert.That(first.Find("Icon").GetComponent<Image>().sprite, Is.SameAs(hull.cardArt));
                Assert.That(first.Find("Count").GetComponent<Text>().text, Is.EqualTo("x2"));
                Assert.That(strip.Badges[1].Find("Count").GetComponent<Text>().text, Is.Empty, "a single stack shows no count");
                Assert.That(strip.Badges[1].anchoredPosition.x, Is.GreaterThan(first.anchoredPosition.x), "in the order taken, ending at the right");

                Assert.That(list.RowCount, Is.EqualTo(2));
                Assert.That(empty.gameObject.activeSelf, Is.False);
                Assert.That(panel.sizeDelta.y, Is.GreaterThan(emptyHeight), "the panel grows to fit the rows");

                manager.ResetRun();
                Assert.That(strip.BadgeCount, Is.Zero, "a new run starts with no badges");
                Assert.That(list.RowCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(set);
                Object.DestroyImmediate(thrusters);
                Object.DestroyImmediate(hull);
            }
        }

        private static void TakeUpgrade(UpgradeManager manager, UpgradeDefinition wanted)
        {
            Assert.That(manager.BuildOffer(), Is.True);
            var offer = manager.CurrentOffer;
            for (var i = 0; i < offer.Count; i++)
                if (offer[i] == wanted)
                {
                    Assert.That(manager.Take(i), Is.True);
                    return;
                }
            Assert.Fail($"{wanted.displayName} was not offered");
        }

        private static UpgradeDefinition Upgrade(UpgradeType type)
        {
            var definition = ScriptableObject.CreateInstance<UpgradeDefinition>();
            definition.type = type;
            definition.displayName = type.ToString();
            definition.effectText = "+10% something";
            definition.maxStacks = 3;
            definition.perStack = type == UpgradeType.ReinforcedHull ? 1f : .1f;
            definition.cardArt = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
            return definition;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Set(object target, string name, object value)
        {
            var type = target.GetType();
            FieldInfo field = null;
            while (field == null && type != null)
            {
                field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                type = type.BaseType;
            }
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }
}
