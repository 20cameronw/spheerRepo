using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    /// <summary>Vector icons drawn with Painter2D so the UI never needs words or imported sprites.</summary>
    public static class Icons
    {
        struct Shape
        {
            public Vector2[] pts;
            public bool closed, fill;
        }

        static Shape S(bool closed, bool fill, params float[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return new Shape { pts = p, closed = closed, fill = fill };
        }

        static Shape Circle(float cx, float cy, float r, bool fill = false, int n = 22)
        {
            var p = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                p[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return new Shape { pts = p, closed = true, fill = fill };
        }

        static Shape Star(float cx, float cy, float ro, float ri, int points, bool fill)
        {
            var p = new Vector2[points * 2];
            for (int i = 0; i < p.Length; i++)
            {
                float a = -Mathf.PI / 2f + i / (float)p.Length * Mathf.PI * 2f;
                float r = i % 2 == 0 ? ro : ri;
                p[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return new Shape { pts = p, closed = true, fill = fill };
        }

        static readonly Dictionary<string, Shape[]> Library = new Dictionary<string, Shape[]>
        {
            { "map", new[] {
                S(true, false, .1f,.22f, .37f,.1f, .63f,.22f, .9f,.1f, .9f,.78f, .63f,.9f, .37f,.78f, .1f,.9f),
                S(false, false, .37f,.1f, .37f,.78f), S(false, false, .63f,.22f, .63f,.9f) } },
            { "build", new[] {
                S(true, false, .18f,.5f, .5f,.14f, .82f,.5f), S(true, false, .26f,.5f, .26f,.88f, .74f,.88f, .74f,.5f),
                S(true, true, .42f,.88f, .42f,.64f, .58f,.64f, .58f,.88f) } },
            { "research", new[] {
                S(true, true, .4f,.1f, .6f,.1f, .6f,.42f, .86f,.86f, .14f,.86f, .4f,.42f),
                S(false, false, .3f,.1f, .7f,.1f), S(false, false, .26f,.68f, .74f,.68f) } },
            { "pack", new[] {
                S(true, false, .24f,.32f, .76f,.32f, .82f,.9f, .18f,.9f), S(false, false, .36f,.32f, .36f,.14f, .64f,.14f, .64f,.32f),
                S(true, false, .34f,.62f, .66f,.62f, .66f,.82f, .34f,.82f) } },
            { "fuel", new[] {
                S(true, true, .5f,.08f, .74f,.44f, .8f,.62f, .72f,.8f, .5f,.92f, .28f,.8f, .2f,.62f, .26f,.44f) } },
            { "wood", new[] {
                S(true, false, .26f,.3f, .86f,.3f, .86f,.7f, .26f,.7f), Circle(.26f,.5f,.2f), Circle(.26f,.5f,.08f),
                S(false, false, .5f,.42f, .76f,.42f), S(false, false, .5f,.58f, .76f,.58f) } },
            { "stone", new[] {
                S(true, true, .12f,.76f, .2f,.44f, .4f,.22f, .66f,.28f, .88f,.54f, .84f,.78f) } },
            { "iron", new[] {
                S(true, true, .3f,.3f, .86f,.3f, .7f,.5f, .14f,.5f), S(true, false, .14f,.5f, .7f,.5f, .7f,.76f, .14f,.76f),
                S(false, false, .7f,.5f, .86f,.3f, .86f,.56f, .7f,.76f) } },
            { "crystal", new[] {
                S(true, true, .5f,.06f, .8f,.38f, .5f,.94f, .2f,.38f), S(false, false, .2f,.38f, .8f,.38f),
                S(false, false, .5f,.06f, .38f,.38f, .5f,.94f), S(false, false, .5f,.06f, .62f,.38f, .5f,.94f) } },
            { "core", new[] {
                Circle(.5f,.5f,.38f), S(false, false, .14f,.5f, .3f,.62f, .5f,.68f, .7f,.62f, .86f,.5f), Circle(.36f,.34f,.06f,true) } },
            { "alarm", new[] {
                S(true, true, .3f,.76f, .3f,.46f, .4f,.26f, .6f,.26f, .7f,.46f, .7f,.76f),
                S(false, false, .18f,.76f, .82f,.76f), Circle(.5f,.88f,.06f,true), S(false, false, .5f,.08f, .5f,.16f),
                S(false, false, .12f,.3f, .2f,.36f), S(false, false, .88f,.3f, .8f,.36f) } },
            { "refinery", new[] {
                S(true, true, .1f,.86f, .1f,.46f, .36f,.62f, .36f,.4f, .62f,.62f, .62f,.16f, .82f,.16f, .82f,.86f),
                S(false, false, .7f,.1f, .74f,.04f) } },
            { "close", new[] { S(false, false, .22f,.22f, .78f,.78f), S(false, false, .78f,.22f, .22f,.78f) } },
            { "plus", new[] { S(false, false, .5f,.2f, .5f,.8f), S(false, false, .2f,.5f, .8f,.5f) } },
            { "minus", new[] { S(false, false, .2f,.5f, .8f,.5f) } },
            { "up", new[] { S(true, true, .5f,.1f, .14f,.52f, .38f,.52f, .38f,.9f, .62f,.9f, .62f,.52f, .86f,.52f) } },
            { "bolt", new[] { S(true, true, .58f,.06f, .22f,.54f, .46f,.54f, .38f,.94f, .78f,.4f, .54f,.4f) } },
            { "play", new[] { S(true, true, .28f,.14f, .86f,.5f, .28f,.86f) } },
            { "star", new[] { Star(.5f,.52f,.44f,.18f,5,true) } },
            { "gear", new[] { Star(.5f,.5f,.44f,.34f,8,false), Circle(.5f,.5f,.14f) } },
            { "base", new[] {
                S(true, false, .1f,.84f, .1f,.5f, .5f,.16f, .9f,.5f, .9f,.84f), S(false, false, .4f,.84f, .4f,.6f, .6f,.6f, .6f,.84f) } },
            { "warp", new[] {
                Circle(.5f,.5f,.36f), Circle(.5f,.5f,.16f), S(false, false, .5f,.04f, .5f,.14f), S(false, false, .5f,.86f, .5f,.96f) } },
            { "skull", new[] {
                Circle(.5f,.42f,.32f), S(true, false, .36f,.7f, .36f,.9f, .64f,.9f, .64f,.7f),
                Circle(.38f,.42f,.07f,true), Circle(.62f,.42f,.07f,true) } },
        };

        public static bool Has(string name) { return Library.ContainsKey(name); }

        public static string ForResource(ResourceSO r)
        {
            if (r == null) return "core";
            string id = (r.id ?? "").ToLower();
            foreach (var k in new[] { "wood", "stone", "iron", "crystal", "fuel", "core" })
                if (id.Contains(k)) return k;
            if (id.Contains("ore") || id.Contains("metal")) return "iron";
            if (id.Contains("gem")) return "crystal";
            return "core";
        }

        public static VisualElement Make(string name, Color color, float size = 0f)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList("fr-icon");
            if (size > 0f) { e.style.width = size; e.style.height = size; e.style.flexShrink = 0; }
            Shape[] shapes;
            if (!Library.TryGetValue(name, out shapes)) shapes = Library["core"];
            Color c = UiKit.Ui(color);
            e.generateVisualContent += ctx =>
            {
                var r = e.contentRect;
                if (r.width < 1f || r.height < 1f) return;
                float s = Mathf.Min(r.width, r.height);
                Vector2 o = new Vector2((r.width - s) * 0.5f, (r.height - s) * 0.5f);
                var p = ctx.painter2D;
                p.lineWidth = Mathf.Max(1.5f, s * 0.075f);
                p.lineJoin = LineJoin.Round;
                p.lineCap = LineCap.Round;
                p.strokeColor = c;
                p.fillColor = new Color(c.r, c.g, c.b, 0.32f);
                foreach (var sh in shapes)
                {
                    p.BeginPath();
                    p.MoveTo(o + sh.pts[0] * s);
                    for (int i = 1; i < sh.pts.Length; i++) p.LineTo(o + sh.pts[i] * s);
                    if (sh.closed) p.ClosePath();
                    if (sh.fill) p.Fill();
                    p.Stroke();
                }
            };
            e.RegisterCallback<GeometryChangedEvent>(_ => e.MarkDirtyRepaint());
            return e;
        }

        /// <summary>A square neon icon button.</summary>
        public static Button IconButton(string icon, Color color, System.Action onClick, float size = 0f)
        {
            var b = new Button();
            b.AddToClassList("fr-iconbtn");
            Color c = UiKit.Ui(color);
            UiKit.Border(b, c, 3f);
            b.style.backgroundColor = new Color(c.r * 0.12f, c.g * 0.12f, c.b * 0.12f, 0.88f);
            if (size > 0f) { b.style.width = size; b.style.height = size; }
            var ic = Make(icon, c);
            ic.AddToClassList("fr-iconbtn-icon");
            b.Add(ic);
            b.clicked += () => { Sfx.Click(); if (onClick != null) onClick(); };
            return b;
        }
    }
}
