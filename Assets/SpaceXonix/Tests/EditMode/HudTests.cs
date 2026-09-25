using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Power;
using SpaceXonix.Scoring;
using SpaceXonix.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class HudTests
    {
        [Test]
        public void Panel_ShowsOneCaptionPerButtonAndHidesTheSpares()
        {
            using (var fixture = new PanelFixture())
            {
                Assert.That(fixture.Panel.ButtonCount, Is.EqualTo(3));

                // A briefing offers a single button; the other two must not linger on screen.
                fixture.Panel.Show("STAGE 1", "body text", new[] { "START" }, index => { });
                Assert.That(fixture.Panel.IsShown, Is.True);
                Assert.That(fixture.Title.text, Is.EqualTo("STAGE 1"));
                Assert.That(fixture.Body.text, Is.EqualTo("body text"));
                Assert.That(fixture.Buttons[0].gameObject.activeSelf, Is.True);
                Assert.That(fixture.Labels[0].text, Is.EqualTo("START"));
                Assert.That(fixture.Buttons[1].gameObject.activeSelf, Is.False);
                Assert.That(fixture.Buttons[2].gameObject.activeSelf, Is.False);

                // An upgrade choice fills all three.
                fixture.Panel.Show("CHOOSE", "", new[] { "A", "B", "C" }, index => { });
                for (var i = 0; i < 3; i++) Assert.That(fixture.Buttons[i].gameObject.activeSelf, Is.True, $"button {i}");
                Assert.That(fixture.Labels[2].text, Is.EqualTo("C"));

                fixture.Panel.Hide();
                Assert.That(fixture.Panel.IsShown, Is.False);
            }
        }

        [Test]
        public void Panel_ReportsTheClickedSlotAndStopsOnceHidden()
        {
            using (var fixture = new PanelFixture())
            {
                var chosen = new List<int>();
                fixture.Panel.Show("CHOOSE", "", new[] { "A", "B", "C" }, chosen.Add);

                fixture.Buttons[2].onClick.Invoke();
                fixture.Buttons[0].onClick.Invoke();
                Assert.That(chosen, Is.EqualTo(new[] { 2, 0 }), "each button reports its own slot");

                fixture.Panel.Hide();
                fixture.Buttons[1].onClick.Invoke();
                Assert.That(chosen.Count, Is.EqualTo(2), "a hidden panel no longer routes clicks");
            }
        }

        [Test]
        public void Hud_ShowsLivesScoreCaptureAndPowerState()
        {
            using (var fixture = new HudFixture())
            {
                fixture.Hud.Refresh();
                Assert.That(fixture.Lives.text, Is.EqualTo("3"));
                Assert.That(fixture.Score.text, Is.EqualTo("0"));
                Assert.That(fixture.Capture.text, Is.EqualTo("0.0%"));
                Assert.That(fixture.PowerFill.fillAmount, Is.Zero);
                Assert.That(fixture.PowerLabel.text, Is.EqualTo("Power 0"));

                // Half a meter reads as half a bar, still in the charging colour.
                fixture.SetPower(50f);
                fixture.Hud.Refresh();
                Assert.That(fixture.PowerFill.fillAmount, Is.EqualTo(.5f).Within(.001f));
                Assert.That(fixture.PowerFill.color, Is.EqualTo(fixture.ChargingColor));
                Assert.That(fixture.PowerLabel.text, Is.EqualTo("Power 50"));

                fixture.SetPower(100f);
                fixture.Hud.Refresh();
                Assert.That(fixture.PowerFill.fillAmount, Is.EqualTo(1f).Within(.001f));
                Assert.That(fixture.PowerFill.color, Is.EqualTo(fixture.ReadyColor), "a full meter switches colour");
                Assert.That(fixture.PowerLabel.text, Is.EqualTo("POWER READY"));
            }
        }

        [Test]
        public void Hud_ShowsLivesAsShipsWithOverflowCounted()
        {
            using (var fixture = new HudFixture())
            {
                var icons = fixture.UseLifeIcons(4);
                fixture.Hud.Refresh();
                Assert.That(Array.FindAll(icons, icon => icon.enabled), Has.Length.EqualTo(3), "one ship per life");
                Assert.That(fixture.Lives.text, Is.Empty, "no overflow while every life has a ship");

                fixture.Game.AddLives(3);
                fixture.Hud.Refresh();
                Assert.That(Array.TrueForAll(icons, icon => icon.enabled), "a full row");
                Assert.That(fixture.Lives.text, Is.EqualTo("+2"), "lives beyond the row are counted, not lost");

                fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact);
                fixture.Hud.Refresh();
                Assert.That(fixture.Lives.text, Is.EqualTo("+1"));
            }
        }

        [Test]
        public void Hud_TracksLivesLostAndTerritoryCaptured()
        {
            using (var fixture = new HudFixture())
            {
                fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact);
                fixture.Hud.Refresh();
                Assert.That(fixture.Lives.text, Is.EqualTo("2"));

                fixture.CaptureAcrossBoard(14);
                fixture.Hud.Refresh();
                Assert.That(fixture.Board.CapturedPercentage, Is.GreaterThan(0f));
                Assert.That(fixture.Capture.text, Is.EqualTo($"{fixture.Board.CapturedPercentage:0.0}%"));
                Assert.That(fixture.Score.text, Is.EqualTo(fixture.ScoreManager.Score.ToString()));
                Assert.That(fixture.ScoreManager.Score, Is.GreaterThan(0), "capturing scores, so the HUD has something to show");
            }
        }

        private sealed class PanelFixture : IDisposable
        {
            private readonly GameObject root = new GameObject("PanelFixture");
            public readonly CampaignPanel Panel;
            public readonly Text Title;
            public readonly Text Body;
            public readonly Button[] Buttons = new Button[3];
            public readonly Text[] Labels = new Text[3];

            public PanelFixture()
            {
                root.SetActive(false);
                Panel = root.AddComponent<CampaignPanel>();
                Title = NewText(root, "Title");
                Body = NewText(root, "Body");
                for (var i = 0; i < 3; i++)
                {
                    var go = new GameObject("Choice" + i, typeof(RectTransform));
                    go.transform.SetParent(root.transform, false);
                    Buttons[i] = go.AddComponent<Button>();
                    Labels[i] = NewText(go, "Text");
                }
                Set(Panel, "root", root);
                Set(Panel, "titleLabel", Title);
                Set(Panel, "bodyLabel", Body);
                Set(Panel, "choiceButtons", Buttons);
                Set(Panel, "choiceLabels", Labels);
                root.SetActive(true);
                // EditMode does not run Awake, which is where the buttons are hooked up.
                typeof(CampaignPanel).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Panel, null);
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);

            private static Text NewText(GameObject parent, string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent.transform, false);
                return go.AddComponent<Text>();
            }
        }

        private sealed class HudFixture : IDisposable
        {
            private readonly GameObject root = new GameObject("HudFixture");
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            public readonly GameHud Hud;
            public readonly GameManager Game;
            public readonly BoardManager Board;
            public readonly ScoreManager ScoreManager;
            public readonly PowerMeter Power;
            public readonly Text Lives;
            public readonly Text Score;
            public readonly Text Capture;
            public readonly Text PowerLabel;
            public readonly Image PowerFill;
            public readonly Color ChargingColor = new Color(.2f, .6f, 1f);
            public readonly Color ReadyColor = new Color(.2f, 1f, 1f);

            public HudFixture()
            {
                root.SetActive(false);
                Board = root.AddComponent<BoardManager>();
                Board.Initialize();
                var input = root.AddComponent<InputRouter>();
                var player = Own(new GameObject("Player")).AddComponent<PlayerController>();
                Invoke(player, "Awake");
                Game = root.AddComponent<GameManager>();
                Set(Game, "inputRouter", input); Set(Game, "playerController", player); Set(Game, "boardManager", Board);
                Invoke(Game, "Awake");

                ScoreManager = root.AddComponent<ScoreManager>();
                var scoring = Own(ScriptableObject.CreateInstance<ScoringDefinition>());
                Set(ScoreManager, "definition", scoring);
                Set(ScoreManager, "boardManager", Board);
                Invoke(ScoreManager, "Awake");

                Power = root.AddComponent<PowerMeter>();
                var powerDefinition = Own(ScriptableObject.CreateInstance<PowerDefinition>());
                Set(Power, "definition", powerDefinition);
                Power.Initialize();

                Hud = root.AddComponent<GameHud>();
                Lives = NewText("Lives"); Score = NewText("Score"); Capture = NewText("Capture"); PowerLabel = NewText("Power");
                var fillObject = new GameObject("PowerFill", typeof(RectTransform));
                fillObject.transform.SetParent(root.transform, false);
                PowerFill = fillObject.AddComponent<Image>();

                Set(Hud, "gameManager", Game);
                Set(Hud, "boardManager", Board);
                Set(Hud, "scoreManager", ScoreManager);
                Set(Hud, "powerMeter", Power);
                Set(Hud, "livesLabel", Lives);
                Set(Hud, "scoreLabel", Score);
                Set(Hud, "captureLabel", Capture);
                Set(Hud, "powerLabel", PowerLabel);
                Set(Hud, "powerFill", PowerFill);
                Set(Hud, "powerChargingColor", ChargingColor);
                Set(Hud, "powerReadyColor", ReadyColor);

                root.SetActive(true);
                Invoke(Game, "Start");
                // ScoreManager subscribes to the board in OnEnable, which EditMode does not run.
                Invoke(ScoreManager, "OnEnable");
            }

            /// <summary>Gives the HUD a row of ship icons, which replaces the plain lives number.</summary>
            public Image[] UseLifeIcons(int count)
            {
                var icons = new Image[count];
                for (var i = 0; i < count; i++)
                {
                    var go = new GameObject($"Life{i}", typeof(RectTransform));
                    go.transform.SetParent(root.transform, false);
                    icons[i] = go.AddComponent<Image>();
                }
                Set(Hud, "lifeIcons", icons);
                return icons;
            }

            public void SetPower(float value)
            {
                var model = typeof(PowerMeter).GetField("model", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Power);
                model.GetType().GetProperty("Power").SetValue(model, value);
            }

            /// <summary>Cuts a full column and reconnects, which is a real capture.</summary>
            public void CaptureAcrossBoard(int column)
            {
                for (var row = 1; row < Board.Rows - 1; row++) Board.Model.MoveTo(new GridCoordinate(column, row));
                Board.Model.MoveTo(new GridCoordinate(column, Board.Rows - 1));
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }

            private Text NewText(string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(root.transform, false);
                return go.AddComponent<Text>();
            }

            private T Own<T>(T item) where T : UnityEngine.Object
            {
                owned.Add(item);
                return item;
            }
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static void Invoke(object target, string name) =>
            target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    }
}
