using System.Reflection;
using NUnit.Framework;
using SpaceXonix.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class PowerGaugeTests
    {
        [Test]
        public void Gauge_FillsToExactlyItsFractionAndIsTrulyFullWhenReady()
        {
            var root = new GameObject("Track", typeof(RectTransform));
            try
            {
                var fill = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
                fill.SetParent(root.transform, false);
                var fillImage = fill.gameObject.AddComponent<Image>();
                var glow = new GameObject("Glow", typeof(RectTransform)).AddComponent<Image>();
                glow.transform.SetParent(root.transform, false);
                var gauge = root.AddComponent<PowerGauge>();
                Set(gauge, "fill", fill); Set(gauge, "fillImage", fillImage); Set(gauge, "readyGlow", glow);

                gauge.Show(0f, false);
                gauge.Animate(.016f, 0f);
                Assert.That(fillImage.enabled, Is.False, "an empty meter draws no sliver of fill");

                gauge.Show(.3f, false);
                gauge.Animate(.016f, 0f);
                Assert.That(fill.anchorMax.x, Is.EqualTo(.3f).Within(.0001f));
                var low = gauge.CurrentFillColor;
                Assert.That(glow.enabled, Is.False);

                gauge.Show(.9f, false);
                gauge.Animate(.016f, 0f);
                Assert.That(gauge.CurrentFillColor.g, Is.GreaterThan(low.g), "charging climbs toward cyan");

                gauge.Show(1f, true);
                gauge.Animate(.016f, 0f);
                Assert.That(fill.anchorMax.x, Is.EqualTo(1f), "ready means edge to edge");
                Assert.That(glow.enabled, Is.True, "a ready shot glows");
                var first = gauge.CurrentFillColor;
                gauge.Animate(.016f, .08f);
                Assert.That(gauge.CurrentFillColor, Is.Not.EqualTo(first), "and throbs");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
