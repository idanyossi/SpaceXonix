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
        [Tooltip("Frame rate asked for on phones. Android runs at 30 unless told otherwise.")]
        [SerializeField, Min(30)] private int mobileFrameRate = 60;

        private float elapsed;
        private bool routed;

        private void Awake()
        {
            if (Application.isMobilePlatform) Application.targetFrameRate = mobileFrameRate;
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // A stack trace on every log line is expensive on a phone and nobody reads it in a release build.
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
#endif
        }

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
