using NUnit.Framework;
using SpaceXonix.Presentation;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class ArenaCameraTests
    {
        private static readonly Rect Board = new Rect(0f, 0f, 54 * .18f, 96 * .18f);

        [TestCase(1080f / 1920f)]
        [TestCase(1920f / 1080f)]
        [TestCase(9f / 19.5f)]
        public void Framing_FitsWholeBoardInsideMarginsForAnyAspect(float aspect)
        {
            var result = ArenaFraming.Solve(Board, .6f, 35f, 30f, aspect, .03f, .08f, .86f);
            Assert.That(result.Fits, Is.True);
            GetBounds(result, aspect, out var minX, out var maxX, out var minY, out var maxY);
            Assert.That(minX, Is.GreaterThanOrEqualTo(.03f - .001f));
            Assert.That(maxX, Is.LessThanOrEqualTo(.97f + .001f));
            Assert.That(minY, Is.GreaterThanOrEqualTo(.08f - .001f));
            Assert.That(maxY, Is.LessThanOrEqualTo(.86f + .001f));
            Assert.That((minY + maxY) * .5f, Is.EqualTo(.47f).Within(.01f), "board is centred between the HUD margins");
            var tightX = Mathf.Abs(minX - .03f) < .01f || Mathf.Abs(maxX - .97f) < .01f;
            var tightY = Mathf.Abs(minY - .08f) < .01f || Mathf.Abs(maxY - .86f) < .01f;
            Assert.That(tightX || tightY, Is.True, "the board is as large as the margins allow");
        }

        [Test]
        public void Framing_LooksDiagonallyDownFromTheNearEdge()
        {
            var result = ArenaFraming.Solve(Board, .6f, 35f, 30f, 1080f / 1920f, .03f, .08f, .86f);
            var forward = result.Rotation * Vector3.forward;
            Assert.That(Vector3.Angle(forward, Vector3.forward), Is.EqualTo(35f).Within(.01f), "35 degrees away from straight down onto the board");
            Assert.That(forward.y, Is.GreaterThan(0f), "looks toward the far (top) rows");
            Assert.That(result.Position.z, Is.LessThan(0f), "camera sits above the board plane (-Z)");
            var near = ArenaFraming.WorldToViewport(new Vector3(Board.xMax, Board.yMin), result.Position, result.Rotation, 30f, 1080f / 1920f).x
                - ArenaFraming.WorldToViewport(new Vector3(Board.xMin, Board.yMin), result.Position, result.Rotation, 30f, 1080f / 1920f).x;
            var far = ArenaFraming.WorldToViewport(new Vector3(Board.xMax, Board.yMax), result.Position, result.Rotation, 30f, 1080f / 1920f).x
                - ArenaFraming.WorldToViewport(new Vector3(Board.xMin, Board.yMax), result.Position, result.Rotation, 30f, 1080f / 1920f).x;
            Assert.That(far / near, Is.InRange(.7f, .9f), "perspective narrows the far rows without making them unreadable");
        }

        [Test]
        public void ViewportProjection_MatchesUnityCamera()
        {
            var cameraObject = new GameObject("ProjectionCamera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 30f;
                camera.aspect = 1080f / 1920f;
                var result = ArenaFraming.Solve(Board, .6f, 35f, 30f, camera.aspect, .03f, .08f, .86f);
                cameraObject.transform.SetPositionAndRotation(result.Position, result.Rotation);
                foreach (var point in new[] { new Vector3(1f, 2f, 0f), new Vector3(9f, 16f, -.5f), new Vector3(4.8f, 8.6f, 0f) })
                {
                    var unity = camera.WorldToViewportPoint(point);
                    var ours = ArenaFraming.WorldToViewport(point, result.Position, result.Rotation, 30f, camera.aspect);
                    Assert.That(ours.x, Is.EqualTo(unity.x).Within(.001f));
                    Assert.That(ours.y, Is.EqualTo(unity.y).Within(.001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Rig_RollPositiveLowersRightSideAndClearsBackToPitchOnly()
        {
            var rigObject = new GameObject("Rig");
            try
            {
                var rig = rigObject.AddComponent<ArenaCameraRig>();
                rig.SetRoll(6f);
                var pitchOnly = Quaternion.Euler(-rig.PitchDegrees, 0f, 0f);
                var rolled = Quaternion.Inverse(pitchOnly) * rigObject.transform.rotation;
                Assert.That(Mathf.DeltaAngle(0f, rolled.eulerAngles.z), Is.EqualTo(6f).Within(.01f));
                Assert.That((Quaternion.Inverse(rolled) * Vector3.right).y, Is.LessThan(0f));
                rig.SetRoll(0f);
                Assert.That(Quaternion.Angle(rigObject.transform.rotation, pitchOnly), Is.LessThan(.01f));
                Assert.That(rig.CurrentRoll, Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(rigObject);
            }
        }

        private static void GetBounds(ArenaFramingResult result, float aspect, out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;
            for (var i = 0; i < 8; i++)
            {
                var point = new Vector3((i & 1) == 0 ? Board.xMin : Board.xMax, (i & 2) == 0 ? Board.yMin : Board.yMax, (i & 4) == 0 ? 0f : -.6f);
                var v = ArenaFraming.WorldToViewport(point, result.Position, result.Rotation, 30f, aspect);
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }
        }
    }
}
