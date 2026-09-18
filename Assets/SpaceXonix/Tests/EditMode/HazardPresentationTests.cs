using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Enemies;
using SpaceXonix.Hazards;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using SpaceXonix.Presentation;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class HazardPresentationTests
    {
        [Test]
        public void LaserPresentation_LiftsTowardTheCameraByTheGivenHeight()
        {
            var host = new GameObject("Beam");
            try
            {
                var presentation = host.AddComponent<LaserPresentation>();
                presentation.Configure(new Vector3(1f, 2f, 0f), LaserAxis.Horizontal, 9f, .12f, .55f);
                Assert.That(host.transform.position.z, Is.EqualTo(-.55f).Within(.0001f));
                Assert.That(host.transform.localScale, Is.EqualTo(new Vector3(9f, .12f, .12f)));
                presentation.Configure(new Vector3(1f, 2f, 0f), LaserAxis.Vertical, 9f, .12f, 0f);
                Assert.That(host.transform.position.z, Is.Zero);
                Assert.That(host.transform.rotation.eulerAngles.z, Is.EqualTo(90f).Within(.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void LaserEmitter_PutsTheWarningOnTheFloorAndTheBeamAtHoverHeight()
        {
            using (var fixture = new Fixture())
            {
                fixture.Emitter.Tick(fixture.Definition.cooldownDuration + .01f);
                var warning = fixture.ActivePresentation("activeWarning");
                Assert.That(warning, Is.Not.Null, "warning is showing");
                Assert.That(warning.transform.position.z, Is.EqualTo(-.02f).Within(.0001f), "warning line stays on the pit floor");

                fixture.Emitter.Tick(fixture.Definition.warningDuration + .01f);
                var beam = fixture.ActivePresentation("activeBeam");
                Assert.That(beam, Is.Not.Null, "beam is firing");
                Assert.That(beam.transform.position.z, Is.EqualTo(-.55f).Within(.0001f), "beam fires at hover height");
            }
        }

        [Test]
        public void ExplosionRing_LiesOnTheFloorSizedToTheBlastAndFadesOut()
        {
            var host = new GameObject("Ring");
            try
            {
                var ring = host.AddComponent<ExplosionRing>();
                host.AddComponent<MeshRenderer>();
                ring.Play(new Vector3(3f, 4f, 0f), .72f);
                Assert.That(host.transform.position, Is.EqualTo(new Vector3(3f, 4f, -.03f)));
                Assert.That(host.transform.localScale.x, Is.EqualTo(1.44f).Within(.0001f), "ring spans the blast diameter");
                Assert.That(ring.IsFinished, Is.False);
                Assert.That(ring.Tick(ring.Duration * .5f), Is.True);
                Assert.That(ring.Tick(ring.Duration), Is.False);
                Assert.That(ring.IsFinished, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ExplosionRingPresenter_SpawnsOnVolatileExplosionAndReturnsRingsToThePool()
        {
            using (var fixture = new Fixture())
            {
                var ring = fixture.Presenter.Spawn(new Vector3(2f, 2f, 0f), .5f);
                Assert.That(ring, Is.Not.Null);
                Assert.That(fixture.Presenter.ActiveRings.Count, Is.EqualTo(1));
                fixture.Presenter.Tick(ring.Duration + .01f);
                Assert.That(fixture.Presenter.ActiveRings, Is.Empty);
                Assert.That(ring.gameObject.activeSelf, Is.False, "released back to the pool");

                var reused = fixture.Presenter.Spawn(new Vector3(5f, 5f, 0f), .5f);
                Assert.That(reused, Is.SameAs(ring), "pooled rings are reused");
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("HazardPresentationFixture");
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            public readonly BoardManager Board;
            public readonly LaserEmitter Emitter;
            public readonly LaserDefinition Definition;
            public readonly ExplosionRingPresenter Presenter;

            public Fixture()
            {
                root.SetActive(false);
                Board = root.AddComponent<BoardManager>();
                Board.Initialize();
                var pool = root.AddComponent<PoolService>();

                Definition = Own(ScriptableObject.CreateInstance<LaserDefinition>());
                Definition.axis = LaserAxis.Horizontal;
                var warningPrefab = Own(new GameObject("WarningPrefab")); warningPrefab.AddComponent<LaserPresentation>(); warningPrefab.SetActive(false);
                var beamPrefab = Own(new GameObject("BeamPrefab")); beamPrefab.AddComponent<LaserPresentation>(); beamPrefab.SetActive(false);
                var emitterObject = Own(new GameObject("Emitter"));
                emitterObject.transform.SetParent(root.transform, false);
                Emitter = emitterObject.AddComponent<LaserEmitter>();
                Emitter.SetDefinition(Definition);
                Emitter.Initialize(Board, null, pool, warningPrefab, beamPrefab);

                var ringPrefab = Own(new GameObject("RingPrefab"));
                ringPrefab.AddComponent<MeshRenderer>();
                ringPrefab.AddComponent<ExplosionRing>();
                ringPrefab.SetActive(false);
                Presenter = root.AddComponent<ExplosionRingPresenter>();
                Set(Presenter, "poolService", pool);
                Set(Presenter, "ringPrefab", ringPrefab);
                root.SetActive(true);
            }

            public GameObject ActivePresentation(string field) =>
                (GameObject)typeof(LaserEmitter).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Emitter);

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

            private static void Set(object target, string name, object value) =>
                target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        }
    }
}
