using NUnit.Framework;
using SpaceXonix.Pooling;
using SpaceXonix.Enemies;
using SpaceXonix.Board;
using System.Reflection;
using UnityEngine;
namespace SpaceXonix.Tests.EditMode
{
    public sealed class PoolServiceTests
    {
        [Test] public void AcquireRelease_ReusesSameInstanceAndTogglesActiveState() { var host=new GameObject("Pool");var prefab=new GameObject("Prefab");prefab.SetActive(false);var pool=host.AddComponent<PoolService>();try { var first=pool.Acquire(prefab,host.transform);Assert.That(first.activeSelf,Is.True);pool.Release(prefab,first);Assert.That(first.activeSelf,Is.False);Assert.That(pool.Acquire(prefab,host.transform),Is.SameAs(first)); } finally { Object.DestroyImmediate(host);Object.DestroyImmediate(prefab); } }
        [Test] public void ReleasingTwice_DoesNotDuplicatePooledInstance() { var host=new GameObject("Pool");var prefab=new GameObject("Prefab");prefab.SetActive(false);var pool=host.AddComponent<PoolService>();try { var first=pool.Acquire(prefab,host.transform);pool.Release(prefab,first);pool.Release(prefab,first);var reused=pool.Acquire(prefab,host.transform);var next=pool.Acquire(prefab,host.transform);Assert.That(reused,Is.SameAs(first));Assert.That(next,Is.Not.SameAs(first)); } finally { Object.DestroyImmediate(host);Object.DestroyImmediate(prefab); } }
        [Test] public void BasicBouncer_SpawnDespawnReuse_ResetsManagerAndOccupancyLifecycle()
        {
            var host=new GameObject("EnemyLifecycle");var prefab=new GameObject("BasicPrefab");prefab.AddComponent<BasicBouncer>();prefab.SetActive(false);var board=host.AddComponent<BoardManager>();board.Initialize();var player=host.AddComponent<SpaceXonix.Player.PlayerController>();player.transform.position=board.GetWorldPosition(new GridCoordinate(0,1));var pool=host.AddComponent<PoolService>();var manager=host.AddComponent<EnemyManager>();var definition=ScriptableObject.CreateInstance<EnemyDefinition>();definition.moveSpeed=2f;Set(manager,"boardManager",board);Set(manager,"playerController",player);Set(manager,"poolService",pool);
            try { var a=new GridCoordinate(3,3);Assert.That(manager.Spawn(prefab,definition,a,Vector2.right),Is.True);var first=manager.ActiveEnemies[0];Assert.That(first.gameObject.activeSelf,Is.True);Assert.That(manager.ActiveEnemies.Count,Is.EqualTo(1));Assert.That(Snapshot(board),Has.Member(a));first.SetMovementSuspended(true);manager.Despawn(first);Assert.That(first.gameObject.activeSelf,Is.False);Assert.That(manager.ActiveEnemies.Count,Is.Zero);Assert.That(Snapshot(board),Has.No.Member(a));var b=new GridCoordinate(5,5);Assert.That(manager.Spawn(prefab,definition,b,Vector2.up),Is.True);var reused=manager.ActiveEnemies[0];Assert.That(reused,Is.SameAs(first));Assert.That(manager.ActiveEnemies.Count,Is.EqualTo(1));Assert.That(reused.LogicalCell,Is.EqualTo(b));Assert.That(reused.MovementEnabled,Is.True);Assert.That(reused.Velocity,Is.EqualTo(Vector2.up*2f));Assert.That(Snapshot(board),Has.Member(b));Assert.That(Snapshot(board),Has.No.Member(a)); } finally { Object.DestroyImmediate(host);Object.DestroyImmediate(prefab);Object.DestroyImmediate(definition); }
        }
        [Test] public void LinearEnemy_Reuse_AppliesNewAxisAndClearsOldOccupancy()
        {
            using (var fixture = new EnemyPoolFixture<LinearEnemy>())
            {
                var horizontal = fixture.Definition(2f); horizontal.linearAxis = EnemyAxis.Horizontal;
                var vertical = fixture.Definition(3f); vertical.linearAxis = EnemyAxis.Vertical;
                var oldCell = new GridCoordinate(6, 6); var newCell = new GridCoordinate(8, 8);
                Assert.That(fixture.Manager.Spawn(fixture.Prefab, horizontal, oldCell, Vector2.left), Is.True);
                var first = fixture.Manager.ActiveEnemies[0]; first.SetMovementSuspended(true); fixture.Manager.Despawn(first);
                Assert.That(fixture.Manager.Spawn(fixture.Prefab, vertical, newCell, Vector2.down), Is.True);
                var reused = fixture.Manager.ActiveEnemies[0];
                Assert.That(reused, Is.SameAs(first));
                Assert.That(reused.Velocity, Is.EqualTo(Vector2.down * 3f));
                Assert.That(reused.MovementEnabled, Is.True);
                Assert.That(fixture.Manager.ActiveEnemies.Count, Is.EqualTo(1));
                Assert.That(Snapshot(fixture.Board), Has.Member(newCell));
                Assert.That(Snapshot(fixture.Board), Has.No.Member(oldCell));
            }
        }
        [Test] public void UnstableEnemy_Reuse_ResetsSpeedTimerPauseAndOccupancy()
        {
            using (var fixture = new EnemyPoolFixture<UnstableEnemy>())
            {
                var firstDefinition = fixture.Definition(2f); firstDefinition.unstableMinSpeed = 1f; firstDefinition.unstableMaxSpeed = 2f; firstDefinition.unstableInterval = .25f;
                var secondDefinition = fixture.Definition(1.25f); secondDefinition.unstableMinSpeed = 1f; secondDefinition.unstableMaxSpeed = 1.5f; secondDefinition.unstableInterval = 4f;
                var oldCell = new GridCoordinate(7, 7); var newCell = new GridCoordinate(9, 9);
                Assert.That(fixture.Manager.Spawn(fixture.Prefab, firstDefinition, oldCell, Vector2.right), Is.True);
                var first = (UnstableEnemy)fixture.Manager.ActiveEnemies[0]; first.SetMovementSuspended(true); fixture.Manager.Despawn(first);
                Assert.That(fixture.Manager.Spawn(fixture.Prefab, secondDefinition, newCell, Vector2.up), Is.True);
                var reused = (UnstableEnemy)fixture.Manager.ActiveEnemies[0];
                Assert.That(reused, Is.SameAs(first));
                Assert.That(reused.CurrentSpeed, Is.EqualTo(1.25f).Within(.0001f));
                Assert.That(reused.SpeedChangeTimeRemaining, Is.EqualTo(4f).Within(.0001f));
                Assert.That(reused.Velocity, Is.EqualTo(Vector2.up * 1.25f));
                Assert.That(reused.MovementEnabled, Is.True);
                Assert.That(fixture.Manager.ActiveEnemies.Count, Is.EqualTo(1));
                Assert.That(Snapshot(fixture.Board), Has.Member(newCell));
                Assert.That(Snapshot(fixture.Board), Has.No.Member(oldCell));
            }
        }
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
        private static System.Collections.Generic.List<GridCoordinate> Snapshot(BoardManager board)=>(System.Collections.Generic.List<GridCoordinate>)typeof(BoardManager).GetField("enemySnapshot",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(board);
        private sealed class EnemyPoolFixture<T> : System.IDisposable where T : EnemyController
        {
            private readonly GameObject host = new GameObject(typeof(T).Name + "PoolTest");
            public readonly GameObject Prefab = new GameObject(typeof(T).Name + "Prefab");
            public readonly BoardManager Board;
            public readonly EnemyManager Manager;
            private readonly System.Collections.Generic.List<EnemyDefinition> definitions = new System.Collections.Generic.List<EnemyDefinition>();
            public EnemyPoolFixture()
            {
                Prefab.AddComponent<T>(); Prefab.SetActive(false);
                Board = host.AddComponent<BoardManager>(); Board.Initialize();
                var player = host.AddComponent<SpaceXonix.Player.PlayerController>(); player.transform.position = Board.GetWorldPosition(new GridCoordinate(0, 1));
                var pool = host.AddComponent<PoolService>(); Manager = host.AddComponent<EnemyManager>();
                Set(Manager, "boardManager", Board); Set(Manager, "playerController", player); Set(Manager, "poolService", pool);
            }
            public EnemyDefinition Definition(float speed) { var value = ScriptableObject.CreateInstance<EnemyDefinition>(); value.moveSpeed = speed; definitions.Add(value); return value; }
            public void Dispose() { Object.DestroyImmediate(host); Object.DestroyImmediate(Prefab); foreach (var definition in definitions) Object.DestroyImmediate(definition); }
        }
    }
}
