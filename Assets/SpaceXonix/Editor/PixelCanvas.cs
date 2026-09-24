using UnityEngine;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// A pixel grid addressed from the top-left, the way pixel art is drawn, for the art generated in
    /// the editor. Shared by the board and UI generators so both draw with the same conventions.
    /// </summary>
    internal sealed class PixelCanvas
    {
        private readonly Color32[] pixels;
        public readonly int Width;
        public readonly int Height;

        public PixelCanvas(int width, int height, Color32 fill)
        {
            Width = width; Height = height;
            pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = fill;
        }

        public static Color32 Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        public void Set(int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            // Textures store rows bottom-up; flip so drawing code can think top-down.
            pixels[(Height - 1 - y) * Width + x] = color;
        }

        public void Line(int x0, int y0, int x1, int y1, Color32 color)
        {
            for (var x = x0; x <= x1; x++) for (var y = y0; y <= y1; y++) Set(x, y, color);
        }

        public void Rect(int x, int y, int width, int height, Color32 color) => Line(x, y, x + width - 1, y + height - 1, color);

        /// <summary>A one-pixel outline of a rectangle.</summary>
        public void Frame(int x, int y, int width, int height, Color32 color)
        {
            Line(x, y, x + width - 1, y, color);
            Line(x, y + height - 1, x + width - 1, y + height - 1, color);
            Line(x, y, x, y + height - 1, color);
            Line(x + width - 1, y, x + width - 1, y + height - 1, color);
        }

        public byte[] Encode()
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            var bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return bytes;
        }
    }
}
