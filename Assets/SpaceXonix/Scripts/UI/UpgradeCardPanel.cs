using System;
using System.Collections.Generic;
using SpaceXonix.Campaign;
using UnityEngine;

namespace SpaceXonix.UI
{
    /// <summary>
    /// The between-stage upgrade choice, dealt as holographic cards. The campaign screens call
    /// <see cref="Show"/> every frame while the choice is open, so it only re-deals when the offer
    /// actually changes; otherwise the cards would restart their deal animation every frame.
    /// </summary>
    public sealed class UpgradeCardPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private UpgradeCard[] cards = new UpgradeCard[0];
        [Tooltip("Seconds between one card landing and the next starting.")]
        [SerializeField, Min(0f)] private float dealStagger = .12f;

        private readonly List<UpgradeDefinition> shown = new List<UpgradeDefinition>();
        private Action<int> choiceHandler;
        private bool chosen;

        public bool IsShown => root != null && root.activeSelf;
        public IReadOnlyList<UpgradeCard> Cards => cards;
        public int DealCount { get; private set; }

        private void Awake()
        {
            for (var i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null || cards[i].Button == null) continue;
                var index = i; // captured per card, so each one reports its own slot
                cards[i].Button.onClick.AddListener(() => Choose(index));
            }
        }

        private void OnDestroy()
        {
            foreach (var card in cards) if (card != null && card.Button != null) card.Button.onClick.RemoveAllListeners();
        }

        public void Show(IReadOnlyList<UpgradeDefinition> offer, Func<UpgradeType, int> stacksOf, Action<int> onChoice)
        {
            choiceHandler = onChoice;
            if (IsShown && SameOffer(offer)) return;
            chosen = false;
            shown.Clear();
            var dealt = 0;
            for (var i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                var used = offer != null && i < offer.Count && offer[i] != null;
                cards[i].gameObject.SetActive(used);
                if (!used) continue;
                shown.Add(offer[i]);
                cards[i].Present(offer[i], stacksOf != null ? stacksOf(offer[i].type) : 0, dealt++ * dealStagger);
            }
            DealCount++;
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            choiceHandler = null;
            shown.Clear();
            if (root != null) root.SetActive(false);
        }

        /// <summary>Picks a card by slot. Public so tests can choose without an EventSystem.</summary>
        public void Choose(int index)
        {
            // One pick per offer: a double tap must not take two upgrades.
            if (chosen || choiceHandler == null) return;
            chosen = true;
            choiceHandler.Invoke(index);
        }

        private bool SameOffer(IReadOnlyList<UpgradeDefinition> offer)
        {
            if (offer == null || offer.Count != shown.Count) return false;
            for (var i = 0; i < offer.Count; i++) if (offer[i] != shown[i]) return false;
            return true;
        }
    }
}
