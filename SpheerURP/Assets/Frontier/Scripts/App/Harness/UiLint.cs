#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    [Serializable]
    public class LintItem
    {
        public string kind;
        public string text;
        public float value;
        public string element;
    }

    [Serializable]
    public class LintReport
    {
        public int labels;
        public float minFontDp;
        public int textUnder12dp;
        public int buttons;
        public float minTouchDp;
        public int touchUnder48dp;
        public int overflowing;
        public List<LintItem> items = new List<LintItem>();
    }

    /// <summary>
    /// Automated UI checks over a live UI Toolkit tree: smallest text, smallest touch target and panels that spill off the
    /// screen. Sizes are converted to dp using the device density, so they mean the same thing as Android/iOS guidance.
    /// </summary>
    public static class UiLint
    {
        const float MinTextDp = 12f, MinTouchDp = 48f;
        static readonly string[] PanelClasses = { "fr-modal", "fr-popup", "fr-map-card", "fr-banner-box", "fr-overlay" };

        public static LintReport Run(VisualElement root, float panelScale, float density, Rect screenPx)
        {
            var report = new LintReport { minFontDp = 0f, minTouchDp = 0f };
            if (root == null) return report;
            float minFont = float.MaxValue, minTouch = float.MaxValue;

            foreach (var text in root.Query<TextElement>().ToList())
            {
                if (string.IsNullOrWhiteSpace(text.text) || !IsShown(text)) continue;
                report.labels++;
                float dp = text.resolvedStyle.fontSize * panelScale / density;
                minFont = Mathf.Min(minFont, dp);
                if (dp < MinTextDp)
                {
                    report.textUnder12dp++;
                    Add(report, "text<12dp", text.text, dp, text);
                }
            }

            foreach (var button in root.Query<Button>().ToList())
            {
                if (!IsShown(button)) continue;
                report.buttons++;
                var b = button.worldBound;
                float dp = Mathf.Min(b.width, b.height) * panelScale / density;
                minTouch = Mathf.Min(minTouch, dp);
                if (dp < MinTouchDp)
                {
                    report.touchUnder48dp++;
                    Add(report, "touch<48dp", string.IsNullOrEmpty(button.text) ? Describe(button) : button.text, dp, button);
                }
            }

            Rect logical = new Rect(0f, 0f, screenPx.width / panelScale, screenPx.height / panelScale);
            foreach (var cls in PanelClasses)
                foreach (var el in root.Query<VisualElement>(className: cls).ToList())
                {
                    if (!IsShown(el)) continue;
                    var wb = el.worldBound;
                    float spill = Mathf.Max(Mathf.Max(0f, logical.xMin - wb.xMin), Mathf.Max(0f, wb.xMax - logical.xMax),
                        Mathf.Max(0f, logical.yMin - wb.yMin), Mathf.Max(0f, wb.yMax - logical.yMax));
                    if (spill > 1.5f)
                    {
                        report.overflowing++;
                        Add(report, "offscreen", cls, spill, el);
                    }
                }

            report.minFontDp = minFont == float.MaxValue ? 0f : minFont;
            report.minTouchDp = minTouch == float.MaxValue ? 0f : minTouch;
            return report;
        }

        static void Add(LintReport r, string kind, string text, float value, VisualElement el)
        {
            if (r.items.Count >= 60) return;
            r.items.Add(new LintItem { kind = kind, text = text.Length > 48 ? text.Substring(0, 48) : text, value = Mathf.Round(value * 10f) / 10f, element = Describe(el) });
        }

        static string Describe(VisualElement el)
        {
            var sb = new System.Text.StringBuilder(el.GetType().Name);
            foreach (var c in el.GetClasses()) sb.Append('.').Append(c);
            return sb.ToString();
        }

        static bool IsShown(VisualElement el)
        {
            if (el.panel == null) return false;
            for (var e = el; e != null; e = e.parent)
            {
                var s = e.resolvedStyle;
                if (s.display == DisplayStyle.None || s.visibility != Visibility.Visible || s.opacity < 0.05f) return false;
            }
            var wb = el.worldBound;
            if (wb.width < 0.5f || wb.height < 0.5f) return false;
            var scroll = el.GetFirstAncestorOfType<ScrollView>();
            if (scroll != null && el != scroll && !scroll.contentViewport.worldBound.Overlaps(wb)) return false;
            return true;
        }
    }
}
#endif
