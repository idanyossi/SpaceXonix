using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Recolours ansimuz's Space Background into the game's navy-to-cyan palette. The original is
    /// magenta and clashed with the teal deck and UI; the user liked the art itself, so it is kept
    /// pixel for pixel and only its colours change.
    ///
    /// Each pixel's brightness is mapped onto one ramp (a gradient map). A plain hue rotation was
    /// tried first: it turned the nebula blue but the orange-to-red big planet violet, which still
    /// clashed. The map reads the untouched originals from AssetSources, so it can be re-run with a
    /// different ramp at any time. SpaceXonix > Recolor Backdrop.
    /// </summary>
    public static class BackdropRecolor
    {
        private const string SourceFolder = "AssetSources/ansimuz_space_background/space_background_pack/layers";
        private const string TargetFolder = PixelArtImporter.BackgroundFolder;

        /// <summary>Darkest to brightest: deep space, navy nebula, steel blue, the deck's teal, then cyan light.</summary>
        public static readonly Color32[] Ramp =
        {
            PixelCanvas.Hex(0x06060f), PixelCanvas.Hex(0x0d1430), PixelCanvas.Hex(0x1b2b5a),
            PixelCanvas.Hex(0x2a5a8a), PixelCanvas.Hex(0x43a3c4), PixelCanvas.Hex(0xaef0ff)
        };

        /// <summary>The art is dark; its brightest pixels sit near this, so they are stretched to the top of the ramp.</summary>
        private const float BrightestSource = .75f;

        private static readonly (string Source, string Target)[] Layers =
        {
            ("parallax-space-backgound", "Backdrop"),
            ("parallax-space-stars", "Stars"),
            ("parallax-space-far-planets", "FarPlanets"),
            ("parallax-space-big-planet", "BigPlanet"),
            ("parallax-space-ring-planet", "RingPlanet"),
        };

        [MenuItem("SpaceXonix/Recolor Backdrop")]
        public static void Recolor()
        {
            var written = 0;
            foreach (var (source, target) in Layers)
            {
                var path = $"{SourceFolder}/{source}.png";
                if (!File.Exists(path)) { Debug.LogWarning($"Missing original backdrop layer {path}."); continue; }
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(File.ReadAllBytes(path));
                var pixels = texture.GetPixels32();
                for (var i = 0; i < pixels.Length; i++) pixels[i] = Map(pixels[i]);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes($"{TargetFolder}/{target}.png", texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                written++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"Recoloured {written} backdrop layers into the navy-to-cyan palette.");
        }

        /// <summary>Maps one pixel onto the ramp by its brightness, keeping its alpha. Public for tests.</summary>
        public static Color32 Map(Color32 pixel)
        {
            if (pixel.a == 0) return pixel;
            var luminance = (pixel.r * .3f + pixel.g * .59f + pixel.b * .11f) / 255f;
            var t = Mathf.Clamp01(luminance / BrightestSource) * (Ramp.Length - 1);
            var index = Mathf.Min(Ramp.Length - 2, (int)t);
            Color32 mapped = Color.Lerp(Ramp[index], Ramp[index + 1], t - index);
            mapped.a = pixel.a;
            return mapped;
        }
    }
}
