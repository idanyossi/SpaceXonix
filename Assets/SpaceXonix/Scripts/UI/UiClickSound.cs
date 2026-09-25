using System.Collections.Generic;
using SpaceXonix.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// Plays the UI click for every button and toggle under this object, so a menu gives feedback
    /// without each screen wiring its own sound. It sits on each root canvas. The on-screen gameplay
    /// buttons are skipped, because the power shot and abilities already have their own sounds.
    /// </summary>
    public sealed class UiClickSound : MonoBehaviour
    {
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<Toggle> toggles = new List<Toggle>();

        public int HookedCount => buttons.Count + toggles.Count;

        private void Awake()
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (IsGameplayControl(button)) continue;
                button.onClick.AddListener(Click);
                buttons.Add(button);
            }
            foreach (var toggle in GetComponentsInChildren<Toggle>(true))
            {
                if (IsGameplayControl(toggle)) continue;
                toggle.onValueChanged.AddListener(OnToggled);
                toggles.Add(toggle);
            }
        }

        private void OnDestroy()
        {
            foreach (var button in buttons) if (button != null) button.onClick.RemoveListener(Click);
            foreach (var toggle in toggles) if (toggle != null) toggle.onValueChanged.RemoveListener(OnToggled);
            buttons.Clear();
            toggles.Clear();
        }

        private static bool IsGameplayControl(Selectable control)
        {
            var touch = control.GetComponentInParent<TouchControls>(true);
            return touch != null && touch.Owns(control);
        }

        private static void OnToggled(bool _) => Click();

        private static void Click()
        {
            var manager = AudioManager.Instance;
            if (manager != null) manager.Play(GameSfx.UiInteraction);
        }
    }
}
