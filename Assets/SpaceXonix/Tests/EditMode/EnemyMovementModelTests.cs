using NUnit.Framework;
using SpaceXonix.Enemies;
using UnityEngine;
namespace SpaceXonix.Tests.EditMode
{
    public sealed class EnemyMovementModelTests
    {
        [Test] public void Bouncer_ReflectsFromHorizontalBlock() { var m=new EnemyMovementModel(Vector2.zero,new Vector2(1,0)); m.Advance(1f,p=>p.x<.5f); Assert.That(m.Velocity.x,Is.LessThan(0)); }
        [Test] public void Bouncer_ReflectsFromVerticalBlock() { var m=new EnemyMovementModel(Vector2.zero,new Vector2(0,1)); m.Advance(1f,p=>p.y<.5f); Assert.That(m.Velocity.y,Is.LessThan(0)); }
        [Test] public void Bouncer_DoesNotEnterBlockedSpace() { var m=new EnemyMovementModel(Vector2.zero,Vector2.right); m.Advance(1f,p=>p.x<.5f); Assert.That(m.Position,Is.EqualTo(Vector2.zero)); }
        [Test] public void Pause_PreventsAdvanceAndResumePreservesDirection() { var m=new EnemyMovementModel(Vector2.zero,Vector2.right); m.MovementEnabled=false;m.Advance(1f,p=>true);Assert.That(m.Position,Is.EqualTo(Vector2.zero));m.MovementEnabled=true;m.Advance(1f,p=>true);Assert.That(m.Position,Is.EqualTo(Vector2.right)); }
        [Test] public void SpeedChange_PreservesDirection() { var m=new EnemyMovementModel(Vector2.zero,new Vector2(1,1));m.SetSpeed(3f);Assert.That(m.Velocity.magnitude,Is.EqualTo(3f).Within(.001f));Assert.That(m.Velocity.x,Is.EqualTo(m.Velocity.y).Within(.001f)); }
        [Test] public void BoardCapture_EnemyOccupiedRegionStaysUncaptured() { var b=new SpaceXonix.Board.BoardModel(8,8);for(var x=1;x<7;x++)b.MoveTo(new SpaceXonix.Board.GridCoordinate(x,3));b.MoveTo(new SpaceXonix.Board.GridCoordinate(7,3),new[]{new SpaceXonix.Board.GridCoordinate(2,1)});Assert.That(b.GetCell(new SpaceXonix.Board.GridCoordinate(2,1)),Is.EqualTo(SpaceXonix.Board.BoardCellState.Uncaptured)); }
        [Test] public void LinearHorizontal_HasNoVerticalVelocity() { var m=new EnemyMovementModel(Vector2.zero,Vector2.right);m.Advance(.5f,p=>true);Assert.That(m.Position.y,Is.EqualTo(0f)); }
        [Test] public void LinearVertical_HasNoHorizontalVelocity() { var m=new EnemyMovementModel(Vector2.zero,Vector2.up);m.Advance(.5f,p=>true);Assert.That(m.Position.x,Is.EqualTo(0f)); }
        [Test] public void Linear_ReversesWhenBlocked() { var m=new EnemyMovementModel(Vector2.zero,Vector2.right);m.Advance(1f,p=>false);Assert.That(m.Velocity.x,Is.LessThan(0f)); }
        [Test] public void ConfiguredSpawns_AreUncapturedInteriorCells() { var b=new SpaceXonix.Board.BoardModel(54,96);foreach(var c in new[]{new SpaceXonix.Board.GridCoordinate(15,20),new SpaceXonix.Board.GridCoordinate(30,45),new SpaceXonix.Board.GridCoordinate(40,70)}) { Assert.That(b.IsInBounds(c),Is.True);Assert.That(b.GetCell(c),Is.EqualTo(SpaceXonix.Board.BoardCellState.Uncaptured)); } }
        [Test] public void AlienFreeRegion_CapturesNormally() { var b=new SpaceXonix.Board.BoardModel(8,8);for(var x=1;x<7;x++)b.MoveTo(new SpaceXonix.Board.GridCoordinate(x,3));b.MoveTo(new SpaceXonix.Board.GridCoordinate(7,3));Assert.That(b.GetCell(new SpaceXonix.Board.GridCoordinate(2,1)),Is.EqualTo(SpaceXonix.Board.BoardCellState.Captured)); }
    }
}
