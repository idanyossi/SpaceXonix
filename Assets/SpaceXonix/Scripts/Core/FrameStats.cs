using UnityEngine;

namespace SpaceXonix.Core
{
    /// <summary>
    /// A frame-time probe for checking smoothness on a real phone. Only compiled into builds with the
    /// SPACEXONIX_FRAMESTATS scripting define: every few seconds it logs the average frame rate, the
    /// worst frame, how many frames missed the target, and how many garbage collections ran, which
    /// is enough to tell collection pauses from one-off hitches from plain slow frames. Read it with
    /// "adb logcat -s Unity".
    /// </summary>
    public sealed class FrameStats : MonoBehaviour
    {
#if SPACEXONIX_FRAMESTATS
        private const float ReportSeconds = 5f;

        private float elapsed;
        private int frames;
        private float worst;
        private int slow;
        private int hitches;
        private int collectionsAtStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("FrameStats");
            DontDestroyOnLoad(go);
            go.AddComponent<FrameStats>();
        }

        private void OnEnable() => collectionsAtStart = System.GC.CollectionCount(0);

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            elapsed += dt;
            frames++;
            if (dt > worst) worst = dt;
            // At 60 fps a frame has 16.7 ms: one over 20 ms is a visible stutter, over 50 ms a hitch.
            if (dt > .02f) slow++;
            if (dt > .05f) hitches++;
            if (elapsed < ReportSeconds) return;
            var collections = System.GC.CollectionCount(0) - collectionsAtStart;
            Debug.Log($"[FrameStats] {frames / elapsed:0.0} fps, worst {worst * 1000f:0} ms, over 20 ms: {slow}, over 50 ms: {hitches}, " +
                      $"GC runs: {collections}, heap {System.GC.GetTotalMemory(false) / 1024} KB, scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            elapsed = 0f;
            frames = 0;
            worst = 0f;
            slow = 0;
            hitches = 0;
            collectionsAtStart = System.GC.CollectionCount(0);
        }
#endif
    }
}
