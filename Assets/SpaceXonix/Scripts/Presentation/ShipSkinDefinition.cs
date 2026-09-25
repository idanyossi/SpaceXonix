using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// One look for the player's ship: its animation frames, drawn nose-up, and a display name.
    /// Skins are cosmetic only; the ship's size and hitbox never change with them.
    /// </summary>
    [CreateAssetMenu(menuName = "SpaceXonix/Ship Skin")]
    public sealed class ShipSkinDefinition : ScriptableObject
    {
        [Tooltip("Saved in the settings, so it must never change once a skin has shipped.")]
        public string id = "skin";
        public string displayName = "Ship";
        [Tooltip("Nose-up frames, looped. The first one doubles as the preview and the life icon.")]
        public Sprite[] frames = new Sprite[0];
        [Min(.1f)] public float framesPerSecond = 12f;

        public Sprite Preview => frames != null && frames.Length > 0 ? frames[0] : null;
    }
}
