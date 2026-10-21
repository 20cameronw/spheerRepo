using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public static class UiKit
    {
        // Accent palette. Keep identical to the --fr-cyan/magenta/lime/amber/danger tokens in Tokens.uss.
        public static readonly Color Cyan = new Color(0.1f, 0.95f, 1f);
        public static readonly Color Magenta = new Color(1f, 0.25f, 0.95f);
        public static readonly Color Lime = new Color(0.5f, 1f, 0.3f);
        public static readonly Color Amber = new Color(1f, 0.7f, 0.25f);
        public static readonly Color Red = new Color(1f, 0.3f, 0.35f);
        /// <summary>Tonal icon colour for inactive tabs and secondary glyphs (--fr-text-lo).</summary>
        public static readonly Color Muted = new Color(0.72f, 0.81f, 0.91f, 0.85f);

        /// <summary>Spacing scale in UI units (1 unit = 0.5 dp). Mirrors --fr-sp-*.</summary>
        public static class Space
        {
            public const float S4 = 4f, S8 = 8f, S12 = 12f, S16 = 16f, S24 = 24f, S32 = 32f;
        }

        /// <summary>Icon sizes and minimum touch target in UI units. Mirrors --fr-icon-* and --fr-touch.</summary>
        public static class Size
        {
            public const float IconSm = 32f, IconMd = 40f, IconLg = 56f, IconXl = 72f, Touch = 104f;
        }

        static readonly Color Neutral = new Color(0.78f, 0.86f, 0.96f);
        static readonly Color[] AccentColors = { Cyan, Magenta, Lime, Amber, Red, Neutral };
        static readonly string[] AccentClasses = { "fr-acc-cyan", "fr-acc-magenta", "fr-acc-lime", "fr-acc-amber", "fr-acc-red", "fr-acc-neutral" };

        /// <summary>Loads the token and component stylesheets onto a root element.</summary>
        public static void ApplyStyles(VisualElement root)
        {
            foreach (var path in new[] { "Frontier/Tokens", "Frontier/Frontier" })
            {
                var sheet = Resources.Load<StyleSheet>(path);
                if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            }
        }

        /// <summary>Clamps an HDR resource color into displayable UI space.</summary>
        public static Color Ui(Color c, float alpha = 1f)
        {
            return Fx.Displayable(c, alpha);
        }

        /// <summary>The stylesheet accent class (.fr-acc-*) nearest to a colour; data-driven colours snap to the palette.</summary>
        public static string AccentClass(Color c)
        {
            c = Ui(c);
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < AccentColors.Length; i++)
            {
                var a = AccentColors[i];
                float d = (a.r - c.r) * (a.r - c.r) + (a.g - c.g) * (a.g - c.g) + (a.b - c.b) * (a.b - c.b);
                if (d < bestD) { bestD = d; best = i; }
            }
            return AccentClasses[best];
        }

        /// <summary>Switches an element (and everything inside it) to the accent nearest to the colour.</summary>
        public static void SetAccent(VisualElement e, Color c)
        {
            ClearAccent(e);
            e.AddToClassList(AccentClass(c));
        }

        public static void ClearAccent(VisualElement e)
        {
            foreach (var cls in AccentClasses) e.RemoveFromClassList(cls);
        }

        /// <summary>A colour mixed into the dark surface colour (an opaque tint; translucent overlays blend unpredictably in linear space).</summary>
        public static Color Tint(Color c, float k)
        {
            return Color.Lerp(new Color(0.03f, 0.047f, 0.125f), Ui(c), k);
        }

        public static VisualElement Div(string cls = null)
        {
            VisualElement e = cls == "fr-tabs" ? new FrontierTabBar() :
                cls == "fr-card-head" ? new FrontierListRow() : new VisualElement();
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

        public static ScrollView VerticalScroll(string cls)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("fr-scroll");
            scroll.AddToClassList(cls);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            // Reserve the gutter from the first frame, including content rebuilds.
            scroll.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            scroll.contentContainer.style.minWidth = 0;
            scroll.contentContainer.style.width = Length.Percent(100);
            scroll.contentContainer.style.paddingRight = Space.S8;
            return scroll;
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

        /// <summary>A soft glowing halo behind an element. Neon glow is reserved for the primary action, selection and live state.</summary>
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

        /// <summary>
        /// A text button. Default is tonal glass with a thin accent outline; <paramref name="primary"/> is the single main
        /// action of a screen (accent fill and outline). Add children (icon + label) to build richer buttons.
        /// </summary>
        public static Button Btn(string text, Color color, Action onClick, bool big = false, bool primary = false)
        {
            var b = new FrontierButton { Large = big, Primary = primary, Accent = color };
            if (!string.IsNullOrEmpty(text)) b.text = text;
            if (onClick != null) b.clicked += onClick;
            return b;
        }

        public static void SetEnabledLook(Button b, bool enabled)
        {
            b.SetEnabled(enabled);
            b.style.opacity = enabled ? 1f : 0.4f;
        }

        public static VisualElement Bar(Color c, out VisualElement fill)
        {
            var track = new FrontierProgress { Tint = c };
            fill = track.Fill;
            return track;
        }

        public static void SetFill(VisualElement fill, float k)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(k) * 100f);
        }

        /// <summary>An icon + value chip (cost, reward, tag). The icon carries the colour, the chip stays tonal.</summary>
        public static VisualElement Chip(string icon, Color iconColor, string text, bool low = false)
        {
            return new FrontierChip { Tint = iconColor, Icon = icon, Text = text, Low = low };
        }

        public static VisualElement ItemChip(ResourceSO r, int amount, int have = -1)
        {
            bool short_ = have >= 0 && have < amount;
            string label = have >= 0 ? have + "/" + amount : amount.ToString();
            return Chip(Icons.ForResource(r), Ui(r.color), label, short_);
        }

        public static VisualElement CostRow(System.Collections.Generic.List<ItemAmount> items, GameState gs, bool showHave,
            Action<ResourceSO> onFind = null)
        {
            var row = Div("fr-row-wrap");
            foreach (var i in items)
            {
                if (i.resource == null) continue;
                var chip = ItemChip(i.resource, i.amount, showHave ? gs.Count(i.resource) : -1);
                if (onFind != null && gs.PlanetCount(i.resource) < i.amount)
                {
                    ResourceSO resource = i.resource;
                    var find = new Button(() => onFind(resource));
                    find.name = "find-resource-" + resource.id;
                    find.tooltip = UiText.Get("resource.find", resource.LocalizedName);
                    find.AddToClassList("fr-cost-resource");
                    chip.pickingMode = PickingMode.Ignore;
                    find.Add(chip);
                    row.Add(find);
                }
                else row.Add(chip);
            }
            return row;
        }
    }
}
