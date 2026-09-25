using SpaceXonix.Settings;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>Every ship skin, in menu order. The first is the default.</summary>
    [CreateAssetMenu(menuName = "SpaceXonix/Ship Skin Library")]
    public sealed class ShipSkinLibrary : ScriptableObject
    {
        public ShipSkinDefinition[] skins = new ShipSkinDefinition[0];

        public ShipSkinDefinition Default => skins != null && skins.Length > 0 ? skins[0] : null;

        /// <summary>The skin with this id, or the default when it is unknown or empty.</summary>
        public ShipSkinDefinition Find(string id)
        {
            if (!string.IsNullOrEmpty(id) && skins != null)
                foreach (var skin in skins)
                    if (skin != null && skin.id == id) return skin;
            return Default;
        }

        /// <summary>The skin the player picked, read from the settings.</summary>
        public ShipSkinDefinition Selected => Find(GameSettings.Current?.ShipSkin);
    }
}
