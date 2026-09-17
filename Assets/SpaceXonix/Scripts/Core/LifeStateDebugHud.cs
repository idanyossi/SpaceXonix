using SpaceXonix.Board;
using SpaceXonix.Scoring;
using UnityEngine;

namespace SpaceXonix.Core
{
    [RequireComponent(typeof(GameManager))]
    public sealed class LifeStateDebugHud : MonoBehaviour
    {
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private BoardManager boardManager;
        [SerializeField, Min(0f)] private float captureAwardDisplaySeconds = 2f;

        private GameManager game;
        private GUIStyle livesStyle;
        private GUIStyle rightStyle;
        private GUIStyle awardStyle;
        private GUIStyle gameOverStyle;
        private float awardShownAt = float.NegativeInfinity;

        private void Awake()
        {
            game = GetComponent<GameManager>();
        }

        private void OnEnable()
        {
            if (scoreManager != null) scoreManager.CaptureScored += OnCaptureScored;
        }

        private void OnDisable()
        {
            if (scoreManager != null) scoreManager.CaptureScored -= OnCaptureScored;
        }

        private void OnCaptureScored(CaptureScoreAward award) => awardShownAt = Time.unscaledTime;

        private void OnGUI()
        {
            if (game == null) return;
            EnsureStyles();
            GUI.Label(new Rect(24f, 20f, 320f, 60f), $"Lives: {game.Lives}", livesStyle);
            if (scoreManager != null)
                GUI.Label(new Rect(Screen.width - 424f, 20f, 400f, 60f), $"Score: {scoreManager.Score}", rightStyle);
            if (boardManager != null)
                GUI.Label(new Rect(Screen.width - 424f, 64f, 400f, 60f), $"{boardManager.CapturedPercentage:0.0}%", rightStyle);
            DrawLastAward();
            if (game.CurrentState != GameplayState.GameOver) return;
            GUI.Label(new Rect(0f, Screen.height * .4f, Screen.width, 120f), "GAME OVER", gameOverStyle);
        }

        private void DrawLastAward()
        {
            if (scoreManager == null || !scoreManager.HasLastAward) return;
            if (Time.unscaledTime - awardShownAt > captureAwardDisplaySeconds) return;
            var award = scoreManager.LastAward;
            var text = award.CaptureMultiplier > 1f
                ? $"+{award.Points}  x{award.CaptureMultiplier:0.#}"
                : $"+{award.Points}";
            GUI.Label(new Rect(0f, Screen.height * .25f, Screen.width, 80f), text, awardStyle);
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
            rightStyle = new GUIStyle(livesStyle) { alignment = TextAnchor.UpperRight };
            awardStyle = new GUIStyle(livesStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 48,
                normal = { textColor = new Color(1f, .85f, .2f) }
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
