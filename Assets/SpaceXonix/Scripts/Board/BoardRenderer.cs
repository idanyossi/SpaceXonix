using UnityEngine;

namespace SpaceXonix.Board
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public sealed class BoardRenderer : MonoBehaviour
    {
        [SerializeField] private Color uncapturedColor = new Color(0.025f, 0.04f, 0.10f);
        [SerializeField] private Color capturedColor = new Color(0.08f, 0.55f, 0.78f);
        [SerializeField] private Color trailColor = new Color(1f, 0.67f, 0.12f);

        private Texture2D stateTexture;
        private Color[] pixels;
        private int width;
        private int height;

        public void Initialize(BoardManager board)
        {
            width = board.Columns; height = board.Rows;
            stateTexture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            pixels = new Color[width * height];
            var mesh = new Mesh { name = "BoardDebugMesh", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { Vector3.zero, new Vector3(width * board.CellWorldSize, 0f), new Vector3(0f, height * board.CellWorldSize), new Vector3(width * board.CellWorldSize, height * board.CellWorldSize) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave };
            renderer.sharedMaterial.mainTexture = stateTexture;
            Refresh(board.Model);
        }

        public void Refresh(BoardModel model)
        {
            if (stateTexture == null) return;
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var state = model.GetCell(new GridCoordinate(x, y));
                pixels[x + y * width] = state == BoardCellState.Captured ? capturedColor : state == BoardCellState.Trail ? trailColor : uncapturedColor;
            }
            stateTexture.SetPixels(pixels); stateTexture.Apply(false, false);
        }
    }
}
