using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Campaign;
using SpaceXonix.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class UpgradeCardTests
    {
        [Test]
        public void Cards_ShowTheOfferWithOwnedStacksAsPips()
        {
            using (var fixture = new Fixture())
            {
                var offer = new[] { fixture.Upgrade("Reinforced Hull", 3), fixture.Upgrade("Gravity Stabilizer", 2), fixture.Upgrade("Rapid Capacitor", 3) };
                fixture.Panel.Show(offer, type => type == offer[0].type ? 2 : 0, _ => { });

                Assert.That(fixture.Panel.IsShown, Is.True);
                Assert.That(fixture.Panel.Cards[0].DisplayedName, Is.EqualTo("REINFORCED HULL"));
                Assert.That(fixture.Panel.Cards[0].DisplayedEffect, Is.EqualTo("effect of Reinforced Hull"));
                Assert.That(fixture.Panel.Cards[0].FilledPips, Is.EqualTo(2), "owned stacks show as filled pips");
                Assert.That(fixture.Panel.Cards[1].VisiblePips, Is.EqualTo(2), "one pip per possible stack");
            }
        }

        [Test]
        public void Cards_DealInOnceAndOnlyAcceptATapAfterLanding()
        {
            using (var fixture = new Fixture())
            {
                var offer = new[] { fixture.Upgrade("A", 3), fixture.Upgrade("B", 3), fixture.Upgrade("C", 3) };
                fixture.Panel.Show(offer, _ => 0, _ => { });
                // The campaign screens re-show every frame; the same offer must not restart the deal.
                fixture.Panel.Show(offer, _ => 0, _ => { });
                Assert.That(fixture.Panel.DealCount, Is.EqualTo(1));

                var card = fixture.Panel.Cards[2];
                var group = card.GetComponent<CanvasGroup>();
                card.Animate(0f);
                Assert.That(group.interactable, Is.False, "a card in flight cannot be picked");
                Assert.That(group.alpha, Is.LessThan(1f));
                card.Animate(5f);
                Assert.That(group.interactable, Is.True);
                Assert.That(group.alpha, Is.EqualTo(1f));
            }
        }

        [Test]
        public void Choosing_TakesOnePickPerOffer()
        {
            using (var fixture = new Fixture())
            {
                var offer = new[] { fixture.Upgrade("A", 3), fixture.Upgrade("B", 3), fixture.Upgrade("C", 3) };
                var picks = new List<int>();
                fixture.Panel.Show(offer, _ => 0, picks.Add);
                fixture.Panel.Cards[1].Button.onClick.Invoke();
                fixture.Panel.Cards[1].Button.onClick.Invoke();
                fixture.Panel.Cards[0].Button.onClick.Invoke();
                Assert.That(picks, Is.EqualTo(new[] { 1 }), "a double tap must not take two upgrades");
            }
        }

        private sealed class Fixture : System.IDisposable
        {
            private readonly GameObject root = new GameObject("Cards");
            private readonly List<Object> owned = new List<Object>();
            public readonly UpgradeCardPanel Panel;

            public Fixture()
            {
                root.SetActive(false);
                var cards = new UpgradeCard[3];
                for (var i = 0; i < cards.Length; i++)
                {
                    var go = new GameObject($"Card{i}", typeof(RectTransform));
                    go.transform.SetParent(root.transform, false);
                    var button = go.AddComponent<Button>();
                    var group = go.AddComponent<CanvasGroup>();
                    cards[i] = go.AddComponent<UpgradeCard>();
                    Set(cards[i], "button", button);
                    Set(cards[i], "group", group);
                    Set(cards[i], "nameLabel", Child<Text>(go, "Name"));
                    Set(cards[i], "effectLabel", Child<Text>(go, "Effect"));
                    var pips = new Image[5];
                    for (var p = 0; p < pips.Length; p++) pips[p] = Child<Image>(go, $"Pip{p}");
                    Set(cards[i], "pips", pips);
                }
                Panel = root.AddComponent<UpgradeCardPanel>();
                Set(Panel, "root", root);
                Set(Panel, "cards", cards);
                typeof(UpgradeCardPanel).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Panel, null);
            }

            public UpgradeDefinition Upgrade(string name, int maxStacks)
            {
                var definition = ScriptableObject.CreateInstance<UpgradeDefinition>();
                definition.type = (UpgradeType)(owned.Count % 7);
                definition.displayName = name;
                definition.effectText = $"effect of {name}";
                definition.maxStacks = maxStacks;
                owned.Add(definition);
                return definition;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
            }

            private static T Child<T>(GameObject parent, string name) where T : Component
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent.transform, false);
                return go.AddComponent<T>();
            }
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
