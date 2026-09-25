using System;
using SpaceXonix.Presentation;
using SpaceXonix.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// The hangar, shown after the difficulty is picked: a grid of every ship, each tile showing the
    /// ship itself, animated, with its name, its perk and its drawback. Tapping a tile equips it and
    /// saves the choice; the equipped tile is lit and marked. Launch starts the run in that ship.
    /// The tiles are laid out by the editor builder, one per ship in the library.
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
            public Text perkLabel;
            public Text drawbackLabel;
            public GameObject equippedBadge;
        }

        [SerializeField] private GameObject root;
        [SerializeField] private ShipSkinLibrary library;
        [SerializeField] private Tile[] tiles = new Tile[0];
        [SerializeField] private Sprite tileFrame;
        [SerializeField] private Sprite equippedFrame;
        [SerializeField] private Button launchButton;
        [SerializeField] private Button backButton;

        private GameSettingsModel settings;
        private Action launch;

        public bool IsShown => root != null && root.activeSelf;
        public int TileCount => tiles.Length;
        public string EquippedId => library != null ? library.Find(settings?.ShipSkin)?.id : null;

        private void Awake()
        {
            if (launchButton != null) launchButton.onClick.AddListener(Launch);
            if (backButton != null) backButton.onClick.AddListener(Hide);
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
            if (launchButton != null) launchButton.onClick.RemoveListener(Launch);
            if (backButton != null) backButton.onClick.RemoveListener(Hide);
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
                var stats = skin.stats ?? ShipStats.Neutral;
                if (tiles[i].perkLabel != null) tiles[i].perkLabel.text = stats.perk;
                if (tiles[i].drawbackLabel != null) tiles[i].drawbackLabel.text = stats.drawback;
            }
            RefreshEquipped();
            if (root != null) root.SetActive(true);
        }

        /// <summary>Opens the hangar as the last step before a run; Launch then calls <paramref name="onLaunch"/>.</summary>
        public void OpenForLaunch(Action onLaunch, GameSettingsModel model = null)
        {
            launch = onLaunch;
            Show(model);
        }

        /// <summary>Starts the run in the equipped ship. Public so tests can launch without a click.</summary>
        public void Launch() => launch?.Invoke();

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
