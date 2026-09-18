using System.Collections.Generic;
using SpaceXonix.Board;
using SpaceXonix.Campaign;
using SpaceXonix.Enemies;
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
        [SerializeField] private CampaignManager campaignManager;
        [SerializeField] private UpgradeManager upgradeManager;
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
        private GUIStyle wrapStyle;
        private GUIStyle upgradeButtonStyle;
        private float awardShownAt = float.NegativeInfinity;
        private readonly List<EnemyType> enemyTypes = new List<EnemyType>();
        private float panelScale = 1f;
        private GUIStyle panelTitleStyle;

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
            if (campaignManager != null && campaignManager.Run != null)
            {
                DrawCampaign();
                return;
            }
            if (game.CurrentState == GameplayState.GameOver)
                GUI.Label(new Rect(0f, Screen.height * .4f, Screen.width, 120f), "GAME OVER", gameOverStyle);
            else if (game.CurrentState == GameplayState.StageComplete)
                DrawStageComplete();
        }

        private void DrawCampaign()
        {
            var run = campaignManager.Run;
            var stage = campaignManager.CurrentStage;
            GUI.Label(new Rect(0f, 20f, Screen.width, 50f), $"Stage {run.CurrentStageNumber}/{run.NormalStageCount}", rightCenteredStyle);
            if (campaignManager.Phase == CampaignPhase.Playing) return;
            var previousMatrix = GUI.matrix;
            panelScale = Mathf.Clamp(Mathf.Min(Screen.width / 900f, Screen.height / 640f), .35f, 2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(panelScale, panelScale, 1f));
            DrawCampaignPanel(run, stage);
            GUI.matrix = previousMatrix;
        }

        private void DrawCampaignPanel(CampaignRunModel run, StageDefinition stage)
        {
            switch (campaignManager.Phase)
            {
                case CampaignPhase.Briefing:
                    var panel = DrawPanel(820f, 620f);
                    GUI.Label(new Rect(panel.x, panel.y + 20f, panel.width, 80f), $"STAGE {stage.stageNumber}: {stage.stageName.ToUpperInvariant()}", panelTitleStyle);
                    GUI.Label(new Rect(panel.x + 40f, panel.y + 110f, panel.width - 80f, 120f), stage.briefing, wrapStyle);
                    GUI.Label(new Rect(panel.x + 40f, panel.y + 240f, panel.width - 80f, 44f), $"Enemies: {DescribeEnemies(stage)}", wrapStyle);
                    GUI.Label(new Rect(panel.x + 40f, panel.y + 290f, panel.width - 80f, 44f), $"Lasers: {(stage.lasers != null ? stage.lasers.Length : 0)}", wrapStyle);
                    GUI.Label(new Rect(panel.x + 40f, panel.y + 340f, panel.width - 80f, 100f),
                        $"Modifier: none
Upgrades: {(upgradeManager != null ? upgradeManager.Describe() : "none")}", wrapStyle);
                    if (GUI.Button(new Rect(panel.center.x - 160f, panel.yMax - 120f, 320f, 80f), "START [Enter]", buttonStyle) || EnterPressed())
                        campaignManager.StartStage();
                    break;
                case CampaignPhase.StageComplete:
                    panel = DrawPanel(820f, 500f);
                    GUI.Label(new Rect(panel.x, panel.y + 20f, panel.width, 90f), "STAGE COMPLETE", panelTitleStyle);
                    DrawStats(panel, $"Captured {campaignManager.StageCompletedPercentage:0.0}%",
                        $"Largest capture {(scoreManager != null ? scoreManager.StageLargestCapturePercentage : 0f):0.0}%",
                        "Modifier bonus x1.00");
                    if (GUI.Button(new Rect(panel.center.x - 160f, panel.yMax - 110f, 320f, 80f), "CONTINUE [Enter]", buttonStyle) || EnterPressed())
                        campaignManager.ContinueAfterStageComplete();
                    break;
                case CampaignPhase.UpgradeChoice:
                    DrawUpgradeChoice();
                    break;
                case CampaignPhase.GameOver:
                    panel = DrawPanel(820f, 460f);
                    GUI.Label(new Rect(panel.x, panel.y + 20f, panel.width, 90f), "GAME OVER", gameOverStyle);
                    DrawStats(panel, $"Highest stage {run.HighestStageReached}", null, null);
                    if (GUI.Button(new Rect(panel.center.x - 200f, panel.yMax - 110f, 400f, 80f), "RETRY CAMPAIGN [Enter]", buttonStyle) || EnterPressed())
                        campaignManager.RetryCampaign();
                    break;
                case CampaignPhase.NormalStagesCleared:
                    panel = DrawPanel(820f, 460f);
                    GUI.Label(new Rect(panel.x, panel.y + 20f, panel.width, 90f), "STAGES 1-4 CLEARED", panelTitleStyle);
                    DrawStats(panel, "Alien Core boss arrives in a later phase", null, null);
                    if (GUI.Button(new Rect(panel.center.x - 200f, panel.yMax - 110f, 400f, 80f), "RETRY CAMPAIGN [Enter]", buttonStyle) || EnterPressed())
                        campaignManager.RetryCampaign();
                    break;
            }
        }

        private void DrawUpgradeChoice()
        {
            var offer = upgradeManager.CurrentOffer;
            var panel = DrawPanel(880f, 200f + offer.Count * 120f);
            GUI.Label(new Rect(panel.x, panel.y + 20f, panel.width, 80f), "CHOOSE AN UPGRADE", panelTitleStyle);
            GUI.Label(new Rect(panel.x, panel.y + 100f, panel.width, 40f), "Lasts for this run only", rightCenteredStyle);
            var chosen = -1;
            var current = Event.current;
            for (var i = 0; i < offer.Count; i++)
            {
                var definition = offer[i];
                var stacks = upgradeManager.Run.GetStacks(definition.type);
                var label = $"{i + 1}. {definition.displayName}{(stacks > 0 ? $" (have {stacks})" : "")}\n{definition.effectText}";
                if (GUI.Button(new Rect(panel.x + 40f, panel.y + 150f + i * 120f, panel.width - 80f, 100f), label, upgradeButtonStyle)) chosen = i;
                if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Alpha1 + i) chosen = i;
            }
            if (chosen >= 0) campaignManager.ChooseUpgrade(chosen);
        }

        private Rect DrawPanel(float width, float height)
        {
            var virtualWidth = Screen.width / panelScale;
            var virtualHeight = Screen.height / panelScale;
            var panel = new Rect(virtualWidth * .5f - width * .5f, virtualHeight * .5f - height * .5f, width, height);
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .82f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;
            return panel;
        }

        private void DrawStats(Rect panel, string first, string second, string third)
        {
            var score = scoreManager != null ? scoreManager.Score : 0;
            GUI.Label(new Rect(panel.x, panel.y + 120f, panel.width, 50f), $"Score {score}", awardStyle);
            var y = panel.y + 180f;
            foreach (var line in new[] { first, second, third })
            {
                if (string.IsNullOrEmpty(line)) continue;
                GUI.Label(new Rect(panel.x, y, panel.width, 44f), line, rightCenteredStyle);
                y += 48f;
            }
        }

        private string DescribeEnemies(StageDefinition stage)
        {
            stage.GetEnemyTypes(enemyTypes);
            if (enemyTypes.Count == 0) return "none";
            var names = new string[enemyTypes.Count];
            for (var i = 0; i < enemyTypes.Count; i++) names[i] = enemyTypes[i] == EnemyType.BasicBouncer ? "Basic Bouncer" : enemyTypes[i] + " Alien";
            return string.Join(", ", names);
        }

        private static bool EnterPressed()
        {
            var current = Event.current;
            return current.type == EventType.KeyDown && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter);
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
            wrapStyle = new GUIStyle(livesStyle) { fontSize = 28, fontStyle = FontStyle.Normal, wordWrap = true };
            panelTitleStyle = new GUIStyle(stageCompleteStyle) { fontSize = 48 };
            upgradeButtonStyle = new GUIStyle(buttonStyle) { fontSize = 24, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(24, 24, 12, 12), wordWrap = true };
        }
    }
}
