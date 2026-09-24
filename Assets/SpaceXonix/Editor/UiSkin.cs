using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Restyles every menu as part of the ship: hull-plate panels with lit teal frames, raised plating
    /// buttons with proper pixel-art pressed states, recessed slots, and Kenney's CC0 pixel fonts.
    ///
    /// The widgets are drawn in code in the board plating's palette, for the same reason the board is:
    /// a generic UI pack would not match the art around it. Roles are recognised from the hierarchy
    /// (a Button, a Slider's parts, a panel), so re-running this after adding a screen styles it too.
    /// SpaceXonix > Apply UI Skin.
    /// </summary>
    public static class UiSkin
    {
        private const string Folder = "Assets/SpaceXonix/Art/Generated/UI";
        private const string FontFolder = "Assets/SpaceXonix/Art/ThirdParty/Kenney/Fonts";
        public const string BodyFontName = "Kenney Mini Square";
        public const string TitleFontName = "Kenney Pixel Square";

        private static readonly Color32 Hull = PixelCanvas.Hex(0x161a2b);
        private static readonly Color32 Void = PixelCanvas.Hex(0x0a0c16);
        private static readonly Color32 Plate = PixelCanvas.Hex(0x1f6f8b);
        private static readonly Color32 PlateHover = PixelCanvas.Hex(0x2a8fad);
        private static readonly Color32 PlateLight = PixelCanvas.Hex(0x48b4cc);
        private static readonly Color32 PlateShade = PixelCanvas.Hex(0x175469);
        private static readonly Color32 Seam = PixelCanvas.Hex(0x0c3040);
        private static readonly Color32 Rivet = PixelCanvas.Hex(0x8ad8e6);
        private static readonly Color32 Glow = PixelCanvas.Hex(0x7ff3ff);
        private static readonly Color32 Edge = PixelCanvas.Hex(0x2aa9c9);
        private static readonly Color32 Grey = PixelCanvas.Hex(0x3a3f55);
        private static readonly Color32 GreyLight = PixelCanvas.Hex(0x565c78);
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        /// <summary>Title text colour: the plating's glow, so headings read as lit displays.</summary>
        public static readonly Color TitleColour = new Color(.5f, .95f, 1f);

        [MenuItem("SpaceXonix/Apply UI Skin")]
        public static void Apply()
        {
            GenerateSprites();
            ImportFonts();
            var skin = Load();
            if (skin == null) return;

            var prefab = "Assets/SpaceXonix/Prefabs/UI/SettingsOverlay.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefab);
            Style(root.transform, skin);
            PrefabUtility.SaveAsPrefabAsset(root, prefab);
            PrefabUtility.UnloadPrefabContents(root);

            foreach (var path in new[] { "Assets/SpaceXonix/Scenes/MainMenu.unity", "Assets/SpaceXonix/Scenes/Game.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (canvas.transform.parent == null) Style(canvas.transform, skin);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("Applied the ship UI skin to the menus, the HUD and the settings overlay.");
        }

        // ---------------------------------------------------------------- styling

        private sealed class Skin
        {
            public Sprite Panel, Button, ButtonHover, ButtonPressed, ButtonDisabled, Slot, Fill, Knob, Check;
            public Font Body, Title;
        }

        private static void Style(Transform node, Skin skin)
        {
            // A prefab instance inside a scene is styled through its prefab, not with overrides.
            if (node.parent != null && PrefabUtility.IsOutermostPrefabInstanceRoot(node.gameObject)) return;

            var image = node.GetComponent<Image>();
            var button = node.GetComponent<Button>();
            var slider = node.GetComponentInParent<Slider>(true);
            var toggle = node.GetComponentInParent<Toggle>(true);

            if (button != null && image != null)
            {
                SetSliced(image, skin.Button);
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = skin.ButtonHover,
                    selectedSprite = skin.ButtonHover,
                    pressedSprite = skin.ButtonPressed,
                    disabledSprite = skin.ButtonDisabled
                };
            }
            else if (image != null && slider != null && node.name == "Background") SetSliced(image, skin.Slot);
            else if (image != null && slider != null && node.name == "Fill") SetSliced(image, skin.Fill);
            else if (image != null && slider != null && node.name == "Handle") SetSimple(image, skin.Knob);
            else if (image != null && toggle != null && toggle.targetGraphic == image) SetSliced(image, skin.Slot);
            else if (image != null && toggle != null && toggle.graphic == image) SetSimple(image, skin.Check);
            else if (image != null && IsPanel(node.name)) SetSliced(image, skin.Panel);
            else if (image != null && (node.name == "PowerTrack" || node.name == "Ability")) SetSliced(image, skin.Slot);
            else if (image != null && node.name == "PowerFill") { image.sprite = skin.Fill; image.color = Color.white; }

            var text = node.GetComponent<Text>();
            if (text != null) StyleText(text, skin);

            foreach (Transform child in node) Style(child, skin);
        }

        private static bool IsPanel(string name) => name == "Panel" || name == "SettingsPanel" || name == "ControlsPanel";

        private static void StyleText(Text text, Skin skin)
        {
            var isTitle = text.name == "Title";
            text.font = isTitle ? skin.Title : skin.Body;
            if (isTitle) text.color = TitleColour;
            // The title font is much wider than the old one, so a long stage name would wrap into the
            // body below it; titles shrink to fit their rect instead. Body text keeps its size, and
            // overflows vertically because the pixel fonts' taller line height would otherwise make a
            // truncating rect drop a line entirely, which blanked single-line captions.
            text.resizeTextForBestFit = isTitle;
            if (isTitle)
            {
                text.resizeTextMaxSize = text.fontSize;
                text.resizeTextMinSize = Mathf.Max(10, text.resizeTextMaxSize / 2);
            }
            text.verticalOverflow = isTitle ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            var shadow = text.GetComponent<Shadow>();
            if (isTitle && shadow == null) shadow = text.gameObject.AddComponent<Shadow>();
            if (shadow != null)
            {
                shadow.effectColor = new Color(.02f, .05f, .1f, .9f);
                shadow.effectDistance = new Vector2(4f, -4f);
            }
            EditorUtility.SetDirty(text);
        }

        private static void SetSliced(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.pixelsPerUnitMultiplier = 1f;
            EditorUtility.SetDirty(image);
        }

        private static void SetSimple(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            EditorUtility.SetDirty(image);
        }

        private static Skin Load()
        {
            var skin = new Skin
            {
                Panel = LoadSprite("UI_Panel"), Button = LoadSprite("UI_Button"), ButtonHover = LoadSprite("UI_ButtonHover"),
                ButtonPressed = LoadSprite("UI_ButtonPressed"), ButtonDisabled = LoadSprite("UI_ButtonDisabled"),
                Slot = LoadSprite("UI_Slot"), Fill = LoadSprite("UI_Fill"), Knob = LoadSprite("UI_Knob"), Check = LoadSprite("UI_Check"),
                Body = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{BodyFontName}.ttf"),
                Title = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{TitleFontName}.ttf"),
            };
            if (skin.Panel == null || skin.Button == null || skin.Body == null || skin.Title == null)
            {
                Debug.LogWarning("UI skin assets are missing; check the generated sprites and the Kenney fonts.");
                return null;
            }
            return skin;
        }

        private static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");

        // ---------------------------------------------------------------- sprites

        /// <summary>Nine-slice borders in pixels, read by the import rule.</summary>
        public static int BorderFor(string name)
        {
            switch (name)
            {
                case "UI_Panel": return 7;
                case "UI_Button": case "UI_ButtonHover": case "UI_ButtonPressed": case "UI_ButtonDisabled": return 5;
                case "UI_Slot": case "UI_Fill": return 3;
                default: return 0;
            }
        }

        public static bool IsUiSprite(string assetPath) => assetPath.StartsWith(Folder);

        private static void GenerateSprites()
        {
            Directory.CreateDirectory(Folder);
            Write("UI_Panel", Panel());
            Write("UI_Button", Button(Plate, PlateLight, PlateShade, false));
            Write("UI_ButtonHover", Button(PlateHover, Glow, Plate, false));
            Write("UI_ButtonPressed", Button(PlateShade, PlateShade, Plate, true));
            Write("UI_ButtonDisabled", Button(Grey, GreyLight, Hull, false));
            Write("UI_Slot", Slot());
            Write("UI_Fill", Fill());
            Write("UI_Knob", Knob());
            Write("UI_Check", Check());
            AssetDatabase.Refresh();
        }

        /// <summary>A hull panel: dark plate inside a bevelled teal frame, riveted at the corners.</summary>
        private static PixelCanvas Panel()
        {
            var c = new PixelCanvas(24, 24, Hull);
            c.Frame(0, 0, 24, 24, Void);
            // Bevel: lit on the top and left, shaded on the bottom and right.
            c.Line(1, 1, 22, 1, PlateLight); c.Line(1, 1, 1, 22, PlateLight);
            c.Line(1, 22, 22, 22, PlateShade); c.Line(22, 1, 22, 22, PlateShade);
            c.Frame(2, 2, 20, 20, Plate);
            c.Frame(3, 3, 18, 18, Seam);
            foreach (var (x, y) in new[] { (4, 4), (19, 4), (4, 19), (19, 19) }) c.Set(x, y, Rivet);
            return c;
        }

        /// <summary>
        /// A plating button with a two-pixel lip at the bottom, so it reads as raised. The pressed
        /// state drops the lip and shifts the face down, so pressing it visibly pushes it in.
        /// </summary>
        private static PixelCanvas Button(Color32 face, Color32 light, Color32 shade, bool pressed)
        {
            var c = new PixelCanvas(16, 16, Clear);
            var top = pressed ? 2 : 0;
            c.Rect(0, top, 16, 16 - top, Seam);
            c.Rect(1, top + 1, 14, 14 - top - (pressed ? 0 : 2), face);
            if (!pressed) c.Line(1, 13, 14, 14, shade);
            c.Line(1, top + 1, 14, top + 1, light);
            c.Line(1, top + 1, 1, pressed ? 14 : 12, light);
            return c;
        }

        /// <summary>A recessed slot, shaded along the top edge where the lip overhangs it.</summary>
        private static PixelCanvas Slot()
        {
            var c = new PixelCanvas(8, 8, Void);
            c.Rect(1, 1, 6, 6, Hull);
            c.Line(1, 1, 6, 1, Void);
            // A plating-coloured rim, so an empty slot still reads against dark space.
            c.Frame(0, 0, 8, 8, PlateShade);
            return c;
        }

        /// <summary>The glowing fill for sliders and the power meter.</summary>
        private static PixelCanvas Fill()
        {
            var c = new PixelCanvas(8, 8, Glow);
            c.Frame(0, 0, 8, 8, Edge);
            c.Line(1, 1, 6, 1, PixelCanvas.Hex(0xdcffff));
            return c;
        }

        /// <summary>A small plating knob with a lit pip.</summary>
        private static PixelCanvas Knob()
        {
            var c = new PixelCanvas(10, 10, Clear);
            c.Rect(0, 0, 10, 10, Seam);
            c.Rect(1, 1, 8, 8, Plate);
            c.Line(1, 1, 8, 1, PlateLight); c.Line(1, 1, 1, 8, PlateLight);
            c.Rect(4, 4, 2, 2, Glow);
            return c;
        }

        /// <summary>A pixel tick for toggles.</summary>
        private static PixelCanvas Check()
        {
            var c = new PixelCanvas(10, 10, Clear);
            foreach (var (x, y) in new[] { (1, 5), (2, 6), (3, 7), (4, 6), (5, 5), (6, 4), (7, 3), (8, 2) })
            {
                c.Set(x, y, Glow);
                c.Set(x, y + 1, Edge);
            }
            return c;
        }

        private static void Write(string name, PixelCanvas canvas) => File.WriteAllBytes($"{Folder}/{name}.png", canvas.Encode());

        // ---------------------------------------------------------------- fonts

        private static void ImportFonts()
        {
            var source = Path.Combine("AssetSources", "kenney_fonts");
            Directory.CreateDirectory(FontFolder);
            foreach (var name in new[] { BodyFontName, TitleFontName })
            {
                var from = Path.Combine(source, "Fonts", name + ".ttf");
                var to = $"{FontFolder}/{name}.ttf";
                if (!File.Exists(to) && File.Exists(from)) File.Copy(from, to);
            }
            var licence = Path.Combine(source, "License.txt");
            if (File.Exists(licence)) File.Copy(licence, $"{FontFolder}/LICENSE.txt", true);
            AssetDatabase.Refresh();
            foreach (var name in new[] { BodyFontName, TitleFontName })
            {
                // Hinted raster keeps a pixel font's edges hard instead of smoothing them.
                if (!(AssetImporter.GetAtPath($"{FontFolder}/{name}.ttf") is TrueTypeFontImporter importer)) continue;
                importer.fontRenderingMode = FontRenderingMode.HintedRaster;
                importer.SaveAndReimport();
            }
        }
    }
}
