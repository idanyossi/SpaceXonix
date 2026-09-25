using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Dresses the player's ship in the skin chosen in the menu. The ship keeps its exact width,
    /// because every visual here is sized to its hitbox: a skin changes how the ship looks, never
    /// how big it is to the aliens.
    /// </summary>
    public sealed class PlayerShipSkin : MonoBehaviour
    {
        [SerializeField] private ShipSkinLibrary library;
        [SerializeField] private ActorVisual actorVisual;

        private float authoredWidth = -1f;

        public ShipSkinDefinition Current { get; private set; }

        /// <summary>The flown ship's stats, or neutral ones when no skin is set.</summary>
        public ShipStats Stats => Current != null && Current.stats != null ? Current.stats : ShipStats.Neutral;

        private void Awake() => Apply(library != null ? library.Selected : null);

        /// <summary>Wears a skin. Public so tests and the editor can show any of them.</summary>
        public void Apply(ShipSkinDefinition skin)
        {
            if (skin == null) return;
            // The ship's stats count even with nothing to draw it on.
            Current = skin;
            if (skin.Preview == null || actorVisual == null || actorVisual.Visual == null) return;
            var visual = actorVisual.Visual;
            // Measure once, from the ship as authored, so repeated swaps cannot drift the size.
            if (authoredWidth < 0f) authoredWidth = actorVisual.VisualWidth();
            var animator = visual.GetComponent<SpriteFrameAnimator>();
            if (animator != null) animator.SetFrames(skin.frames, skin.framesPerSecond);
            else
            {
                var renderer = visual.GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.sprite = skin.Preview;
            }
            var spriteWidth = skin.Preview.bounds.size.x;
            if (spriteWidth > 0f)
            {
                var scale = authoredWidth / spriteWidth;
                visual.localScale = new Vector3(scale, scale, visual.localScale.z);
            }
        }
    }
}
