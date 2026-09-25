using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>Loops sprite frames on a UI image, on unscaled time, for animated previews in menus.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class UiSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames = new Sprite[0];
        [SerializeField, Min(.1f)] private float framesPerSecond = 12f;

        private Image image;
        private float elapsed;

        public int CurrentFrame { get; private set; }

        private void Awake() => image = GetComponent<Image>();

        public void SetFrames(Sprite[] value, float fps)
        {
            frames = value ?? new Sprite[0];
            framesPerSecond = Mathf.Max(.1f, fps);
            CurrentFrame = 0;
            elapsed = 0f;
            Show();
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        public void Advance(float deltaTime)
        {
            if (frames.Length < 2 || deltaTime <= 0f) return;
            elapsed += deltaTime;
            var step = (int)(elapsed * framesPerSecond);
            if (step == 0) return;
            elapsed -= step / framesPerSecond;
            CurrentFrame = (CurrentFrame + step) % frames.Length;
            Show();
        }

        private void Show()
        {
            if (image == null) image = GetComponent<Image>();
            if (image != null && frames.Length > 0) image.sprite = frames[CurrentFrame];
        }
    }
}
