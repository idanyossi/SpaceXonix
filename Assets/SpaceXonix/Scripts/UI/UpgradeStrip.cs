using System.Collections.Generic;
using SpaceXonix.Campaign;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// The run's upgrades on the HUD: a row of small badges, one per upgrade taken, each showing the
    /// upgrade's card picture and, when it was taken more than once, how many times. The row ends at
    /// its right edge, in the order the upgrades were taken. The pause menu lists what each one does.
    /// </summary>
    public sealed class UpgradeStrip : MonoBehaviour
    {
        [SerializeField] private UpgradeManager upgradeManager;
        [Tooltip("An inactive badge to copy: an Image frame with an Image child named Icon and a Text child named Count.")]
        [SerializeField] private RectTransform badgeTemplate;
        [SerializeField, Min(0f)] private float spacing = 10f;

        private readonly List<RectTransform> badges = new List<RectTransform>();

        public int BadgeCount { get; private set; }
        public IReadOnlyList<RectTransform> Badges => badges;

        private void OnEnable()
        {
            if (upgradeManager != null) upgradeManager.Changed += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            if (upgradeManager != null) upgradeManager.Changed -= Rebuild;
        }

        public void Rebuild()
        {
            if (badgeTemplate == null) return;
            badgeTemplate.gameObject.SetActive(false);
            var run = upgradeManager != null ? upgradeManager.Run : null;
            var taken = run != null ? run.Taken : null;
            BadgeCount = taken != null ? taken.Count : 0;
            var step = badgeTemplate.sizeDelta.x + spacing;
            for (var i = 0; i < BadgeCount; i++)
            {
                var badge = Badge(i);
                var definition = taken[i];
                badge.gameObject.SetActive(true);
                badge.anchoredPosition = badgeTemplate.anchoredPosition - new Vector2((BadgeCount - 1 - i) * step, 0f);
                var frame = badge.GetComponent<Image>();
                if (frame != null) frame.color = definition.cardAccent;
                var icon = badge.Find("Icon");
                if (icon != null) icon.GetComponent<Image>().sprite = definition.cardArt;
                var count = badge.Find("Count");
                if (count != null)
                {
                    var stacks = run.GetStacks(definition.type);
                    count.GetComponent<Text>().text = stacks > 1 ? $"x{stacks}" : "";
                }
            }
            for (var i = BadgeCount; i < badges.Count; i++) badges[i].gameObject.SetActive(false);
        }

        private RectTransform Badge(int index)
        {
            while (badges.Count <= index)
            {
                var copy = Instantiate(badgeTemplate, badgeTemplate.parent);
                copy.name = $"Upgrade{badges.Count}";
                badges.Add(copy);
            }
            return badges[index];
        }
    }
}
