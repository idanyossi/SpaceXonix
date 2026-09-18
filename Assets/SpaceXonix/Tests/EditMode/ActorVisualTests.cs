using System;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Presentation;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class ActorVisualTests
    {
        [Test]
        public void Visual_HoversAboveTheBoardWhileTheLogicalRootStaysOnThePlane()
        {
            using (var fixture = new Fixture())
            {
                var logical = fixture.Board.GetWorldPosition(new GridCoordinate(10, 20));
                fixture.Actor.transform.position = logical;
                fixture.Actor.Apply(Quaternion.identity);

                Assert.That(fixture.Actor.transform.position, Is.EqualTo(logical), "the logical root never leaves the board plane");
                Assert.That(fixture.Actor.Visual.position.x, Is.EqualTo(logical.x).Within(.0001f));
                Assert.That(fixture.Actor.Visual.position.y, Is.EqualTo(logical.y).Within(.0001f));
                Assert.That(fixture.Actor.Visual.position.z, Is.EqualTo(logical.z - fixture.Actor.HoverHeight).Within(.0001f), "hovers toward the camera (-Z)");
            }
        }

        [Test]
        public void Visual_FacesTheCameraWhenBillboarded()
        {
            using (var fixture = new Fixture())
            {
                var cameraRotation = Quaternion.Euler(-35f, 0f, 6f);
                fixture.Actor.Apply(cameraRotation);
                Assert.That(Quaternion.Angle(fixture.Actor.Visual.rotation, cameraRotation), Is.LessThan(.01f));

                Fixture.Set(fixture.Actor, "faceCamera", false);
                fixture.Actor.Visual.rotation = Quaternion.identity;
                fixture.Actor.Apply(cameraRotation);
                Assert.That(Quaternion.Angle(fixture.Actor.Visual.rotation, Quaternion.identity), Is.LessThan(.01f), "non-billboarded visuals keep their own rotation");
            }
        }

        [Test]
        public void Shadow_SitsOnThePitFloorAndOnRaisedTerritory()
        {
            using (var fixture = new Fixture())
            {
                var openCell = new GridCoordinate(10, 20);
                fixture.Actor.transform.position = fixture.Board.GetWorldPosition(openCell);
                fixture.Actor.Apply(Quaternion.identity);
                Assert.That(fixture.Actor.Shadow.position.z, Is.EqualTo(-.01f).Within(.0001f), "shadow rests just above the pit floor");

                var perimeter = new GridCoordinate(0, 20);
                fixture.Actor.transform.position = fixture.Board.GetWorldPosition(perimeter);
                fixture.Actor.Apply(Quaternion.identity);
                Assert.That(fixture.Actor.Shadow.position.z, Is.EqualTo(-fixture.Renderer.TerritoryHeight - .01f).Within(.0001f), "shadow climbs onto raised territory");
                Assert.That(fixture.Actor.Shadow.position.x, Is.EqualTo(fixture.Actor.transform.position.x).Within(.0001f));
                Assert.That(fixture.Actor.Shadow.rotation, Is.EqualTo(Quaternion.identity));
            }
        }

        [Test]
        public void Shadow_FollowsRisingTerritoryDuringTheCaptureAnimation()
        {
            using (var fixture = new Fixture())
            {
                for (var x = 1; x < 53; x++) fixture.Board.Model.MoveTo(new GridCoordinate(x, 3));
                fixture.Board.Model.MoveTo(new GridCoordinate(53, 3));
                var captured = new GridCoordinate(10, 2);
                fixture.Actor.transform.position = fixture.Board.GetWorldPosition(captured);

                fixture.Actor.Apply(Quaternion.identity);
                Assert.That(fixture.Actor.Shadow.position.z, Is.EqualTo(-.01f).Within(.0001f));
                fixture.Renderer.Tick(1f);
                fixture.Actor.Apply(Quaternion.identity);
                Assert.That(fixture.Actor.Shadow.position.z, Is.EqualTo(-fixture.Renderer.TerritoryHeight - .01f).Within(.0001f));
            }
        }

        [Test]
        public void ConfiguredPrefabs_HoverWithSeparatedLogicAndVisuals()
        {
            foreach (var path in new[]
            {
                "Assets/SpaceXonix/Prefabs/Gameplay/Player.prefab",
                "Assets/SpaceXonix/Prefabs/Enemies/BasicBouncer.prefab",
                "Assets/SpaceXonix/Prefabs/Enemies/LinearAlien.prefab",
                "Assets/SpaceXonix/Prefabs/Enemies/UnstableAlien.prefab",
                "Assets/SpaceXonix/Prefabs/Enemies/VolatileAlien.prefab",
                "Assets/SpaceXonix/Prefabs/PowerUps/PowerUpPickup.prefab"
            })
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                var actor = prefab.GetComponent<ActorVisual>();
                Assert.That(actor, Is.Not.Null, path + " needs an ActorVisual");
                Assert.That(actor.HoverHeight, Is.GreaterThan(0f), path);
                Assert.That(actor.Visual, Is.Not.Null, path + " needs a visual child");
                Assert.That(actor.Shadow, Is.Not.Null, path + " needs a shadow");
                Assert.That(prefab.GetComponent<MeshRenderer>(), Is.Null, path + " root must stay logic-only");
                Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one), path + " root keeps unit scale");
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("ActorVisualFixture");
            private readonly GameObject actorObject = new GameObject("Actor");
            public readonly BoardManager Board;
            public readonly BoardRenderer Renderer;
            public readonly ActorVisual Actor;

            public Fixture()
            {
                root.SetActive(false);
                Board = root.AddComponent<BoardManager>();
                var view = new GameObject("BoardView");
                view.transform.SetParent(root.transform, false);
                Renderer = view.AddComponent<BoardRenderer>();
                Set(Board, "boardRenderer", Renderer);
                Board.Initialize();
                Renderer.Initialize(Board);

                actorObject.SetActive(false);
                Actor = actorObject.AddComponent<ActorVisual>();
                var visual = new GameObject("Visual"); visual.transform.SetParent(actorObject.transform, false);
                var shadow = new GameObject("Shadow"); shadow.transform.SetParent(actorObject.transform, false);
                Set(Actor, "visual", visual.transform);
                Set(Actor, "shadow", shadow.transform);
                Set(Actor, "boardManager", Board);
                Set(Actor, "hoverHeight", .55f);
                Set(Actor, "faceCamera", true);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(actorObject);
            }

            public static void Set(object target, string name, object value) =>
                target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        }
    }
}
