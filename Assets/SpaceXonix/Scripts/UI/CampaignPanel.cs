using System;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// One reusable panel for every between-stage screen: briefing, stage complete, upgrade choice,
    /// game over, stages cleared and campaign complete. They differ only in title, body and buttons,
    /// so a single panel driven by <see cref="CampaignScreens"/> beats six near-identical ones.
    /// </summary>
    public sealed class CampaignPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private Text[] choiceLabels;

        private Action<int> choiceHandler;

        public bool IsShown => root != null && root.activeSelf;
        public int ButtonCount => choiceButtons != null ? choiceButtons.Length : 0;

        private void Awake() => HookButtons();

        private void OnDestroy()
        {
            if (choiceButtons == null) return;
            foreach (var button in choiceButtons) if (button != null) button.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// Shows the panel with a title, body and one caption per button. Captions beyond the
        /// available buttons are ignored and spare buttons are hidden, so the same panel serves a
        /// one-button briefing and a three-option upgrade choice.
        /// </summary>
        public void Show(string title, string body, string[] captions, Action<int> onChoice)
        {
            choiceHandler = onChoice;
            if (titleLabel != null) titleLabel.text = title;
            if (bodyLabel != null) bodyLabel.text = body;
            if (choiceButtons != null)
            {
                for (var i = 0; i < choiceButtons.Length; i++)
                {
                    var used = captions != null && i < captions.Length;
                    if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(used);
                    if (used && choiceLabels != null && i < choiceLabels.Length && choiceLabels[i] != null)
                        choiceLabels[i].text = captions[i];
                }
            }
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            choiceHandler = null;
            if (root != null) root.SetActive(false);
        }

        /// <summary>Invokes the handler for a button index. Public so tests can click without an EventSystem.</summary>
        public void Choose(int index) => choiceHandler?.Invoke(index);

        private void HookButtons()
        {
            if (choiceButtons == null) return;
            for (var i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null) continue;
                var index = i; // captured per button, so each one reports its own slot
                choiceButtons[i].onClick.AddListener(() => Choose(index));
            }
        }
    }
}
