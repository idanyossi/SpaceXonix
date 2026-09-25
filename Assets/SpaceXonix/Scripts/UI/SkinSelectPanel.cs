using System;
using SpaceXonix.Presentation;
using SpaceXonix.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// The hangar: a grid of every ship skin, each tile showing the ship itself, animated, with its
    /// name. Tapping a tile equips it at once and saves the choice; the equipped tile is lit and
    /// marked. The tiles are laid out by the editor builder, one per skin in the library.
    /// </summary>
    public sealed class SkinSelectPanel : MonoBehaviour
    {
        [Serializable]
        public sealed class Tile
        {
            public Button button;
            public Image frame;
            public UiSpriteAnimator preview;
            public Text nameLabel;
            public GameObject equippedBadge;
        }

        [SerializeField] private GameObject root;
        [SerializeField] private ShipSkinLibrary library;
        [SerializeField] private Tile[] tiles = new Tile[0];
        [SerializeField] private Sprite tileFrame;
        [SerializeField] private Sprite equippedFrame;

        private GameSettingsModel settings;

        public bool IsShown => root != null && root.activeSelf;
        public int TileCount => tiles.Length;
        public string EquippedId => library != null ? library.Find(settings?.ShipSkin)?.id : null;

        private void Awake()
        {
            for (var i = 0; i < tiles.Length; i++)
            {
                if (tiles[i]?.button == null) continue;
                var index = i; // captured per tile, so each one equips its own skin
                tiles[i].button.onClick.AddListener(() => Equip(index));
            }
        }

        private void OnDestroy()
        {
            foreach (var tile in tiles) if (tile?.button != null) tile.button.onClick.RemoveAllListeners();
        }

        /// <summary>Opens the hangar. The settings model is passed in so tests need no live service.</summary>
        public void Show(GameSettingsModel model = null)
        {
            settings = model ?? GameSettings.Current;
            for (var i = 0; i < tiles.Length; i++)
            {
                var skin = SkinAt(i);
                if (tiles[i] == null || skin == null) continue;
                if (tiles[i].preview != null) tiles[i].preview.SetFrames(skin.frames, skin.framesPerSecond);
                if (tiles[i].nameLabel != null) tiles[i].nameLabel.text = skin.displayName.ToUpperInvariant();
            }
            RefreshEquipped();
            if (root != null) root.SetActive(true);
        }

        /// <summary>For the menu button, which can only call a method without arguments.</summary>
        public void Open() => Show();

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        /// <summary>Equips the skin on a tile and saves it. Public so tests can tap without an EventSystem.</summary>
        public void Equip(int index)
        {
            var skin = SkinAt(index);
            if (skin == null) return;
            settings ??= GameSettings.Current;
            if (settings != null) settings.ShipSkin = skin.id;
            RefreshEquipped();
        }

        private void RefreshEquipped()
        {
            var equipped = EquippedId;
            for (var i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null) continue;
                var on = SkinAt(i) != null && SkinAt(i).id == equipped;
                if (tiles[i].frame != null) tiles[i].frame.sprite = on ? equippedFrame : tileFrame;
                if (tiles[i].equippedBadge != null) tiles[i].equippedBadge.SetActive(on);
            }
        }

        private ShipSkinDefinition SkinAt(int index) =>
            library != null && library.skins != null && index >= 0 && index < library.skins.Length ? library.skins[index] : null;
    }
}
