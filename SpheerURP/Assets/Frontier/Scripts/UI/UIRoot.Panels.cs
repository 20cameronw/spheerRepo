using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        VisualElement modal;
        ScrollView modalScroll;
        Action<VisualElement> modalBuild;
        string modalKey;
        int researchTab;

        // ------------------------------------------------------------ modal core
        void OpenModal(string key, string icon, string title, Color accent, Action<VisualElement> build, bool wide = false)
        {
            if (modalKey == key) { CloseModal(); return; }
            ClosePopup();
            CloseModal();
            CloseMap();
            ExitBuild();
            modalKey = key; modalBuild = build;
            Color c = UiKit.Ui(accent);

            var backdropEl = UiKit.Div("fr-backdrop");
            backdropEl.pickingMode = PickingMode.Position;
            backdropEl.RegisterCallback<PointerDownEvent>(e => { if (e.target == backdropEl) CloseModal(); });
            modal = backdropEl;

            var panel = UiKit.Div("fr-modal");
            if (wide) panel.AddToClassList("fr-modal-wide");
            UiKit.Border(panel, c, 3f);
            UiKit.Halo(panel, c, 30f, 0.22f);
            var head = UiKit.Div("fr-modal-head");
            head.Add(Icons.Make(icon, c, 52f));
            head.Add(UiKit.Text(title, "fr-modal-title", c));
            head.Add(Icons.IconButton("close", UiKit.Red, CloseModal, 76f));
            panel.Add(head);

            modalScroll = new ScrollView(ScrollViewMode.Vertical);
            modalScroll.AddToClassList("fr-modal-scroll");
            panel.Add(modalScroll);
            backdropEl.Add(panel);
            modalLayer.Add(backdropEl);
            build(modalScroll.contentContainer);
        }

        void CloseModal()
        {
            if (modal != null) modal.RemoveFromHierarchy();
            modal = null; modalScroll = null; modalBuild = null; modalKey = null;
        }

        void RefreshModal()
        {
            if (modal == null || modalBuild == null || modalScroll == null) return;
            var offset = modalScroll.scrollOffset;
            modalScroll.contentContainer.Clear();
            modalBuild(modalScroll.contentContainer);
            modalScroll.schedule.Execute(() => modalScroll.scrollOffset = offset).StartingIn(1);
        }

        void OpenShop() { if (defense.Active || busy) return; OpenModal("shop", "build", "BUILD", UiKit.Lime, BuildShop); }
        void OpenResearch() { if (defense.Active || busy) return; OpenModal("research", "research", "COMMAND", UiKit.Magenta, BuildResearch, true); }
        void OpenPack() { if (defense.Active || busy) return; OpenModal("pack", "pack", "BACKPACK", UiKit.Amber, BuildPack, true); }

        // ------------------------------------------------------------ build shop
        void BuildShop(VisualElement body)
        {
            body.Add(UiKit.Text("Place structures on " + gs.planet.displayName + ". One of each per planet.", "fr-muted"));
            foreach (var sd in gs.db.structures)
            {
                if (sd == null) continue;
                var def = sd;
                Color c = UiKit.Ui(def.color);
                var card = Card(c);
                card.style.marginTop = 10;
                var top = UiKit.Div("fr-row");
                top.Add(Icons.Make(StructureIcon(def.kind), c, 56f));
                var col = UiKit.Div(); col.style.flexGrow = 1; col.style.marginLeft = 10; col.pickingMode = PickingMode.Ignore;
                col.Add(UiKit.Text(def.displayName.ToUpper(), "fr-h2", c));
                col.Add(UiKit.Text(def.description, "fr-tiny"));
                top.Add(col);
                card.Add(top);

                bool built = gs.HasStructure(gs.planet, def);
                if (built) card.Add(UiKit.Text("BUILT - tap it on the planet", "fr-small", UiKit.Lime));
                else
                {
                    card.Add(UiKit.CostRow(def.buildCost, gs, true));
                    bool can = gs.Has(def.buildCost);
                    var place = IconBtn("PLACE", "build", c, () => { CloseModal(); EnterBuild(def); });
                    UiKit.SetEnabledLook(place, can);
                    card.Add(place);
                }
                body.Add(card);
            }
        }

        // ------------------------------------------------------------ command (bases + research)
        void BuildResearch(VisualElement body)
        {
            var tabs = UiKit.Div("fr-tabs");
            tabs.Add(Tab("base", "BASES", 0));
            tabs.Add(Tab("research", "RESEARCH", 1));
            body.Add(tabs);
            if (researchTab == 0) BuildBases(body); else BuildUpgrades(body);
        }

        VisualElement Tab(string icon, string label, int index)
        {
            bool on = researchTab == index;
            Color c = on ? UiKit.Magenta : new Color(0.55f, 0.65f, 0.8f);
            var t = new Button();
            t.AddToClassList("fr-tab");
            UiKit.Border(t, UiKit.Ui(c, on ? 1f : 0.4f), 2f);
            t.Add(Icons.Make(icon, c, 36f));
            t.Add(UiKit.Text(label, "fr-small", UiKit.Ui(c)));
            t.clicked += () => { Sfx.Click(); researchTab = index; RefreshModal(); };
            return t;
        }

        void BuildBases(VisualElement body)
        {
            var planets = new List<PlanetSO>(gs.db.planets);
            planets.Sort((a, b) => (b == gs.planet ? 1 : 0).CompareTo(a == gs.planet ? 1 : 0));
            bool any = false;
            foreach (var p in planets)
            {
                var here = gs.structures.FindAll(s => s.planetId == p.id);
                if (p != gs.planet && here.Count == 0) continue;
                any = true;
                var h = UiKit.Div("fr-row"); h.style.marginTop = 6; h.style.marginBottom = 8;
                h.Add(UiKit.Text(p.displayName.ToUpper(), "fr-small", UiKit.Ui(p.rim)));
                if (p == gs.planet) h.Add(UiKit.Text("YOU ARE HERE", "fr-tiny"));
                body.Add(h);
                if (here.Count == 0) body.Add(UiKit.Text("No base yet - place structures from the build menu.", "fr-muted"));
                foreach (var st in here)
                {
                    var s = st;
                    Color c = UiKit.Ui(s.def.color);
                    var card = Card(c);
                    var top = UiKit.Div("fr-row");
                    top.Add(Icons.Make(StructureIcon(s.def.kind), c, 48f));
                    var col = UiKit.Div(); col.style.flexGrow = 1; col.style.marginLeft = 10; col.pickingMode = PickingMode.Ignore;
                    col.Add(UiKit.Text(s.def.displayName.ToUpper() + "  LV " + s.level + "/" + s.def.maxLevel, "fr-h2", c));
                    bool max = s.level >= s.def.maxLevel;
                    col.Add(UiKit.Text(StructureEffect(s.def, s.level) + (max ? "" : "  >  " + StructureEffect(s.def, s.level + 1)), "fr-tiny"));
                    top.Add(col);
                    card.Add(top);
                    if (!max)
                    {
                        var cost = s.def.UpgradeCost(s.level);
                        card.Add(UiKit.CostRow(cost, gs, true));
                        var up = IconBtn("UPGRADE", "up", c, () =>
                        {
                            if (gs.UpgradeStructure(s)) { Sfx.Good(); Toast(s.def.displayName + " LV " + s.level, c); }
                            else Sfx.Bad();
                        });
                        UiKit.SetEnabledLook(up, gs.Has(cost));
                        card.Add(up);
                    }
                    else card.Add(UiKit.Text("MAXED", "fr-small", UiKit.Lime));
                    body.Add(card);
                }
            }
            if (!any) body.Add(UiKit.Text("Build a refinery and an alarm to establish a base.", "fr-muted"));
        }

        void BuildUpgrades(VisualElement body)
        {
            foreach (UpgradeCategory cat in Enum.GetValues(typeof(UpgradeCategory)))
            {
                var h = UiKit.Text(cat.ToString().ToUpper(), "fr-small", UiKit.Magenta);
                h.style.marginTop = 6; h.style.marginBottom = 8;
                body.Add(h);
                foreach (var up in gs.db.upgrades)
                {
                    if (up == null || up.category != cat) continue;
                    var u = up;
                    int lvl = gs.Level(u);
                    bool max = lvl >= u.maxLevel;
                    var card = Card(u.color);
                    var top = UiKit.Div("fr-row");
                    top.Add(UiKit.Text(u.NameAtLevel(lvl), "fr-h2", UiKit.Ui(u.color)));
                    top.Add(UiKit.Div("fr-spacer"));
                    top.Add(UiKit.Text("LV " + lvl + "/" + u.maxLevel, "fr-small"));
                    card.Add(top);
                    card.Add(UiKit.Text(u.description, "fr-muted"));
                    card.Add(UiKit.Text("Now " + UpgradeEffectText(u, lvl) + (max ? "" : "  >  " + UpgradeEffectText(u, lvl + 1)), "fr-tiny"));
                    if (!max)
                    {
                        var cost = u.CostAt(lvl);
                        card.Add(UiKit.CostRow(cost, gs, true));
                        var buy = IconBtn("UPGRADE", "up", u.color, () =>
                        {
                            if (gs.Buy(u)) { Sfx.Good(); Toast(u.NameAtLevel(gs.Level(u)) + " ready!", UiKit.Ui(u.color)); planetView.Punch(); }
                            else Sfx.Bad();
                        });
                        UiKit.SetEnabledLook(buy, gs.Has(cost));
                        card.Add(buy);
                    }
                    else card.Add(UiKit.Text("MAXED", "fr-small", UiKit.Lime));
                    body.Add(card);
                }
            }
        }

        // ------------------------------------------------------------ backpack (Minecraft style)
        const int PackColumns = 9;

        void BuildPack(VisualElement body)
        {
            body.Add(UiKit.Text(gs.SlotsUsed + " / " + gs.Slots + " slots used", "fr-muted"));
            var grid = UiKit.Div("fr-grid");
            grid.style.marginTop = 8;

            var slots = new List<VisualElement>();
            int used = 0;
            foreach (var r in gs.db.resources)
            {
                int have = gs.Count(r);
                while (have > 0 && used < gs.Slots)
                {
                    int inSlot = Mathf.Min(have, r.stackSize);
                    slots.Add(Slot(r, inSlot, false));
                    have -= inSlot; used++;
                }
            }
            for (; used < gs.Slots; used++) slots.Add(Slot(null, 0, false));

            // Pad the final row and show the next upgrade tier of slots as locked.
            int rows = Mathf.CeilToInt(slots.Count / (float)PackColumns);
            while (slots.Count < rows * PackColumns) slots.Add(Slot(null, 0, true));
            foreach (var s in slots) grid.Add(s);

            grid.RegisterCallback<GeometryChangedEvent>(e =>
            {
                float cell = Mathf.Floor(grid.resolvedStyle.width / PackColumns);
                if (float.IsNaN(cell) || cell < 8f) return;
                foreach (var s in slots) { s.style.width = cell; s.style.height = cell; }
            });
            body.Add(grid);

            var list = UiKit.Div(); list.style.marginTop = 14;
            foreach (var r in gs.db.resources)
                if (gs.Count(r) > 0)
                {
                    var row = UiKit.Div("fr-row");
                    row.Add(Icons.Make(Icons.ForResource(r), UiKit.Ui(r.color), 34f));
                    row.Add(UiKit.Text(r.displayName, "fr-small"));
                    row.Add(UiKit.Div("fr-spacer"));
                    row.Add(UiKit.Text(gs.Count(r).ToString(), "fr-small"));
                    list.Add(row);
                }
            body.Add(list);
            body.Add(UiKit.Text("Upgrade the backpack in Command > Research for more slots.", "fr-tiny"));
        }

        VisualElement Slot(ResourceSO r, int count, bool locked)
        {
            var s = UiKit.Div("fr-slot");
            if (locked) s.AddToClassList("fr-slot-locked");
            if (r != null)
            {
                Color c = UiKit.Ui(r.color);
                UiKit.Border(s, new Color(c.r, c.g, c.b, 0.9f), 2f);
                s.Add(Icons.Make(Icons.ForResource(r), c));
                s.Add(UiKit.Text(count.ToString(), "fr-slot-count"));
            }
            return s;
        }
    }
}
