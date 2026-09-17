using SpaceXonix.Board;
using SpaceXonix.Power;
using SpaceXonix.PowerUps;
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
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField, Min(0f)] private float captureAwardDisplaySeconds = 2f;

        private GameManager game;
        private GUIStyle livesStyle;
        private GUIStyle rightStyle;
        private GUIStyle awardStyle;
        private GUIStyle gameOverStyle;
        private GUIStyle stageCompleteStyle;
        private GUIStyle awardStyleLeft;
        private GUIStyle rightCenteredStyle;
        private GUIStyle buttonStyle;
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
            DrawPowerUps();
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

        private void DrawPowerUps()
        {
            if (powerUpManager == null) return;
            var stored = powerUpManager.StoredPowerUp;
            var slotText = stored.HasValue ? $"Ability: {Name(stored.Value)} [E]" : "Ability: -";
            GUI.Label(new Rect(24f, 64f, 520f, 44f), slotText, livesStyle);
            var line = 108f;
            foreach (PowerUpType type in System.Enum.GetValues(typeof(PowerUpType)))
            {
                if (!powerUpManager.IsEffectActive(type)) continue;
                GUI.Label(new Rect(24f, line, 520f, 44f), $"{Name(type)} {powerUpManager.GetEffectRemaining(type):0.0}s", awardStyleLeft);
                line += 40f;
            }
            if (!powerUpManager.IsAwaitingDecision) return;
            DrawPickupDecision(powerUpManager.PendingOffer.Value, stored);
        }

        private void DrawPickupDecision(PowerUpType offered, PowerUpType? stored)
        {
            var panel = new Rect(Screen.width * .5f - 300f, Screen.height * .5f - 130f, 600f, 260f);
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .8f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;
            var storedName = stored.HasValue ? Name(stored.Value) : "-";
            GUI.Label(new Rect(panel.x, panel.y + 16f, panel.width, 60f), $"Picked up {Name(offered)}", awardStyle);
            GUI.Label(new Rect(panel.x, panel.y + 76f, panel.width, 50f), $"Stored: {storedName}", rightCenteredStyle);
            var keep = GUI.Button(new Rect(panel.x + 40f, panel.y + 160f, 240f, 70f), $"Keep {storedName} [K]", buttonStyle);
            var replace = GUI.Button(new Rect(panel.xMax - 280f, panel.y + 160f, 240f, 70f), $"Take {Name(offered)} [R]", buttonStyle);
            var current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.K) keep = true;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.R) replace = true;
            if (keep) powerUpManager.ResolvePickupDecision(false);
            else if (replace) powerUpManager.ResolvePickupDecision(true);
        }

        private static string Name(PowerUpType type) => type == PowerUpType.ArenaTilt ? "Arena Tilt" : type.ToString();

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
            awardStyleLeft = new GUIStyle(livesStyle) { fontSize = 28, normal = { textColor = new Color(.6f, 1f, 1f) } };
            rightCenteredStyle = new GUIStyle(livesStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 30 };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
        }
    }
}
