using SpaceXonix.Input;
using SpaceXonix.Power;
using SpaceXonix.PowerUps;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// The on-screen Ability and Power Shot buttons. They route through <see cref="InputRouter"/>,
    /// so a tap and a key press are the same request as far as the game is concerned. Hidden on
    /// desktop, where the keyboard covers both, unless forced on for testing in the editor.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        [SerializeField] private InputRouter inputRouter;
        [SerializeField] private PowerMeter powerMeter;
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private Button abilityButton;
        [SerializeField] private Button powerButton;
        [Tooltip("Show the touch buttons even on desktop, for trying them in the editor.")]
        [SerializeField] private bool forceVisible;

        public bool ButtonsVisible { get; private set; }

        /// <summary>Whether a control is one of the on-screen gameplay buttons this owns.</summary>
        public bool Owns(Selectable control) => control != null && (control == abilityButton || control == powerButton);

        private void OnEnable()
        {
            if (abilityButton != null) abilityButton.onClick.AddListener(UseAbility);
            if (powerButton != null) powerButton.onClick.AddListener(FirePowerShot);
            ApplyVisibility(forceVisible || TouchIsAvailable());
        }

        private void OnDisable()
        {
            if (abilityButton != null) abilityButton.onClick.RemoveListener(UseAbility);
            if (powerButton != null) powerButton.onClick.RemoveListener(FirePowerShot);
        }

        private void Update()
        {
            if (!ButtonsVisible) return;
            // A button that cannot do anything is dimmed rather than hidden, so its place is stable.
            if (abilityButton != null) abilityButton.interactable = powerUpManager != null && powerUpManager.StoredPowerUp.HasValue;
            if (powerButton != null) powerButton.interactable = powerMeter != null && powerMeter.IsReady;
        }

        /// <summary>
        /// True when the player can actually tap. Tested by the presence of a touchscreen rather
        /// than by platform, so the buttons also appear in the Device Simulator, which is where
        /// mobile gets tried long before there is an APK on a phone.
        /// </summary>
        public static bool TouchIsAvailable() =>
            Application.isMobilePlatform || UnityEngine.InputSystem.Touchscreen.current != null;

        public void UseAbility()
        {
            if (inputRouter != null) inputRouter.RequestAbility();
        }

        public void FirePowerShot()
        {
            if (inputRouter != null) inputRouter.RequestPowerShot();
        }

        /// <summary>Shows or hides the touch buttons. Public so tests can drive both cases.</summary>
        public void ApplyVisibility(bool visible)
        {
            ButtonsVisible = visible;
            if (abilityButton != null) abilityButton.gameObject.SetActive(visible);
            if (powerButton != null) powerButton.gameObject.SetActive(visible);
        }
    }
}
