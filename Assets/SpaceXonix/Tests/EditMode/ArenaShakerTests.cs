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
        public void OrdinaryCaptures_DoNotShakeAndLargeCapturesRampToFullForce()
        {
            Assert.That(ShakeStrength.ForCapture(0f, 15f, 30f, .25f), Is.Zero);
            Assert.That(ShakeStrength.ForCapture(3f, 15f, 30f, .25f), Is.Zero, "routine captures leave the camera steady");
            Assert.That(ShakeStrength.ForCapture(14.9f, 15f, 30f, .25f), Is.Zero);
            Assert.That(ShakeStrength.ForCapture(15f, 15f, 30f, .25f), Is.EqualTo(.125f).Within(.0001f), "the threshold starts at half force");
            Assert.That(ShakeStrength.ForCapture(22.5f, 15f, 30f, .25f), Is.EqualTo(.1875f).Within(.0001f));
            Assert.That(ShakeStrength.ForCapture(30f, 15f, 30f, .25f), Is.EqualTo(.25f).Within(.0001f));
            Assert.That(ShakeStrength.ForCapture(80f, 15f, 30f, .25f), Is.EqualTo(.25f).Within(.0001f), "clamped at full force");
        }

        [Test]
        public void DeathShake_VariesInDirectionAndStrengthWithinItsRange()
        {
            var straightUp = ShakeStrength.ForDeath(.6f, 1f, .25f, 1f);
            Assert.That(straightUp.magnitude, Is.EqualTo(1f).Within(.0001f));
            Assert.That(straightUp.y, Is.EqualTo(1f).Within(.0001f));
            Assert.That(straightUp.z, Is.Zero, "the kick stays in the arena plane");

            var weakest = ShakeStrength.ForDeath(.6f, 1f, 0f, 0f);
            Assert.That(weakest.magnitude, Is.EqualTo(.6f).Within(.0001f));
            Assert.That(weakest.x, Is.EqualTo(.6f).Within(.0001f));

            var other = ShakeStrength.ForDeath(.6f, 1f, .5f, .5f);
            Assert.That(other.magnitude, Is.EqualTo(.8f).Within(.0001f));
            Assert.That(Vector3.Angle(straightUp, other), Is.GreaterThan(45f), "different rolls point different ways");
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
                // a two-row cut is a routine capture: it must not shake
                for (var x = 1; x < 53; x++) fixture.Board.Model.MoveTo(new GridCoordinate(x, 3));
                fixture.Board.Model.MoveTo(new GridCoordinate(53, 3));
                Assert.That(fixture.Shaker.LastForce, Is.Zero, "routine captures leave the camera steady");

                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
                var firstDeath = fixture.Shaker.LastVelocity;
                Assert.That(firstDeath.magnitude, Is.InRange(.6f, 1f), "death kicks hardest");
                fixture.CompleteRespawn();
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.Laser), Is.True);
                var secondDeath = fixture.Shaker.LastVelocity;
                Assert.That(secondDeath, Is.Not.EqualTo(firstDeath), "each death shakes differently");

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
                Shaker.SetRandom(new System.Random(7));
                Invoke(Game, "Awake");
                root.SetActive(true);
                Invoke(Game, "Start");
                Invoke(Shaker, "OnEnable");
            }

            public void CompleteRespawn()
            {
                Invoke(Game, "CompleteRespawn");
                Set(Game, "failureGateReleaseFrame", Time.frameCount - 1);
                Invoke(Game, "LateUpdate");
                Game.AdvanceLifecycle(5f);
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
