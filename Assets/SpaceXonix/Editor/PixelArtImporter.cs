using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Keeps pixel art crisp and cuts sprite sheets into individual frames.
    ///
    /// Every texture under the third-party art folder is imported with point filtering, no
    /// compression and no mipmaps, because any one of those would smear a 16-pixel sprite into mush.
    /// Sheets are cut into one PNG per frame rather than sliced, which avoids the deprecated
    /// spritesheet API and needs no 2D Sprite package.
    /// </summary>
    public sealed class PixelArtImporter : AssetPostprocessor
    {
        public const string ArtRoot = "Assets/SpaceXonix/Art/ThirdParty";
        public const string FramesFolder = ArtRoot + "/Ansimuz/Frames";
        /// <summary>Backdrop layers tile across the view, so they import like the board textures, not as sprites.</summary>
        public const string BackgroundFolder = ArtRoot + "/Ansimuz/Background";
        private const int PixelsPerUnit = 16;

        /// <summary>Each sheet's frame size, read left to right and top to bottom.</summary>
        private static readonly (string Sheet, int Width, int Height)[] Sheets =
        {
            ("ship", 16, 24),
            ("enemy-small", 16, 16),
            ("enemy-medium", 32, 16),
            ("enemy-big", 32, 32),
            ("explosion", 16, 16),
            ("laser-bolts", 16, 16),
            ("power-up", 16, 16),
        };

        private void OnPreprocessTexture()
        {
            if (UiSkin.IsUiSprite(assetPath))
            {
                // Menu widgets are nine-sliced sprites: their frames keep their size however big the panel.
                var ui = (TextureImporter)assetImporter;
                ui.textureType = TextureImporterType.Sprite;
                ui.spriteImportMode = SpriteImportMode.Single;
                ui.spritePixelsPerUnit = PixelsPerUnit;
                ui.filterMode = FilterMode.Point;
                ui.textureCompression = TextureImporterCompression.Uncompressed;
                ui.mipmapEnabled = false;
                ui.alphaIsTransparency = true;
                ui.wrapMode = TextureWrapMode.Clamp;
                var border = UiSkin.BorderFor(System.IO.Path.GetFileNameWithoutExtension(assetPath));
                ui.spriteBorder = new Vector4(border, border, border, border);
                return;
            }
            if (assetPath.StartsWith(BoardArtGenerator.Folder) || assetPath.StartsWith(BackgroundFolder))
            {
                // Board, laser and backdrop textures tile across meshes, so they repeat and are not sprites,
                // but they are still pixel art and must stay crisp.
                var tiling = (TextureImporter)assetImporter;
                tiling.textureType = TextureImporterType.Default;
                tiling.filterMode = FilterMode.Point;
                tiling.textureCompression = TextureImporterCompression.Uncompressed;
                tiling.mipmapEnabled = false;
                tiling.alphaIsTransparency = true;
                // The feature planets are single objects: repeating them would bleed a row of pixels
                // from the opposite edge onto their borders.
                var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                var single = assetPath.StartsWith(BackgroundFolder) && (name == "BigPlanet" || name == "RingPlanet");
                tiling.wrapMode = single ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                // The territory trim repeats along its edge but spans its depth exactly once, so its
                // inner edge must not wrap round to the lit outer row.
                if (name == "TerritoryRim") tiling.wrapModeV = TextureWrapMode.Clamp;
                return;
            }
            if (!assetPath.StartsWith(ArtRoot)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        [MenuItem("SpaceXonix/Rebuild Pixel Art Frames")]
        public static void RebuildFrames()
        {
            Directory.CreateDirectory(FramesFolder);
            var written = 0;
            foreach (var (sheet, frameWidth, frameHeight) in Sheets)
                written += CutSheet(sheet, frameWidth, frameHeight);
            AssetDatabase.Refresh();
            Debug.Log($"Cut {written} pixel-art frames into {FramesFolder}.");
        }

        /// <summary>Loads a sheet's frame by name and index, e.g. ("ship", 2).</summary>
        public static Sprite LoadFrame(string sheet, int index) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{FramesFolder}/{sheet}_{index}.png");

        private static int CutSheet(string sheet, int frameWidth, int frameHeight)
        {
            var sourcePath = $"{ArtRoot}/Ansimuz/{sheet}.png";
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"Missing sprite sheet {sourcePath}.");
                return 0;
            }
            // Read the file directly: the imported texture is not CPU-readable, and does not need to be.
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(sourcePath));
            var columns = source.width / frameWidth;
            var rows = source.height / frameHeight;
            var index = 0;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    // Texture rows count from the bottom, frames are numbered from the top.
                    var y = source.height - (row + 1) * frameHeight;
                    var frame = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false);
                    frame.SetPixels(source.GetPixels(column * frameWidth, y, frameWidth, frameHeight));
                    frame.Apply();
                    File.WriteAllBytes($"{FramesFolder}/{sheet}_{index}.png", frame.EncodeToPNG());
                    Object.DestroyImmediate(frame);
                    index++;
                }
            }
            Object.DestroyImmediate(source);
            return index;
        }
    }
}
