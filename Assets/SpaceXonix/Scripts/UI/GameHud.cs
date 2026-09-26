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
        [Tooltip("When set, the life icons show the ship the player is flying.")]
        [SerializeField] private SpaceXonix.Presentation.PlayerShipSkin playerSkin;

        [Header("Power meter")]
        [Tooltip("The animated energy gauge. When set it replaces the plain fill image below.")]
        [SerializeField] private PowerGauge powerGauge;
        [SerializeField] private Image powerFill;
        [SerializeField] private Color powerChargingColor = new Color(.2f, .6f, 1f);
        [SerializeField] private Color powerReadyColor = new Color(.2f, 1f, 1f);

        [SerializeField, Min(0f)] private float captureAwardDisplaySeconds = 2f;

        private static readonly PowerUpType[] EffectTypes = (PowerUpType[])System.Enum.GetValues(typeof(PowerUpType));

        private float awardShownAt = float.NegativeInfinity;
        // The last value each label was built from. Labels are only rebuilt when these change, so a
        // steady HUD makes no garbage; garbage collections on a phone are visible hitches.
        private int shownScore = int.MinValue;
        private int shownCaptureTenths = int.MinValue;
        private int shownStage = int.MinValue, shownStageTotal = int.MinValue;
        private object shownModifier = new object();
        private int shownPower = int.MinValue;
        private int shownStored = int.MinValue;
        private int shownEffectsKey = int.MinValue;
        private int shownAwardKey = int.MinValue;
        private readonly System.Text.StringBuilder effectsText = new System.Text.StringBuilder(64);

        private void Start()
        {
            // The skin is applied in the player's Awake, so it is known by now.
            var icon = playerSkin != null && playerSkin.Current != null ? playerSkin.Current.Preview : null;
            if (icon == null || lifeIcons == null) return;
            foreach (var life in lifeIcons) if (life != null) life.sprite = icon;
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

        private void LateUpdate() => Refresh();

        /// <summary>Pulls every displayed value. Public so tests can drive one frame deterministically.</summary>
        public void Refresh()
        {
            RefreshLives();
            if (scoreLabel != null && scoreManager != null && scoreManager.Score != shownScore)
            {
                shownScore = scoreManager.Score;
                scoreLabel.text = shownScore.ToString();
            }
            if (captureLabel != null && boardManager != null)
            {
                var tenths = Mathf.RoundToInt(boardManager.CapturedPercentage * 10f);
                if (tenths != shownCaptureTenths)
                {
                    shownCaptureTenths = tenths;
                    captureLabel.text = $"{tenths / 10f:0.0}%";
                }
            }
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
                if (shownStage == 0) return;
                shownStage = shownStageTotal = 0;
                shownModifier = null;
                if (stageLabel != null) stageLabel.text = string.Empty;
                if (modifierLabel != null) modifierLabel.text = string.Empty;
                return;
            }
            var run = campaignManager.Run;
            if (stageLabel != null && (run.CurrentStageNumber != shownStage || run.TotalStageCount != shownStageTotal))
            {
                shownStage = run.CurrentStageNumber;
                shownStageTotal = run.TotalStageCount;
                stageLabel.text = $"Stage {shownStage}/{shownStageTotal}";
            }
            if (modifierLabel == null) return;
            var modifier = campaignManager.CurrentModifier;
            if (ReferenceEquals(modifier, shownModifier)) return;
            shownModifier = modifier;
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
            if (powerLabel == null) return;
            // -1 stands for "ready", so the label only changes when the shown number does.
            var power = powerMeter.IsReady ? -1 : Mathf.FloorToInt(powerMeter.Power);
            if (power == shownPower) return;
            shownPower = power;
            powerLabel.text = power < 0 ? "POWER READY" : $"Power {power}";
        }

        private void RefreshAbility()
        {
            if (powerUpManager == null) return;
            if (abilityLabel != null)
            {
                var stored = powerUpManager.StoredPowerUp;
                var storedKey = stored.HasValue ? (int)stored.Value : -1;
                if (storedKey != shownStored)
                {
                    shownStored = storedKey;
                    abilityLabel.text = stored.HasValue ? DisplayName(stored.Value) : "-";
                }
            }
            if (effectsLabel == null) return;
            // The timers show tenths of a second, so the text changes ten times a second at most.
            var key = 17;
            for (var i = 0; i < EffectTypes.Length; i++)
            {
                var type = EffectTypes[i];
                var tenths = powerUpManager.IsEffectActive(type) ? Mathf.CeilToInt(powerUpManager.GetEffectRemaining(type) * 10f) : -1;
                key = key * 31 + tenths;
            }
            if (key == shownEffectsKey) return;
            shownEffectsKey = key;
            effectsText.Length = 0;
            for (var i = 0; i < EffectTypes.Length; i++)
            {
                var type = EffectTypes[i];
                if (!powerUpManager.IsEffectActive(type)) continue;
                if (effectsText.Length > 0) effectsText.Append('\n');
                effectsText.Append(DisplayName(type)).Append(' ')
                    .Append((Mathf.CeilToInt(powerUpManager.GetEffectRemaining(type) * 10f) / 10f).ToString("0.0")).Append('s');
            }
            effectsLabel.text = effectsText.ToString();
        }

        private void RefreshAward()
        {
            if (awardLabel == null) return;
            if (scoreManager == null || !scoreManager.HasLastAward ||
                Time.unscaledTime - awardShownAt > captureAwardDisplaySeconds)
            {
                if (shownAwardKey == 0) return;
                shownAwardKey = 0;
                awardLabel.text = string.Empty;
                return;
            }
            // Each award is its own showing, even when two in a row score the same.
            var awardKey = awardShownAt.GetHashCode() | 1;
            if (awardKey == shownAwardKey) return;
            shownAwardKey = awardKey;
            var award = scoreManager.LastAward;
            awardLabel.text = award.CaptureMultiplier > 1f ? $"+{award.Points}  x{award.CaptureMultiplier:0.#}" : $"+{award.Points}";
        }

        private static string DisplayName(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Shield: return "Shield";
                case PowerUpType.Freeze: return "Freeze";
                case PowerUpType.ArenaTilt: return "Arena Tilt";
                default: return type.ToString();
            }
        }
    }
}
