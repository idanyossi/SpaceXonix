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
        private const string FontFolder = "Assets/SpaceXonix/Art/ThirdParty/Kenney/Fonts";
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
            new SheetShip("cobalt-delta", "Cobalt Delta", 0, 7, new ShipStats
                { perk = "+25% speed off your territory", drawback = "-20% speed on your territory", exposedSpeed = 1.25f, safeSpeed = .8f }),
            new SheetShip("viper", "Viper", 1, 66, new ShipStats
                { perk = "+15% ship speed", drawback = "-20% power charge", speed = 1.15f, powerCharge = .8f }),
            new SheetShip("ember-talon", "Ember Talon", 2, 17, new ShipStats
                { perk = "+40% power charge", drawback = "Power Shot flies 35% slower", powerCharge = 1.4f, shotSpeed = .65f }),
            new SheetShip("solar-hornet", "Solar Hornet", 3, 78, new ShipStats
                { perk = "+50% power-up spawns", drawback = "Abilities last 25% shorter", pickupChance = 1.5f, abilityDuration = .75f }),
            new SheetShip("nebula-dart", "Nebula Dart", 4, 81, new ShipStats
                { perk = "+30% power-up spawns", drawback = "-15% power charge", pickupChance = 1.3f, powerCharge = .85f }),
            new SheetShip("phantom-rail", "Phantom Rail", 2, 69, new ShipStats
                { perk = "+1 extra life", drawback = "-12% ship speed", extraLives = 1, speed = .88f }),
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
            Place(panel.rectTransform, new Vector2(0f, 0f), new Vector2(1000f, 1700f));
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
            // was unreadable on a phone and on a PC.
            var card = NewImage("Card", panel.transform, Ui("UI_Card"));
            card.type = Image.Type.Sliced;
            card.raycastTarget = true;
            PlaceTop(card.rectTransform, -150f, new Vector2(640f, 1150f));
            var cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            var swipe = card.gameObject.AddComponent<HorizontalSwipe>();

            var screen = NewImage("Screen", card.transform, Ui("UI_Slot"));
            screen.type = Image.Type.Sliced;
            PlaceTop(screen.rectTransform, -40f, new Vector2(520f, 600f));
            var preview = NewImage("Preview", screen.transform, null);
            preview.preserveAspect = true;
            // 16x20 ships at 24x, so every pixel stays a crisp square.
            Place(preview.rectTransform, Vector2.zero, new Vector2(384f, 480f));
            var animator = preview.gameObject.AddComponent<UiSpriteAnimator>();

            var name = NewText("Name", card.transform, title, 60, UiSkin.TitleColour);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 36; name.resizeTextMaxSize = 60;
            PlaceTop(name.rectTransform, -670f, new Vector2(600f, 84f));
            var nameShadow = name.gameObject.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(.02f, .05f, .1f, .9f);
            nameShadow.effectDistance = new Vector2(4f, -4f);

            // The ship's strength in green and its weakness in red, big enough to read at a glance.
            var perk = NewText("Perk", card.transform, body, 40, new Color(.45f, 1f, .6f));
            perk.resizeTextForBestFit = true; perk.resizeTextMinSize = 30; perk.resizeTextMaxSize = 40;
            PlaceTop(perk.rectTransform, -775f, new Vector2(580f, 120f));
            var drawback = NewText("Drawback", card.transform, body, 40, new Color(1f, .55f, .5f));
            drawback.resizeTextForBestFit = true; drawback.resizeTextMinSize = 30; drawback.resizeTextMaxSize = 40;
            PlaceTop(drawback.rectTransform, -905f, new Vector2(580f, 120f));

            var counter = NewText("Counter", card.transform, body, 30, new Color(.6f, .8f, .9f));
            var counterRect = counter.rectTransform;
            counterRect.anchorMin = counterRect.anchorMax = counterRect.pivot = new Vector2(.5f, 0f);
            counterRect.anchoredPosition = new Vector2(0f, 34f);
            counterRect.sizeDelta = new Vector2(300f, 44f);

            // Big arrows either side of the card, the easiest target on a phone.
            var previous = NewButton("PreviousButton", panel.transform, "<", title);
            PlaceTop((RectTransform)previous.transform, -625f, new Vector2(150f, 200f));
            ((RectTransform)previous.transform).anchoredPosition = new Vector2(-410f, -625f);
            var next = NewButton("NextButton", panel.transform, ">", title);
            PlaceTop((RectTransform)next.transform, -625f, new Vector2(150f, 200f));
            ((RectTransform)next.transform).anchoredPosition = new Vector2(410f, -625f);
            foreach (var arrow in new[] { previous, next }) arrow.GetComponentInChildren<Text>().fontSize = 80;

            var dotRow = new GameObject("Dots", typeof(RectTransform));
            dotRow.transform.SetParent(panel.transform, false);
            PlaceTop((RectTransform)dotRow.transform, -1330f, new Vector2(700f, 30f));
            var dotLayout = dotRow.AddComponent<HorizontalLayoutGroup>();
            dotLayout.childAlignment = TextAnchor.MiddleCenter;
            dotLayout.spacing = 18f;
            dotLayout.childControlWidth = false; dotLayout.childControlHeight = false;
            dotLayout.childForceExpandWidth = false; dotLayout.childForceExpandHeight = false;
            var dots = new Image[library.skins.Length];
            for (var i = 0; i < dots.Length; i++)
            {
                dots[i] = NewImage($"Dot{i}", dotRow.transform, Ui("UI_PipEmpty"));
                dots[i].rectTransform.sizeDelta = new Vector2(28f, 28f);
            }

            var back = NewButton("BackButton", panel.transform, "BACK", body);
            PlaceBottom((RectTransform)back.transform, new Vector2(-200f, 40f), new Vector2(340f, 96f));
            var launch = NewButton("LaunchButton", panel.transform, "LAUNCH", body);
            PlaceBottom((RectTransform)launch.transform, new Vector2(200f, 40f), new Vector2(340f, 96f));

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
