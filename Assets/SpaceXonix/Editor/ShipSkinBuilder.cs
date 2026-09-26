using System.Collections.Generic;
using System.IO;
using SpaceXonix.Presentation;
using SpaceXonix.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Builds the ship skins and the hangar screen. The default skin is ansimuz's ship; the rest are
    /// fighters from Master484's CC0 16x16 Ship Collection, cut from the sheet, turned nose-up (they
    /// are drawn facing right) and given a two-frame thruster flicker, so they animate like the
    /// default ship. SpaceXonix > Build Ship Skins.
    /// </summary>
    public static class ShipSkinBuilder
    {
        private const string SourceSheet = "AssetSources/master484_16ship/16ShipCollection.png";
        private const string ArtFolder = "Assets/SpaceXonix/Art/ThirdParty/Master484";
        private const string FrameFolder = ArtFolder + "/Skins";
        private const string SkinFolder = "Assets/SpaceXonix/ScriptableObjects/Skins";
        private const string LibraryPath = SkinFolder + "/ShipSkinLibrary.asset";
        private const string UiFolder = "Assets/SpaceXonix/Art/Generated/UI";
        private const string FontFolder = UiSkin.FontFolder;
        private const int Cell = 16;
        private const int FlameRows = 4;

        /// <summary>A Master484 ship: its colour group on the sheet (0-4) and its cell (row-major, 5 per row).</summary>
        private readonly struct SheetShip
        {
            public readonly string Id, Name;
            public readonly int Group, Index;
            public readonly ShipStats Stats;
            public SheetShip(string id, string name, int group, int index, ShipStats stats) { Id = id; Name = name; Group = group; Index = index; Stats = stats; }
        }

        /// <summary>Every ship but the default trades one strength for one weakness.</summary>
        private static readonly SheetShip[] Ships =
        {
            // Big swings on purpose: at 10-25% the ships all felt the same in play. Speed swings are the
            // mildest, because at 30-50% the ship itself became hard to steer.
            new SheetShip("cobalt-delta", "Cobalt Delta", 0, 7, new ShipStats
                { perk = "+35% speed off your territory", drawback = "-25% speed on your territory", exposedSpeed = 1.35f, safeSpeed = .75f }),
            new SheetShip("viper", "Viper", 1, 66, new ShipStats
                { perk = "+20% ship speed", drawback = "Power charges 50% slower", speed = 1.2f, powerCharge = .5f }),
            new SheetShip("ember-talon", "Ember Talon", 2, 17, new ShipStats
                { perk = "Power charges 2x faster", drawback = "Start with 1 fewer life", powerCharge = 2f, extraLives = -1 }),
            new SheetShip("solar-hornet", "Solar Hornet", 3, 78, new ShipStats
                { perk = "2x power-up spawns", drawback = "Abilities last half as long", pickupChance = 2f, abilityDuration = .5f }),
            new SheetShip("nebula-dart", "Nebula Dart", 4, 81, new ShipStats
                { perk = "Abilities last 2x longer", drawback = "-20% ship speed", abilityDuration = 2f, speed = .8f }),
            new SheetShip("phantom-rail", "Phantom Rail", 2, 69, new ShipStats
                { perk = "+2 extra lives", drawback = "-20% ship speed", extraLives = 2, speed = .8f }),
        };

        [MenuItem("SpaceXonix/Build Ship Skins")]
        public static void Build()
        {
            if (!File.Exists(SourceSheet))
            {
                Debug.LogWarning($"Missing {SourceSheet}. Download Master484's 16x16 Ship Collection (CC0) from OpenGameArt into AssetSources.");
                return;
            }
            Directory.CreateDirectory(FrameFolder);
            Directory.CreateDirectory(SkinFolder);
            WriteLicence();
            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sheet.LoadImage(File.ReadAllBytes(SourceSheet));
            foreach (var ship in Ships) WriteFrames(sheet, ship);
            Object.DestroyImmediate(sheet);
            AssetDatabase.Refresh();

            var library = BuildLibrary();
            BuildMenu(library);
            WireGame(library);
            Debug.Log($"Built {library.skins.Length} ship skins and the hangar screen.");
        }

        // ---------------------------------------------------------------- art

        private static void WriteLicence()
        {
            File.WriteAllText($"{ArtFolder}/LICENSE.txt",
                "16x16 Ship Collection by Master484 (M484 Games)\n" +
                "https://opengameart.org/content/1616-ship-collection\n\n" +
                "License: Public Domain / CC0. \"These graphics are in the Public Domain. Attribution is not needed.\"\n" +
                "https://creativecommons.org/publicdomain/zero/1.0/\n\n" +
                "Modified for SpaceXonix: ships cut from the sheet, rotated nose-up, with thruster frames added.\n");
        }

        /// <summary>Writes a ship's two animation frames: the ship, nose-up, over a long and a short flame.</summary>
        private static void WriteFrames(Texture2D sheet, SheetShip ship)
        {
            var pixels = CutRotated(sheet, ship);
            for (var frame = 0; frame < 2; frame++)
            {
                var texture = new Texture2D(Cell, Cell + FlameRows, TextureFormat.RGBA32, false);
                var clear = new Color32[Cell * (Cell + FlameRows)];
                texture.SetPixels32(clear);
                for (var y = 0; y < Cell; y++)
                for (var x = 0; x < Cell; x++)
                    if (pixels[x, y].a > 0) Put(texture, x, y, pixels[x, y]);
                DrawFlame(texture, pixels, frame == 0 ? 3 : 2);
                texture.Apply();
                File.WriteAllBytes($"{FrameFolder}/{ship.Id}_{frame}.png", texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>Cuts a 16x16 cell from the sheet, turning it from nose-right to nose-up. Black is the sheet's background.</summary>
        private static Color32[,] CutRotated(Texture2D sheet, SheetShip ship)
        {
            // Cells sit in white frames on a 20-pixel stride; each colour group is 110 pixels wide.
            int left = 28 + 110 * ship.Group + 20 * (ship.Index % 5);
            int top = 42 + 20 * (ship.Index / 5);
            var result = new Color32[Cell, Cell];
            for (var y = 0; y < Cell; y++)
            for (var x = 0; x < Cell; x++)
            {
                Color32 c = sheet.GetPixel(left + x, sheet.height - 1 - (top + y));
                if (c.r < 5 && c.g < 5 && c.b < 5) continue;
                c.a = 255;
                // A quarter turn anticlockwise: the nose on the right comes to the top.
                result[y, Cell - 1 - x] = c;
            }
            return result;
        }

        /// <summary>A flame under every column of the ship's rearmost row: white-hot, then orange, then red.</summary>
        private static void DrawFlame(Texture2D texture, Color32[,] ship, int length)
        {
            var rear = -1;
            for (var y = Cell - 1; y >= 0 && rear < 0; y--)
                for (var x = 0; x < Cell; x++)
                    if (ship[x, y].a > 0) { rear = y; break; }
            if (rear < 0) return;
            var columns = new List<int>();
            for (var x = 0; x < Cell; x++) if (ship[x, rear].a > 0) columns.Add(x);
            var colours = new[] { new Color32(255, 244, 196, 255), new Color32(255, 170, 60, 255), new Color32(214, 70, 40, 255) };
            for (var i = 0; i < columns.Count; i++)
            {
                // The outer columns burn shorter, which rounds the flame off.
                var edge = i == 0 || i == columns.Count - 1;
                var run = Mathf.Max(1, edge && columns.Count > 2 ? length - 1 : length);
                for (var d = 0; d < run; d++) Put(texture, columns[i], rear + 1 + d, colours[Mathf.Min(d, colours.Length - 1)]);
            }
        }

        /// <summary>Sets a pixel addressed top-down, the way the art is drawn.</summary>
        private static void Put(Texture2D texture, int x, int y, Color32 colour)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height) return;
            texture.SetPixel(x, texture.height - 1 - y, colour);
        }

        // ---------------------------------------------------------------- assets

        private static ShipSkinLibrary BuildLibrary()
        {
            var skins = new List<ShipSkinDefinition>
            {
                Skin("crimson-vanguard", "Crimson Vanguard",
                    new[] { PixelArtImporter.LoadFrame("ship", 2), PixelArtImporter.LoadFrame("ship", 7) }, 12f,
                    new ShipStats { perk = "Balanced all-rounder", drawback = "No weaknesses" })
            };
            foreach (var ship in Ships)
                skins.Add(Skin(ship.Id, ship.Name, new[]
                {
                    AssetDatabase.LoadAssetAtPath<Sprite>($"{FrameFolder}/{ship.Id}_0.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>($"{FrameFolder}/{ship.Id}_1.png")
                }, 12f, ship.Stats));

            var library = AssetDatabase.LoadAssetAtPath<ShipSkinLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ShipSkinLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.skins = skins.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        private static ShipSkinDefinition Skin(string id, string name, Sprite[] frames, float fps, ShipStats stats)
        {
            var path = $"{SkinFolder}/Skin_{id}.asset";
            var skin = AssetDatabase.LoadAssetAtPath<ShipSkinDefinition>(path);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<ShipSkinDefinition>();
                AssetDatabase.CreateAsset(skin, path);
            }
            skin.id = id;
            skin.displayName = name;
            skin.frames = frames;
            skin.framesPerSecond = fps;
            skin.stats = stats;
            EditorUtility.SetDirty(skin);
            return skin;
        }

        // ---------------------------------------------------------------- menu

        private static void BuildMenu(ShipSkinLibrary library)
        {
            var scene = EditorSceneManager.OpenScene("Assets/SpaceXonix/Scenes/MainMenu.unity");
            // Opening a scene can unload assets nothing referenced yet, so reload it by path.
            library = AssetDatabase.LoadAssetAtPath<ShipSkinLibrary>(LibraryPath);
            var canvas = GameObject.Find("MenuCanvas");
            var safeArea = canvas != null ? canvas.transform.Find("SafeArea") ?? canvas.transform : null;
            if (safeArea == null) { Debug.LogWarning("No MenuCanvas in the main menu."); return; }
            Remove(safeArea, "SkinsButton");
            Remove(safeArea, "SkinOverlay");
            var body = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{UiSkin.BodyFontName}.ttf");
            var title = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{UiSkin.TitleFontName}.ttf");

            // The hangar overlay, drawn over the menu.
            var overlay = new GameObject("SkinOverlay", typeof(RectTransform));
            overlay.transform.SetParent(safeArea, false);
            Stretch((RectTransform)overlay.transform, 0f);
            var dim = NewImage("Dim", overlay.transform, null);
            dim.color = new Color(.04f, .05f, .09f, .92f);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform, 0f);

            var panel = NewImage("Panel", overlay.transform, Ui("UI_Panel"));
            panel.type = Image.Type.Sliced;
            // Just tall enough for the card, the dots and the buttons, so there is no dead gap above the buttons.
            Place(panel.rectTransform, new Vector2(0f, 0f), new Vector2(1000f, 1420f));
            // Designed for a portrait phone; shrinks evenly to fit anything squatter.
            panel.gameObject.AddComponent<UniformFit>();

            var heading = NewText("Title", panel.transform, title, 58, UiSkin.TitleColour);
            heading.text = "CHOOSE YOUR SHIP";
            heading.resizeTextForBestFit = true; heading.resizeTextMinSize = 29; heading.resizeTextMaxSize = 58;
            PlaceTop(heading.rectTransform, -40f, new Vector2(900f, 80f));
            var shadow = heading.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.02f, .05f, .1f, .9f);
            shadow.effectDistance = new Vector2(4f, -4f);

            // One ship at a time, big, browsed as an endless carousel. A grid of seven small tiles
            // was unreadable on a phone and on a PC; plain boxed arrows and floating text looked cheap.
            GenerateHangarArt();
            var card = NewImage("Card", panel.transform, Ui("UI_Card"));
            card.type = Image.Type.Sliced;
            card.raycastTarget = true;
            PlaceTop(card.rectTransform, -150f, new Vector2(700f, 1010f));
            var cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            var swipe = card.gameObject.AddComponent<HorizontalSwipe>();

            // The neighbours peek in dimly at the sides, so it reads as a carousel, not a single card.
            var previousPeek = Peek("PreviousPeek", panel.transform, -418f);
            var nextPeek = Peek("NextPeek", panel.transform, 418f);
            card.transform.SetAsLastSibling();

            // The hero: a breathing light, a hologram pedestal, and the ship floating above it.
            var glow = NewImage("Glow", card.transform, Ui("UI_Glow"));
            glow.color = new Color(.5f, .95f, 1f, .12f);
            PlaceTop(glow.rectTransform, -10f, new Vector2(620f, 620f));
            var pedestal = NewImage("Pedestal", card.transform, Ui("UI_Pedestal"));
            PlaceTop(pedestal.rectTransform, -470f, new Vector2(384f, 96f));
            var preview = NewImage("Preview", card.transform, null);
            preview.preserveAspect = true;
            // 16x20 ships at 20x, so every pixel stays a crisp square.
            PlaceTop(preview.rectTransform, -60f, new Vector2(320f, 400f));
            var animator = preview.gameObject.AddComponent<UiSpriteAnimator>();

            var name = NewText("Name", card.transform, title, 62, UiSkin.TitleColour);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 40; name.resizeTextMaxSize = 62;
            PlaceTop(name.rectTransform, -590f, new Vector2(640f, 84f));
            var divider = NewImage("Divider", card.transform, Ui("UI_Divider"));
            PlaceTop(divider.rectTransform, -684f, new Vector2(520f, 6f));

            // Strength and weakness each in their own recessed slot, with an up or down marker.
            var perk = StatRow("Perk", card.transform, -712f, "UI_StatUp", new Color(.45f, 1f, .6f), body);
            var drawback = StatRow("Drawback", card.transform, -836f, "UI_StatDown", new Color(1f, .55f, .5f), body);

            var counter = NewText("Counter", card.transform, body, 28, new Color(.6f, .8f, .9f));
            var counterRect = counter.rectTransform;
            counterRect.anchorMin = counterRect.anchorMax = counterRect.pivot = new Vector2(.5f, 0f);
            counterRect.anchoredPosition = new Vector2(0f, 26f);
            counterRect.sizeDelta = new Vector2(300f, 40f);

            // Glowing chevrons over the peeking neighbours, instead of boxed buttons.
            var previous = Chevron("PreviousButton", panel.transform, -418f, true);
            var next = Chevron("NextButton", panel.transform, 418f, false);

            var dotRow = new GameObject("Dots", typeof(RectTransform));
            dotRow.transform.SetParent(panel.transform, false);
            PlaceTop((RectTransform)dotRow.transform, -1190f, new Vector2(700f, 34f));
            var dotLayout = dotRow.AddComponent<HorizontalLayoutGroup>();
            dotLayout.childAlignment = TextAnchor.MiddleCenter;
            dotLayout.spacing = 22f;
            dotLayout.childControlWidth = false; dotLayout.childControlHeight = false;
            dotLayout.childForceExpandWidth = false; dotLayout.childForceExpandHeight = false;
            var dots = new Image[library.skins.Length];
            for (var i = 0; i < dots.Length; i++)
            {
                dots[i] = NewImage($"Dot{i}", dotRow.transform, Ui("UI_PipEmpty"));
                dots[i].rectTransform.sizeDelta = new Vector2(30f, 30f);
            }

            // Launch is the primary action: bigger, lit, and pulsing. Back is secondary.
            var back = NewButton("BackButton", panel.transform, "BACK", title);
            PlaceBottom((RectTransform)back.transform, new Vector2(-270f, 52f), new Vector2(280f, 100f));
            var launch = NewButton("LaunchButton", panel.transform, "LAUNCH", title);
            PlaceBottom((RectTransform)launch.transform, new Vector2(150f, 40f), new Vector2(440f, 124f));
            ((Image)launch.targetGraphic).sprite = Ui("UI_ButtonHover");
            launch.GetComponentInChildren<Text>().fontSize = 50;

            var hangar = overlay.AddComponent<SkinSelectPanel>();
            var serialized = new SerializedObject(hangar);
            serialized.FindProperty("root").objectReferenceValue = overlay;
            serialized.FindProperty("library").objectReferenceValue = library;
            serialized.FindProperty("card").objectReferenceValue = card.rectTransform;
            serialized.FindProperty("cardGroup").objectReferenceValue = cardGroup;
            serialized.FindProperty("preview").objectReferenceValue = animator;
            serialized.FindProperty("nameLabel").objectReferenceValue = name;
            serialized.FindProperty("perkLabel").objectReferenceValue = perk;
            serialized.FindProperty("drawbackLabel").objectReferenceValue = drawback;
            serialized.FindProperty("counterLabel").objectReferenceValue = counter;
            serialized.FindProperty("previousButton").objectReferenceValue = previous;
            serialized.FindProperty("nextButton").objectReferenceValue = next;
            serialized.FindProperty("swipe").objectReferenceValue = swipe;
            serialized.FindProperty("dotOn").objectReferenceValue = Ui("UI_Pip");
            serialized.FindProperty("dotOff").objectReferenceValue = Ui("UI_PipEmpty");
            serialized.FindProperty("previousPeek").objectReferenceValue = previousPeek;
            serialized.FindProperty("nextPeek").objectReferenceValue = nextPeek;
            serialized.FindProperty("floatingShip").objectReferenceValue = preview.rectTransform;
            serialized.FindProperty("glow").objectReferenceValue = glow;
            serialized.FindProperty("previousArrow").objectReferenceValue = previous.transform;
            serialized.FindProperty("nextArrow").objectReferenceValue = next.transform;
            serialized.FindProperty("launchPulse").objectReferenceValue = launch.transform;
            serialized.FindProperty("launchButton").objectReferenceValue = launch;
            serialized.FindProperty("backButton").objectReferenceValue = back;
            var dotArray = serialized.FindProperty("dots");
            dotArray.arraySize = dots.Length;
            for (var i = 0; i < dots.Length; i++) dotArray.GetArrayElementAtIndex(i).objectReferenceValue = dots[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The hangar follows the difficulty choice, so it sits above that screen.
            var difficulty = Object.FindFirstObjectByType<DifficultyPanel>(FindObjectsInactive.Include);
            if (difficulty != null)
            {
                var difficultySerialized = new SerializedObject(difficulty);
                difficultySerialized.FindProperty("hangar").objectReferenceValue = hangar;
                difficultySerialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else Debug.LogWarning("No difficulty screen in the main menu; the hangar has no way in.");
            overlay.transform.SetAsLastSibling();
            overlay.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- hangar dressing

        /// <summary>A dim neighbouring ship at the side of the carousel.</summary>
        private static UiSpriteAnimator Peek(string name, Transform parent, float x)
        {
            var image = NewImage(name, parent, null);
            image.preserveAspect = true;
            image.color = new Color(.55f, .65f, .8f, .35f);
            PlaceTop(image.rectTransform, -480f, new Vector2(128f, 160f));
            image.rectTransform.anchoredPosition = new Vector2(x, -480f);
            return image.gameObject.AddComponent<UiSpriteAnimator>();
        }

        /// <summary>A glowing pixel chevron that is itself the button.</summary>
        private static Button Chevron(string name, Transform parent, float x, bool left)
        {
            var image = NewImage(name, parent, Ui("UI_Chevron"));
            image.raycastTarget = true;
            image.preserveAspect = true;
            PlaceTop(image.rectTransform, -500f, new Vector2(96f, 132f));
            image.rectTransform.anchoredPosition = new Vector2(x, -500f);
            // One drawing serves both sides: the left arrow is the right one mirrored.
            if (left) image.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = Ui("UI_ChevronHover"), pressedSprite = Ui("UI_ChevronHover"), selectedSprite = Ui("UI_Chevron") };
            return button;
        }

        /// <summary>One stat line: a recessed slot, an up or down marker, and the text beside it.</summary>
        private static Text StatRow(string name, Transform card, float top, string marker, Color colour, Font font)
        {
            var slot = NewImage(name + "Slot", card, Ui("UI_Slot"));
            slot.type = Image.Type.Sliced;
            PlaceTop(slot.rectTransform, top, new Vector2(620f, 108f));
            var icon = NewImage("Marker", slot.transform, Ui(marker));
            icon.color = colour;
            icon.preserveAspect = true;
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, .5f);
            iconRect.anchoredPosition = new Vector2(26f, 0f);
            iconRect.sizeDelta = new Vector2(40f, 32f);
            var text = NewText(name, slot.transform, font, 36, colour);
            text.alignment = TextAnchor.MiddleLeft;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 26; text.resizeTextMaxSize = 36;
            var textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0f); textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(84f, 8f); textRect.offsetMax = new Vector2(-20f, -8f);
            return text;
        }

        /// <summary>Draws the hangar's pixel-art dressing into the UI folder.</summary>
        private static void GenerateHangarArt()
        {
            var edge = PixelCanvas.Hex(0x0c3040);
            var glow = PixelCanvas.Hex(0x7ff3ff);
            var light = PixelCanvas.Hex(0x48b4cc);
            var clear = new Color32(0, 0, 0, 0);
            Directory.CreateDirectory(UiFolder);

            // A thick chevron pointing right: dark rim, lit body, white-hot leading edge.
            foreach (var (file, body, core) in new[] { ("UI_Chevron", light, glow), ("UI_ChevronHover", glow, new Color32(255, 255, 255, 255)) })
            {
                var c = new PixelCanvas(16, 22, clear);
                for (var y = 0; y < 22; y++)
                {
                    var left = Mathf.RoundToInt(Mathf.Abs(y - 10.5f) * .9f);
                    for (var x = left - 1; x <= left + 5; x++)
                        c.Set(15 - x, y, x < left || x > left + 4 ? edge : x == left + 4 ? core : body);
                }
                File.WriteAllBytes($"{UiFolder}/{file}.png", c.Encode());
            }

            // Up and down markers, drawn white and tinted green or red in the UI.
            foreach (var (file, up) in new[] { ("UI_StatUp", true), ("UI_StatDown", false) })
            {
                var c = new PixelCanvas(9, 7, clear);
                for (var row = 0; row < 5; row++)
                    for (var x = 4 - row; x <= 4 + row; x++)
                        c.Set(x, up ? row + 1 : 5 - row, new Color32(255, 255, 255, 255));
                File.WriteAllBytes($"{UiFolder}/{file}.png", c.Encode());
            }

            // A hologram pedestal: a lit elliptical rim over a dark disc.
            var pedestal = new PixelCanvas(48, 12, clear);
            for (var y = 0; y < 12; y++)
            for (var x = 0; x < 48; x++)
            {
                var d = Mathf.Pow((x - 23.5f) / 23.5f, 2f) + Mathf.Pow((y - 5.5f) / 5.5f, 2f);
                if (d > 1f) continue;
                pedestal.Set(x, y, d > .72f ? (y < 6 ? glow : light) : d > .5f ? edge : new Color32(22, 26, 43, 230));
            }
            File.WriteAllBytes($"{UiFolder}/UI_Pedestal.png", pedestal.Encode());

            // A soft light in four stepped rings, so it stays pixel art rather than a smooth blur.
            var halo = new PixelCanvas(32, 32, clear);
            for (var y = 0; y < 32; y++)
            for (var x = 0; x < 32; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
                if (d >= 1f) continue;
                var step = Mathf.Floor((1f - d) * 4f) / 4f;
                halo.Set(x, y, new Color32(255, 255, 255, (byte)(step * 200f)));
            }
            File.WriteAllBytes($"{UiFolder}/UI_Glow.png", halo.Encode());

            // A glowing line that fades out at both ends.
            var line = new PixelCanvas(32, 3, clear);
            for (var x = 0; x < 32; x++)
            {
                var a = (byte)(255 * Mathf.Clamp01(1f - Mathf.Abs(x - 15.5f) / 16f));
                line.Set(x, 0, new Color32(glow.r, glow.g, glow.b, (byte)(a / 3)));
                line.Set(x, 1, new Color32(255, 255, 255, a));
                line.Set(x, 2, new Color32(glow.r, glow.g, glow.b, (byte)(a / 3)));
            }
            File.WriteAllBytes($"{UiFolder}/UI_Divider.png", line.Encode());
            AssetDatabase.Refresh();
        }

        // ---------------------------------------------------------------- game

        private static void WireGame(ShipSkinLibrary library)
        {
            var scene = EditorSceneManager.OpenScene("Assets/SpaceXonix/Scenes/Game.unity");
            library = AssetDatabase.LoadAssetAtPath<ShipSkinLibrary>(LibraryPath);
            var player = Object.FindFirstObjectByType<SpaceXonix.Player.PlayerController>(FindObjectsInactive.Include);
            var hud = Object.FindFirstObjectByType<GameHud>(FindObjectsInactive.Include);
            if (player == null) { Debug.LogWarning("No player in the game scene."); return; }
            var skin = player.GetComponent<PlayerShipSkin>() ?? player.gameObject.AddComponent<PlayerShipSkin>();
            var serialized = new SerializedObject(skin);
            serialized.FindProperty("library").objectReferenceValue = library;
            serialized.FindProperty("actorVisual").objectReferenceValue = player.GetComponent<ActorVisual>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (hud != null)
            {
                var hudSerialized = new SerializedObject(hud);
                hudSerialized.FindProperty("playerSkin").objectReferenceValue = skin;
                hudSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
            // The ship's stats reach the game through the upgrades and the campaign's starting lives.
            foreach (var target in new Object[]
            {
                Object.FindFirstObjectByType<SpaceXonix.Campaign.UpgradeManager>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<SpaceXonix.Campaign.CampaignManager>(FindObjectsInactive.Include)
            })
            {
                if (target == null) { Debug.LogWarning("Missing a campaign manager in the game scene."); continue; }
                var targetSerialized = new SerializedObject(target);
                targetSerialized.FindProperty("playerShip").objectReferenceValue = skin;
                targetSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- plumbing

        private static Sprite Ui(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/{name}.png");

        private static void Remove(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Text NewText(string name, Transform parent, Font font, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>A button in the menu's plating style, with its pressed state.</summary>
        private static Button NewButton(string name, Transform parent, string caption, Font font)
        {
            var image = NewImage(name, parent, Ui("UI_Button"));
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = Ui("UI_ButtonHover"), selectedSprite = Ui("UI_ButtonHover"),
                pressedSprite = Ui("UI_ButtonPressed"), disabledSprite = Ui("UI_ButtonDisabled")
            };
            var text = NewText("Text", image.transform, font, 40, Color.white);
            text.text = caption;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(text.rectTransform, 0f);
            return button;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void PlaceBottom(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
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
