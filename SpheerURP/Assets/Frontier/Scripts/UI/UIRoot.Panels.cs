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
        void OpenPack() { if (defense.Active || busy) return; menuPage = "pack"; OpenModal("menu", "grid", "MENU", UiKit.Cyan, BuildMenu, true); }

        // ------------------------------------------------------------ build shop
        int shopTab;
        int toolTab;

        VisualElement TabButton(string icon, string label, bool on, Color accent, Action onClick)
        {
            Color c = on ? accent : new Color(0.55f, 0.65f, 0.8f);
            var t = new Button();
            t.AddToClassList("fr-tab");
            UiKit.Border(t, UiKit.Ui(c, on ? 1f : 0.4f), 2f);
            t.Add(Icons.Make(icon, c, 32f));
            t.Add(UiKit.Text(label, "fr-small", UiKit.Ui(c)));
            t.clicked += () => { Sfx.Click(); onClick(); RefreshModal(); };
            return t;
        }

        void BuildShop(VisualElement body)
        {
            var tabs = UiKit.Div("fr-tabs");
            tabs.Add(TabButton("refinery", "ECON", shopTab == 0, UiKit.Lime, () => shopTab = 0));
            tabs.Add(TabButton("tools", "GATHER", shopTab == 1, UiKit.Lime, () => shopTab = 1));
            tabs.Add(TabButton("alien", "DEFENSE", shopTab == 2, UiKit.Lime, () => shopTab = 2));
            tabs.Add(TabButton("warp", "ORBIT", shopTab == 3, UiKit.Lime, () => shopTab = 3));
            body.Add(tabs);
            body.Add(UiKit.Text("Build on " + gs.planet.displayName + ". Tap a structure on the planet to use it.", "fr-muted"));
            switch (shopTab)
            {
                case 0: ShopSection(body, StructureKind.Refinery, StructureKind.Fabricator, StructureKind.Alarm); break;
                case 1: ShopSection(body, StructureKind.Lumber, StructureKind.Mine, StructureKind.Drill); break;
                case 2: ShopSection(body, StructureKind.Gunner, StructureKind.Laser); break;
                default: ShopSection(body, StructureKind.Satellite); break;
            }
        }

        void ShopSection(VisualElement body, params StructureKind[] kinds)
        {
            var list = new List<StructureSO>();
            foreach (var sd in gs.db.structures)
                if (sd != null && Array.IndexOf(kinds, sd.kind) >= 0) list.Add(sd);
            if (list.Count == 0) { body.Add(UiKit.Text("Nothing here yet.", "fr-muted")); return; }
            foreach (var sd in list)
            {
                var def = sd;
                Color c = UiKit.Ui(def.color);
                var card = Card(c);
                var top = UiKit.Div("fr-row");
                top.Add(Icons.Make(StructureIcon(def.kind), c, 56f));
                var col = UiKit.Div(); col.style.flexGrow = 1; col.style.marginLeft = 10; col.pickingMode = PickingMode.Ignore;
                col.Add(UiKit.Text(def.displayName.ToUpper(), "fr-h2", c));
                col.Add(UiKit.Text(def.description, "fr-tiny"));
                top.Add(col);
                int have = gs.CountOnPlanet(gs.planet, def);
                top.Add(UiKit.Text(def.maxPerPlanet > 0 ? have + "/" + def.maxPerPlanet : "x" + have, "fr-small", c));
                card.Add(top);

                if (gs.AtLimit(gs.planet, def)) card.Add(UiKit.Text("BUILT - tap it on the planet", "fr-small", UiKit.Lime));
                else if (!gs.CanBuildHere(def)) card.Add(UiKit.Text("Nothing to harvest on this planet", "fr-small", UiKit.Amber));
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

        // ------------------------------------------------------------ menu (leaderboard, tutorial, about, settings)
        void OpenMenu() { if (defense.Active || busy) return; menuPage = ""; OpenModal("menu", "grid", "MENU", UiKit.Cyan, BuildMenu, true); }

        string menuPage = "";

        void BuildMenu(VisualElement body)
        {
            if (menuPage != "")
            {
                var back = IconBtn("BACK", "up", UiKit.Cyan, () => { menuPage = ""; RefreshModal(); });
                back.AddToClassList("fr-back");
                body.Add(back);
                switch (menuPage)
                {
                    case "pack": BuildPack(body); break;
                    case "ranks": BuildLeaderboard(body); break;
                    case "tutorial": BuildTutorial(body); break;
                    case "about": BuildAbout(body); break;
                    default: BuildSettings(body); break;
                }
                return;
            }
            var grid = UiKit.Div("fr-menu-grid");
            grid.Add(MenuTile("pack", "BACKPACK", UiKit.Amber, "pack"));
            grid.Add(MenuTile("ranks", "RANKS", UiKit.Lime, "ranks"));
            grid.Add(MenuTile("tutorial", "HOW TO PLAY", UiKit.Cyan, "tutorial"));
            grid.Add(MenuTile("about", "ABOUT", UiKit.Magenta, "about"));
            grid.Add(MenuTile("gear", "SETTINGS", UiKit.Cyan, "settings"));
            body.Add(grid);
        }

        VisualElement MenuTile(string icon, string label, Color color, string page)
        {
            Color c = UiKit.Ui(color);
            var b = new Button();
            b.AddToClassList("fr-menu-tile");
            UiKit.Border(b, c, 2f);
            b.Add(Icons.Make(icon, c));
            b.Add(UiKit.Text(label, "fr-small", c));
            b.clicked += () => { Sfx.Click(); menuPage = page; RefreshModal(); };
            return b;
        }

        void StatRow(VisualElement body, string icon, Color color, string label, string value)
        {
            var row = UiKit.Div("fr-row"); row.style.marginBottom = 10;
            row.Add(Icons.Make(icon, UiKit.Ui(color), 44f));
            row.Add(UiKit.Text(label, "fr-small"));
            row.Add(UiKit.Div("fr-spacer"));
            row.Add(UiKit.Text(value, "fr-h2", color));
            body.Add(row);
        }

        void BuildLeaderboard(VisualElement body)
        {
            var card = Card(UiKit.Lime);
            card.Add(UiKit.Text("YOUR RECORD", "fr-h2", UiKit.Lime));
            StatRow(card, "star", UiKit.Magenta, "Level", gs.PlayerLevel.ToString());
            StatRow(card, "alien", UiKit.Red, "Waves cleared", gs.totalWavesCleared.ToString());
            StatRow(card, "base", UiKit.Cyan, "Structures", gs.structures.Count.ToString());
            var planets = new HashSet<string>();
            foreach (var s in gs.structures) planets.Add(s.planetId);
            StatRow(card, "map", UiKit.Amber, "Bases", planets.Count.ToString());
            body.Add(card);
            body.Add(UiKit.Text("Online leaderboards aren't connected yet - this shows your local progress.", "fr-muted"));
        }

        void BuildTutorial(VisualElement body)
        {
            TutorialStep(body, "build", UiKit.Lime, "1. Build", "Open the shop (bottom right) and place a Lumber Camp, then a Refinery.");
            TutorialStep(body, "wood", UiKit.Amber, "2. Gather", "Tap a Lumber Camp, Mine or Drill on the planet to start its mini-game and collect resources.");
            TutorialStep(body, "fuel", UiKit.Amber, "3. Refine", "Tap the Refinery to turn raw resources into fuel.");
            TutorialStep(body, "alarm", UiKit.Magenta, "4. Defend", "Tap the Alarm and spend fuel to call a wave. More fuel means a longer fight and bigger rewards. Gunners and Lasers defend automatically.");
            TutorialStep(body, "map", UiKit.Cyan, "5. Travel", "Open the world map (top left) and spend fuel to visit new planets. Your bases stay where you built them.");
            TutorialStep(body, "research", UiKit.Magenta, "6. Upgrade", "The research button (bottom left) manages your bases and unlocks upgrades, including more backpack space.");
        }

        void TutorialStep(VisualElement body, string icon, Color color, string title, string text)
        {
            var card = Card(color);
            var row = UiKit.Div("fr-row");
            row.Add(Icons.Make(icon, UiKit.Ui(color), 56f));
            var col = UiKit.Div(); col.style.marginLeft = 12; col.style.flexShrink = 1; col.pickingMode = PickingMode.Ignore;
            col.Add(UiKit.Text(title, "fr-h2", color));
            col.Add(UiKit.Text(text, "fr-muted"));
            row.Add(col);
            card.Add(row);
            body.Add(card);
        }

        void BuildAbout(VisualElement body)
        {
            var card = Card(UiKit.Magenta);
            card.Add(UiKit.Text("FRONTIER", "fr-title", UiKit.Magenta));
            card.Add(UiKit.Text("Build bases across the stars. Gather, refine, travel, and defend your worlds from alien waves.", "fr-muted"));
            card.Add(UiKit.Text("Version " + Application.version, "fr-tiny"));
            body.Add(card);
        }

        bool confirmReset;

        void BuildSettings(VisualElement body)
        {
            var snd = IconBtn(Sfx.Muted ? "SOUND OFF" : "SOUND ON", "sound", Sfx.Muted ? UiKit.Red : UiKit.Lime, () =>
            {
                Sfx.Muted = !Sfx.Muted;
                PlayerPrefs.SetInt("fr_muted", Sfx.Muted ? 1 : 0);
                RefreshModal();
            });
            body.Add(snd);

            var reset = IconBtn(confirmReset ? "TAP AGAIN TO ERASE" : "RESET PROGRESS", "close", UiKit.Red, () =>
            {
                if (!confirmReset) { confirmReset = true; RefreshModal(); return; }
                confirmReset = false;
                CloseModal();
                gs.ResetProgress();
                planetView.Show(gs.planet, true);
                selected = gs.planet;
            });
            body.Add(reset);
        }

        // ------------------------------------------------------------ command (bases + research)
        void BuildResearch(VisualElement body)
        {
            var tabs = UiKit.Div("fr-tabs");
            tabs.Add(TabButton("base", "BASES", researchTab == 0, UiKit.Magenta, () => researchTab = 0));
            tabs.Add(TabButton("tools", "TOOLS", researchTab == 1, UiKit.Magenta, () => researchTab = 1));
            tabs.Add(TabButton("warp", "SHIP", researchTab == 2, UiKit.Magenta, () => researchTab = 2));
            tabs.Add(TabButton("alien", "DEFENSE", researchTab == 3, UiKit.Magenta, () => researchTab = 3));
            body.Add(tabs);
            switch (researchTab)
            {
                case 0: BuildBases(body); break;
                case 1: BuildTools(body); break;
                case 2: BuildUpgrades(body, UpgradeCategory.Ship); break;
                default: BuildUpgrades(body, UpgradeCategory.Defense); break;
            }
        }

        void BuildTools(VisualElement body)
        {
            var tools = new List<UpgradeSO>();
            foreach (var u in gs.db.upgrades)
                if (u != null && u.category == UpgradeCategory.Tools) tools.Add(u);
            if (tools.Count == 0) return;
            toolTab = Mathf.Clamp(toolTab, 0, tools.Count - 1);
            var sub = UiKit.Div("fr-tabs");
            for (int i = 0; i < tools.Count; i++)
            {
                int idx = i;
                string ic = tools[i].tool == MinigameKind.Chop ? "wood" : tools[i].tool == MinigameKind.Mine ? "iron" : "tools";
                sub.Add(TabButton(ic, tools[i].displayName.ToUpper(), toolTab == i, tools[i].color, () => toolTab = idx));
            }
            body.Add(sub);

            var u2 = tools[toolTab];
            int lvl = gs.Level(u2);
            body.Add(UiKit.Text(u2.description, "fr-muted"));
            body.Add(UiKit.Text("Equipped: " + u2.NameAtLevel(lvl) + "   DMG x" + (1f + 0.7f * lvl).ToString("0.0") + "   Yield x" + (1f + u2.valuePerLevel * lvl).ToString("0.0"), "fr-small", UiKit.Ui(u2.color)));
            for (int l = 0; l <= u2.maxLevel; l++)
            {
                int level = l;
                bool owned = l <= lvl, next = l == lvl + 1;
                Color c = UiKit.Ui(u2.color);
                var card = Card(owned ? c : new Color(0.5f, 0.6f, 0.75f));
                card.style.opacity = owned || next ? 1f : 0.55f;
                var top = UiKit.Div("fr-row");
                top.Add(UiKit.Text(u2.NameAtLevel(l), "fr-h2", owned ? c : Color.white));
                top.Add(UiKit.Div("fr-spacer"));
                top.Add(UiKit.Text(owned ? (l == lvl ? "EQUIPPED" : "OWNED") : next ? "NEXT" : "LOCKED", "fr-tiny", owned ? UiKit.Lime : UiKit.Amber));
                card.Add(top);
                card.Add(UiKit.Text("DMG x" + (1f + 0.7f * l).ToString("0.0") + "   Yield x" + (1f + u2.valuePerLevel * l).ToString("0.0") + "   Faster swing", "fr-tiny"));
                if (next)
                {
                    var cost = u2.CostAt(lvl);
                    card.Add(UiKit.CostRow(cost, gs, true));
                    var buy = IconBtn("UPGRADE", "up", u2.color, () =>
                    {
                        if (gs.Buy(u2)) { Sfx.Good(); Toast(u2.NameAtLevel(gs.Level(u2)) + " ready!", UiKit.Ui(u2.color)); planetView.Punch(); }
                        else Sfx.Bad();
                    });
                    UiKit.SetEnabledLook(buy, gs.Has(cost));
                    card.Add(buy);
                }
                body.Add(card);
            }
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

        void BuildUpgrades(VisualElement body, UpgradeCategory cat)
        {
            {
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
