using SpaceXonix.Presentation;
using SpaceXonix.PowerUps;
using UnityEditor;
using UnityEngine;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Swaps each actor's placeholder mesh for its pixel-art sprite. Kept as a command, not a
    /// one-off edit, so the art mapping is written down in one place and can be re-run whenever
    /// the frames are rebuilt.
    ///
    /// Every sprite is scaled so its width equals the actor's collision diameter. That keeps the
    /// rule the hitboxes were built on: what you see is exactly what can hit you.
    /// </summary>
    public static class PixelArtSpriteApplier
    {
        private const float PixelsPerUnit = 16f;

        private struct Mapping
        {
            public string prefab;
            public string sheet;
            public int[] frames;
            public float fps;
            public float diameter;
            public Color tint;
            public bool shipHeading;

            public Mapping(string prefab, string sheet, int[] frames, float fps, float diameter, Color tint, bool shipHeading = false)
            {
                this.prefab = prefab; this.sheet = sheet; this.frames = frames; this.fps = fps;
                this.diameter = diameter; this.tint = tint; this.shipHeading = shipHeading;
            }
        }

        /// <summary>
        /// Three alien silhouettes cover four alien types, so the Unstable and Volatile aliens are
        /// separated by tint: electric blue for the erratic one, orange for the one that explodes.
        /// </summary>
        private static Mapping[] Mappings => new[]
        {
            new Mapping("Gameplay/Player", "ship", new[] { 2, 7 }, 12f, .75f, Color.white, shipHeading: true),
            new Mapping("Enemies/BasicBouncer", "enemy-small", new[] { 0, 1 }, 4f, 1f, Color.white),
            new Mapping("Enemies/LinearAlien", "enemy-medium", new[] { 0, 1 }, 4f, 1f, Color.white),
            new Mapping("Enemies/UnstableAlien", "enemy-big", new[] { 0, 1 }, 6f, 1f, new Color(.55f, .85f, 1f)),
            new Mapping("Enemies/VolatileAlien", "enemy-small", new[] { 0, 1 }, 8f, .28f, new Color(1f, .65f, .3f)),
            new Mapping("PowerUps/PowerUpPickup", "power-up", new[] { 0, 1 }, 6f, .5f, Color.white),
        };

        [MenuItem("SpaceXonix/Apply Pixel Art Sprites")]
        public static void Apply()
        {
            var applied = 0;
            foreach (var mapping in Mappings)
                if (ApplyToPrefab(mapping)) applied++;
            ApplyToPickupDefinitions();
            ApplyToBossProjectile();
            AssetDatabase.SaveAssets();
            Debug.Log($"Applied pixel-art sprites to {applied} actor prefabs, the boss projectile and the pickup definitions.");
        }

        /// <summary>Sets whether sprite actors billboard toward the camera or lie flat on the board.</summary>
        public static void SetBillboarding(bool faceCamera)
        {
            foreach (var mapping in Mappings)
            {
                var path = $"Assets/SpaceXonix/Prefabs/{mapping.prefab}.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                var actor = root.GetComponent<ActorVisual>();
                if (actor != null)
                {
                    var serialized = new SerializedObject(actor);
                    serialized.FindProperty("faceCamera").boolValue = faceCamera;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool ApplyToPrefab(Mapping mapping)
        {
            var path = $"Assets/SpaceXonix/Prefabs/{mapping.prefab}.prefab";
            var frames = LoadFrames(mapping.sheet, mapping.frames);
            if (frames == null)
            {
                Debug.LogWarning($"Frames for '{mapping.sheet}' are missing; run Rebuild Pixel Art Frames first.");
                return false;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var actor = root.GetComponent<ActorVisual>();
                var visual = actor != null && actor.Visual != null ? actor.Visual : root.transform.GetChild(0);
                ReplaceWithSprite(visual.gameObject, frames, mapping.fps, mapping.tint);
                var widthInUnits = frames[0].rect.width / PixelsPerUnit;
                var scale = mapping.diameter / widthInUnits;
                visual.localScale = new Vector3(scale, scale, scale);

                if (mapping.shipHeading && root.GetComponent<ShipHeading>() == null) root.AddComponent<ShipHeading>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The boss projectile scales its own visual to its radius at launch, so only the sprite is
        /// set here. A 16-pixel frame is exactly one unit, which is what that scaling assumes.
        /// </summary>
        private static void ApplyToBossProjectile()
        {
            const string path = "Assets/SpaceXonix/Prefabs/Boss/BossProjectile.prefab";
            var frames = LoadFrames("laser-bolts", new[] { 1 });
            if (frames == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = root.transform.childCount > 0 ? root.transform.GetChild(0) : null;
                if (visual == null) return;
                ReplaceWithSprite(visual.gameObject, frames, 1f, Color.white);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Two orb designs serve three power-ups: the warm orb is the Shield, and the silver orb is
        /// tinted icy blue for Freeze and gold for Arena Tilt.
        /// </summary>
        private static void ApplyToPickupDefinitions()
        {
            Configure("Shield", new[] { 0, 1 }, Color.white);
            Configure("Freeze", new[] { 2, 3 }, new Color(.6f, .9f, 1f));
            Configure("ArenaTilt", new[] { 2, 3 }, new Color(1f, .85f, .45f));
        }

        private static void Configure(string name, int[] frames, Color tint)
        {
            var definition = AssetDatabase.LoadAssetAtPath<PowerUpDefinition>($"Assets/SpaceXonix/ScriptableObjects/PowerUps/{name}.asset");
            var sprites = LoadFrames("power-up", frames);
            if (definition == null || sprites == null) return;
            definition.pickupFrames = sprites;
            definition.pickupTint = tint;
            EditorUtility.SetDirty(definition);
        }

        private static void ReplaceWithSprite(GameObject visual, Sprite[] frames, float fps, Color tint)
        {
            var meshRenderer = visual.GetComponent<MeshRenderer>();
            if (meshRenderer != null) Object.DestroyImmediate(meshRenderer);
            var meshFilter = visual.GetComponent<MeshFilter>();
            if (meshFilter != null) Object.DestroyImmediate(meshFilter);

            var sprite = visual.GetComponent<SpriteRenderer>();
            if (sprite == null) sprite = visual.AddComponent<SpriteRenderer>();
            sprite.sprite = frames[0];
            sprite.color = tint;

            var animator = visual.GetComponent<SpriteFrameAnimator>();
            if (animator == null) animator = visual.AddComponent<SpriteFrameAnimator>();
            animator.SetFrames(frames, fps);
        }

        private static Sprite[] LoadFrames(string sheet, int[] indices)
        {
            var sprites = new Sprite[indices.Length];
            for (var i = 0; i < indices.Length; i++)
            {
                sprites[i] = PixelArtImporter.LoadFrame(sheet, indices[i]);
                if (sprites[i] == null) return null;
            }
            return sprites;
        }
    }
}
