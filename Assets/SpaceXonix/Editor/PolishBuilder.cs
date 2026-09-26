using System.IO;
using SpaceXonix.Board;
using SpaceXonix.Boss;
using SpaceXonix.Enemies;
using SpaceXonix.Presentation;
using SpaceXonix.PowerUps;
using SpaceXonix.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Builds the Phase 20 polish into the game scene: the capture flash's material, the Freeze frost
    /// border, the Arena Tilt chevrons, the boss destruction flash, and their presenters. Re-running
    /// it rebuilds everything in place. SpaceXonix > Build Polish Effects.
    /// </summary>
    public static class PolishBuilder
    {
        private const string UiFolder = "Assets/SpaceXonix/Art/Generated/UI";
        private const string TextureFolder = "Assets/SpaceXonix/Art/Generated";
        private const string FlashMaterialPath = "Assets/SpaceXonix/Materials/Board/CaptureFlash.mat";

        [MenuItem("SpaceXonix/Build Polish Effects")]
        public static void Build()
        {
            GenerateArt();
            var flashMaterial = BuildFlashMaterial();

            var scene = EditorSceneManager.OpenScene("Assets/SpaceXonix/Scenes/Game.unity");
            flashMaterial = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
            var board = Object.FindFirstObjectByType<BoardRenderer>(FindObjectsInactive.Include);
            Set(board, "flashMaterial", flashMaterial);

            var hud = Object.FindFirstObjectByType<GameHud>(FindObjectsInactive.Include);
            var canvas = hud.GetComponentInParent<Canvas>(true).rootCanvas.transform;
            // "FrostOverlay" was the first name; anything ending in Overlay is treated as a screen by UiSkin.
            foreach (var name in new[] { "FrostOverlay", "FrostBorder", "TiltStreaks", "BossFlash" }) Remove(canvas, name);

            // Frost and chevrons sit behind the HUD's text; the boss flash covers everything.
            var frost = NewImage("FrostBorder", canvas, AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/UI_Frost.png"));
            frost.type = Image.Type.Sliced;
            Stretch(frost.rectTransform);
            frost.color = new Color(1f, 1f, 1f, 0f);
            frost.enabled = false;
            frost.transform.SetAsFirstSibling();

            var streaks = new GameObject("TiltStreaks", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RawImage>();
            streaks.transform.SetParent(canvas, false);
            streaks.raycastTarget = false;
            streaks.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/UI_TiltStreaks.png");
            streaks.color = new Color(1f, .85f, .45f, 0f);
            var streakRect = streaks.rectTransform;
            streakRect.anchorMin = new Vector2(1f, 0f); streakRect.anchorMax = new Vector2(1f, 1f);
            streakRect.sizeDelta = new Vector2(150f, 0f);
            // Chevrons 50 units square, stacked down the whole height.
            streaks.uvRect = new Rect(0f, 0f, 3f, 1920f / 50f);
            streaks.enabled = false;
            streaks.transform.SetSiblingIndex(1);

            var flash = NewImage("BossFlash", canvas, null);
            Stretch(flash.rectTransform);
            flash.color = new Color(1f, 1f, 1f, 0f);
            flash.enabled = false;
            flash.transform.SetAsLastSibling();

            // The presenters live beside the explosion presenter the boss sequence uses.
            var bursts = Object.FindFirstObjectByType<SpriteBurstPresenter>(FindObjectsInactive.Include);
            var host = bursts.gameObject;
            var powerUps = Object.FindFirstObjectByType<PowerUpManager>(FindObjectsInactive.Include);
            var enemies = Object.FindFirstObjectByType<EnemyManager>(FindObjectsInactive.Include);
            var boss = Object.FindFirstObjectByType<BossController>(FindObjectsInactive.Include);
            var shaker = Object.FindFirstObjectByType<ArenaShaker>(FindObjectsInactive.Include);

            var freeze = host.GetComponent<FreezePresenter>() ?? host.AddComponent<FreezePresenter>();
            Set(freeze, "powerUpManager", powerUps);
            Set(freeze, "enemyManager", enemies);
            Set(freeze, "frostOverlay", frost);

            var tilt = host.GetComponent<TiltPresenter>() ?? host.AddComponent<TiltPresenter>();
            Set(tilt, "powerUpManager", powerUps);
            Set(tilt, "streaks", streaks);

            var sequence = host.GetComponent<BossDeathSequence>() ?? host.AddComponent<BossDeathSequence>();
            Set(sequence, "boss", boss);
            Set(sequence, "explosions", bursts);
            Set(sequence, "shaker", shaker);
            Set(sequence, "flash", flash);

            var screens = Object.FindFirstObjectByType<CampaignScreens>(FindObjectsInactive.Include);
            Set(screens, "bossSequence", sequence);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Built the capture flash, Freeze and Arena Tilt presentation, and the boss destruction sequence.");
        }

        // ---------------------------------------------------------------- art

        private static void GenerateArt()
        {
            Directory.CreateDirectory(UiFolder);
            var clear = new Color32(0, 0, 0, 0);

            // A frost border: icy at the very edge, thinning inward in dithered steps, with jagged
            // crystals reaching in, so the middle of the screen stays clear. Nine-sliced (border 12).
            var frost = new PixelCanvas(32, 32, clear);
            var random = new System.Random(11);
            for (var y = 0; y < 32; y++)
            for (var x = 0; x < 32; x++)
            {
                var edge = Mathf.Min(Mathf.Min(x, 31 - x), Mathf.Min(y, 31 - y));
                var reach = 5 + (((x * 7 + y * 13) ^ (x * y)) % 5);
                if (edge >= reach) continue;
                var strength = 1f - edge / (float)reach;
                if (strength < .5f && (x + y) % 2 == 1) continue;
                var alpha = (byte)(Mathf.Floor(strength * 4f) / 4f * 230f + 25f);
                var tone = random.Next(3) == 0 ? new Color32(255, 255, 255, alpha) : new Color32(160, 225, 255, alpha);
                frost.Set(x, y, tone);
            }
            File.WriteAllBytes($"{UiFolder}/UI_Frost.png", frost.Encode());

            // One chevron pointing right (">"), white, for the Arena Tilt band to tint and repeat. Clear
            // rows above and below keep the stacked chevrons apart, so they read as arrows, not a zigzag.
            var chevron = new PixelCanvas(16, 16, clear);
            for (var y = 3; y <= 12; y++)
            {
                var right = 12 - Mathf.RoundToInt(Mathf.Abs(y - 7.5f) * 1.3f);
                for (var x = right - 3; x <= right; x++) chevron.Set(x, y, new Color32(255, 255, 255, 255));
            }
            File.WriteAllBytes($"{TextureFolder}/UI_TiltStreaks.png", chevron.Encode());
            AssetDatabase.Refresh();
        }

        /// <summary>An additive, unlit, see-through material, so the capture flash brightens what is under it.</summary>
        private static Material BuildFlashMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, FlashMaterialPath);
            }
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(.55f, 1f, 1f, .6f));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        // ---------------------------------------------------------------- plumbing

        private static void Set(Object target, string field, Object value)
        {
            if (target == null) { Debug.LogWarning($"Missing a component to set {field} on."); return; }
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
