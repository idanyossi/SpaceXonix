using System.Collections.Generic;
using System.Text;
using SpaceXonix.Campaign;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using SpaceXonix.Scoring;
using SpaceXonix.Settings;
using UnityEngine;

namespace SpaceXonix.UI
{
    /// <summary>
    /// Drives the shared <see cref="CampaignPanel"/> from the campaign phase: what the panel says,
    /// which buttons it offers, and what each one does. Keeps every screen's copy in one place.
    /// </summary>
    public sealed class CampaignScreens : MonoBehaviour
    {
        [SerializeField] private CampaignManager campaignManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private UpgradeManager upgradeManager;
        [Tooltip("Holographic cards for the upgrade choice. Without them the choice falls back to the plain panel.")]
        [SerializeField] private UpgradeCardPanel upgradeCards;
        [SerializeField] private CampaignPanel panel;

        private readonly List<EnemyType> enemyTypes = new List<EnemyType>();
        private readonly List<string> captions = new List<string>();
        private readonly StringBuilder body = new StringBuilder();
        private CampaignPhase? shownPhase;

        /// <summary>The phase currently on screen, or null when the panel is hidden.</summary>
        public CampaignPhase? ShownPhase => shownPhase;

        private void LateUpdate() => Refresh();

        /// <summary>Re-evaluates the phase and rebuilds the panel when it changed. Public for tests.</summary>
        public void Refresh()
        {
            if (campaignManager == null || campaignManager.Run == null || panel == null) return;
            var phase = campaignManager.Phase;
            if (phase == CampaignPhase.Playing)
            {
                if (shownPhase != null) { panel.Hide(); shownPhase = null; }
                if (upgradeCards != null && upgradeCards.IsShown) upgradeCards.Hide();
                return;
            }
            if (phase != CampaignPhase.UpgradeChoice && upgradeCards != null && upgradeCards.IsShown) upgradeCards.Hide();
            // The upgrade offer changes its captions without changing phase, so it always rebuilds.
            if (shownPhase == phase && phase != CampaignPhase.UpgradeChoice) return;
            shownPhase = phase;
            BuildPanel(phase);
        }

        private void BuildPanel(CampaignPhase phase)
        {
            switch (phase)
            {
                case CampaignPhase.Briefing: ShowBriefing(); break;
                case CampaignPhase.StageComplete: ShowStageComplete(); break;
                case CampaignPhase.UpgradeChoice: ShowUpgradeChoice(); break;
                case CampaignPhase.GameOver: ShowGameOver(); break;
                case CampaignPhase.NormalStagesCleared: ShowStagesCleared(); break;
                case CampaignPhase.CampaignComplete: ShowCampaignComplete(); break;
            }
        }

        private void ShowBriefing()
        {
            var stage = campaignManager.CurrentStage;
            body.Clear();
            body.AppendLine(stage.briefing).AppendLine();
            body.Append(stage.IsBossStage
                ? $"Boss: {stage.boss.displayName} — capture {gameManager.CaptureTargetPercentage:0}% to destroy it"
                : $"Enemies: {DescribeEnemies(stage)}").AppendLine();
            body.Append($"Lasers: {(stage.lasers != null ? stage.lasers.Length : 0)}").AppendLine();
            body.Append($"Difficulty: {campaignManager.Difficulty.DisplayName()}").AppendLine();
            // Easy has no modifiers at all, so the line would always read "none" and only add noise.
            if (campaignManager.Difficulty.UsesStageModifiers())
                body.Append($"Modifier: {campaignManager.DescribeModifier()}").AppendLine();
            body.Append($"Upgrades: {(upgradeManager != null ? upgradeManager.Describe() : "none")}");

            panel.Show($"STAGE {stage.stageNumber}: {stage.stageName.ToUpperInvariant()}", body.ToString(),
                new[] { "START" }, index => campaignManager.StartStage());
        }

        private void ShowStageComplete()
        {
            body.Clear();
            body.Append($"Score {Score()}").AppendLine();
            body.Append($"Captured {campaignManager.StageCompletedPercentage:0.0}%").AppendLine();
            body.Append($"Largest capture {(scoreManager != null ? scoreManager.StageLargestCapturePercentage : 0f):0.0}%").AppendLine();
            body.Append($"Modifier bonus x{campaignManager.ModifierScoreMultiplier:0.00}");

            panel.Show("STAGE COMPLETE", body.ToString(),
                new[] { "CONTINUE" }, index => campaignManager.ContinueAfterStageComplete());
        }

        private void ShowUpgradeChoice()
        {
            var offer = upgradeManager.CurrentOffer;
            if (upgradeCards != null)
            {
                panel.Hide();
                upgradeCards.Show(offer, type => upgradeManager.Run.GetStacks(type), index => campaignManager.ChooseUpgrade(index));
                return;
            }
            captions.Clear();
            for (var i = 0; i < offer.Count && i < panel.ButtonCount; i++)
            {
                var definition = offer[i];
                var stacks = upgradeManager.Run.GetStacks(definition.type);
                captions.Add($"{definition.displayName}{(stacks > 0 ? $" (have {stacks})" : "")}\n{definition.effectText}");
            }
            panel.Show("CHOOSE AN UPGRADE", "Lasts for this run only",
                captions.ToArray(), index => campaignManager.ChooseUpgrade(index));
        }

        private void ShowGameOver()
        {
            body.Clear();
            body.Append($"Final score {Score()}").AppendLine();
            body.Append($"Highest stage {campaignManager.Run.HighestStageReached}").AppendLine();
            body.Append(BestRunLine());

            panel.Show("GAME OVER", body.ToString(),
                new[] { "RETRY CAMPAIGN", "MAIN MENU" }, index =>
                {
                    if (index == 0) campaignManager.RetryCampaign();
                    else SceneRouter.GoToMainMenu();
                });
        }

        private void ShowStagesCleared()
        {
            body.Clear();
            body.Append($"Final score {Score()}").AppendLine();
            body.Append(BestRunLine());

            panel.Show("ALL STAGES CLEARED", body.ToString(),
                new[] { "NEW CAMPAIGN", "MAIN MENU" }, index =>
                {
                    if (index == 0) campaignManager.RetryCampaign();
                    else SceneRouter.GoToMainMenu();
                });
        }

        private void ShowCampaignComplete()
        {
            body.Clear();
            body.AppendLine("The Alien Core is destroyed.").AppendLine();
            body.Append($"Final score {Score()}").AppendLine();
            body.Append($"Largest capture {(scoreManager != null ? scoreManager.LargestCapturePercentage : 0f):0.0}%").AppendLine();
            body.Append($"Upgrades: {(upgradeManager != null ? upgradeManager.Describe() : "none")}").AppendLine();
            body.Append(BestRunLine());

            panel.Show("CAMPAIGN COMPLETE", body.ToString(),
                new[] { "NEW CAMPAIGN", "MAIN MENU" }, index =>
                {
                    if (index == 0) campaignManager.RetryCampaign();
                    else SceneRouter.GoToMainMenu();
                });
        }

        private int Score() => scoreManager != null ? scoreManager.Score : 0;

        /// <summary>Celebrates a record, or shows the bar the run fell short of.</summary>
        private string BestRunLine()
        {
            var settings = GameSettings.Current;
            if (settings == null) return string.Empty;
            return campaignManager.IsNewHighScore
                ? $"NEW BEST RUN: {settings.CampaignHighScore}"
                : $"Best run: {settings.CampaignHighScore}";
        }

        private string DescribeEnemies(StageDefinition stage)
        {
            stage.GetEnemyTypes(enemyTypes);
            if (enemyTypes.Count == 0) return "none";
            var names = new List<string>();
            foreach (var type in enemyTypes) names.Add(Readable(type));
            return string.Join(", ", names);
        }

        private static string Readable(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.BasicBouncer: return "Basic Bouncer";
                case EnemyType.Linear: return "Linear Alien";
                case EnemyType.Unstable: return "Unstable Alien";
                case EnemyType.Volatile: return "Volatile Alien";
                default: return type.ToString();
            }
        }
    }
}
