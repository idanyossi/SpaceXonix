using UnityEngine;

namespace SpaceXonix.UI
{
    /// <summary>
    /// The Boot scene's only job: let the persistent services wake up, then route to the Main Menu.
    /// Runs after them so <see cref="Settings.GameSettings"/> has already loaded by the time the menu
    /// asks for the high score.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class BootLoader : MonoBehaviour
    {
        [Tooltip("Seconds to hold on the boot screen before routing. 0 routes on the first frame.")]
        [SerializeField, Min(0f)] private float holdSeconds;

        private float elapsed;
        private bool routed;

        private void Update()
        {
            if (routed) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < holdSeconds) return;
            routed = true;
            SceneRouter.GoToMainMenu();
        }
    }
}
