using System;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Presentation;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class ArenaShakerTests
    {
        [Test]
        public void CaptureShake_GrowsWithCapturedAreaUpToTheLargeCapturePunch()
        {
            Assert.That(ShakeStrength.ForCapture(0f, .1f, .5f, 15f), Is.Zero, "nothing captured, nothing shakes");
            Assert.That(ShakeStrength.ForCapture(1f, .1f, .5f, 15f), Is.EqualTo(.1267f).Within(.001f));
            var small = ShakeStrength.ForCapture(3f, .1f, .5f, 15f);
            var medium = ShakeStrength.ForCapture(8f, .1f, .5f, 15f);
            Assert.That(medium, Is.GreaterThan(small));
            Assert.That(ShakeStrength.ForCapture(15f, .1f, .5f, 15f), Is.EqualTo(.5f).Within(.0001f));
            Assert.That(ShakeStrength.ForCapture(40f, .1f, .5f, 15f), Is.EqualTo(.5f).Within(.0001f), "clamped at the large-capture force");
        }

        [Test]
        public void ExplosionShake_ScalesWithBlastRadiusWithinLimits()
        {
            Assert.That(ShakeStrength.ForExplosion(.75f, .75f, .6f), Is.EqualTo(.6f).Within(.0001f));
            Assert.That(ShakeStrength.ForExplosion(1.5f, .75f, .6f), Is.EqualTo(1.2f).Within(.0001f));
            Assert.That(ShakeStrength.ForExplosion(10f, .75f, .6f), Is.EqualTo(1.2f).Within(.0001f), "clamped at twice the reference");
            Assert.That(ShakeStrength.ForExplosion(.05f, .75f, .6f), Is.EqualTo(.3f).Within(.0001f), "clamped at half the reference");
        }

        [Test]
        public void Shaker_RecordsForcesForCapturesAndDeathsAndObeysTheShakeSetting()
        {
            using (var fixture = new Fixture())
            {
                for (var x = 1; x < 53; x++) fixture.Board.Model.MoveTo(new GridCoordinate(x, 3));
                fixture.Board.Model.MoveTo(new GridCoordinate(53, 3));
                Assert.That(fixture.Shaker.LastForce, Is.GreaterThan(0f), "a capture shakes the camera");
                var captureForce = fixture.Shaker.LastForce;

                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
                Assert.That(fixture.Shaker.LastForce, Is.GreaterThan(0f), "death shakes the camera");
                Assert.That(fixture.Shaker.LastForce, Is.Not.EqualTo(captureForce));

                fixture.Shaker.SetShakeEnabled(false);
                fixture.Shaker.Shake(1f);
                Assert.That(fixture.Shaker.LastForce, Is.Zero, "the camera-shake setting suppresses shakes");
                fixture.Shaker.SetShakeEnabled(true);
                fixture.Shaker.Shake(.3f);
                Assert.That(fixture.Shaker.LastForce, Is.EqualTo(.3f).Within(.0001f));
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("ArenaShakerFixture");
            private readonly GameObject playerObject = new GameObject("Player");
            public readonly BoardManager Board;
            public readonly GameManager Game;
            public readonly ArenaShaker Shaker;

            public Fixture()
            {
                root.SetActive(false);
                Board = root.AddComponent<BoardManager>();
                Board.Initialize();
                var input = root.AddComponent<InputRouter>();
                var player = playerObject.AddComponent<PlayerController>();
                Invoke(player, "Awake");
                Game = root.AddComponent<GameManager>();
                Set(Game, "inputRouter", input); Set(Game, "playerController", player); Set(Game, "boardManager", Board);
                Shaker = root.AddComponent<ArenaShaker>();
                Set(Shaker, "boardManager", Board); Set(Shaker, "gameManager", Game);
                Invoke(Game, "Awake");
                root.SetActive(true);
                Invoke(Game, "Start");
                Invoke(Shaker, "OnEnable");
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(playerObject);
            }

            private static void Set(object target, string name, object value) =>
                target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

            private static void Invoke(object target, string name) =>
                target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        }
    }
}
