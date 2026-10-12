using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public static class UiKit
    {
        public static readonly Color Cyan = new Color(0.1f, 0.95f, 1f);
        public static readonly Color Magenta = new Color(1f, 0.25f, 0.95f);
        public static readonly Color Lime = new Color(0.5f, 1f, 0.3f);
        public static readonly Color Amber = new Color(1f, 0.7f, 0.25f);
        public static readonly Color Red = new Color(1f, 0.3f, 0.35f);

        /// <summary>Clamps an HDR resource color into displayable UI space.</summary>
        public static Color Ui(Color c, float alpha = 1f)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (m > 1f) c = new Color(c.r / m, c.g / m, c.b / m);
            return new Color(c.r, c.g, c.b, alpha);
        }

        public static VisualElement Div(string cls = null)
        {
            var e = new VisualElement();
            if (cls != null) foreach (var c in cls.Split(' ')) e.AddToClassList(c);
            return e;
        }

        public static Label Text(string s, string cls = null, Color? color = null)
        {
            var l = new Label(s);
            l.pickingMode = PickingMode.Ignore;
            if (cls != null) foreach (var c in cls.Split(' ')) l.AddToClassList(c);
            if (color.HasValue) l.style.color = color.Value;
            return l;
        }

        public static void Border(VisualElement e, Color c, float w = 2f)
        {
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
        }

        public static void Radius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        /// <summary>A soft glowing halo behind an element (uses a radial gradient texture).</summary>
        public static VisualElement Halo(VisualElement parent, Color c, float spread = 40f, float alpha = 0.45f)
        {
            var h = new VisualElement { pickingMode = PickingMode.Ignore };
            h.style.position = Position.Absolute;
            h.style.left = -spread; h.style.right = -spread; h.style.top = -spread; h.style.bottom = -spread;
            h.style.backgroundImage = Background.FromTexture2D(Fx.Glow);
            h.style.unityBackgroundImageTintColor = new Color(c.r, c.g, c.b, alpha);
            parent.Insert(0, h);
            return h;
        }

        public static Button Btn(string text, Color color, Action onClick, bool big = false)
        {
            var b = new Button();
            b.text = text;
            b.AddToClassList("fr-btn");
            if (big) b.AddToClassList("fr-btn-big");
            Color c = Ui(color);
            Border(b, c, 3f);
            b.style.color = c;
            b.style.backgroundColor = new Color(c.r * 0.12f, c.g * 0.12f, c.b * 0.12f, 0.9f);
            b.clicked += () => { Sfx.Click(); if (onClick != null) onClick(); };
            return b;
        }

        public static void SetEnabledLook(Button b, bool enabled)
        {
            b.SetEnabled(enabled);
            b.style.opacity = enabled ? 1f : 0.4f;
        }

        public static VisualElement Bar(Color c, out VisualElement fill)
        {
            var track = Div("fr-bar");
            Border(track, new Color(c.r, c.g, c.b, 0.5f), 2f);
            fill = Div("fr-bar-fill");
            fill.style.backgroundColor = Ui(c);
            track.Add(fill);
            return track;
        }

        public static void SetFill(VisualElement fill, float k)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(k) * 100f);
        }

        public static VisualElement ItemChip(ResourceSO r, int amount, int have = -1)
        {
            var chip = Div("fr-itemchip");
            Color c = Ui(r.color);
            Border(chip, new Color(c.r, c.g, c.b, 0.8f), 2f);
            var dot = Div("fr-dot");
            dot.style.backgroundColor = c;
            chip.Add(dot);
            bool short_ = have >= 0 && have < amount;
            string label = have >= 0 ? have + "/" + amount : amount.ToString();
            chip.Add(Text(label, "fr-small", short_ ? Red : Color.white));
            return chip;
        }

        public static VisualElement CostRow(System.Collections.Generic.List<ItemAmount> items, GameState gs, bool showHave)
        {
            var row = Div("fr-row-wrap");
            foreach (var i in items)
                if (i.resource != null) row.Add(ItemChip(i.resource, i.amount, showHave ? gs.Count(i.resource) : -1));
            return row;
        }
    }
}
