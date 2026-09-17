using System;
using SpaceXonix.Core;
using SpaceXonix.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceXonix.Input
{
    public sealed class InputRouter : MonoBehaviour
    {
        public event Action<CardinalDirection> DirectionChanged;
        public event Action DirectionReleased;
        public event Action PowerShotRequested;

        public bool GameplayInputEnabled { get; private set; }
        public CardinalDirection CurrentDirection { get; private set; } = CardinalDirection.Right;
        public bool IsDirectionHeld { get; private set; }

        private void Update()
        {
            if (!GameplayInputEnabled || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TraceRawInput("Space");
                RequestPowerShot();
            }

            if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                TraceRawInput(Keyboard.current.wKey.wasPressedThisFrame ? "W" : "UpArrow");
                TrySelectDirection(CardinalDirection.Up);
            }
            else if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                TraceRawInput(Keyboard.current.sKey.wasPressedThisFrame ? "S" : "DownArrow");
                TrySelectDirection(CardinalDirection.Down);
            }
            else if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                TraceRawInput(Keyboard.current.aKey.wasPressedThisFrame ? "A" : "LeftArrow");
                TrySelectDirection(CardinalDirection.Left);
            }
            else if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                TraceRawInput(Keyboard.current.dKey.wasPressedThisFrame ? "D" : "RightArrow");
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
            if (GameManager.Instance != null)
                GameManager.Instance.TracePlayerLifecycle($"InputDirectionAccepted:{direction}", force: true);
            DirectionChanged?.Invoke(direction);
            return true;
        }

        public bool RequestPowerShot()
        {
            if (!GameplayInputEnabled) return false;
            PowerShotRequested?.Invoke();
            return true;
        }

        public void ReleaseDirection()
        {
            if (!IsDirectionHeld) return;
            IsDirectionHeld = false;
            if (GameManager.Instance != null)
                GameManager.Instance.TracePlayerLifecycle($"InputDirectionReleased:{CurrentDirection}", force: true);
            DirectionReleased?.Invoke();
        }

        private static void TraceRawInput(string key)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TracePlayerLifecycle($"RawInputDetected:{key}", force: true);
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
