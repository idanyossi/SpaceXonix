using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Player;
using SpaceXonix.PowerUps;
using SpaceXonix.Presentation;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class PixelArtTests
    {
        [TestCase("Backdrop")]
        [TestCase("Stars")]
        [TestCase("FarPlanets")]
        [TestCase("BigPlanet")]
        [TestCase("RingPlanet")]
        public void BackdropLayers_SitInTheBluePalette(string layer)
        {
            // The original background was magenta and clashed with the teal deck and UI; it was
            // recoloured onto a navy-to-cyan ramp, so no layer may drift warm again.
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(texture.LoadImage(System.IO.File.ReadAllBytes($"Assets/SpaceXonix/Art/ThirdParty/Ansimuz/Background/{layer}.png")), Is.True);
                var opaque = 0;
                foreach (var pixel in texture.GetPixels32())
                {
                    if (pixel.a < 128) continue;
                    opaque++;
                    Assert.That(pixel.b, Is.GreaterThanOrEqualTo(pixel.r), $"{layer} has a warm pixel {pixel}");
                }
                Assert.That(opaque, Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Animator_LoopsItsFramesAndCatchesUpOnALongFrame()
        {
            var host = new GameObject("Animated");
            var textures = new Texture2D[3];
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>();
                var animator = host.AddComponent<SpriteFrameAnimator>();
                var frames = new Sprite[3];
                for (var i = 0; i < 3; i++)
                {
                    textures[i] = new Texture2D(4, 4);
                    frames[i] = Sprite.Create(textures[i], new Rect(0, 0, 4, 4), Vector2.one * .5f);
                }
                Invoke(animator, "Awake");
                animator.SetFrames(frames, 10f);
                Assert.That(animator.CurrentFrame, Is.Zero);
                Assert.That(renderer.sprite, Is.SameAs(frames[0]));

                animator.Advance(.05f);
                Assert.That(animator.CurrentFrame, Is.Zero, "half a frame is not enough to step");
                animator.Advance(.05f);
                Assert.That(animator.CurrentFrame, Is.EqualTo(1));
                Assert.That(renderer.sprite, Is.SameAs(frames[1]));

                // A hitch of several frames lands where the loop would have been, not one step on.
                animator.Advance(.2f);
                Assert.That(animator.CurrentFrame, Is.Zero, "1 + 2 wraps back to the first frame");
            }
            finally
            {
                Object.DestroyImmediate(host);
                foreach (var texture in textures) if (texture != null) Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ShipHeading_PointsTheNoseAlongEachDirection()
        {
            // The sprite is drawn nose-up; the board's up is the screen's up.
            Assert.That(ShipHeading.DegreesFor(CardinalDirection.Up), Is.EqualTo(0f));
            Assert.That(ShipHeading.DegreesFor(CardinalDirection.Left), Is.EqualTo(90f));
            Assert.That(ShipHeading.DegreesFor(CardinalDirection.Down), Is.EqualTo(180f));
            Assert.That(ShipHeading.DegreesFor(CardinalDirection.Right), Is.EqualTo(-90f));
        }

        [Test]
        public void Billboard_TurnsOnScreenWithoutTiltingAwayFromTheCamera()
        {
            var host = new GameObject("Actor");
            try
            {
                var visual = new GameObject("Visual").transform;
                visual.SetParent(host.transform, false);
                var actor = host.AddComponent<ActorVisual>();
                Set(actor, "visual", visual);
                Set(actor, "faceCamera", true);
                var camera = Quaternion.Euler(-35f, 0f, 0f);

                actor.HeadingDegrees = -90f;
                actor.Apply(camera);

                // Still facing the camera: its forward axis is the camera's forward axis.
                Assert.That(Vector3.Angle(visual.forward, camera * Vector3.forward), Is.LessThan(.01f));
                // And turned in the screen plane: its up now points along the camera's right.
                Assert.That(Vector3.Angle(visual.up, camera * Vector3.right), Is.LessThan(.01f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Shadow_IsSizedToTheActorRatherThanAFixedWidth()
        {
            var small = MakeActor(.28f);
            var large = MakeActor(1f);
            try
            {
                var smallWidth = small.Shadow.localScale.x;
                var largeWidth = large.Shadow.localScale.x;
                Assert.That(small.VisualWidth(), Is.EqualTo(.28f).Within(.001f));
                Assert.That(smallWidth / largeWidth, Is.EqualTo(.28f).Within(.001f),
                    "a tiny Volatile gets a proportionally tiny shadow");
            }
            finally
            {
                Object.DestroyImmediate(small.gameObject);
                Object.DestroyImmediate(large.gameObject);
            }
        }

        [Test]
        public void Pickup_TakesItsLookFromWhicheverPowerUpItIs()
        {
            var host = new GameObject("Pickup");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(host.transform, false);
            var texture = new Texture2D(4, 4);
            var definition = ScriptableObject.CreateInstance<PowerUpDefinition>();
            try
            {
                var sprite = visual.AddComponent<SpriteRenderer>();
                var pickup = host.AddComponent<PowerUpPickup>();
                Set(pickup, "pickupRenderer", sprite);
                var frame = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
                definition.pickupFrames = new[] { frame };
                definition.pickupTint = new Color(.6f, .9f, 1f);

                pickup.Configure(definition, default, Vector3.zero, 5f);

                Assert.That(sprite.sprite, Is.SameAs(frame));
                Assert.That(sprite.color, Is.EqualTo(definition.pickupTint), "one orb design is tinted per power-up");

                var before = visual.transform.rotation;
                pickup.Tick(.5f);
                Assert.That(visual.transform.rotation, Is.EqualTo(before), "pixel art is not spun, which would smear it");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(definition);
            }
        }

        private static ActorVisual MakeActor(float width)
        {
            var host = new GameObject("Actor");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(host.transform, false);
            var texture = new Texture2D(16, 16);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * .5f, 16f);
            visual.transform.localScale = Vector3.one * width;
            var shadow = new GameObject("Shadow").transform;
            shadow.SetParent(host.transform, false);
            var actor = host.AddComponent<ActorVisual>();
            Set(actor, "visual", visual.transform);
            Set(actor, "shadow", shadow);
            Invoke(actor, "Awake");
            return actor;
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static void Invoke(object target, string name) =>
            target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    }
}
