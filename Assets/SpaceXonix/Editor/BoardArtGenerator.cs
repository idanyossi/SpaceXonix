using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Draws the board's pixel art: the derelict ship deck you start on, the plating you claim it
    /// with, that plating's walls, the trail, and both laser states. These are patterns rather than
    /// characters, so they are drawn here in ansimuz's palette and at the sprites' 16 pixels per unit,
    /// which guarantees they match the art they sit under. Being generated in the project, they carry
    /// no licence.
    ///
    /// The design reads as taking over a ship: the deck is dull, worn slate; claimed territory is
    /// clean, raised and lit in the ship's own cyan.
    /// </summary>
    public static class BoardArtGenerator
    {
        public const string Folder = "Assets/SpaceXonix/Art/Generated";

        // Derelict deck: cold, dull and a little worn.
        private static readonly Color32 DeckBase = Hex(0x2a2f45);
        private static readonly Color32 DeckSeam = Hex(0x151827);
        private static readonly Color32 DeckLight = Hex(0x3b4260);
        private static readonly Color32 DeckShade = Hex(0x1f2336);
        private static readonly Color32 DeckRivet = Hex(0x4e5679);
        private static readonly Color32 DeckHazard = Hex(0x5c4e2a);

        // Claimed plating: clean, bright, and powered in the ship's cockpit cyan.
        private static readonly Color32 PlateBase = Hex(0x1f6f8b);
        private static readonly Color32 PlateSeam = Hex(0x0c3040);
        private static readonly Color32 PlateLight = Hex(0x48b4cc);
        private static readonly Color32 PlateShade = Hex(0x175469);
        private static readonly Color32 PlateRivet = Hex(0x8ad8e6);
        private static readonly Color32 Glow = Hex(0x7ff3ff);
        private static readonly Color32 Hot = Hex(0xdcffff);

        private static readonly Color32 LaserDark = Hex(0x7a1020);
        private static readonly Color32 LaserRed = Hex(0xff3040);
        private static readonly Color32 LaserCore = Hex(0xffe0e0);
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        [MenuItem("SpaceXonix/Generate Board Art")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            Write("DeckFloor", DeckFloor());
            Write("TerritoryTop", TerritoryTop());
            Write("TerritoryWall", TerritoryWall());
            Write("TerritoryRim", TerritoryRim());
            Write("TrailCell", TrailCell());
            Write("LaserBeam", LaserBeam());
            Write("LaserWarning", LaserWarning());
            AssetDatabase.Refresh();

            Assign("Board/SpaceFloor", "DeckFloor", false);
            Assign("Board/TerritoryTop", "TerritoryTop", false);
            Assign("Board/TerritoryWall", "TerritoryWall", false);
            EnsureMaterial("Board/TerritoryRim", "Board/TerritoryTop");
            Assign("Board/TerritoryRim", "TerritoryRim", false);
            Assign("Board/Trail", "TrailCell", false);
            Assign("Hazards/LaserBeam", "LaserBeam", false);
            Assign("Hazards/LaserWarning", "LaserWarning", true);
            AssetDatabase.SaveAssets();
            Debug.Log($"Generated the board art in {Folder} and assigned it to the board and laser materials.");
        }

        // ---------------------------------------------------------------- textures

        /// <summary>Four 16-pixel deck plates per tile, each different, so the floor does not read as a grid.</summary>
        private static PixelCanvas DeckFloor()
        {
            var c = new PixelCanvas(32, 32, DeckBase);
            for (var py = 0; py < 2; py++)
                for (var px = 0; px < 2; px++)
                    Plate(c, px * 16, py * 16, DeckSeam, DeckLight, DeckShade, DeckRivet);

            // Top right: a vent grille.
            for (var y = 5; y <= 10; y += 2) c.Line(20, y, 27, y, DeckSeam);
            // Bottom left: faded hazard stripes along one edge, worn half away.
            for (var x = 3; x <= 12; x++)
                if (((x + 13) / 2) % 2 == 0) { c.Set(x, 27, DeckHazard); if (x % 3 != 0) c.Set(x, 28, DeckHazard); }
            // Bottom right: scuffs and a dent, so the ship looks abandoned rather than new.
            c.Set(21, 20, DeckShade); c.Set(22, 20, DeckShade); c.Set(22, 21, DeckShade);
            c.Set(26, 25, DeckLight); c.Set(19, 26, DeckShade); c.Set(24, 22, DeckShade);
            // Top left: one missing rivet.
            c.Set(13, 13, DeckShade); c.Set(14, 14, DeckBase);
            return c;
        }

        /// <summary>The same plate layout as the deck, but clean and powered, so capturing reads as converting it.</summary>
        private static PixelCanvas TerritoryTop()
        {
            var c = new PixelCanvas(32, 32, PlateBase);
            for (var py = 0; py < 2; py++)
                for (var px = 0; px < 2; px++)
                    Plate(c, px * 16, py * 16, PlateSeam, PlateLight, PlateShade, PlateRivet);

            // Top right: a lit power strip.
            c.Line(19, 7, 28, 7, Glow); c.Line(19, 8, 28, 8, Glow);
            c.Set(19, 7, PlateSeam); c.Set(28, 8, PlateSeam);
            c.Line(20, 7, 27, 7, Hot);
            // Bottom left: a chevron, the mark of claimed territory.
            for (var i = 0; i < 4; i++) { c.Set(5 + i, 23 - i, Glow); c.Set(12 - i, 23 - i, Glow); c.Set(5 + i, 24 - i, PlateLight); c.Set(12 - i, 24 - i, PlateLight); }
            // Bottom right: a small status light.
            c.Rect(22, 22, 3, 3, PlateSeam); c.Set(23, 23, Glow);
            return c;
        }

        /// <summary>
        /// Walls only ever show their bottom six pixels (0.35 units at 16 per unit), so all the detail
        /// is there: dark at the floor, a lit edge at the top where the plating begins.
        /// </summary>
        private static PixelCanvas TerritoryWall()
        {
            var c = new PixelCanvas(32, 32, PlateShade);
            // Rows count from the top of the image; the wall's base is the bottom of the image.
            for (var x = 0; x < 32; x++)
            {
                c.Set(x, 31, PlateSeam);
                c.Set(x, 30, PlateSeam);
                c.Set(x, 29, x % 8 == 0 ? PlateLight : PlateShade);
                c.Set(x, 28, x % 8 == 0 ? PlateLight : PlateShade);
                c.Set(x, 27, PlateBase);
                c.Set(x, 26, Glow);
            }
            return c;
        }

        /// <summary>
        /// The trim along captured territory's exposed top edges, two pixels deep: a lit outer edge
        /// and a dark groove inside it, so every region ends on a finished edge instead of stopping
        /// mid-plate. Rows count from the top of the image, which is the inner side (v = 1).
        /// </summary>
        private static PixelCanvas TerritoryRim()
        {
            var c = new PixelCanvas(16, 2, PlateSeam);
            for (var x = 0; x < 16; x++) c.Set(x, 1, x % 8 == 3 ? Hot : Glow);
            return c;
        }

        /// <summary>
        /// One trail cell. The rim is mid cyan rather than dark, so neighbouring cells join into one
        /// continuous energy line instead of a string of beads, and each cell's hot core gives it a pulse.
        /// </summary>
        private static PixelCanvas TrailCell()
        {
            var c = new PixelCanvas(8, 8, Hex(0x2aa9c9));
            c.Rect(1, 1, 6, 6, Glow);
            c.Rect(3, 3, 2, 2, Hot);
            return c;
        }

        /// <summary>Across the beam: dark edges, red, then a white-hot core. Along it: bright pulses that scroll.</summary>
        private static PixelCanvas LaserBeam()
        {
            var c = new PixelCanvas(16, 8, LaserRed);
            for (var x = 0; x < 16; x++)
            {
                c.Set(x, 0, LaserDark); c.Set(x, 7, LaserDark);
                var pulse = x % 8 < 2;
                c.Set(x, 3, pulse ? Color.white : LaserCore);
                c.Set(x, 4, pulse ? Color.white : LaserCore);
                if (pulse) { c.Set(x, 2, LaserCore); c.Set(x, 5, LaserCore); }
            }
            return c;
        }

        /// <summary>A dashed red line with clear gaps, cut out rather than blended so the pixels stay hard.</summary>
        private static PixelCanvas LaserWarning()
        {
            var c = new PixelCanvas(16, 8, Clear);
            for (var x = 0; x < 10; x++) { c.Set(x, 3, LaserRed); c.Set(x, 4, LaserRed); }
            c.Set(0, 3, LaserDark); c.Set(9, 4, LaserDark);
            return c;
        }

        /// <summary>A bevelled plate: seam on the far edges, light on the near edges, rivets in the corners.</summary>
        private static void Plate(PixelCanvas c, int x0, int y0, Color32 seam, Color32 light, Color32 shade, Color32 rivet)
        {
            for (var i = 0; i < 16; i++)
            {
                c.Set(x0 + 15, y0 + i, seam); c.Set(x0 + i, y0 + 15, seam);
                if (i < 15) { c.Set(x0, y0 + i, light); c.Set(x0 + i, y0, light); }
                if (i > 0 && i < 15) { c.Set(x0 + 14, y0 + i, shade); c.Set(x0 + i, y0 + 14, shade); }
            }
            foreach (var (rx, ry) in new[] { (2, 2), (12, 2), (2, 12), (12, 12) })
            {
                c.Set(x0 + rx, y0 + ry, rivet);
                c.Set(x0 + rx + 1, y0 + ry + 1, seam);
            }
        }

        // ---------------------------------------------------------------- plumbing

        private static void Write(string name, PixelCanvas canvas) =>
            File.WriteAllBytes($"{Folder}/{name}.png", canvas.Encode());

        private static void Assign(string material, string texture, bool cutout)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/SpaceXonix/Materials/{material}.mat");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{texture}.png");
            if (mat == null || tex == null)
            {
                Debug.LogWarning($"Could not assign {texture} to {material}.");
                return;
            }
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            // White, so the texture shows its own colours instead of being tinted by the old flat one.
            mat.SetColor("_BaseColor", Color.white);
            mat.color = Color.white;
            mat.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            mat.SetFloat("_Cutoff", .5f);
            if (cutout) mat.EnableKeyword("_ALPHATEST_ON"); else mat.DisableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(mat);
        }

        /// <summary>Creates a material by copying a sibling, so it shares the shader and settings.</summary>
        private static void EnsureMaterial(string material, string copyFrom)
        {
            var path = $"Assets/SpaceXonix/Materials/{material}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            AssetDatabase.CopyAsset($"Assets/SpaceXonix/Materials/{copyFrom}.mat", path);
            AssetDatabase.ImportAsset(path);
        }

        private static Color32 Hex(int rgb) => PixelCanvas.Hex(rgb);
    }
}
