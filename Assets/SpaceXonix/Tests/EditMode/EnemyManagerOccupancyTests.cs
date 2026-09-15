using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Enemies;
using UnityEngine;
namespace SpaceXonix.Tests.EditMode
{
    public sealed class EnemyManagerOccupancyTests
    {
        [Test] public void Register_DeduplicatesAndReportsOccupancy() { using(var f=new Fixture()) { f.manager.Register(f.enemy);f.manager.Register(f.enemy);Assert.That(f.manager.ActiveEnemies.Count,Is.EqualTo(1));Assert.That(f.board.Model.GetCell(f.enemy.LogicalCell),Is.EqualTo(BoardCellState.Uncaptured)); } }
        [Test] public void MovingEnemy_ReplacesPreviousOccupancy() { using(var f=new Fixture()) { f.manager.Register(f.enemy);var old=f.enemy.LogicalCell;f.enemy.transform.position=f.board.GetWorldPosition(new GridCoordinate(4,4));f.manager.RefreshOccupancy();Assert.That(f.enemy.LogicalCell,Is.EqualTo(new GridCoordinate(4,4)));Assert.That(old,Is.Not.EqualTo(f.enemy.LogicalCell)); } }
        [Test] public void Unregister_RemovesActiveEnemy() { using(var f=new Fixture()) { f.manager.Register(f.enemy);f.manager.Unregister(f.enemy);Assert.That(f.manager.ActiveEnemies.Count,Is.Zero); } }
        [Test] public void InactiveEnemy_IsNotRegistered() { using(var f=new Fixture()) { f.enemy.Deactivate();f.manager.Register(f.enemy);Assert.That(f.manager.ActiveEnemies.Count,Is.Zero); } }
        private sealed class Fixture : System.IDisposable
        {
            public readonly GameObject root=new GameObject("EnemyManagerTest"); public readonly BoardManager board; public readonly EnemyManager manager; public readonly EnemyController enemy;
            public Fixture() { board=root.AddComponent<BoardManager>();board.Initialize();manager=root.AddComponent<EnemyManager>();typeof(EnemyManager).GetField("boardManager",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(manager,board);var e=new GameObject("Enemy");enemy=e.AddComponent<BasicBouncer>();var d=ScriptableObject.CreateInstance<EnemyDefinition>();enemy.Activate(d,board,null,board.GetWorldPosition(new GridCoordinate(3,3)),Vector2.right); }
            public void Dispose() { Object.DestroyImmediate(root);Object.DestroyImmediate(enemy.gameObject); }
        }
    }
}
