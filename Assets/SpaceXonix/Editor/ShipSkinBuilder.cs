using System.Collections.Generic;
using System.IO;
using SpaceXonix.Presentation;
using SpaceXonix.UI;
using UnityEditor;
using UnityEditor.Events;
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
        private const string FontFolder = "Assets/SpaceXonix/Art/ThirdParty/Kenney/Fonts";
        private const int Cell = 16;
        private const int FlameRows = 4;

        /// <summary>A Master484 ship: its colour group on the sheet (0-4) and its cell (row-major, 5 per row).</summary>
        private readonly struct SheetShip
        {
            public readonly string Id, Name;
            public readonly int Group, Index;
            public SheetShip(string id, string name, int group, int index) { Id = id; Name = name; Group = group; Index = index; }
        }

        private static readonly SheetShip[] Ships =
        {
            new SheetShip("cobalt-delta", "Cobalt Delta", 0, 7),
            new SheetShip("viper", "Viper", 1, 66),
            new SheetShip("ember-talon", "Ember Talon", 2, 17),
            new SheetShip("solar-hornet", "Solar Hornet", 3, 78),
            new SheetShip("nebula-dart", "Nebula Dart", 4, 81),
            new SheetShip("phantom-rail", "Phantom Rail", 2, 69),
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
                    new[] { PixelArtImporter.LoadFrame("ship", 2), PixelArtImporter.LoadFrame("ship", 7) }, 12f)
            };
            foreach (var ship in Ships)
                skins.Add(Skin(ship.Id, ship.Name, new[]
                {
                    AssetDatabase.LoadAssetAtPath<Sprite>($"{FrameFolder}/{ship.Id}_0.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>($"{FrameFolder}/{ship.Id}_1.png")
                }, 12f));

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

        private static ShipSkinDefinition Skin(string id, string name, Sprite[] frames, float fps)
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
            Place(panel.rectTransform, new Vector2(0f, 0f), new Vector2(1000f, 1500f));

            var heading = NewText("Title", panel.transform, title, 58, UiSkin.TitleColour);
            heading.text = "CHOOSE YOUR SHIP";
            heading.resizeTextForBestFit = true; heading.resizeTextMinSize = 29; heading.resizeTextMaxSize = 58;
            PlaceTop(heading.rectTransform, -40f, new Vector2(900f, 80f));
            var shadow = heading.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.02f, .05f, .1f, .9f);
            shadow.effectDistance = new Vector2(4f, -4f);

            var grid = new GameObject("Grid", typeof(RectTransform));
            grid.transform.SetParent(panel.transform, false);
            PlaceTop((RectTransform)grid.transform, -150f, new Vector2(912f, 1150f));
            var layout = grid.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(280f, 330f);
            layout.spacing = new Vector2(36f, 30f);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;

            var tiles = new SkinSelectPanel.Tile[library.skins.Length];
            for (var i = 0; i < tiles.Length; i++) tiles[i] = BuildTile(grid.transform, i, body, title);

            var back = NewButton("BackButton", panel.transform, "BACK", body);
            var backRect = (RectTransform)back.transform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(.5f, 0f);
            backRect.pivot = new Vector2(.5f, 0f);
            backRect.anchoredPosition = new Vector2(0f, 40f);
            backRect.sizeDelta = new Vector2(360f, 90f);

            var hangar = overlay.AddComponent<SkinSelectPanel>();
            var serialized = new SerializedObject(hangar);
            serialized.FindProperty("root").objectReferenceValue = overlay;
            serialized.FindProperty("library").objectReferenceValue = library;
            serialized.FindProperty("tileFrame").objectReferenceValue = Ui("UI_Card");
            serialized.FindProperty("equippedFrame").objectReferenceValue = Ui("UI_CardHover");
            var array = serialized.FindProperty("tiles");
            array.arraySize = tiles.Length;
            for (var i = 0; i < tiles.Length; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("button").objectReferenceValue = tiles[i].button;
                element.FindPropertyRelative("frame").objectReferenceValue = tiles[i].frame;
                element.FindPropertyRelative("preview").objectReferenceValue = tiles[i].preview;
                element.FindPropertyRelative("nameLabel").objectReferenceValue = tiles[i].nameLabel;
                element.FindPropertyRelative("equippedBadge").objectReferenceValue = tiles[i].equippedBadge;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(back.onClick, hangar.Hide);

            // The button that opens it, in the top-right corner of the menu.
            var open = NewButton("SkinsButton", safeArea, "SKINS", body);
            var openRect = (RectTransform)open.transform;
            openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(1f, 1f);
            openRect.anchoredPosition = new Vector2(-36f, -36f);
            openRect.sizeDelta = new Vector2(230f, 96f);
            UnityEventTools.AddPersistentListener(open.onClick, hangar.Open);
            // Opened from the menu only; the overlay sits above everything else in the canvas.
            overlay.transform.SetAsLastSibling();
            overlay.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static SkinSelectPanel.Tile BuildTile(Transform grid, int index, Font body, Font title)
        {
            var frame = NewImage($"Tile{index}", grid, Ui("UI_Card"));
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = true;
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = Ui("UI_CardHover"), pressedSprite = Ui("UI_CardPressed"), selectedSprite = Ui("UI_CardHover") };

            var screen = NewImage("Screen", frame.transform, Ui("UI_Slot"));
            screen.type = Image.Type.Sliced;
            PlaceTop(screen.rectTransform, -22f, new Vector2(200f, 210f));
            var preview = NewImage("Preview", screen.transform, null);
            preview.preserveAspect = true;
            // 16x20 ships at 8x, so every pixel stays a crisp square.
            Place(preview.rectTransform, Vector2.zero, new Vector2(128f, 160f));
            var animator = preview.gameObject.AddComponent<UiSpriteAnimator>();

            var name = NewText("Name", frame.transform, body, 26, Color.white);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 14; name.resizeTextMaxSize = 26;
            var nameRect = name.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f); nameRect.anchorMax = new Vector2(1f, 0f); nameRect.pivot = new Vector2(.5f, 0f);
            nameRect.offsetMin = new Vector2(14f, 52f); nameRect.offsetMax = new Vector2(-14f, 92f);

            var badge = NewImage("Equipped", frame.transform, Ui("UI_CardBand"));
            badge.type = Image.Type.Sliced;
            badge.color = new Color(.5f, .95f, 1f);
            var badgeRect = badge.rectTransform;
            badgeRect.anchorMin = badgeRect.anchorMax = badgeRect.pivot = new Vector2(.5f, 0f);
            badgeRect.anchoredPosition = new Vector2(0f, 14f);
            badgeRect.sizeDelta = new Vector2(190f, 34f);
            var badgeText = NewText("Label", badge.transform, body, 18, new Color(.03f, .04f, .08f));
            badgeText.text = "EQUIPPED";
            Stretch(badgeText.rectTransform, 0f);
            badge.gameObject.SetActive(false);

            return new SkinSelectPanel.Tile { button = button, frame = frame, preview = animator, nameLabel = name, equippedBadge = badge.gameObject };
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

        private static void PlaceTop(RectTransform rect, float top, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, top);
            rect.sizeDelta = size;
        }
    }
}
