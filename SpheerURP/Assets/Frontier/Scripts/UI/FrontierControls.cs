using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    [UxmlElement]
    public partial class FrontierButton : Button
    {
        [UxmlAttribute]
        public bool Primary
        {
            get { return ClassListContains("fr-btn-primary"); }
            set { EnableInClassList("fr-btn-primary", value); }
        }

        [UxmlAttribute]
        public bool Large
        {
            get { return ClassListContains("fr-btn-big"); }
            set { EnableInClassList("fr-btn-big", value); }
        }

        Color accent = UiKit.Cyan;
        [UxmlAttribute]
        public Color Accent
        {
            get { return accent; }
            set { accent = value; UiKit.SetAccent(this, value); }
        }

        public FrontierButton()
        {
            AddToClassList("fr-btn");
            Accent = accent;
            clicked += Sfx.Click;
        }
    }

    [UxmlElement]
    public partial class FrontierCard : VisualElement
    {
        [UxmlAttribute]
        public bool Highlighted
        {
            get { return ClassListContains("fr-card-on"); }
            set
            {
                EnableInClassList("fr-card-on", value);
                if (value) UiKit.SetAccent(this, accent);
                else UiKit.ClearAccent(this);
            }
        }

        Color accent = UiKit.Cyan;
        [UxmlAttribute]
        public Color Accent
        {
            get { return accent; }
            set { accent = value; if (Highlighted) UiKit.SetAccent(this, value); }
        }

        public FrontierCard()
        {
            AddToClassList("fr-card");
        }

    }

    [UxmlElement]
    public partial class FrontierProgress : VisualElement
    {
        public VisualElement Fill { get; }
        Color tint = UiKit.Cyan;

        [UxmlAttribute]
        public float Value
        {
            get { return Fill.style.width.value.value / 100f; }
            set
            {
                UiKit.SetFill(Fill, float.IsNaN(value) ? 0f : value);
            }
        }

        [UxmlAttribute]
        public Color Tint
        {
            get { return tint; }
            set { tint = value; Fill.style.backgroundColor = UiKit.Ui(value); }
        }

        public FrontierProgress()
        {
            AddToClassList("fr-bar");
            pickingMode = PickingMode.Ignore;
            Fill = UiKit.Div("fr-bar-fill");
            hierarchy.Add(Fill);
            Tint = tint;
            Value = 0f;
        }
    }

    [UxmlElement]
    public partial class FrontierChip : VisualElement
    {
        readonly Label caption;
        VisualElement glyph;
        string icon = "";
        Color tint = UiKit.Cyan;

        [UxmlAttribute]
        public string Text
        {
            get { return caption.text; }
            set { caption.text = value ?? ""; }
        }

        [UxmlAttribute]
        public string Icon
        {
            get { return icon; }
            set
            {
                icon = value ?? "";
                RefreshGlyph();
            }
        }

        [UxmlAttribute]
        public Color Tint
        {
            get { return tint; }
            set { tint = value; RefreshGlyph(); }
        }

        [UxmlAttribute]
        public bool Low
        {
            get { return ClassListContains("fr-itemchip-low"); }
            set
            {
                EnableInClassList("fr-itemchip-low", value);
                caption.style.color = value ? new StyleColor(UiKit.Red) : new StyleColor(StyleKeyword.Null);
            }
        }

        public FrontierChip()
        {
            AddToClassList("fr-itemchip");
            caption = UiKit.Text("", "fr-small");
            Add(caption);
        }

        void RefreshGlyph()
        {
            if (glyph != null) glyph.RemoveFromHierarchy();
            glyph = null;
            if (string.IsNullOrEmpty(icon)) return;
            glyph = Icons.Make(icon, tint);
            Insert(0, glyph);
        }
    }

    [UxmlElement]
    public partial class FrontierTab : Button
    {
        readonly Label caption;
        VisualElement glyph;
        string icon = "";
        Color accent = UiKit.Cyan;
        bool selected;

        [UxmlAttribute]
        public string Caption
        {
            get { return caption.text; }
            set { caption.text = value ?? ""; }
        }

        [UxmlAttribute]
        public string Icon
        {
            get { return icon; }
            set { icon = value ?? ""; RefreshGlyph(); }
        }

        [UxmlAttribute]
        public bool Selected
        {
            get { return selected; }
            set { selected = value; EnableInClassList("fr-tab-on", value); RefreshGlyph(); }
        }

        [UxmlAttribute]
        public Color Accent
        {
            get { return accent; }
            set { accent = value; UiKit.SetAccent(this, value); RefreshGlyph(); }
        }

        public FrontierTab()
        {
            AddToClassList("fr-tab");
            caption = UiKit.Text("", "fr-tab-label");
            Add(caption);
            Accent = accent;
            clicked += Sfx.Click;
        }

        void RefreshGlyph()
        {
            if (glyph != null) glyph.RemoveFromHierarchy();
            glyph = null;
            if (string.IsNullOrEmpty(icon)) return;
            glyph = Icons.Make(icon, selected ? UiKit.Ui(accent) : UiKit.Muted, UiKit.Size.IconMd);
            Insert(0, glyph);
        }
    }

    [UxmlElement]
    public partial class FrontierTabBar : VisualElement
    {
        public FrontierTabBar() { AddToClassList("fr-tabs"); }
    }

    [UxmlElement]
    public partial class FrontierStatRow : VisualElement
    {
        readonly Label caption, amount;
        VisualElement glyph;
        string icon = "";
        Color tint = UiKit.Cyan;

        [UxmlAttribute]
        public string Caption
        {
            get { return caption.text; }
            set { caption.text = value ?? ""; }
        }

        [UxmlAttribute]
        public string Value
        {
            get { return amount.text; }
            set { amount.text = value ?? ""; }
        }

        [UxmlAttribute]
        public string Icon
        {
            get { return icon; }
            set { icon = value ?? ""; RefreshGlyph(); }
        }

        [UxmlAttribute]
        public Color Tint
        {
            get { return tint; }
            set { tint = value; RefreshGlyph(); }
        }

        public FrontierStatRow()
        {
            AddToClassList("fr-row");
            style.marginTop = UiKit.Space.S12;
            caption = UiKit.Text("", "fr-small");
            amount = UiKit.Text("", "fr-h2");
            Add(caption);
            Add(UiKit.Div("fr-spacer"));
            Add(amount);
        }

        void RefreshGlyph()
        {
            if (glyph != null) glyph.RemoveFromHierarchy();
            glyph = null;
            if (string.IsNullOrEmpty(icon)) return;
            glyph = Icons.Make(icon, UiKit.Ui(tint), UiKit.Size.IconMd);
            glyph.style.marginRight = UiKit.Space.S12;
            Insert(0, glyph);
        }
    }

    [UxmlElement]
    public partial class FrontierModal : VisualElement
    {
        [UxmlAttribute]
        public bool Wide
        {
            get { return ClassListContains("fr-modal-wide"); }
            set { EnableInClassList("fr-modal-wide", value); }
        }

        public FrontierModal() { AddToClassList("fr-modal"); FrontierMotion.Prepare(this); }
    }

    [UxmlElement]
    public partial class FrontierPopup : VisualElement
    {
        [UxmlAttribute]
        public bool Compact
        {
            get { return ClassListContains("fr-popup-compact"); }
            set { EnableInClassList("fr-popup-compact", value); }
        }

        public FrontierPopup() { AddToClassList("fr-popup"); FrontierMotion.Prepare(this); }
    }

    [UxmlElement]
    public partial class FrontierListRow : VisualElement
    {
        public FrontierListRow() { AddToClassList("fr-card-head"); }
    }

    [UxmlElement]
    public partial class FrontierBadge : Label
    {
        Color tint = UiKit.Muted;
        [UxmlAttribute]
        public Color Tint
        {
            get { return tint; }
            set { tint = value; style.color = value; }
        }

        public FrontierBadge()
        {
            AddToClassList("fr-tiny");
            pickingMode = PickingMode.Ignore;
            Tint = tint;
        }
    }

    [UxmlElement]
    public partial class FrontierToast : VisualElement
    {
        readonly Label caption;
        Color tint = UiKit.Cyan;

        [UxmlAttribute]
        public string Text
        {
            get { return caption.text; }
            set { caption.text = value ?? ""; }
        }

        [UxmlAttribute]
        public Color Tint
        {
            get { return tint; }
            set { tint = value; UiKit.Border(this, value); caption.style.color = value; }
        }

        public FrontierToast()
        {
            AddToClassList("fr-toast");
            FrontierMotion.Prepare(this);
            pickingMode = PickingMode.Ignore;
            caption = UiKit.Text("", "fr-h2");
            Add(caption);
            Tint = tint;
        }
    }

    static class FrontierMotion
    {
        public static void Prepare(VisualElement element)
        {
            element.usageHints |= UsageHints.DynamicTransform;
            element.AddToClassList("fr-surface-enter");
            element.AddToClassList("fr-surface-enter-start");
            element.RegisterCallbackOnce<GeometryChangedEvent>(e =>
            {
                // Wait for the first styled layout so a slow first frame cannot skip the entrance.
                element.schedule.Execute(() => element.RemoveFromClassList("fr-surface-enter-start")).StartingIn(16);
            });
        }
    }
}
