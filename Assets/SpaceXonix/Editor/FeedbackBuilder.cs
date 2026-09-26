using System.IO;
using SpaceXonix.Board;
using SpaceXonix.Campaign;
using SpaceXonix.Core;
using SpaceXonix.Pooling;
using SpaceXonix.Power;
using SpaceXonix.PowerUps;
using SpaceXonix.Presentation;
using SpaceXonix.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Builds the second polish pass into the game: the shield bubble and its floor footprint, the
    /// arena's raised rim, the Power Shot's bolt, streak, muzzle flash and shockwave, and the run's
    /// upgrades on the HUD and in the pause menu. Generates its own pixel art. Re-running it rebuilds
    /// everything in place. SpaceXonix > Build Shield, Rim, Shot and Upgrade HUD.
    /// </summary>
    public static class FeedbackBuilder
    {
        private const string FxFolder = PixelArtImporter.FxFolder;
        private const string RimTexturePath = BoardArtGenerator.Folder + "/ArenaRimTop.png";
        private const string BoardMaterials = "Assets/SpaceXonix/Materials/Board";
        private const string PowerMaterials = "Assets/SpaceXonix/Materials/Power";
        private const string PowerUpMaterials = "Assets/SpaceXonix/Materials/PowerUps";
        private const string PowerPrefabs = "Assets/SpaceXonix/Prefabs/Power";
        private const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string UiFolder = "Assets/SpaceXonix/Art/Generated/UI";

        [MenuItem("SpaceXonix/Build Shield, Rim, Shot and Upgrade HUD")]
        public static void Build()
        {
            GenerateArt();
            BuildMaterials();
            BuildShotPrefabs();

            var scene = EditorSceneManager.OpenScene("Assets/SpaceXonix/Scenes/Game.unity");
            BuildShield();
            BuildRim();
            BuildShotPresenter();
            BuildUpgradeHud();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Built the shield bubble, the arena rim, the Power Shot effects and the upgrade HUD.");
        }

        // ---------------------------------------------------------------- art

        private static void GenerateArt()
        {
            Directory.CreateDirectory(FxFolder);
            for (var f = 0; f < 4; f++) Write($"{FxFolder}/ShieldBubble_{f}.png", Bubble(f, false));
            Write($"{FxFolder}/ShieldBubble_Hit.png", Bubble(0, true));
            for (var f = 0; f < 2; f++) Write($"{FxFolder}/PowerBolt_{f}.png", Bolt(f));
            for (var f = 0; f < 4; f++) Write($"{FxFolder}/MuzzleFlash_{f}.png", Muzzle(f));
            Write(RimTexturePath, RimTop());
            AssetDatabase.Refresh();
        }

        private static void Write(string path, PixelCanvas canvas) => File.WriteAllBytes(path, canvas.Encode());

        private static Color32 A(int rgb, int alpha)
        {
            var c = PixelCanvas.Hex(rgb);
            c.a = (byte)Mathf.Clamp(alpha, 0, 255);
            return c;
        }

        /// <summary>
        /// A 24-pixel energy bubble: a bright mint rim, a faint lattice inside, a highlight crescent at
        /// the top left, and a glint that sweeps across over the four frames. The hit frame is the same
        /// shape filled almost white.
        /// </summary>
        private static PixelCanvas Bubble(int frame, bool hit)
        {
            var canvas = new PixelCanvas(24, 24, new Color32(0, 0, 0, 0));
            const float c = 11.5f;
            var glint = frame * 6 - 7;
            for (var y = 0; y < 24; y++)
            for (var x = 0; x < 24; x++)
            {
                float dx = x - c, dy = y - c;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 11.6f) continue;
                if (hit)
                {
                    canvas.Set(x, y, d > 10.4f ? A(0xffffff, 255) : A(0xe8fff6, 150));
                    continue;
                }
                Color32 colour;
                if (d > 10.4f) colour = A(0xd8fff4, 250);
                else if (d > 9.2f) colour = A(0x6fffd0, 200);
                else
                {
                    // The fill thickens toward the edge, so the bubble reads as a sphere rather than a disc.
                    colour = A(0x3fe0b0, d > 7f ? 95 : 60);
                    // A faint hex-like lattice that crawls a little each frame.
                    if (y % 3 == 0 && (x + y * 2 + frame * 2) % 6 == 0) colour = A(0xa0ffe0, 130);
                    if (Mathf.Abs(x - y - glint) < 1.5f && d < 9f) colour = A(0xeafffa, 150);
                }
                // The highlight crescent, upper left (canvas y runs down).
                var angle = Mathf.Atan2(-dy, dx) * Mathf.Rad2Deg;
                if (d >= 6.5f && d <= 8.5f && angle >= 105f && angle <= 165f) colour = A(0xffffff, 215);
                canvas.Set(x, y, colour);
            }
            canvas.Set(7, 6, A(0xffffff, 240));
            return canvas;
        }

        /// <summary>A plasma bolt pointing up: white-hot core near the nose, cyan glow round it, sparks behind.</summary>
        private static PixelCanvas Bolt(int frame)
        {
            var canvas = new PixelCanvas(8, 16, new Color32(0, 0, 0, 0));
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 8; x++)
            {
                float nx = (x - 3.5f) / 3.6f, ny = (y - 7f) / 7.6f;
                var r = Mathf.Sqrt(nx * nx + ny * ny);
                if (r > 1f) continue;
                // The outer glow flickers: alternate frames drop alternate edge pixels.
                if (r > .78f && (x + y + frame) % 2 == 0) continue;
                float cx = (x - 3.5f) / 2.2f, cy = (y - 5f) / 4.4f;
                var core = Mathf.Sqrt(cx * cx + cy * cy);
                Color32 colour = r > .78f ? A(0x3fd8ff, 150) : A(0x9ff4ff, 235);
                if (core <= 1f) colour = A(0xffffff, 255);
                canvas.Set(x, y, colour);
            }
            // Sparks trailing behind.
            canvas.Set(frame == 0 ? 3 : 4, 15, A(0x9ff4ff, 200));
            canvas.Set(frame == 0 ? 4 : 3, 14, A(0x3fd8ff, 170));
            return canvas;
        }

        /// <summary>A four-frame muzzle flash: a white-hot star that blooms into a cyan ring and fades.</summary>
        private static PixelCanvas Muzzle(int frame)
        {
            var canvas = new PixelCanvas(16, 16, new Color32(0, 0, 0, 0));
            const float c = 7.5f;
            float[] ring = { 3.5f, 5f, 6.2f, 7.2f };
            float[] core = { 2.6f, 2.6f, 1.8f, 0f };
            int[] spike = { 5, 7, 7, 0 };
            int[] alpha = { 255, 235, 170, 110 };
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
            {
                float dx = x - c, dy = y - c;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (Mathf.Abs(d - ring[frame]) < .6f && (frame < 3 || (x + y) % 2 == 0)) canvas.Set(x, y, A(0x5ff0ff, alpha[frame]));
                if (d <= core[frame]) canvas.Set(x, y, frame < 2 ? A(0xffffff, 255) : A(0x9ff4ff, 190));
                // The star's spikes along the axes, and shorter ones on the diagonals at its peak.
                var onAxis = (Mathf.Abs(dx) < .6f || Mathf.Abs(dy) < .6f) && d <= spike[frame];
                var onDiagonal = frame == 1 && Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)) < .6f && d <= 4.5f;
                if (onAxis || onDiagonal) canvas.Set(x, y, d < 3f ? A(0xffffff, alpha[frame]) : A(0x9ff4ff, alpha[frame]));
            }
            return canvas;
        }

        /// <summary>
        /// The frame's top, 32 x 5, repeating along the frame. The bottom row meets the arena (UV v = 0):
        /// a lit bevel, then steel plate with seams, rivets and cyan running lights, then a dark outer edge.
        /// </summary>
        private static PixelCanvas RimTop()
        {
            var canvas = new PixelCanvas(32, 5, PixelCanvas.Hex(0x2b3d66));
            canvas.Line(0, 0, 31, 0, PixelCanvas.Hex(0x1a2644));
            canvas.Line(0, 1, 31, 1, PixelCanvas.Hex(0x26375c));
            canvas.Line(0, 4, 31, 4, PixelCanvas.Hex(0x6d8fbf));
            foreach (var seam in new[] { 15, 31 }) canvas.Line(seam, 1, seam, 3, PixelCanvas.Hex(0x16203a));
            foreach (var rivet in new[] { 2, 18 }) canvas.Set(rivet, 3, PixelCanvas.Hex(0x5a78a8));
            foreach (var light in new[] { 5, 21 }) canvas.Line(light, 2, light + 3, 2, PixelCanvas.Hex(0x7ff0ff));
            return canvas;
        }

        // ---------------------------------------------------------------- materials

        private static void BuildMaterials()
        {
            Directory.CreateDirectory(PowerMaterials);
            var rimTop = Unlit($"{BoardMaterials}/ArenaRimTop.mat", Color.white);
            rimTop.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RimTexturePath));
            EditorUtility.SetDirty(rimTop);
            Unlit($"{BoardMaterials}/ArenaRimLip.mat", new Color(.5f, .95f, 1f));

            var footprint = Unlit($"{PowerUpMaterials}/ShieldFootprint.mat", new Color(.45f, 1f, .8f, .5f));
            MakeTransparent(footprint, additive: false);

            var streak = Material($"{PowerMaterials}/PowerShotStreak.mat", "Universal Render Pipeline/Particles/Unlit");
            streak.SetColor("_BaseColor", Color.white);
            MakeTransparent(streak, additive: true);

            // The shockwave is the Volatile blast ring in the Power Shot's cyan.
            var shockwavePath = $"{PowerMaterials}/Shockwave.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(shockwavePath) == null)
                AssetDatabase.CopyAsset($"{BoardMaterials}/ExplosionRing.mat", shockwavePath);
            var shockwave = AssetDatabase.LoadAssetAtPath<Material>(shockwavePath);
            shockwave.SetColor("_BaseColor", new Color(.45f, .95f, 1f, 1f));
            EditorUtility.SetDirty(shockwave);
            AssetDatabase.SaveAssets();
        }

        private static Material Unlit(string path, Color colour)
        {
            var material = Material(path, "Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", colour);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Material(string path, string shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void MakeTransparent(Material material, bool additive)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
        }

        private static Sprite[] Sprites(string prefix, int count)
        {
            var sprites = new Sprite[count];
            for (var i = 0; i < count; i++) sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{FxFolder}/{prefix}_{i}.png");
            return sprites;
        }

        // ---------------------------------------------------------------- power shot prefabs

        private static void BuildShotPrefabs()
        {
            var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);

            // The bolt: a flickering plasma sprite with a fading streak behind it, in place of the old box.
            var shotPath = $"{PowerPrefabs}/PowerShot.prefab";
            var shot = PrefabUtility.LoadPrefabContents(shotPath);
            var visual = shot.transform.Find("Visual").gameObject;
            Object.DestroyImmediate(visual.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(visual.GetComponent<MeshFilter>());
            visual.transform.localScale = Vector3.one * .9f;
            var sprite = GetOrAdd<SpriteRenderer>(visual);
            var bolts = Sprites("PowerBolt", 2);
            sprite.sprite = bolts[0];
            sprite.sharedMaterial = spriteMaterial;
            sprite.sortingOrder = 3;
            var animator = GetOrAdd<SpriteFrameAnimator>(visual);
            Set(animator, "frames", bolts);
            SetFloat(animator, "framesPerSecond", 16f);
            var streakTransform = visual.transform.Find("Streak");
            if (streakTransform != null) Object.DestroyImmediate(streakTransform.gameObject);
            var streakObject = new GameObject("Streak");
            streakObject.transform.SetParent(visual.transform, false);
            var streak = streakObject.AddComponent<TrailRenderer>();
            streak.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{PowerMaterials}/PowerShotStreak.mat");
            streak.time = .16f;
            streak.minVertexDistance = .04f;
            streak.widthCurve = new AnimationCurve(new Keyframe(0f, .34f), new Keyframe(1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(.85f, 1f, 1f), 0f), new GradientColorKey(new Color(.25f, .8f, 1f), 1f) },
                new[] { new GradientAlphaKey(.9f, 0f), new GradientAlphaKey(0f, 1f) });
            streak.colorGradient = gradient;
            streak.shadowCastingMode = ShadowCastingMode.Off;
            streak.receiveShadows = false;
            streak.alignment = LineAlignment.View;
            streak.sortingOrder = 2;
            Set(shot.GetComponent<PowerShotProjectile>(), "streak", streak);
            PrefabUtility.SaveAsPrefabAsset(shot, shotPath);
            PrefabUtility.UnloadPrefabContents(shot);

            // The muzzle flash is a copy of the explosion burst with its own frames.
            var muzzlePath = $"{PowerPrefabs}/MuzzleFlash.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(muzzlePath) == null)
                AssetDatabase.CopyAsset("Assets/SpaceXonix/Prefabs/Presentation/Explosion.prefab", muzzlePath);
            var muzzle = PrefabUtility.LoadPrefabContents(muzzlePath);
            var burst = muzzle.GetComponent<SpriteBurst>();
            Set(burst, "frames", Sprites("MuzzleFlash", 4));
            SetFloat(burst, "framesPerSecond", 22f);
            muzzle.GetComponent<SpriteRenderer>().sortingOrder = 4;
            PrefabUtility.SaveAsPrefabAsset(muzzle, muzzlePath);
            PrefabUtility.UnloadPrefabContents(muzzle);

            // The shockwave is a copy of the Volatile blast ring, cyan and quicker.
            var shockwavePath = $"{PowerPrefabs}/Shockwave.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(shockwavePath) == null)
                AssetDatabase.CopyAsset("Assets/SpaceXonix/Prefabs/Presentation/ExplosionRing.prefab", shockwavePath);
            var shockwave = PrefabUtility.LoadPrefabContents(shockwavePath);
            shockwave.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{PowerMaterials}/Shockwave.mat");
            SetFloat(shockwave.GetComponent<ExplosionRing>(), "durationSeconds", .3f);
            SetFloat(shockwave.GetComponent<ExplosionRing>(), "startAlpha", .45f);
            PrefabUtility.SaveAsPrefabAsset(shockwave, shockwavePath);
            PrefabUtility.UnloadPrefabContents(shockwave);
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- scene

        private static void BuildShield()
        {
            var powerUps = Object.FindFirstObjectByType<PowerUpManager>(FindObjectsInactive.Include);
            var shield = (GameObject)new SerializedObject(powerUps).FindProperty("shieldVisual").objectReferenceValue;
            // The old flat ring on the root becomes the footprint on the floor.
            var ringMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/SpaceXonix/Meshes/ShieldRing.asset");
            Object.DestroyImmediate(shield.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(shield.GetComponent<MeshFilter>());
            foreach (var name in new[] { "Bubble", "HitGlow", "Footprint" }) RemoveChild(shield.transform, name);

            var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            var frames = Sprites("ShieldBubble", 4);
            var bubble = NewSprite("Bubble", shield.transform, frames[0], spriteMaterial, 6);
            var animator = bubble.gameObject.AddComponent<SpriteFrameAnimator>();
            Set(animator, "frames", frames);
            SetFloat(animator, "framesPerSecond", 9f);
            var hitGlow = NewSprite("HitGlow", bubble.transform, AssetDatabase.LoadAssetAtPath<Sprite>($"{FxFolder}/ShieldBubble_Hit.png"), spriteMaterial, 7);
            hitGlow.enabled = false;

            var footprint = new GameObject("Footprint");
            footprint.transform.SetParent(shield.transform, false);
            footprint.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            var footprintRenderer = footprint.AddComponent<MeshRenderer>();
            footprintRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{PowerUpMaterials}/ShieldFootprint.mat");
            footprintRenderer.shadowCastingMode = ShadowCastingMode.Off;
            footprintRenderer.receiveShadows = false;

            var presenter = GetOrAdd<ShieldBubble>(shield);
            Set(presenter, "powerUpManager", powerUps);
            Set(presenter, "gameManager", Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include));
            Set(presenter, "boardManager", Object.FindFirstObjectByType<BoardManager>(FindObjectsInactive.Include));
            Set(presenter, "bubble", bubble);
            Set(presenter, "hitGlow", hitGlow);
            Set(presenter, "footprint", footprintRenderer);
            SetFloat(presenter, "footprintMeshRadius", ringMesh != null ? ringMesh.bounds.extents.x : .88f);
        }

        private static void BuildRim()
        {
            var board = Object.FindFirstObjectByType<BoardRenderer>(FindObjectsInactive.Include);
            RemoveChild(board.transform, "ArenaRim");
            var rimObject = new GameObject("ArenaRim", typeof(MeshFilter), typeof(MeshRenderer));
            rimObject.transform.SetParent(board.transform, false);
            var rim = rimObject.AddComponent<ArenaRim>();
            Set(rim, "topMaterial", AssetDatabase.LoadAssetAtPath<Material>($"{BoardMaterials}/ArenaRimTop.mat"));
            Set(rim, "wallMaterial", new SerializedObject(board).FindProperty("territoryWallMaterial").objectReferenceValue);
            Set(rim, "lipMaterial", AssetDatabase.LoadAssetAtPath<Material>($"{BoardMaterials}/ArenaRimLip.mat"));
            Set(board, "arenaRim", rim);
        }

        private static void BuildShotPresenter()
        {
            var meter = Object.FindFirstObjectByType<PowerMeter>(FindObjectsInactive.Include);
            var pool = Object.FindFirstObjectByType<PoolService>(FindObjectsInactive.Include);
            var explosions = Object.FindFirstObjectByType<SpriteBurstPresenter>(FindObjectsInactive.Include);
            var existing = GameObject.Find("PowerShotFx");
            if (existing != null) Object.DestroyImmediate(existing);
            var host = new GameObject("PowerShotFx");

            var muzzles = host.AddComponent<SpriteBurstPresenter>();
            Set(muzzles, "poolService", pool);
            Set(muzzles, "explosionPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{PowerPrefabs}/MuzzleFlash.prefab"));
            var shockwaves = host.AddComponent<ExplosionRingPresenter>();
            Set(shockwaves, "poolService", pool);
            Set(shockwaves, "ringPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{PowerPrefabs}/Shockwave.prefab"));

            var presenter = host.AddComponent<PowerShotPresenter>();
            Set(presenter, "powerMeter", meter);
            Set(presenter, "muzzleFlashes", muzzles);
            Set(presenter, "explosions", explosions);
            Set(presenter, "shockwaves", shockwaves);
            Set(presenter, "shaker", Object.FindFirstObjectByType<ArenaShaker>(FindObjectsInactive.Include));
            // A shot kill blows the alien apart harder than before.
            SetFloat(explosions, "enemyExplosionScale", 3f);
        }

        private static void BuildUpgradeHud()
        {
            var hud = Object.FindFirstObjectByType<GameHud>(FindObjectsInactive.Include);
            var safeArea = hud.transform.root.Find("SafeArea") ?? hud.transform;
            var upgrades = Object.FindFirstObjectByType<UpgradeManager>(FindObjectsInactive.Include);
            var body = AssetDatabase.LoadAssetAtPath<Font>($"{UiSkin.FontFolder}/Exo2-SemiBold.ttf");
            var bold = AssetDatabase.LoadAssetAtPath<Font>($"{UiSkin.FontFolder}/Exo2-Bold.ttf");
            var slot = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/UI_Slot.png");

            // HUD: a row of badges under the score and capture percentage, top right.
            RemoveChild(safeArea, "UpgradeStrip");
            var strip = NewRect("UpgradeStrip", safeArea);
            strip.anchorMin = strip.anchorMax = strip.pivot = Vector2.one;
            strip.anchoredPosition = new Vector2(-36f, -160f);
            strip.sizeDelta = new Vector2(420f, 72f);
            var badge = NewRect("Badge", strip);
            badge.anchorMin = badge.anchorMax = badge.pivot = Vector2.one;
            badge.anchoredPosition = Vector2.zero;
            badge.sizeDelta = new Vector2(72f, 72f);
            var frame = badge.gameObject.AddComponent<Image>();
            frame.sprite = slot;
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;
            var icon = NewRect("Icon", badge);
            icon.sizeDelta = new Vector2(56f, 56f);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            var count = NewText("Count", badge, body, 24, TextAnchor.LowerRight, Color.white);
            count.rectTransform.anchorMin = Vector2.zero; count.rectTransform.anchorMax = Vector2.one;
            count.rectTransform.offsetMin = new Vector2(0f, -6f); count.rectTransform.offsetMax = new Vector2(4f, 0f);
            count.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, .9f);
            badge.gameObject.SetActive(false);
            var stripView = strip.gameObject.AddComponent<UpgradeStrip>();
            Set(stripView, "upgradeManager", upgrades);
            Set(stripView, "badgeTemplate", badge);

            // Pause menu: a panel under the buttons listing what each upgrade does.
            var pause = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            var overlay = ((GameObject)new SerializedObject(pause).FindProperty("panelRoot").objectReferenceValue).transform;
            var pausePanel = overlay.Find("Panel").GetComponent<Image>();
            RemoveChild(overlay, "UpgradesPanel");
            var panel = NewRect("UpgradesPanel", overlay);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.pivot = new Vector2(.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -400f);
            panel.sizeDelta = new Vector2(760f, 216f);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = pausePanel.sprite;
            panelImage.type = pausePanel.type;
            panelImage.color = pausePanel.color;

            var heading = NewText("Heading", panel, bold, 36, TextAnchor.MiddleCenter, new Color(.5f, .95f, 1f));
            TopStretch(heading.rectTransform, 22f, 56f, 24f);
            heading.text = "UPGRADES THIS RUN";
            heading.gameObject.AddComponent<Shadow>();

            var empty = NewText("Empty", panel, body, 30, TextAnchor.MiddleCenter, new Color(.75f, .85f, 1f));
            TopStretch(empty.rectTransform, 96f, 96f, 24f);
            empty.text = "None yet. Clear a stage to pick one.";

            var row = NewRect("Row", panel);
            TopStretch(row, 96f, 96f, 28f);
            var rowIcon = NewRect("Icon", row);
            rowIcon.anchorMin = rowIcon.anchorMax = new Vector2(0f, .5f);
            rowIcon.pivot = new Vector2(0f, .5f);
            rowIcon.anchoredPosition = Vector2.zero;
            rowIcon.sizeDelta = new Vector2(72f, 72f);
            var rowIconImage = rowIcon.gameObject.AddComponent<Image>();
            rowIconImage.preserveAspect = true;
            rowIconImage.raycastTarget = false;
            var name = NewText("Name", row, bold, 34, TextAnchor.LowerLeft, Color.white);
            Band(name.rectTransform, .5f, 1f, 92f, 110f);
            var effect = NewText("Effect", row, body, 28, TextAnchor.UpperLeft, new Color(.8f, .9f, 1f));
            Band(effect.rectTransform, 0f, .5f, 92f, 110f);
            var stacks = NewText("Count", row, bold, 40, TextAnchor.MiddleRight, new Color(1f, .87f, .3f));
            stacks.rectTransform.anchorMin = new Vector2(1f, 0f); stacks.rectTransform.anchorMax = Vector2.one;
            stacks.rectTransform.pivot = new Vector2(1f, .5f);
            stacks.rectTransform.anchoredPosition = Vector2.zero;
            stacks.rectTransform.sizeDelta = new Vector2(100f, 0f);
            foreach (var text in new[] { name, effect, stacks }) text.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, .8f);
            row.gameObject.SetActive(false);

            var list = panel.gameObject.AddComponent<UpgradeList>();
            Set(list, "upgradeManager", upgrades);
            Set(list, "panel", panel);
            Set(list, "rowTemplate", row);
            Set(list, "emptyLabel", empty);
        }

        // ---------------------------------------------------------------- plumbing

        private static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, Material material, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Text NewText(string name, Transform parent, Font font, int size, TextAnchor anchor, Color colour)
        {
            var rect = NewRect(name, parent);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = colour;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>Stretches across the parent's width (less a side margin) at a fixed distance from its top.</summary>
        private static void TopStretch(RectTransform rect, float fromTop, float height, float side)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -fromTop);
            rect.sizeDelta = new Vector2(-side * 2f, height);
        }

        /// <summary>A horizontal band of the parent between two heights, indented from both sides.</summary>
        private static void Band(RectTransform rect, float bottom, float top, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, bottom);
            rect.anchorMax = new Vector2(1f, top);
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(-right, 0f);
        }

        private static void RemoveChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
