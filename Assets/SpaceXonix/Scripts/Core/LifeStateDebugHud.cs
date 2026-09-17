using SpaceXonix.Board;
using SpaceXonix.Power;
using SpaceXonix.Scoring;
using UnityEngine;

namespace SpaceXonix.Core
{
    [RequireComponent(typeof(GameManager))]
    public sealed class LifeStateDebugHud : MonoBehaviour
    {
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private PowerMeter powerMeter;
        [SerializeField, Min(0f)] private float captureAwardDisplaySeconds = 2f;

        private GameManager game;
        private GUIStyle livesStyle;
        private GUIStyle rightStyle;
        private GUIStyle awardStyle;
        private GUIStyle gameOverStyle;
        private GUIStyle stageCompleteStyle;
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
            DrawPowerMeter();
            DrawLastAward();
            if (game.CurrentState == GameplayState.GameOver)
                GUI.Label(new Rect(0f, Screen.height * .4f, Screen.width, 120f), "GAME OVER", gameOverStyle);
            else if (game.CurrentState == GameplayState.StageComplete)
                DrawStageComplete();
        }

        private void DrawPowerMeter()
        {
            if (powerMeter == null) return;
            const float width = 400f;
            const float height = 26f;
            var frame = new Rect(Screen.width - 424f, 116f, width, height);
            var fill = powerMeter.MaxPower > 0f ? Mathf.Clamp01(powerMeter.Power / powerMeter.MaxPower) : 0f;
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .6f);
            GUI.DrawTexture(frame, Texture2D.whiteTexture);
            GUI.color = powerMeter.IsReady ? new Color(.2f, 1f, 1f) : new Color(.2f, .6f, 1f);
            GUI.DrawTexture(new Rect(frame.x, frame.y, frame.width * fill, frame.height), Texture2D.whiteTexture);
            GUI.color = previous;
            var label = powerMeter.IsReady ? "POWER READY [SPACE]" : $"Power {Mathf.FloorToInt(powerMeter.Power)}";
            GUI.Label(new Rect(frame.x, frame.yMax + 2f, width, 44f), label, rightStyle);
        }

        private void DrawStageComplete()
        {
            GUI.Label(new Rect(0f, Screen.height * .4f, Screen.width, 120f), "STAGE COMPLETE", stageCompleteStyle);
            if (scoreManager == null) return;
            GUI.Label(new Rect(0f, Screen.height * .4f + 110f, Screen.width, 60f),
                $"Score {scoreManager.Score}   Largest capture {scoreManager.LargestCapturePercentage:0.0}%", awardStyle);
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
            stageCompleteStyle = new GUIStyle(gameOverStyle) { normal = { textColor = new Color(.3f, 1f, .5f) } };
        }
    }
}
