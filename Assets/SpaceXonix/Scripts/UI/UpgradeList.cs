using System.Collections.Generic;
using SpaceXonix.Campaign;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// The pause menu's record of this run's upgrades: one row per upgrade with its picture, name,
    /// what it does and how many times it was taken. The panel grows to fit the rows.
    /// </summary>
    public sealed class UpgradeList : MonoBehaviour
    {
        [SerializeField] private UpgradeManager upgradeManager;
        [Tooltip("The panel that holds the list; its height follows the number of rows.")]
        [SerializeField] private RectTransform panel;
        [Tooltip("An inactive row to copy: an Image named Icon, and Texts named Name, Effect and Count.")]
        [SerializeField] private RectTransform rowTemplate;
        [Tooltip("Shown instead of rows before any upgrade is taken.")]
        [SerializeField] private Text emptyLabel;
        [SerializeField, Min(1f)] private float rowHeight = 96f;
        [Tooltip("Panel height above the first row (the heading) and below the last.")]
        [SerializeField, Min(0f)] private float headerHeight = 96f;
        [SerializeField, Min(0f)] private float footerHeight = 24f;

        private readonly List<RectTransform> rows = new List<RectTransform>();

        public int RowCount { get; private set; }

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
            if (rowTemplate == null) return;
            rowTemplate.gameObject.SetActive(false);
            var run = upgradeManager != null ? upgradeManager.Run : null;
            var taken = run != null ? run.Taken : null;
            RowCount = taken != null ? taken.Count : 0;
            for (var i = 0; i < RowCount; i++)
            {
                var row = Row(i);
                var definition = taken[i];
                var stacks = run.GetStacks(definition.type);
                row.gameObject.SetActive(true);
                row.anchoredPosition = rowTemplate.anchoredPosition - new Vector2(0f, i * rowHeight);
                SetImage(row, "Icon", definition.cardArt);
                SetText(row, "Name", definition.displayName, definition.cardAccent);
                SetText(row, "Effect", definition.effectText, null);
                SetText(row, "Count", stacks > 1 ? $"x{stacks}" : "", null);
            }
            for (var i = RowCount; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
            if (emptyLabel != null) emptyLabel.gameObject.SetActive(RowCount == 0);
            if (panel != null)
            {
                var size = panel.sizeDelta;
                size.y = headerHeight + Mathf.Max(1, RowCount) * rowHeight + footerHeight;
                panel.sizeDelta = size;
            }
        }

        private RectTransform Row(int index)
        {
            while (rows.Count <= index)
            {
                var copy = Instantiate(rowTemplate, rowTemplate.parent);
                copy.name = $"Row{rows.Count}";
                rows.Add(copy);
            }
            return rows[index];
        }

        private static void SetImage(Transform row, string child, Sprite sprite)
        {
            var target = row.Find(child);
            if (target != null) target.GetComponent<Image>().sprite = sprite;
        }

        private static void SetText(Transform row, string child, string value, Color? colour)
        {
            var target = row.Find(child);
            if (target == null) return;
            var text = target.GetComponent<Text>();
            text.text = value;
            if (colour.HasValue) text.color = colour.Value;
        }
    }
}
