using NUnit.Framework;
using SpaceXonix.Input;
using SpaceXonix.Player;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class PlayerMovementModelTests
    {
        [TestCase(CardinalDirection.Up, 0f, 5f)]
        [TestCase(CardinalDirection.Down, 0f, -5f)]
        [TestCase(CardinalDirection.Left, -5f, 0f)]
        [TestCase(CardinalDirection.Right, 5f, 0f)]
        public void Advance_MovesOnOnlyTheSelectedAxis(CardinalDirection direction, float expectedX, float expectedY)
        {
            var movement = new PlayerMovementModel(Vector2.zero, 5f, direction);

            var position = movement.Advance(1f);

            Assert.That(position.x, Is.EqualTo(expectedX));
            Assert.That(position.y, Is.EqualTo(expectedY));
        }

        [Test]
        public void SetDirection_ReplacesVelocityInsteadOfCombiningAxes()
        {
            var movement = new PlayerMovementModel(Vector2.zero, 5f, CardinalDirection.Right);
            movement.Advance(1f);

            movement.SetDirection(CardinalDirection.Up);
            var position = movement.Advance(1f);

            Assert.That(position, Is.EqualTo(new Vector2(5f, 5f)));
        }

        [Test]
        public void Advance_UsesConfiguredMoveSpeed()
        {
            var movement = new PlayerMovementModel(Vector2.zero, 3.5f, CardinalDirection.Down);

            var position = movement.Advance(2f);

            Assert.That(position, Is.EqualTo(new Vector2(0f, -7f)));
        }

        [Test]
        public void DisabledInputRouter_RejectsNewDirectionCommands()
        {
            var gameObject = new GameObject("InputRouterTest");
            var router = gameObject.AddComponent<InputRouter>();

            try
            {
                Assert.That(router.TrySelectDirection(CardinalDirection.Up), Is.True);
                router.SetGameplayInputEnabled(false);

                Assert.That(router.TrySelectDirection(CardinalDirection.Left), Is.False);
                Assert.That(router.CurrentDirection, Is.EqualTo(CardinalDirection.Up));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
