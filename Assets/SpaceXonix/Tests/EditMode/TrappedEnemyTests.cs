using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    /// <summary>
    /// A Shield lets an alien survive standing on the player's trail. When that trail is committed
    /// the territory closes around the alien, and every candidate move is then blocked, so it used
    /// to sit in place flipping its velocity for the rest of the stage.
    /// </summary>
    public sealed class TrappedEnemyTests
    {
        [Test]
        public void AlienEnclosedByNewTerritory_IsReportedAsTrappedAndCannotMove()
        {
            using (var fixture = new Fixture())
            {
                var enemy = fixture.SpawnEnemyAt(new GridCoordinate(14, 40), Vector2.right);
                Assert.That(enemy.IsTrappedInCapturedTerritory(), Is.False);

                fixture.CaptureThroughColumn(enemy.LogicalCell.X);

                Assert.That(enemy.IsTrappedInCapturedTerritory(), Is.True);
                var before = enemy.transform.position;
                for (var step = 0; step < 30; step++) enemy.AdvanceMovement(.02f);
                Assert.That(enemy.transform.position, Is.EqualTo(before),
                    "this is the stuck alien: blocked in every direction, it never moves again");
            }
        }

        [Test]
        public void EjectingTrappedAliens_ReturnsThemToOpenSpaceAndTheyMoveAgain()
        {
            using (var fixture = new Fixture())
            {
                var enemy = fixture.SpawnEnemyAt(new GridCoordinate(14, 40), Vector2.right);
                var velocityBefore = enemy.Velocity;
                fixture.CaptureThroughColumn(enemy.LogicalCell.X);
                Assert.That(enemy.IsTrappedInCapturedTerritory(), Is.True);

                Assert.That(fixture.Enemies.EjectTrappedEnemies(), Is.EqualTo(1));

                Assert.That(enemy.IsTrappedInCapturedTerritory(), Is.False, "it is back in open space");
                Assert.That(enemy.IsActiveEnemy, Is.True, "freeing it must not despawn it");
                Assert.That(enemy.Velocity, Is.EqualTo(velocityBefore), "it keeps its heading and speed");

                var before = enemy.transform.position;
                for (var step = 0; step < 30; step++) enemy.AdvanceMovement(.02f);
                Assert.That(enemy.transform.position, Is.Not.EqualTo(before), "and it is moving again");
            }
        }

        [Test]
        public void EjectionLeavesUntrappedAliensWhereTheyAre()
        {
            using (var fixture = new Fixture())
            {
                var free = fixture.SpawnEnemyAt(new GridCoordinate(30, 60), Vector2.right);
                var position = free.transform.position;

                Assert.That(fixture.Enemies.EjectTrappedEnemies(), Is.Zero);
                Assert.That(free.transform.position, Is.EqualTo(position));
            }
        }

        [Test]
        public void CompletingACaptureFreesTheAlienWithoutAnyoneAskingIt()
        {
            using (var fixture = new Fixture())
            {
                fixture.StartPlaying();
                var enemy = fixture.SpawnEnemyAt(new GridCoordinate(14, 40), Vector2.right);

                // No manual call: completing the capture raises CaptureCompleted, which the manager
                // is already subscribed to after Start.
                fixture.CaptureThroughColumn(enemy.LogicalCell.X);

                Assert.That(enemy.IsTrappedInCapturedTerritory(), Is.False,
                    "the capture itself frees the alien, with no extra call needed");
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("TrappedEnemyFixture");
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            private readonly GameObject enemyPrefab;
            private readonly EnemyDefinition enemyDefinition;
            public readonly BoardManager Board;
            public readonly GameManager Game;
            public readonly EnemyManager Enemies;

            public Fixture()
            {
                root.SetActive(false);
                Board = root.AddComponent<BoardManager>();
                Board.Initialize();
                var input = root.AddComponent<InputRouter>();
                var player = Own(new GameObject("Player")).AddComponent<PlayerController>();
                InvokeStatic(player, "Awake");
                Game = root.AddComponent<GameManager>();
                Set(Game, "inputRouter", input); Set(Game, "playerController", player); Set(Game, "boardManager", Board);
                Enemies = root.AddComponent<EnemyManager>();
                Set(Enemies, "boardManager", Board); Set(Enemies, "gameManager", Game);
                Set(Enemies, "playerController", player); Set(Enemies, "poolService", root.AddComponent<PoolService>());
                enemyPrefab = Own(new GameObject("EnemyPrefab"));
                enemyPrefab.AddComponent<BasicBouncer>();
                enemyPrefab.SetActive(false);
                enemyDefinition = Own(ScriptableObject.CreateInstance<EnemyDefinition>());
                enemyDefinition.moveSpeed = 4f;
                enemyDefinition.collisionRadius = .5f;
                InvokeStatic(Game, "Awake");
                root.SetActive(true);
                // The player sits far from the test area so contact never interferes.
                player.transform.position = Board.GetWorldPosition(new GridCoordinate(50, 5));
            }

            /// <summary>
            /// Runs both Start methods. EnemyManager subscribes to the board's capture event in its
            /// own Start, which EditMode never calls for us.
            /// </summary>
            public void StartPlaying()
            {
                InvokeStatic(Game, "Start");
                InvokeStatic(Enemies, "Start");
            }

            public EnemyController SpawnEnemyAt(GridCoordinate cell, Vector2 direction)
            {
                Assert.That(Enemies.Spawn(enemyPrefab, enemyDefinition, cell, direction), Is.True);
                return Enemies.ActiveEnemies[Enemies.ActiveEnemies.Count - 1];
            }

            /// <summary>
            /// Cuts a trail straight through the given column and reconnects, exactly as a player
            /// does. The cells of the committed trail become territory, so anything standing on the
            /// line is enclosed. A cell on one side is reported as occupied so that region stays
            /// open, which is what the freed alien is then moved back into.
            /// </summary>
            public void CaptureThroughColumn(int column)
            {
                var keepOpen = new List<GridCoordinate> { new GridCoordinate(column + 3, 40) };
                for (var row = 1; row < Board.Rows - 1; row++)
                    Board.Model.MoveTo(new GridCoordinate(column, row), keepOpen);
                Board.Model.MoveTo(new GridCoordinate(column, Board.Rows - 1), keepOpen);
            }

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

            private static void InvokeStatic(object target, string name) =>
                target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        }
    }
}
