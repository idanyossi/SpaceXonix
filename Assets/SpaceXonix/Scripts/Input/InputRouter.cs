using System;
using SpaceXonix.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceXonix.Input
{
    public sealed class InputRouter : MonoBehaviour
    {
        public event Action<CardinalDirection> DirectionChanged;

        public bool GameplayInputEnabled { get; private set; }
        public CardinalDirection CurrentDirection { get; private set; } = CardinalDirection.Right;

        private void Update()
        {
            if (!GameplayInputEnabled || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                TrySelectDirection(CardinalDirection.Up);
            }
            else if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                TrySelectDirection(CardinalDirection.Down);
            }
            else if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                TrySelectDirection(CardinalDirection.Left);
            }
            else if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                TrySelectDirection(CardinalDirection.Right);
            }
        }

        public void SetGameplayInputEnabled(bool enabled)
        {
            GameplayInputEnabled = enabled;
        }

        public void ResetDirection(CardinalDirection direction)
        {
            CurrentDirection = direction;
        }

        public bool TrySelectDirection(CardinalDirection direction)
        {
            if (!GameplayInputEnabled)
            {
                return false;
            }

            CurrentDirection = direction;
            DirectionChanged?.Invoke(direction);
            return true;
        }
    }
}
