using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// Fades to black between scenes and back in on the other side, instead of cutting. It builds its
    /// own overlay canvas the first time it is needed and survives scene loads, so no scene has to
    /// carry it. Runs on unscaled time, because scenes are often left from a paused game.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        private const float FadeOutSeconds = .22f;
        private const float FadeInSeconds = .3f;

        private static ScreenFader instance;
        private CanvasGroup group;
        private bool busy;

        public static bool IsFading => instance != null && instance.busy;

        /// <summary>Fades out, loads the scene, and fades back in. Outside play mode it just loads.</summary>
        public static void LoadScene(string sceneName)
        {
            if (!Application.isPlaying)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }
            var fader = Instance();
            // A second request while fading is ignored, so a double tap cannot load twice.
            if (fader.busy) return;
            fader.StartCoroutine(fader.Fade(sceneName));
        }

        private static ScreenFader Instance()
        {
            if (instance != null) return instance;
            var go = new GameObject("ScreenFader", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above every other canvas, including the pause and campaign screens.
            canvas.sortingOrder = 1000;
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var black = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
            black.transform.SetParent(go.transform, false);
            black.color = new Color(.02f, .02f, .06f, 1f);
            var rect = black.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            instance = go.AddComponent<ScreenFader>();
            instance.group = group;
            return instance;
        }

        private IEnumerator Fade(string sceneName)
        {
            busy = true;
            // Swallow taps while the screen is dark, so nothing underneath can be clicked mid-fade.
            group.blocksRaycasts = true;
            yield return FadeTo(1f, FadeOutSeconds);
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(sceneName);
            yield return FadeTo(0f, FadeInSeconds);
            group.blocksRaycasts = false;
            busy = false;
        }

        private IEnumerator FadeTo(float target, float seconds)
        {
            var start = group.alpha;
            for (var t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(start, target, t / seconds);
                yield return null;
            }
            group.alpha = target;
        }
    }
}
