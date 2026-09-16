using UnityEngine;

namespace SpaceXonix.Core
{
    [RequireComponent(typeof(GameManager))]
    public sealed class LifeStateDebugHud : MonoBehaviour
    {
        private GameManager game;
        private GUIStyle livesStyle;
        private GUIStyle gameOverStyle;

        private void Awake()
        {
            game = GetComponent<GameManager>();
        }

        private void OnGUI()
        {
            if (game == null) return;
            EnsureStyles();
            GUI.Label(new Rect(24f, 20f, 320f, 60f), $"Lives: {game.Lives}", livesStyle);
            if (game.CurrentState != GameplayState.GameOver) return;
            GUI.Label(new Rect(0f, Screen.height * .4f, Screen.width, 120f), "GAME OVER", gameOverStyle);
        }

        private void EnsureStyles()
        {
            if (livesStyle != null) return;
            livesStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            gameOverStyle = new GUIStyle(livesStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 64,
                normal = { textColor = new Color(1f, .2f, .2f) }
            };
        }
    }
}
