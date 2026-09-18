using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>A flat blast ring that lies on the board floor and fades out. Presentation only.</summary>
    public sealed class ExplosionRing : MonoBehaviour
    {
        [SerializeField] private Renderer ringRenderer;
        [SerializeField, Min(.01f)] private float durationSeconds = .45f;
        [SerializeField, Min(0f)] private float floorLift = .03f;
        [SerializeField, Range(0f, 1f)] private float startAlpha = .75f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;
        private float elapsed;
        private Color color = new Color(1f, .55f, .1f);

        public bool IsFinished => elapsed >= durationSeconds;
        public float Duration => durationSeconds;

        public void Play(Vector3 boardPosition, float radius)
        {
            if (ringRenderer == null) ringRenderer = GetComponentInChildren<Renderer>();
            if (ringRenderer != null) color = ringRenderer.sharedMaterial != null && ringRenderer.sharedMaterial.HasProperty(BaseColor)
                ? ringRenderer.sharedMaterial.GetColor(BaseColor) : color;
            elapsed = 0f;
            transform.position = new Vector3(boardPosition.x, boardPosition.y, -floorLift);
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(radius * 2f, radius * 2f, .02f);
            ApplyAlpha(startAlpha);
        }

        /// <summary>Returns false once the ring has finished and should be released.</summary>
        public bool Tick(float deltaTime)
        {
            elapsed += Mathf.Max(0f, deltaTime);
            var progress = Mathf.Clamp01(elapsed / durationSeconds);
            ApplyAlpha(Mathf.Lerp(startAlpha, 0f, progress));
            transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y, .02f);
            return !IsFinished;
        }

        private void ApplyAlpha(float alpha)
        {
            if (ringRenderer == null) return;
            block ??= new MaterialPropertyBlock();
            ringRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, new Color(color.r, color.g, color.b, alpha));
            ringRenderer.SetPropertyBlock(block);
        }
    }
}
