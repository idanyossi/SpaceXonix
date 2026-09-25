using SpaceXonix.Board;
using SpaceXonix.Campaign;
using SpaceXonix.Core;
using SpaceXonix.Power;
using SpaceXonix.PowerUps;
using SpaceXonix.Scoring;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// The gameplay HUD: lives, score, capture percentage, power meter, the one stored ability and
    /// any running effect timers. Deliberately quiet during play, as the GDD asks, so it only shows
    /// the capture award briefly and otherwise stays out of the way.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private PowerMeter powerMeter;
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private CampaignManager campaignManager;

        [Header("Labels")]
        [SerializeField] private Text livesLabel;
        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text captureLabel;
        [SerializeField] private Text stageLabel;
        [SerializeField] private Text modifierLabel;
        [SerializeField] private Text abilityLabel;
        [SerializeField] private Text effectsLabel;
        [SerializeField] private Text awardLabel;
        [SerializeField] private Text powerLabel;

        [Header("Lives")]
        [Tooltip("One ship icon per life, left to right. Lives beyond the row show as +N in the lives label.")]
        [SerializeField] private Image[] lifeIcons = new Image[0];

        [Header("Power meter")]
        [Tooltip("The animated energy gauge. When set it replaces the plain fill image below.")]
        [SerializeField] private PowerGauge powerGauge;
        [SerializeField] private Image powerFill;
        [SerializeField] private Color powerChargingColor = new Color(.2f, .6f, 1f);
        [SerializeField] private Color powerReadyColor = new Color(.2f, 1f, 1f);

        [SerializeField, Min(0f)] private float captureAwardDisplaySeconds = 2f;

        private float awardShownAt = float.NegativeInfinity;

        private void OnEnable()
        {
            if (scoreManager != null) scoreManager.CaptureScored += OnCaptureScored;
        }

        private void OnDisable()
        {
            if (scoreManager != null) scoreManager.CaptureScored -= OnCaptureScored;
        }

        private void OnCaptureScored(CaptureScoreAward award) => awardShownAt = Time.unscaledTime;

        private void LateUpdate() => Refresh();

        /// <summary>Pulls every displayed value. Public so tests can drive one frame deterministically.</summary>
        public void Refresh()
        {
            RefreshLives();
            if (scoreLabel != null && scoreManager != null) scoreLabel.text = scoreManager.Score.ToString();
            if (captureLabel != null && boardManager != null) captureLabel.text = $"{boardManager.CapturedPercentage:0.0}%";
            RefreshStage();
            RefreshPower();
            RefreshAbility();
            RefreshAward();
        }

        /// <summary>
        /// Shows lives as a row of ships, like spare hulls in reserve. With no icons assigned it falls
        /// back to the plain number.
        /// </summary>
        private void RefreshLives()
        {
            if (gameManager == null) return;
            var lives = gameManager.Lives;
            if (lifeIcons == null || lifeIcons.Length == 0)
            {
                if (livesLabel != null) livesLabel.text = lives.ToString();
                return;
            }
            for (var i = 0; i < lifeIcons.Length; i++)
                if (lifeIcons[i] != null && lifeIcons[i].enabled != i < lives) lifeIcons[i].enabled = i < lives;
            if (livesLabel == null) return;
            var extra = lives - lifeIcons.Length;
            var text = extra > 0 ? $"+{extra}" : string.Empty;
            if (livesLabel.text != text) livesLabel.text = text;
        }

        private void RefreshStage()
        {
            if (campaignManager == null || campaignManager.Run == null)
            {
                if (stageLabel != null) stageLabel.text = string.Empty;
                if (modifierLabel != null) modifierLabel.text = string.Empty;
                return;
            }
            var run = campaignManager.Run;
            if (stageLabel != null) stageLabel.text = $"Stage {run.CurrentStageNumber}/{run.TotalStageCount}";
            if (modifierLabel == null) return;
            var modifier = campaignManager.CurrentModifier;
            modifierLabel.text = modifier != null ? $"{modifier.displayName}  x{modifier.scoreMultiplier:0.00}" : string.Empty;
        }

        private void RefreshPower()
        {
            if (powerMeter == null) return;
            var fill = powerMeter.MaxPower > 0f ? Mathf.Clamp01(powerMeter.Power / powerMeter.MaxPower) : 0f;
            if (powerGauge != null) powerGauge.Show(fill, powerMeter.IsReady);
            else if (powerFill != null)
            {
                powerFill.fillAmount = fill;
                powerFill.color = powerMeter.IsReady ? powerReadyColor : powerChargingColor;
            }
            if (powerLabel != null)
                powerLabel.text = powerMeter.IsReady ? "POWER READY" : $"Power {Mathf.FloorToInt(powerMeter.Power)}";
        }

        private void RefreshAbility()
        {
            if (powerUpManager == null) return;
            if (abilityLabel != null)
            {
                var stored = powerUpManager.StoredPowerUp;
                abilityLabel.text = stored.HasValue ? DisplayName(stored.Value) : "-";
            }
            if (effectsLabel == null) return;
            var lines = string.Empty;
            foreach (PowerUpType type in System.Enum.GetValues(typeof(PowerUpType)))
            {
                if (!powerUpManager.IsEffectActive(type)) continue;
                if (lines.Length > 0) lines += "\n";
                lines += $"{DisplayName(type)} {powerUpManager.GetEffectRemaining(type):0.0}s";
            }
            effectsLabel.text = lines;
        }

        private void RefreshAward()
        {
            if (awardLabel == null) return;
            if (scoreManager == null || !scoreManager.HasLastAward ||
                Time.unscaledTime - awardShownAt > captureAwardDisplaySeconds)
            {
                awardLabel.text = string.Empty;
                return;
            }
            var award = scoreManager.LastAward;
            awardLabel.text = award.CaptureMultiplier > 1f ? $"+{award.Points}  x{award.CaptureMultiplier:0.#}" : $"+{award.Points}";
        }

        private static string DisplayName(PowerUpType type) => type == PowerUpType.ArenaTilt ? "Arena Tilt" : type.ToString();
    }
}
