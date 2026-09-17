using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using SpaceXonix.Hazards;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using SpaceXonix.Power;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class HitboxTests
    {
        private const float EnemyRadius = .24f;
        private const float ShipRadius = .16f;

        [Test]
        public void CircleOverlap_ZeroRadiusIsContainingCellAndRadiusCoversNeighbours()
        {
            using (var fixture = new Fixture())
            {
                var cells = new List<GridCoordinate>();
                var center = fixture.Board.GetWorldPosition(new GridCoordinate(10, 10));
                fixture.Board.GetCellsOverlappingCircle(center, 0f, cells);
                Assert.That(cells, Is.EqualTo(new[] { new GridCoordinate(10, 10) }));
                fixture.Board.GetCellsOverlappingCircle(center, EnemyRadius, cells);
                Assert.That(cells.Count, Is.EqualTo(9));
                Assert.That(cells, Has.Member(new GridCoordinate(9, 9)));
                Assert.That(cells, Has.No.Member(new GridCoordinate(12, 10)));
            }
        }

        [Test]
        public void EnemyContact_UsesShipPlusEnemyRadius()
        {
            using (var fixture = new Fixture())
            {
                fixture.StartPlaying();
                var enemy = fixture.SpawnEnemy(new GridCoordinate(20, 40));
                Assert.That(enemy.GetPlayerContactDistance(fixture.Player), Is.EqualTo(EnemyRadius + ShipRadius).Within(.0001f));
                fixture.PlacePlayerNear(enemy, EnemyRadius + ShipRadius + .02f);
                enemy.SetMovementSuspended(true);
                enemy.AdvanceMovement(.02f);
                Assert.That(fixture.Game.Lives, Is.EqualTo(3), "bodies are not touching yet");
                fixture.PlacePlayerNear(enemy, EnemyRadius + ShipRadius - .02f);
                enemy.AdvanceMovement(.02f);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2), "overlapping bodies collide");
            }
        }

        [TestCase(1, true)]
        [TestCase(2, false)]
        public void TrailContact_UsesEnemyBody(int columnsAway, bool expectsHit)
        {
            using (var fixture = new Fixture())
            {
                fixture.StartPlaying();
                var enemy = fixture.SpawnEnemy(new GridCoordinate(20, 40));
                enemy.SetMovementSuspended(true);
                fixture.DrawTrailThroughColumn(20 + columnsAway, 40);
                PlayerFailureReason? reason = null;
                fixture.Game.PlayerFailed += r => reason = r;
                enemy.AdvanceMovement(.02f);
                Assert.That(reason == PlayerFailureReason.TrailHit, Is.EqualTo(expectsHit));
            }
        }

        [Test]
        public void EnemyBody_BouncesBeforeOverlappingCapturedTerritory()
        {
            using (var fixture = new Fixture())
            {
                fixture.StartPlaying();
                var enemy = fixture.SpawnEnemyAt(fixture.Board.GetWorldPosition(new GridCoordinate(3, 40)), Vector2.left * 1.5f);
                var cells = new List<GridCoordinate>();
                for (var i = 0; i < 200; i++)
                {
                    enemy.AdvanceMovement(.02f);
                    enemy.GetFootprintCells(cells);
                    foreach (var cell in cells) Assert.That(fixture.Board.Model.GetCell(cell), Is.EqualTo(BoardCellState.Uncaptured), $"body entered {cell}");
                }
                Assert.That(enemy.Velocity.x, Is.GreaterThan(0f), "enemy bounced off the perimeter");
            }
        }

        [Test]
        public void CaptureOccupancy_IncludesWholeEnemyBody()
        {
            using (var fixture = new Fixture())
            {
                var enemy = fixture.SpawnEnemyAt(fixture.Board.GetWorldPosition(new GridCoordinate(20, 41)), Vector2.zero);
                enemy.SetMovementSuspended(true);
                fixture.Enemies.RefreshOccupancy();
                var snapshot = (List<GridCoordinate>)typeof(BoardManager).GetField("enemySnapshot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fixture.Board);
                Assert.That(snapshot, Has.Member(new GridCoordinate(20, 40)), "the cell below the centre is part of the body");
                Assert.That(snapshot, Has.Member(new GridCoordinate(21, 42)));
            }
        }

        [Test]
        public void Laser_HitsShipBodyNotJustCentre()
        {
            using (var fixture = new Fixture())
            {
                var definition = fixture.Own(ScriptableObject.CreateInstance<LaserDefinition>());
                definition.axis = LaserAxis.Horizontal;
                definition.beamWidth = .12f;
                var emitter = fixture.Own(new GameObject("Emitter")).AddComponent<LaserEmitter>();
                emitter.SetDefinition(definition);
                emitter.Initialize(fixture.Board, null, null, null, null);
                var row = fixture.Board.GetWorldPosition(new GridCoordinate(0, 30));
                emitter.transform.position = row;
                var offset = definition.beamWidth * .5f + ShipRadius - .01f;
                Assert.That(emitter.ContainsPoint(row + Vector3.up * offset), Is.False);
                Assert.That(emitter.ContainsCircle(row + Vector3.up * offset, ShipRadius), Is.True);
                Assert.That(emitter.ContainsCircle(row + Vector3.up * (offset + .03f), ShipRadius), Is.False);
            }
        }

        [Test]
        public void PowerShot_HitsEnemyBodyEdge()
        {
            using (var fixture = new Fixture())
            {
                var enemy = fixture.SpawnEnemyAt(new Vector3(2f, .29f, 0f), Vector2.zero);
                var enemies = new List<EnemyController> { enemy };
                Assert.That(PowerShotProjectile.FindFirstHit(Vector2.zero, new Vector2(4f, 0f), enemies, .06f), Is.SameAs(enemy));
                enemy.transform.position = new Vector3(2f, .32f, 0f);
                Assert.That(PowerShotProjectile.FindFirstHit(Vector2.zero, new Vector2(4f, 0f), enemies, .06f), Is.Null);
            }
        }

        [Test]
        public void ConfiguredPrefabs_VisualSizeMatchesCollisionRadius()
        {
            AssertVisualMatches("Assets/SpaceXonix/Prefabs/Gameplay/Player.prefab",
                AssetDatabase("Assets/SpaceXonix/Prefabs/Gameplay/Player.prefab").GetComponent<PlayerController>().CollisionRadius);
            foreach (var pair in new[]
            {
                ("BasicBouncer", "BasicBouncer"), ("LinearAlien", "LinearAlien"), ("UnstableAlien", "UnstableAlien"), ("VolatileAlien", "VolatileAlien")
            })
            {
                var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"Assets/SpaceXonix/ScriptableObjects/Enemies/{pair.Item2}.asset");
                Assert.That(definition.collisionRadius, Is.GreaterThan(0f), pair.Item2);
                AssertVisualMatches($"Assets/SpaceXonix/Prefabs/Enemies/{pair.Item1}.prefab", definition.collisionRadius);
            }
            var vertical = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/SpaceXonix/ScriptableObjects/Enemies/LinearAlienVertical.asset");
            var horizontal = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/SpaceXonix/ScriptableObjects/Enemies/LinearAlien.asset");
            Assert.That(vertical.collisionRadius, Is.EqualTo(horizontal.collisionRadius));
            var spawning = UnityEditor.AssetDatabase.LoadAssetAtPath<SpaceXonix.PowerUps.PowerUpSpawnDefinition>("Assets/SpaceXonix/ScriptableObjects/Balance/PowerUpSpawning.asset");
            AssertVisualMatches("Assets/SpaceXonix/Prefabs/PowerUps/PowerUpPickup.prefab", spawning.pickupRadius);
        }

        private static GameObject AssetDatabase(string path) => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private static void AssertVisualMatches(string prefabPath, float radius)
        {
            var prefab = AssetDatabase(prefabPath);
            var filter = prefab.GetComponentInChildren<MeshFilter>();
            Assert.That(filter, Is.Not.Null, prefabPath);
            var scale = filter.transform == prefab.transform ? prefab.transform.localScale : Vector3.Scale(prefab.transform.localScale, filter.transform.localScale);
            var width = filter.sharedMesh.bounds.size.x * scale.x;
            Assert.That(width, Is.EqualTo(radius * 2f).Within(.02f), prefabPath);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("HitboxFixture");
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            private readonly GameObject enemyPrefab;
            private readonly EnemyDefinition enemyDefinition;
            public readonly BoardManager Board;
            public readonly InputRouter Input;
            public readonly PlayerController Player;
            public readonly GameManager Game;
            public readonly EnemyManager Enemies;

            public Fixture()
            {
                root.SetActive(false);
                Board = root.AddComponent<BoardManager>();
                Board.Initialize();
                Input = root.AddComponent<InputRouter>();
                Player = Own(new GameObject("Player")).AddComponent<PlayerController>();
                Set(Player, "collisionRadius", ShipRadius);
                Invoke(Player, "Awake");
                Game = root.AddComponent<GameManager>();
                Set(Game, "inputRouter", Input); Set(Game, "playerController", Player); Set(Game, "boardManager", Board);
                Enemies = root.AddComponent<EnemyManager>();
                Set(Enemies, "boardManager", Board); Set(Enemies, "gameManager", Game);
                Set(Enemies, "playerController", Player); Set(Enemies, "poolService", root.AddComponent<PoolService>());
                enemyPrefab = Own(new GameObject("EnemyPrefab")); enemyPrefab.AddComponent<BasicBouncer>(); enemyPrefab.SetActive(false);
                enemyDefinition = Own(ScriptableObject.CreateInstance<EnemyDefinition>());
                enemyDefinition.collisionRadius = EnemyRadius;
                Invoke(Game, "Awake");
                root.SetActive(true);
            }

            public void StartPlaying() => Invoke(Game, "Start");

            public EnemyController SpawnEnemy(GridCoordinate cell)
            {
                Assert.That(Enemies.Spawn(enemyPrefab, enemyDefinition, cell, Vector2.right), Is.True);
                return Enemies.ActiveEnemies[Enemies.ActiveEnemies.Count - 1];
            }

            public EnemyController SpawnEnemyAt(Vector3 world, Vector2 velocity)
            {
                var enemy = SpawnEnemy(new GridCoordinate(30, 60));
                enemy.transform.position = world;
                typeof(EnemyController).GetField("movement", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(enemy, new EnemyMovementModel(world, velocity));
                Enemies.RefreshOccupancy();
                return enemy;
            }

            public void PlacePlayerNear(EnemyController enemy, float distance)
            {
                var position = enemy.transform.position + Vector3.right * distance;
                Player.transform.position = position;
            }

            public void DrawTrailThroughColumn(int column, int row)
            {
                for (var y = 1; y <= row; y++) Board.Model.MoveTo(new GridCoordinate(column, y));
                Assert.That(Board.Model.GetCell(new GridCoordinate(column, row)), Is.EqualTo(BoardCellState.Trail));
            }

            public T Own<T>(T item) where T : UnityEngine.Object
            {
                owned.Add(item);
                return item;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }

            private static void Set(object target, string name, object value) =>
                target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

            private static void Invoke(object target, string name) =>
                target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        }
    }
}
