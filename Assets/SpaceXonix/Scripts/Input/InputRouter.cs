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
        public event Action AbilityRequested;
        public event Action PauseToggleRequested;

        public bool GameplayInputEnabled { get; private set; }
        /// <summary>
        /// True while the heading came from a swipe. A finger cannot hold a direction the way a
        /// key can, so a swipe latches until the next swipe instead of being released the moment
        /// the keyboard poll finds nothing pressed.
        /// </summary>
        public bool IsDirectionLatched { get; private set; }
        public CardinalDirection CurrentDirection { get; private set; } = CardinalDirection.Right;
        public bool IsDirectionHeld { get; private set; }

        private void Update()
        {
            // Pause is checked before the gameplay gate, because a paused game has gameplay input
            // disabled and Escape still has to bring the player back out of the pause menu.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TraceRawInput("Escape");
                RequestPauseToggle();
            }

            if (!GameplayInputEnabled || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TraceRawInput("Space");
                RequestPowerShot();
            }

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                TraceRawInput("E");
                RequestAbility();
            }

            if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                TraceRawInput(Keyboard.current.wKey.wasPressedThisFrame ? "W" : "UpArrow");
                IsDirectionLatched = false;
                TrySelectDirection(CardinalDirection.Up);
            }
            else if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                TraceRawInput(Keyboard.current.sKey.wasPressedThisFrame ? "S" : "DownArrow");
                IsDirectionLatched = false;
                TrySelectDirection(CardinalDirection.Down);
            }
            else if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                TraceRawInput(Keyboard.current.aKey.wasPressedThisFrame ? "A" : "LeftArrow");
                IsDirectionLatched = false;
                TrySelectDirection(CardinalDirection.Left);
            }
            else if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                TraceRawInput(Keyboard.current.dKey.wasPressedThisFrame ? "D" : "RightArrow");
                IsDirectionLatched = false;
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
            else if (!IsDirectionLatched)
            {
                ReleaseDirection();
            }
        }

        public void SetGameplayInputEnabled(bool enabled)
        {
            GameplayInputEnabled = enabled;
            if (enabled) return;
            IsDirectionHeld = false;
            IsDirectionLatched = false;
        }

        public void ResetDirection(CardinalDirection direction)
        {
            CurrentDirection = direction;
            IsDirectionHeld = false;
            IsDirectionLatched = false;
        }

        /// <summary>
        /// Steers from a swipe. The heading is latched, because the player cannot keep a finger
        /// pressed the way they hold a key, so it persists until the next swipe or a key press.
        /// </summary>
        public bool TrySelectLatchedDirection(CardinalDirection direction)
        {
            if (!TrySelectDirection(direction)) return false;
            IsDirectionLatched = true;
            return true;
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

        public bool RequestAbility()
        {
            if (!GameplayInputEnabled) return false;
            AbilityRequested?.Invoke();
            return true;
        }

        /// <summary>
        /// Asks for the pause menu to open or close. Unlike the gameplay actions this is never gated,
        /// because pausing is exactly what disables gameplay input.
        /// </summary>
        public void RequestPauseToggle() => PauseToggleRequested?.Invoke();

        public void ReleaseDirection()
        {
            if (!IsDirectionHeld) return;
            IsDirectionHeld = false;
            IsDirectionLatched = false;
            if (GameManager.Instance != null)
                GameManager.Instance.TracePlayerLifecycle($"InputDirectionReleased:{CurrentDirection}", force: true);
            DirectionReleased?.Invoke();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
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
