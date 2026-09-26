using System.IO;
using SpaceXonix.Campaign;
using SpaceXonix.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Builds the holographic upgrade cards and the Power Shot energy gauge: draws their pixel art,
    /// draws a picture for every upgrade from the game's own sprites, and assembles both in the game
    /// scene. Re-running it rebuilds everything in place. SpaceXonix > Build Upgrade Cards.
    ///
    /// The card pictures reuse ansimuz's ship, bolt and power-up frames on a star-grid backdrop, so
    /// they match the art the player has been looking at all run; they are drawn in code for the same
    /// reason the board and the menus are.
    /// </summary>
    public static class UpgradeCardBuilder
    {
        private const string UiFolder = "Assets/SpaceXonix/Art/Generated/UI";
        private const string CardArtFolder = UiFolder + "/Cards";
        private const string TextureFolder = "Assets/SpaceXonix/Art/Generated";
        private const string UpgradeFolder = "Assets/SpaceXonix/ScriptableObjects/Upgrades";
        private const string FontFolder = UiSkin.FontFolder;
        private const int ArtSize = 48;
        private const int ArtScale = 5;

        private static readonly Color32 Void = PixelCanvas.Hex(0x0a0c16);
        private static readonly Color32 Hull = PixelCanvas.Hex(0x161a2b);
        private static readonly Color32 HullLight = PixelCanvas.Hex(0x1f2640);
        private static readonly Color32 Seam = PixelCanvas.Hex(0x0c3040);
        private static readonly Color32 Plate = PixelCanvas.Hex(0x1f6f8b);
        private static readonly Color32 PlateLight = PixelCanvas.Hex(0x48b4cc);
        private static readonly Color32 PlateShade = PixelCanvas.Hex(0x175469);
        private static readonly Color32 Rivet = PixelCanvas.Hex(0x8ad8e6);
        private static readonly Color32 Edge = PixelCanvas.Hex(0x2aa9c9);
        private static readonly Color32 Glow = PixelCanvas.Hex(0x7ff3ff);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        /// <summary>Header band and pip colour per upgrade.</summary>
        public static Color AccentFor(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.ReinforcedHull: return new Color(.55f, .78f, .9f);
                case UpgradeType.ImprovedThrusters: return new Color(1f, .58f, .22f);
                case UpgradeType.RapidCapacitor: return new Color(1f, .87f, .3f);
                case UpgradeType.ShieldCapacitor: return new Color(.35f, .8f, 1f);
                case UpgradeType.CryogenicCore: return new Color(.72f, .95f, 1f);
                case UpgradeType.GravityStabilizer: return new Color(.78f, .55f, 1f);
                default: return new Color(.45f, 1f, .58f);
            }
        }

        [MenuItem("SpaceXonix/Build Upgrade Cards and Power Gauge")]
        public static void Build()
        {
            GenerateSprites();
            GenerateCardArt();
            AssetDatabase.Refresh();
            AssignCardArt();

            var scene = EditorSceneManager.OpenScene("Assets/SpaceXonix/Scenes/Game.unity");
            var hud = Object.FindFirstObjectByType<GameHud>(FindObjectsInactive.Include);
            if (hud == null)
            {
                Debug.LogWarning("No GameHud in the game scene; nothing was built.");
                return;
            }
            BuildGauge(hud);
            BuildCards(hud.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Built the upgrade cards and the power gauge.");
        }

        // ---------------------------------------------------------------- sprites

        private static void GenerateSprites()
        {
            Directory.CreateDirectory(UiFolder);
            Directory.CreateDirectory(CardArtFolder);
            Write(UiFolder, "UI_Card", CardFrame(Edge, Glow, Hull));
            Write(UiFolder, "UI_CardHover", CardFrame(Glow, White, HullLight));
            Write(UiFolder, "UI_CardPressed", CardFrame(PlateLight, Glow, PlateShade));
            Write(UiFolder, "UI_CardBand", Band());
            Write(UiFolder, "UI_Pip", Pip(true));
            Write(UiFolder, "UI_PipEmpty", Pip(false));
            Write(UiFolder, "UI_GaugeGlow", GaugeGlow());
            Write(TextureFolder, "UI_Scanlines", Scanlines());
            Write(TextureFolder, "UI_EnergyStripes", EnergyStripes());
        }

        /// <summary>
        /// A card: chamfered corners, a lit outline, an inner seam and bright corner brackets, like
        /// the frame of a holographic display.
        /// </summary>
        private static PixelCanvas CardFrame(Color32 edge, Color32 bracket, Color32 fill)
        {
            const int size = 40, chamfer = 5, bracketLength = 11;
            var c = new PixelCanvas(size, size, Clear);
            var body = new Color32(fill.r, fill.g, fill.b, 238);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                int cx = Mathf.Min(x, size - 1 - x), cy = Mathf.Min(y, size - 1 - y);
                if (cx + cy < chamfer) continue;
                var outline = cx == 0 || cy == 0 || cx + cy == chamfer;
                var nearCorner = Mathf.Max(cx, cy) < bracketLength;
                if (outline) c.Set(x, y, nearCorner ? bracket : edge);
                else if (nearCorner && (cx == 1 || cy == 1 || cx + cy == chamfer + 1)) c.Set(x, y, bracket);
                else if (cx == 3 || cy == 3) c.Set(x, y, Seam);
                else c.Set(x, y, body);
            }
            return c;
        }

        /// <summary>A white header band, tinted per upgrade: a bright top edge and a shaded bottom.</summary>
        private static PixelCanvas Band()
        {
            var c = new PixelCanvas(16, 16, new Color32(215, 215, 215, 255));
            c.Line(0, 0, 15, 0, White);
            c.Line(0, 15, 15, 15, new Color32(140, 140, 140, 255));
            foreach (var (x, y) in new[] { (0, 0), (15, 0), (0, 15), (15, 15), (1, 0), (0, 1), (14, 0), (15, 1), (0, 14), (1, 15), (15, 14), (14, 15) })
                c.Set(x, y, Clear);
            return c;
        }

        /// <summary>A diamond pip: filled white (tinted in the UI) or an empty socket.</summary>
        private static PixelCanvas Pip(bool full)
        {
            var c = new PixelCanvas(10, 10, Clear);
            for (var y = 0; y < 10; y++)
            for (var x = 0; x < 10; x++)
            {
                var d = Mathf.Abs(x - 4.5f) + Mathf.Abs(y - 4.5f);
                if (d > 5f) continue;
                var rim = d > 3.9f;
                c.Set(x, y, full ? (rim ? new Color32(200, 200, 200, 255) : White) : (rim ? PlateShade : Void));
            }
            return c;
        }

        /// <summary>A soft pixel glow frame drawn around the full power gauge.</summary>
        private static PixelCanvas GaugeGlow()
        {
            var c = new PixelCanvas(16, 16, Clear);
            c.Frame(0, 0, 16, 16, new Color32(Glow.r, Glow.g, Glow.b, 70));
            c.Frame(1, 1, 14, 14, new Color32(Glow.r, Glow.g, Glow.b, 140));
            c.Frame(2, 2, 12, 12, Glow);
            return c;
        }

        /// <summary>One faint line in four, scrolled over the card pictures like a display's refresh.</summary>
        private static PixelCanvas Scanlines()
        {
            var c = new PixelCanvas(4, 4, Clear);
            // Faint: enough to read as a display, not so much it bars the picture.
            c.Line(0, 0, 3, 0, new Color32(255, 255, 255, 16));
            c.Line(0, 2, 3, 2, new Color32(0, 0, 0, 22));
            return c;
        }

        /// <summary>Diagonal energy stripes streamed through the power gauge's fill.</summary>
        private static PixelCanvas EnergyStripes()
        {
            var c = new PixelCanvas(16, 8, Clear);
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 16; x++)
                if ((x + y) % 8 < 3) c.Set(x, y, new Color32(255, 255, 255, 80));
            return c;
        }

        private static void Write(string folder, string name, PixelCanvas canvas) =>
            File.WriteAllBytes($"{folder}/{name}.png", canvas.Encode());

        // ---------------------------------------------------------------- card pictures

        /// <summary>A pixel grid that can read back and blend, for composing the card pictures.</summary>
        private sealed class ArtCanvas
        {
            private readonly Color32[] pixels = new Color32[ArtSize * ArtSize];

            public Color32 Get(int x, int y) => pixels[Index(x, y)];

            public void Set(int x, int y, Color32 color)
            {
                if (x < 0 || y < 0 || x >= ArtSize || y >= ArtSize) return;
                pixels[Index(x, y)] = color;
            }

            public void Blend(int x, int y, Color color, float amount)
            {
                if (x < 0 || y < 0 || x >= ArtSize || y >= ArtSize) return;
                var under = (Color)Get(x, y);
                Set(x, y, Color.Lerp(under, new Color(color.r, color.g, color.b, 1f), Mathf.Clamp01(amount)));
            }

            /// <summary>
            /// Draws a sprite frame centred on (cx, cy) at a whole-number scale. With <paramref name="wash"/>
            /// above 0 the colours are pulled toward <paramref name="tint"/> instead of multiplied by it,
            /// which recolours a warm sprite cold without turning it muddy.
            /// </summary>
            public void Stamp(string frame, int cx, int cy, int scale, Color tint, float wash = 0f)
            {
                var path = $"{PixelArtImporter.FramesFolder}/{frame}.png";
                if (!File.Exists(path)) { Debug.LogWarning($"Missing frame {path}."); return; }
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(File.ReadAllBytes(path));
                int w = source.width * scale, h = source.height * scale;
                int left = cx - w / 2, top = cy - h / 2;
                for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    // Texture rows run bottom-up; the canvas runs top-down.
                    var c = source.GetPixel(x / scale, source.height - 1 - y / scale);
                    if (c.a < .5f) continue;
                    Set(left + x, top + y, wash > 0f ? Color.Lerp(c, tint, wash) : c * tint);
                }
                Object.DestroyImmediate(source);
            }

            public byte[] Encode()
            {
                var texture = new Texture2D(ArtSize, ArtSize, TextureFormat.RGBA32, false);
                var flipped = new Color32[pixels.Length];
                for (var y = 0; y < ArtSize; y++)
                for (var x = 0; x < ArtSize; x++)
                    flipped[(ArtSize - 1 - y) * ArtSize + x] = pixels[Index(x, y)];
                texture.SetPixels32(flipped);
                texture.Apply();
                var bytes = texture.EncodeToPNG();
                Object.DestroyImmediate(texture);
                return bytes;
            }

            private static int Index(int x, int y) => y * ArtSize + x;
        }

        private static void GenerateCardArt()
        {
            foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
            {
                var art = Backdrop(type);
                DrawSubject(art, type);
                File.WriteAllBytes($"{CardArtFolder}/Card_{type}.png", art.Encode());
            }
        }

        /// <summary>Deep space behind a faint display grid, a few stars, and a glow in the card's colour.</summary>
        private static ArtCanvas Backdrop(UpgradeType type)
        {
            var art = new ArtCanvas();
            var accent = AccentFor(type);
            var random = new System.Random((int)type * 7919 + 17);
            for (var y = 0; y < ArtSize; y++)
            for (var x = 0; x < ArtSize; x++)
            {
                var background = Color.Lerp(Void, HullLight, y / (float)(ArtSize - 1));
                if (x % 8 == 0 || y % 8 == 0) background = Color.Lerp(background, (Color)Seam, .6f);
                art.Set(x, y, background);
                // A dithered glow, so it stays pixel art rather than a smooth gradient.
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(23.5f, 23.5f)) / 21f;
                var glow = Mathf.Clamp01(1f - d);
                glow *= glow * .55f;
                if (glow > .02f && (glow > .22f || (x + y) % 2 == 0)) art.Blend(x, y, accent, glow);
            }
            for (var i = 0; i < 12; i++)
            {
                int x = random.Next(ArtSize), y = random.Next(ArtSize);
                if (Mathf.Abs(x - 24) < 12 && Mathf.Abs(y - 24) < 14) continue;
                art.Set(x, y, random.Next(3) == 0 ? Glow : White);
            }
            return art;
        }

        private static void DrawSubject(ArtCanvas art, UpgradeType type)
        {
            var accent = AccentFor(type);
            switch (type)
            {
                case UpgradeType.ReinforcedHull:
                    // Riveted armour plates either side of the ship.
                    foreach (var left in new[] { 2, 41 })
                    {
                        for (var y = 11; y <= 36; y++)
                        for (var x = left; x <= left + 4; x++)
                            art.Set(x, y, x == left || y == 11 ? PlateLight : x == left + 4 || y == 36 ? PlateShade : Plate);
                        art.Set(left + 2, 14, Rivet); art.Set(left + 2, 33, Rivet); art.Set(left + 2, 24, Rivet);
                    }
                    art.Stamp("ship_2", 24, 24, 2, Color.white);
                    break;

                case UpgradeType.ImprovedThrusters:
                    // Speed streaks trailing behind the ship.
                    var streaks = new System.Random(5);
                    for (var i = 0; i < 9; i++)
                    {
                        var x = 4 + streaks.Next(40);
                        var top = 26 + streaks.Next(10);
                        var length = 6 + streaks.Next(10);
                        for (var y = top; y < top + length; y++) art.Blend(x, y, i % 2 == 0 ? Color.white : accent, .75f);
                    }
                    art.Stamp("ship_2", 24, 22, 2, Color.white);
                    break;

                case UpgradeType.RapidCapacitor:
                    // An energy arc under a volley of bolts.
                    var arc = new[] { (2, 30), (10, 22), (16, 28), (24, 18), (32, 27), (38, 20), (46, 26) };
                    for (var i = 0; i < arc.Length - 1; i++) Line(art, arc[i], arc[i + 1], accent, Color.white);
                    art.Stamp("laser-bolts_2", 13, 20, 2, Color.white);
                    art.Stamp("laser-bolts_2", 24, 14, 2, Color.white);
                    art.Stamp("laser-bolts_2", 35, 20, 2, Color.white);
                    break;

                case UpgradeType.ShieldCapacitor:
                    // A shield bubble around the shield orb.
                    for (var y = 0; y < ArtSize; y++)
                    for (var x = 0; x < ArtSize; x++)
                    {
                        var r = Vector2.Distance(new Vector2(x, y), new Vector2(23.5f, 23.5f));
                        if (r > 18.5f && r < 20.5f) art.Set(x, y, Glow);
                        else if (r >= 20.5f && r < 21.6f && (x + y) % 2 == 0) art.Set(x, y, Edge);
                    }
                    art.Stamp("power-up_2", 24, 24, 2, Color.white);
                    break;

                case UpgradeType.CryogenicCore:
                    art.Stamp("power-up_1", 24, 24, 2, new Color(.7f, .92f, 1f), .55f);
                    foreach (var (x, y) in new[] { (8, 9), (40, 11), (6, 38), (41, 37), (24, 4), (24, 44) })
                    {
                        art.Set(x, y, Color.white);
                        art.Set(x - 1, y, accent); art.Set(x + 1, y, accent); art.Set(x, y - 1, accent); art.Set(x, y + 1, accent);
                    }
                    break;

                case UpgradeType.GravityStabilizer:
                    // Level bars and inward arrows: the tilt, held steady.
                    foreach (var y in new[] { 10, 38 })
                    {
                        for (var x = 6; x <= 41; x++) art.Set(x, y, x % 6 == 0 ? Color.white : (Color)Glow);
                        art.Set(6, y - 1, Glow); art.Set(6, y + 1, Glow); art.Set(41, y - 1, Glow); art.Set(41, y + 1, Glow);
                    }
                    foreach (var (tip, step) in new[] { (5, 1), (42, -1) })
                        for (var i = 0; i < 4; i++)
                        {
                            art.Set(tip + i * step, 24 - i, accent);
                            art.Set(tip + i * step, 24 + i, accent);
                        }
                    art.Stamp("power-up_0", 24, 24, 2, accent, .45f);
                    break;

                default:
                    // Three pickups caught in a targeting reticle.
                    foreach (var (x, y, dx, dy) in new[] { (4, 4, 1, 1), (43, 4, -1, 1), (4, 43, 1, -1), (43, 43, -1, -1) })
                        for (var i = 0; i < 7; i++)
                        {
                            art.Set(x + i * dx, y, accent);
                            art.Set(x, y + i * dy, accent);
                        }
                    art.Stamp("power-up_0", 16, 18, 1, Color.white);
                    art.Stamp("power-up_1", 32, 18, 1, Color.white);
                    art.Stamp("power-up_2", 24, 31, 1, Color.white);
                    break;
            }
        }

        /// <summary>A two-pixel bolt of energy: a coloured edge with a white core.</summary>
        private static void Line(ArtCanvas art, (int x, int y) from, (int x, int y) to, Color edge, Color core)
        {
            var steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
            for (var i = 0; i <= steps; i++)
            {
                var t = steps == 0 ? 0f : i / (float)steps;
                var x = Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t));
                var y = Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t));
                art.Set(x, y + 1, edge);
                art.Set(x, y, core);
            }
        }

        private static void AssignCardArt()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UpgradeDefinition", new[] { UpgradeFolder }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null) continue;
                definition.cardArt = AssetDatabase.LoadAssetAtPath<Sprite>($"{CardArtFolder}/Card_{definition.type}.png");
                definition.cardAccent = AccentFor(definition.type);
                EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- power gauge

        private static void BuildGauge(GameHud hud)
        {
            var track = hud.transform.Find("PowerTrack") as RectTransform;
            var fill = track != null ? track.Find("PowerFill") as RectTransform : null;
            if (track == null || fill == null)
            {
                Debug.LogWarning("The HUD has no PowerTrack/PowerFill; the gauge was not built.");
                return;
            }
            foreach (var name in new[] { "ReadyGlow", "Energy" }) Remove(track, name);
            Remove(fill, "Energy");
            for (var i = 1; i < 10; i++) Remove(track, $"Tick{i}");

            var fillImage = fill.GetComponent<Image>();
            fillImage.sprite = LoadUiSprite("UI_Fill");
            fillImage.type = Image.Type.Sliced;
            fillImage.fillAmount = 1f;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            if (fill.GetComponent<RectMask2D>() == null) fill.gameObject.AddComponent<RectMask2D>();

            var glow = NewImage("ReadyGlow", track, LoadUiSprite("UI_GaugeGlow"), Image.Type.Sliced);
            Stretch(glow.rectTransform, -10f);
            glow.transform.SetAsFirstSibling();
            glow.enabled = false;

            // The stripes span the whole track and are only uncovered as the fill grows, so they stay
            // the same size instead of squashing into a short fill.
            var trackWidth = track.rect.width > 0f ? track.rect.width : track.sizeDelta.x;
            var energy = NewGraphic<RawImage>("Energy", fill);
            energy.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/UI_EnergyStripes.png");
            var energyRect = energy.rectTransform;
            energyRect.anchorMin = new Vector2(0f, 0f);
            energyRect.anchorMax = new Vector2(0f, 1f);
            energyRect.pivot = new Vector2(0f, .5f);
            energyRect.anchoredPosition = Vector2.zero;
            energyRect.sizeDelta = new Vector2(trackWidth - 8f, 0f);
            energy.uvRect = new Rect(0f, 0f, (trackWidth - 8f) / 32f, 1f);

            for (var i = 1; i < 10; i++)
            {
                var tick = NewImage($"Tick{i}", track, null, Image.Type.Simple);
                tick.color = new Color(Void.r / 255f, Void.g / 255f, Void.b / 255f, .75f);
                var rect = tick.rectTransform;
                rect.anchorMin = new Vector2(i / 10f, 0f);
                rect.anchorMax = new Vector2(i / 10f, 1f);
                rect.sizeDelta = new Vector2(3f, -8f);
                rect.anchoredPosition = Vector2.zero;
            }

            var gauge = track.GetComponent<PowerGauge>() ?? track.gameObject.AddComponent<PowerGauge>();
            var serialized = new SerializedObject(gauge);
            serialized.FindProperty("fill").objectReferenceValue = fill;
            serialized.FindProperty("fillImage").objectReferenceValue = fillImage;
            serialized.FindProperty("energy").objectReferenceValue = energy;
            serialized.FindProperty("readyGlow").objectReferenceValue = glow;
            var label = hud.transform.Find("PowerLabel");
            serialized.FindProperty("label").objectReferenceValue = label != null ? label.GetComponent<Text>() : null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var hudSerialized = new SerializedObject(hud);
            hudSerialized.FindProperty("powerGauge").objectReferenceValue = gauge;
            hudSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- cards

        private static void BuildCards(Transform safeArea)
        {
            Remove(safeArea, "UpgradeCards");
            var body = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{UiSkin.BodyFontName}.ttf");
            var title = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{UiSkin.TitleFontName}.ttf");

            var overlay = new GameObject("UpgradeCards", typeof(RectTransform));
            overlay.transform.SetParent(safeArea, false);
            // Above the other campaign screens, below nothing it needs to share the screen with.
            var campaign = safeArea.Find("CampaignOverlay");
            if (campaign != null) overlay.transform.SetSiblingIndex(campaign.GetSiblingIndex() + 1);
            Stretch((RectTransform)overlay.transform, 0f);

            var dim = NewImage("Dim", overlay.transform, null, Image.Type.Simple);
            dim.color = new Color(Void.r / 255f, Void.g / 255f, Void.b / 255f, .9f);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform, 0f);

            var heading = NewText("Heading", overlay.transform, title, 60, TextAnchor.MiddleCenter, UiSkin.TitleColour);
            heading.text = "CHOOSE AN UPGRADE";
            heading.resizeTextForBestFit = true; heading.resizeTextMinSize = 30; heading.resizeTextMaxSize = 60;
            Place(heading.rectTransform, new Vector2(0f, 360f), new Vector2(960f, 80f));
            var shadow = heading.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.02f, .05f, .1f, .9f);
            shadow.effectDistance = new Vector2(4f, -4f);

            var sub = NewText("Subtitle", overlay.transform, body, 28, TextAnchor.MiddleCenter, new Color(.7f, .86f, .95f));
            sub.text = "INSTALL ONE. IT LASTS FOR THIS RUN.";
            Place(sub.rectTransform, new Vector2(0f, 300f), new Vector2(960f, 40f));

            var cards = new UpgradeCard[3];
            for (var i = 0; i < cards.Length; i++)
                cards[i] = BuildCard(overlay.transform, i, new Vector2((i - 1) * 345f, -30f), body, title);

            var panel = overlay.AddComponent<UpgradeCardPanel>();
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("root").objectReferenceValue = overlay;
            var array = serialized.FindProperty("cards");
            array.arraySize = cards.Length;
            for (var i = 0; i < cards.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var screens = Object.FindFirstObjectByType<CampaignScreens>(FindObjectsInactive.Include);
            if (screens != null)
            {
                var screensSerialized = new SerializedObject(screens);
                screensSerialized.FindProperty("upgradeCards").objectReferenceValue = panel;
                screensSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
            overlay.SetActive(false);
        }

        private static UpgradeCard BuildCard(Transform parent, int index, Vector2 position, Font body, Font title)
        {
            var frame = NewImage($"Card{index}", parent, LoadUiSprite("UI_Card"), Image.Type.Sliced);
            frame.raycastTarget = true;
            // As wide as three cards can be on a portrait phone, and tall enough for readable text.
            Place(frame.rectTransform, position, new Vector2(330f, 560f));
            var group = frame.gameObject.AddComponent<CanvasGroup>();
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = LoadUiSprite("UI_CardHover"),
                selectedSprite = LoadUiSprite("UI_CardHover"),
                pressedSprite = LoadUiSprite("UI_CardPressed"),
            };

            var band = NewImage("Band", frame.transform, LoadUiSprite("UI_CardBand"), Image.Type.Sliced);
            var bandRect = band.rectTransform;
            bandRect.anchorMin = new Vector2(0f, 1f); bandRect.anchorMax = new Vector2(1f, 1f); bandRect.pivot = new Vector2(.5f, 1f);
            bandRect.offsetMin = new Vector2(12f, -88f); bandRect.offsetMax = new Vector2(-12f, -12f);
            var name = NewText("Name", band.transform, title, 26, TextAnchor.MiddleCenter, new Color(.03f, .04f, .08f));
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 24; name.resizeTextMaxSize = 32;
            Stretch(name.rectTransform, -8f);

            var screen = NewImage("Screen", frame.transform, LoadUiSprite("UI_Slot"), Image.Type.Sliced);
            PlaceTop(screen.rectTransform, -100f, new Vector2(256f, 256f));
            var art = NewImage("Art", screen.transform, null, Image.Type.Simple);
            art.preserveAspect = true;
            Place(art.rectTransform, Vector2.zero, new Vector2(ArtSize * ArtScale, ArtSize * ArtScale));
            var scan = NewGraphic<RawImage>("Scanlines", screen.transform);
            scan.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/UI_Scanlines.png");
            // One scanline texel per picture pixel row, so the lines sit on the pixel grid.
            scan.uvRect = new Rect(0f, 0f, 1f, ArtSize / 4f);
            Place(scan.rectTransform, Vector2.zero, new Vector2(ArtSize * ArtScale, ArtSize * ArtScale));

            var pipRow = new GameObject("Pips", typeof(RectTransform));
            pipRow.transform.SetParent(frame.transform, false);
            PlaceTop((RectTransform)pipRow.transform, -366f, new Vector2(220f, 24f));
            var layout = pipRow.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 12f;
            layout.childControlWidth = false; layout.childControlHeight = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            var pips = new Image[5];
            for (var i = 0; i < pips.Length; i++)
            {
                pips[i] = NewImage($"Pip{i}", pipRow.transform, LoadUiSprite("UI_PipEmpty"), Image.Type.Simple);
                pips[i].rectTransform.sizeDelta = new Vector2(20f, 20f);
            }

            var effect = NewText("Effect", frame.transform, body, 34, TextAnchor.MiddleCenter, Color.white);
            effect.resizeTextForBestFit = true; effect.resizeTextMinSize = 26; effect.resizeTextMaxSize = 34;
            var effectRect = effect.rectTransform;
            effectRect.anchorMin = new Vector2(0f, 0f); effectRect.anchorMax = new Vector2(1f, 0f); effectRect.pivot = new Vector2(.5f, 0f);
            effectRect.offsetMin = new Vector2(14f, 46f); effectRect.offsetMax = new Vector2(-14f, 162f);

            var footer = NewText("Footer", frame.transform, body, 22, TextAnchor.MiddleCenter, new Color(.5f, .85f, .95f, .9f));
            footer.text = "TAP TO INSTALL";
            var footerRect = footer.rectTransform;
            footerRect.anchorMin = new Vector2(0f, 0f); footerRect.anchorMax = new Vector2(1f, 0f); footerRect.pivot = new Vector2(.5f, 0f);
            footerRect.offsetMin = new Vector2(14f, 10f); footerRect.offsetMax = new Vector2(-14f, 42f);

            var card = frame.gameObject.AddComponent<UpgradeCard>();
            var serialized = new SerializedObject(card);
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.FindProperty("group").objectReferenceValue = group;
            serialized.FindProperty("band").objectReferenceValue = band;
            serialized.FindProperty("nameLabel").objectReferenceValue = name;
            serialized.FindProperty("art").objectReferenceValue = art;
            serialized.FindProperty("scanlines").objectReferenceValue = scan;
            serialized.FindProperty("pipFull").objectReferenceValue = LoadUiSprite("UI_Pip");
            serialized.FindProperty("pipEmpty").objectReferenceValue = LoadUiSprite("UI_PipEmpty");
            serialized.FindProperty("effectLabel").objectReferenceValue = effect;
            var pipArray = serialized.FindProperty("pips");
            pipArray.arraySize = pips.Length;
            for (var i = 0; i < pips.Length; i++) pipArray.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        // ---------------------------------------------------------------- plumbing

        private static Sprite LoadUiSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/{name}.png");

        private static void Remove(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static T NewGraphic<T>(string name, Transform parent) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var graphic = go.AddComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Image.Type type)
        {
            var image = NewGraphic<Image>(name, parent);
            image.sprite = sprite;
            image.type = type;
            return image;
        }

        private static Text NewText(string name, Transform parent, Font font, int size, TextAnchor alignment, Color color)
        {
            var text = NewGraphic<Text>(name, parent);
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void PlaceTop(RectTransform rect, float top, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, top);
            rect.sizeDelta = size;
        }
    }
}
