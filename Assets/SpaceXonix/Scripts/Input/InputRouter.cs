using System;
using SpaceXonix.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceXonix.Input
{
    public sealed class InputRouter : MonoBehaviour
    {
        public event Action<CardinalDirection> DirectionChanged;
        public event Action DirectionReleased;

        public bool GameplayInputEnabled { get; private set; }
        public CardinalDirection CurrentDirection { get; private set; } = CardinalDirection.Right;
        public bool IsDirectionHeld { get; private set; }

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
            else if (IsDirectionHeld && IsPressed(CurrentDirection))
            {
                return;
            }
            else if (TrySelectPressedDirection())
            {
                return;
            }
            else
            {
                ReleaseDirection();
            }
        }

        public void SetGameplayInputEnabled(bool enabled)
        {
            GameplayInputEnabled = enabled;
            if (!enabled) IsDirectionHeld = false;
        }

        public void ResetDirection(CardinalDirection direction)
        {
            CurrentDirection = direction;
            IsDirectionHeld = false;
        }

        public bool TrySelectDirection(CardinalDirection direction)
        {
            if (!GameplayInputEnabled)
            {
                return false;
            }

            CurrentDirection = direction;
            IsDirectionHeld = true;
            DirectionChanged?.Invoke(direction);
            return true;
        }

        public void ReleaseDirection()
        {
            if (!IsDirectionHeld) return;
            IsDirectionHeld = false;
            DirectionReleased?.Invoke();
        }

        private bool TrySelectPressedDirection()
        {
            if (IsPressed(CardinalDirection.Up)) return TrySelectDirection(CardinalDirection.Up);
            if (IsPressed(CardinalDirection.Down)) return TrySelectDirection(CardinalDirection.Down);
            if (IsPressed(CardinalDirection.Left)) return TrySelectDirection(CardinalDirection.Left);
            return IsPressed(CardinalDirection.Right) && TrySelectDirection(CardinalDirection.Right);
        }

        private static bool IsPressed(CardinalDirection direction)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            switch (direction)
            {
                case CardinalDirection.Up:
                    return keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
                case CardinalDirection.Down:
                    return keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
                case CardinalDirection.Left:
                    return keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
                default:
                    return keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
            }
        }
    }
}
