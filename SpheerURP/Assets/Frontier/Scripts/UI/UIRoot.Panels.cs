using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UBiome = Frontier.Universe.Biome;
using USize = Frontier.Universe.SizeClass;

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
        void OpenModal(string key, string icon, string title, Color accent, Action<VisualElement> build, bool wide = true)
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
            UiKit.SetAccent(panel, c);
            var head = UiKit.Div("fr-modal-head");
            head.Add(Icons.Make(icon, c));
            head.Add(UiKit.Text(title, "fr-modal-title"));
            head.Add(Icons.IconButton("close", UiKit.Muted, CloseModal));
            panel.Add(head);

            modalScroll = UiKit.VerticalScroll("fr-modal-scroll");
            panel.Add(modalScroll);
            backdropEl.Add(panel);
            modalLayer.Add(backdropEl);
            if (scrimLayer != null) scrimLayer.style.display = DisplayStyle.Flex;
            build(modalScroll.contentContainer);
        }

        void CloseModal()
        {
            EndInventoryDrag();
            if (scrimLayer != null) scrimLayer.style.display = DisplayStyle.None;
            if (modalKey == "warehouse")
            {
                planetView.Frozen = false;
                world.Selected = null;
            }
            if (modal != null) modal.RemoveFromHierarchy();
            modal = null; modalScroll = null; modalBuild = null; modalKey = null;
        }

        void RefreshModal()
        {
            if (modal == null || modalBuild == null || modalScroll == null) return;
            var offset = modalScroll.scrollOffset;
            EndInventoryDrag();
            modalScroll.contentContainer.Clear();
            modalBuild(modalScroll.contentContainer);
            var scroll = modalScroll;
            scroll.schedule.Execute(() => scroll.scrollOffset = offset).StartingIn(1);
        }

        void OpenShop() { if (defense.Active || busy) return; OpenModal("shop", "build", "BUILD", UiKit.Lime, BuildShop, true); }
        void OpenResearch() { if (defense.Active || busy) return; OpenModal("research", "research", "COMMAND", UiKit.Magenta, BuildResearch, true); }
        void OpenPack() { if (defense.Active || busy) return; OpenModal("pack", "pack", "BACKPACK", UiKit.Amber, BuildPack, true); }

        // ------------------------------------------------------------ build shop
        int shopTab;
        int toolTab;

        VisualElement TabButton(string icon, string label, bool on, Color accent, Action onClick)
        {
            var t = new Button();
            t.AddToClassList("fr-tab");
            UiKit.SetAccent(t, accent);
            if (on) t.AddToClassList("fr-tab-on");
            t.Add(Icons.Make(icon, on ? UiKit.Ui(accent) : UiKit.Muted, UiKit.Size.IconMd));
            t.Add(UiKit.Text(label, "fr-tab-label"));
            t.clicked += () => { Sfx.Click(); onClick(); RefreshModal(); };
            return t;
        }

        void BuildShop(VisualElement body)
        {
            var tabs = UiKit.Div("fr-tabs");
            tabs.Add(TabButton("refinery", "ECON", shopTab == 0, UiKit.Lime, () => shopTab = 0));
            tabs.Add(TabButton("mine", "GATHER", shopTab == 1, UiKit.Lime, () => shopTab = 1));
            tabs.Add(TabButton("shield", "DEFENSE", shopTab == 2, UiKit.Lime, () => shopTab = 2));
            tabs.Add(TabButton("research", "UTILITY", shopTab == 3, UiKit.Lime, () => shopTab = 3));
            body.Add(tabs);
            var note = UiKit.Text("Build on " + gs.planet.Name + "  -  " + gs.FreePads(gs.planet) + " / " + gs.planet.BuildingSlots +
                " free slots  ·  CREWS " + gs.BusyBuilderCount + "/" + gs.BuilderCount + " busy. Tap a structure on the planet to use it.", "fr-muted");
            note.style.marginBottom = UiKit.Space.S12;
            body.Add(note);
            switch (shopTab)
            {
                case 0: ShopSection(body, StructureKind.Refinery, StructureKind.Fabricator, StructureKind.Foundry,
                    StructureKind.ChemReactor, StructureKind.QuantumLab, StructureKind.Alarm, StructureKind.Warehouse,
                    StructureKind.FuelDepot); break;
                case 1: ShopSection(body, StructureKind.Lumber, StructureKind.Mine, StructureKind.Drill,
                    StructureKind.SiphonRig, StructureKind.Excavator, StructureKind.SalvageYard); break;
                case 2: ShopSection(body, StructureKind.Gunner, StructureKind.Laser); break;
                default: ShopSection(body, StructureKind.ResearchLab, StructureKind.RadarTower, StructureKind.ColonyHub,
                    StructureKind.Satellite, StructureKind.Terraformer); break;
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
                var top = UiKit.Div("fr-card-head");
                top.Add(Icons.Make(StructureIcon(def.kind), c, UiKit.Size.IconXl));
                var col = UiKit.Div("fr-card-text"); col.pickingMode = PickingMode.Ignore;
                col.Add(UiKit.Text(def.displayName.ToUpper(), "fr-h2"));
                col.Add(UiKit.Text(def.description, "fr-tiny"));
                top.Add(col);
                int have = gs.CountOnPlanet(gs.planet, def);
                var count = UiKit.Text(def.maxPerPlanet > 0 ? have + "/" + def.maxPerPlanet : "x" + have, "fr-small");
                count.style.marginLeft = UiKit.Space.S12;
                top.Add(count);
                card.Add(top);

                PlacedStructure activeJob = null;
                foreach (var structure in gs.StructuresOn(gs.planet))
                    if (structure.def == def && gs.IsBuilderBusy(structure)) { activeJob = structure; break; }
                if (activeJob != null) card.Add(Note(gs.BuilderJobStatus(activeJob), UiKit.Cyan));
                else if (gs.AtLimit(gs.planet, def)) card.Add(Note("BUILT - tap it on the planet", UiKit.Lime));
                else if (gs.BuildLockReason(def) != null) card.Add(Note(gs.BuildLockReason(def), UiKit.Amber));
                else if (!gs.CanBuildHere(def)) card.Add(Note("Nothing to harvest on this planet", UiKit.Amber));
                else
                {
                    card.Add(UiKit.CostRow(def.buildCost, gs, true));
                    bool crewAvailable = gs.StructureJobSeconds(def, 1) <= 0 || gs.AvailableBuilderCount > 0;
                    if (!crewAvailable) card.Add(Note(gs.BuilderAvailabilityReason(), UiKit.Amber));
                    bool can = gs.HasPlanetResources(def.buildCost) && crewAvailable;
                    var place = IconBtn("PLACE", "build", UiKit.Lime, () => { CloseModal(); EnterBuild(def); });
                    UiKit.SetEnabledLook(place, can);
                    card.Add(place);
                }
                body.Add(card);
            }
        }

        /// <summary>A short status line inside a card (built, locked, maxed).</summary>
        static Label Note(string text, Color color)
        {
            var l = UiKit.Text(text, "fr-small", color);
            l.style.marginTop = UiKit.Space.S8;
            return l;
        }

        // ------------------------------------------------------------ menu (leaderboard, tutorial, about, settings)
        void OpenMenu() { if (defense.Active || busy) return; menuPage = ""; OpenModal("menu", "menu", "MENU", UiKit.Cyan, BuildMenu, true); }

        string menuPage = "";

        /// <summary>The DEBUG tile and its cheat tools exist only in the editor and development builds.</summary>
        public static bool DebugToolsAvailable
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (debugToolsOverride.HasValue) return debugToolsOverride.Value;
#endif
                return Debug.isDebugBuild || Application.isEditor;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static bool? debugToolsOverride;
#endif

        void BuildMenu(VisualElement body)
        {
            if (menuPage == "debug" && !DebugToolsAvailable) menuPage = "";
            if (menuPage != "")
            {
                var back = IconBtn("BACK", "back", UiKit.Muted, () => { menuPage = ""; RefreshModal(); });
                back.AddToClassList("fr-btn-ghost");
                back.AddToClassList("fr-back");
                body.Add(back);
                switch (menuPage)
                {
                    case "debug": BuildDebug(body); break;
                    case "ranks": BuildLeaderboard(body); break;
                    case "tutorial": BuildTutorial(body); break;
                    case "about": BuildAbout(body); break;
                    case "atlas": BuildAtlas(body); break;
                    default: BuildSettings(body); break;
                }
                return;
            }
            var grid = UiKit.Div("fr-menu-grid");
            grid.Add(MenuTile("ranks", "RANKS", UiKit.Lime, "ranks"));
            grid.Add(MenuTile("map", "ATLAS", UiKit.Cyan, "atlas"));
            grid.Add(MenuTile("tutorial", "HOW TO PLAY", UiKit.Cyan, "tutorial"));
            grid.Add(MenuTile("about", "ABOUT", UiKit.Magenta, "about"));
            grid.Add(MenuTile("gear", "SETTINGS", UiKit.Cyan, "settings"));
            if (DebugToolsAvailable) grid.Add(MenuTile("tools", "DEBUG", UiKit.Red, "debug"));
            body.Add(grid);
        }

        VisualElement MenuTile(string icon, string label, Color color, string page)
        {
            Color c = UiKit.Ui(color);
            var b = new Button();
            b.AddToClassList("fr-menu-tile");
            UiKit.SetAccent(b, c);
            b.Add(Icons.Make(icon, c));
            b.Add(UiKit.Text(label, "fr-menu-tile-label"));
            b.clicked += () => { Sfx.Click(); menuPage = page; RefreshModal(); };
            return b;
        }

        void StatRow(VisualElement body, string icon, Color color, string label, string value)
        {
            var row = UiKit.Div("fr-row"); row.style.marginTop = UiKit.Space.S12;
            var ic = Icons.Make(icon, UiKit.Ui(color), UiKit.Size.IconMd);
            ic.style.marginRight = UiKit.Space.S12;
            row.Add(ic);
            row.Add(UiKit.Text(label, "fr-small"));
            row.Add(UiKit.Div("fr-spacer"));
            row.Add(UiKit.Text(value, "fr-h2"));
            body.Add(row);
        }

        void BuildLeaderboard(VisualElement body)
        {
            var card = Card(UiKit.Lime);
            card.Add(UiKit.Text("YOUR RECORD", "fr-h2"));
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
            TutorialStep(body, "build", UiKit.Lime, "1. Build", "Open the shop (bottom right) and place a Lumber Camp, then a Refinery and a Warehouse.");
            TutorialStep(body, "pack", UiKit.Cyan, "2. Storage", "Your starter backpack has only 9 slots. Build a Warehouse early to store extra resources and drag stacks between grids.");
            TutorialStep(body, "wood", UiKit.Amber, "3. Gather", "Tap a Lumber Camp, Mine or Drill on the planet to start its mini-game and collect resources.");
            TutorialStep(body, "fuel", UiKit.Amber, "4. Refine", "Tap the Refinery to turn raw resources into fuel.");
            TutorialStep(body, "alarm", UiKit.Magenta, "5. Defend", "Tap the Alarm and spend fuel to call a wave. More fuel means a longer fight and bigger rewards. Gunners and Lasers defend automatically.");
            TutorialStep(body, "map", UiKit.Cyan, "6. Travel", "Open the world map (top left) and spend fuel to visit new planets. Your bases stay where you built them.");
            TutorialStep(body, "research", UiKit.Magenta, "7. Upgrade", "The research button (bottom left) manages your bases and unlocks upgrades, including more backpack space.");
        }

        void TutorialStep(VisualElement body, string icon, Color color, string title, string text)
        {
            var card = Card(color);
            var row = UiKit.Div("fr-card-head");
            row.style.alignItems = Align.FlexStart;
            row.Add(Icons.Make(icon, UiKit.Ui(color), UiKit.Size.IconLg));
            var col = UiKit.Div("fr-card-text"); col.pickingMode = PickingMode.Ignore;
            col.Add(UiKit.Text(title, "fr-h2"));
            col.Add(UiKit.Text(text, "fr-muted"));
            row.Add(col);
            card.Add(row);
            body.Add(card);
        }

        void BuildAbout(VisualElement body)
        {
            var card = Card(UiKit.Magenta);
            card.Add(UiKit.Text("FRONTIER", "fr-title", UiKit.Magenta));
            var blurb = UiKit.Text("Build bases across the stars. Gather, refine, travel, and defend your worlds from alien waves.", "fr-muted");
            blurb.style.marginTop = UiKit.Space.S8;
            card.Add(blurb);
            var version = UiKit.Text("Version " + Application.version, "fr-tiny");
            version.style.marginTop = UiKit.Space.S8;
            card.Add(version);
            body.Add(card);
        }

        bool confirmReset;

        void BuildSettings(VisualElement body)
        {
            var snd = IconBtn(Sfx.Muted ? "SOUND OFF" : "SOUND ON", Sfx.Muted ? "mute" : "sound", Sfx.Muted ? UiKit.Red : UiKit.Lime, () =>
            {
                Sfx.Muted = !Sfx.Muted;
                PlayerPrefs.SetInt("fr_muted", Sfx.Muted ? 1 : 0);
                RefreshModal();
            });
            snd.style.marginTop = 0;
            body.Add(snd);
            body.Add(IconBtn(world.ShowStructureLabels ? "STRUCTURE LABELS: ALWAYS" : "STRUCTURE LABELS: SELECTED ONLY", "base", UiKit.Cyan, () =>
            {
                world.ShowStructureLabels = !world.ShowStructureLabels;
                PlayerPrefs.SetInt("fr_structure_labels", world.ShowStructureLabels ? 1 : 0);
                PlayerPrefs.Save();
                RefreshModal();
            }));
            BuildTravelSettings(body);

            var reset = IconBtn(confirmReset ? "TAP AGAIN TO ERASE" : "RESET PROGRESS", "close", UiKit.Red, () =>
            {
                if (!confirmReset) { confirmReset = true; RefreshModal(); return; }
                confirmReset = false;
                CloseModal();
                gs.ResetProgress();
                planetView.Show(gs.planet, true, gs.TerraformerStageOn(gs.planet));
                selected = gs.planet;
            });
            reset.style.marginTop = UiKit.Space.S24;
            body.Add(reset);
        }

        // ------------------------------------------------------------ command (bases + research)
        void BuildResearch(VisualElement body)
        {
            var tabs = UiKit.Div("fr-tabs");
            tabs.Add(TabButton("base", "BASES", researchTab == 0, UiKit.Magenta, () => researchTab = 0));
            tabs.Add(TabButton("tools", "TOOLS", researchTab == 1, UiKit.Magenta, () => researchTab = 1));
            tabs.Add(TabButton("research", "UPGRADES", researchTab == 2, UiKit.Magenta, () => researchTab = 2));
            body.Add(tabs);
            switch (researchTab)
            {
                case 0: BuildBases(body); break;
                case 1: BuildTools(body); break;
                default: BuildProgression(body); break;
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
                sub.Add(TabButton(Icons.ForTool(tools[i].tool), tools[i].displayName.ToUpper(), toolTab == i, UiKit.Magenta, () => toolTab = idx));
            }
            body.Add(sub);

            var u2 = tools[toolTab];
            int lvl = gs.Level(u2);
            body.Add(UiKit.Text(u2.description, "fr-muted"));
            var equipped = UiKit.Text("Equipped: " + gs.ToolName(u2.tool) + "   DMG x" + gs.ToolDamage(u2.tool).ToString("0.0") + "   Yield x" + gs.ToolMultiplier(u2.tool).ToString("0.0"), "fr-small");
            equipped.style.marginTop = UiKit.Space.S8; equipped.style.marginBottom = UiKit.Space.S12;
            body.Add(equipped);
            for (int l = 0; l <= u2.maxLevel; l++)
            {
                int level = l;
                bool owned = l <= lvl, next = l == lvl + 1;
                var card = Card(UiKit.Magenta, l == lvl);
                if (!owned && !next) card.AddToClassList("fr-card-dim");
                var top = UiKit.Div("fr-row");
                top.Add(UiKit.Text(u2.NameAtLevel(l), "fr-h2"));
                top.Add(UiKit.Div("fr-spacer"));
                top.Add(UiKit.Text(owned ? (l == lvl ? "EQUIPPED" : "OWNED") : next ? "NEXT" : "LOCKED", "fr-tiny", l == lvl ? UiKit.Lime : (Color?)null));
                card.Add(top);
                card.Add(UiKit.Text("DMG x" + (1f + FrontierSettings.I.toolDamagePerLevel * l).ToString("0.0") + "   Yield x" + ((1f + u2.valuePerLevel * l) * gs.CatalogToolYieldMultiplier(u2.tool)).ToString("0.0") + "   Faster swing", "fr-tiny"));
                if (next)
                {
                    var cost = u2.CostAt(lvl);
                    card.Add(UiKit.CostRow(cost, gs, true));
                    var buy = IconBtn("UPGRADE", "up", UiKit.Magenta, () =>
                    {
                        if (gs.Buy(u2)) { Sfx.Good(); Toast(u2.NameAtLevel(gs.Level(u2)) + " ready!", UiKit.Ui(u2.color)); planetView.Punch(); }
                        else Sfx.Bad();
                    });
                    UiKit.SetEnabledLook(buy, gs.CanBuy(u2));
                    card.Add(buy);
                }
                body.Add(card);
            }
            BuildCatalogTools(body);
        }

        void BuildBases(VisualElement body)
        {
            // The Core Worlds first (in their list order), then any generated world with a base; the current world always leads.
            var planets = new List<PlanetData>(gs.universe.AuthoredPlanets);
            var known = new HashSet<string>();
            foreach (var p in planets) known.Add(p.Id);
            foreach (var s in gs.structures)
            {
                if (!known.Add(s.planetId)) continue;
                var found = gs.universe.Get(s.planetId);
                if (found != null) planets.Add(found);
            }
            if (known.Add(gs.planet.Id)) planets.Add(gs.planet);
            planets.Sort((a, b) => (b.Id == gs.planet.Id ? 1 : 0).CompareTo(a.Id == gs.planet.Id ? 1 : 0));
            bool any = false;
            foreach (var p in planets)
            {
                var here = gs.StructuresOn(p);
                if (p.Id != gs.planet.Id && here.Count == 0) continue;
                any = true;
                var h = UiKit.Div("fr-row"); h.style.marginTop = UiKit.Space.S8; h.style.marginBottom = UiKit.Space.S8;
                var hn = UiKit.Text(p.Name.ToUpper(), "fr-small", UiKit.Ui(p.Rim));
                hn.style.marginRight = UiKit.Space.S12;
                h.Add(hn);
                if (p.Id == gs.planet.Id) h.Add(UiKit.Text("YOU ARE HERE", "fr-tiny"));
                body.Add(h);
                if (here.Count == 0) body.Add(UiKit.Text("No base yet - place structures from the build menu.", "fr-muted"));
                foreach (var st in here)
                {
                    var s = st;
                    Color c = UiKit.Ui(s.def.color);
                    var card = Card(c);
                    var top = UiKit.Div("fr-card-head");
                    top.Add(Icons.Make(StructureIcon(s.def.kind), c, UiKit.Size.IconLg));
                    var col = UiKit.Div("fr-card-text"); col.pickingMode = PickingMode.Ignore;
                    col.Add(UiKit.Text(s.def.displayName.ToUpper() + "  LV " + s.level + "/" + s.def.maxLevel, "fr-h2"));
                    bool max = s.level >= s.def.maxLevel;
                    col.Add(UiKit.Text(StructureEffect(s.def, s.level) + (max ? "" : "  >  " + StructureEffect(s.def, s.level + 1)), "fr-tiny"));
                    top.Add(col);
                    card.Add(top);
                    if (gs.IsBuilderBusy(s)) card.Add(Note(gs.BuilderJobStatus(s), UiKit.Cyan));
                    if (!max && !gs.IsBuilderBusy(s))
                    {
                        var cost = s.def.UpgradeCost(s.level);
                        card.Add(UiKit.CostRow(cost, gs, true));
                        var up = IconBtn("UPGRADE", "up", UiKit.Magenta, () =>
                        {
                            if (gs.UpgradeStructure(s))
                            {
                                Sfx.Good();
                                Toast(gs.IsUpgradeInProgress(s) ? "Upgrade started · " + gs.BuilderJobStatus(s) :
                                    s.def.displayName + " LV " + s.level, c);
                            }
                            else Sfx.Bad();
                        });
                        if (gs.StructureUpgradeLockReason(s) != null) card.Add(Note(gs.StructureUpgradeLockReason(s), UiKit.Amber));
                        UiKit.SetEnabledLook(up, gs.CanUpgradeStructure(s));
                        card.Add(up);
                    }
                    else card.Add(Note("MAXED", UiKit.Lime));
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
                    var card = Card(UiKit.Magenta);
                    var top = UiKit.Div("fr-row");
                    top.Add(UiKit.Text(u.NameAtLevel(lvl), "fr-h2"));
                    top.Add(UiKit.Div("fr-spacer"));
                    top.Add(UiKit.Text("LV " + lvl + "/" + u.maxLevel, "fr-small"));
                    card.Add(top);
                    card.Add(UiKit.Text(u.description, "fr-muted"));
                    card.Add(UiKit.Text("Now " + UpgradeEffectText(u, lvl) + (max ? "" : "  >  " + UpgradeEffectText(u, lvl + 1)), "fr-tiny"));
                    if (!max)
                    {
                        var cost = u.CostAt(lvl);
                        card.Add(UiKit.CostRow(cost, gs, true));
                        var buy = IconBtn("UPGRADE", "up", UiKit.Magenta, () =>
                        {
                            if (gs.Buy(u)) { Sfx.Good(); Toast(u.NameAtLevel(gs.Level(u)) + " ready!", UiKit.Ui(u.color)); planetView.Punch(); }
                            else Sfx.Bad();
                        });
                        if (gs.UpgradeLockReason(u) != null) card.Add(Note(gs.UpgradeLockReason(u), UiKit.Amber));
                        UiKit.SetEnabledLook(buy, gs.CanBuy(u));
                        card.Add(buy);
                    }
                    else card.Add(Note("MAXED", UiKit.Lime));
                    body.Add(card);
                }
            }
        }

        // ------------------------------------------------------------ backpack (Minecraft style)
        const int PackColumns = 9;

        void BuildPack(VisualElement body)
        {
            SyncPack();
            body.Add(UiKit.Text(gs.SlotsUsed + " / " + gs.Slots + " slots used  -  drag to rearrange", "fr-muted"));
            var grid = UiKit.Div("fr-grid");
            grid.style.marginTop = UiKit.Space.S8;

            var slots = new List<VisualElement>();
            for (int i = 0; i < packRes.Length; i++) slots.Add(PackSlot(i, slots));

            int rows = Mathf.CeilToInt(slots.Count / (float)PackColumns);
            int padded = slots.Count;
            while (padded < rows * PackColumns) { slots.Add(Slot(null, 0, true)); padded++; }
            foreach (var s in slots) grid.Add(s);

            SizeInventoryGrid(grid, slots);
            body.Add(grid);

            var detail = Card(UiKit.Amber);
            detail.style.marginTop = UiKit.Space.S16;
            detail.style.minHeight = 200;
            if (packSel >= 0 && packSel < packRes.Length && packRes[packSel] != null)
            {
                var r = packRes[packSel];
                Color c = UiKit.Ui(r.color);
                var row = UiKit.Div("fr-card-head");
                row.Add(Icons.Make(Icons.ForResource(r), c, UiKit.Size.IconXl));
                var col = UiKit.Div("fr-card-text"); col.pickingMode = PickingMode.Ignore;
                col.Add(UiKit.Text(r.displayName.ToUpper(), "fr-h2"));
                col.Add(UiKit.Text("In this slot: " + packCnt[packSel] + " / " + r.stackSize, "fr-small"));
                col.Add(UiKit.Text("Total carried: " + gs.Count(r), "fr-small"));
                row.Add(col);
                detail.Add(row);
                if (!string.IsNullOrEmpty(r.description))
                {
                    var desc = UiKit.Text(r.description, "fr-muted");
                    desc.style.marginTop = UiKit.Space.S8;
                    detail.Add(desc);
                }
            }
            else detail.Add(UiKit.Text("Tap an item to see its details.", "fr-muted"));
            body.Add(detail);
            var salvage = gs.ObstacleRewardsOn(gs.planet);
            if (salvage.Count > 0)
            {
                body.Add(SectionLabel("RECOVERED SURFACE SALVAGE"));
                foreach (var reward in salvage)
                {
                    ResourceSO resource = gs.db.FindResource(reward.resourceId);
                    if (resource == null) continue;
                    var row = UiKit.Div("fr-row");
                    row.AddToClassList("fr-popup-line");
                    row.Add(Icons.Make(Icons.ForResource(resource), UiKit.Ui(resource.color), UiKit.Size.IconLg));
                    var text = UiKit.Div("fr-card-text");
                    text.Add(UiKit.Text(resource.displayName, "fr-small"));
                    text.Add(UiKit.Text(reward.amount + " waiting on " + gs.planet.Name, "fr-tiny"));
                    row.Add(text);
                    int pad = reward.pad;
                    var claim = IconBtn("CLAIM", "pack", UiKit.Amber, () =>
                    {
                        if (gs.ClaimObstacleReward(gs.planet, pad))
                        {
                            Sfx.Good();
                            Toast("Surface salvage added to backpack", UiKit.Lime);
                        }
                        else
                        {
                            Sfx.Bad();
                            Toast("Make room in your backpack to claim salvage", UiKit.Red);
                        }
                    });
                    UiKit.SetEnabledLook(claim, gs.FreeSpace(resource) > 0);
                    row.Add(claim);
                    body.Add(row);
                }
            }
            body.Add(UiKit.Text("Upgrade the backpack in Command > Research for more slots.", "fr-tiny"));
        }

        ResourceSO[] packRes = new ResourceSO[0];
        int[] packCnt = new int[0];
        int packSel = -1;

        // Keeps the arranged slot layout consistent with the real inventory totals.
        void SyncPack()
        {
            int n = gs.Slots;
            if (packRes.Length != n)
            {
                Array.Resize(ref packRes, n);
                Array.Resize(ref packCnt, n);
            }
            foreach (var r in gs.db.resources)
            {
                int total = gs.Count(r), sum = 0;
                for (int i = 0; i < n; i++) if (packRes[i] == r) sum += packCnt[i];
                for (int i = n - 1; i >= 0 && sum > total; i--)
                {
                    if (packRes[i] != r) continue;
                    int take = Mathf.Min(packCnt[i], sum - total);
                    packCnt[i] -= take; sum -= take;
                    if (packCnt[i] <= 0) { packRes[i] = null; packCnt[i] = 0; }
                }
                for (int i = 0; i < n && sum < total; i++)
                {
                    if (packRes[i] != r || packCnt[i] >= r.stackSize) continue;
                    int add = Mathf.Min(r.stackSize - packCnt[i], total - sum);
                    packCnt[i] += add; sum += add;
                }
                for (int i = 0; i < n && sum < total; i++)
                {
                    if (packRes[i] != null) continue;
                    int add = Mathf.Min(r.stackSize, total - sum);
                    packRes[i] = r; packCnt[i] = add; sum += add;
                }
            }
            for (int i = 0; i < n; i++) if (packRes[i] != null && packCnt[i] <= 0) packRes[i] = null;
            if (packSel >= n || (packSel >= 0 && packRes[packSel] == null)) packSel = -1;
        }

        VisualElement PackSlot(int index, List<VisualElement> all)
        {
            var s = Slot(packRes[index], packCnt[index], false);
            if (index == packSel)
            {
                Color c = packRes[index] != null ? UiKit.Ui(packRes[index].color) : UiKit.Cyan;
                UiKit.Border(s, Color.white, 5f);
                UiKit.Halo(s, c, 18f, 0.6f);
            }
            BindInventoryDrag(s, packRes[index], packCnt[index], () =>
            {
                packSel = (packRes[index] != null && packSel != index) ? index : -1;
                Sfx.Click(); RefreshModal();
            }, position =>
            {
                int target = SlotAt(all, position);
                if (target >= 0 && target < packRes.Length && target != index && packRes[index] != null)
                {
                    MovePackSlot(index, target); packSel = target; Sfx.Click();
                }
                RefreshModal();
            });
            return s;
        }

        void MovePackSlot(int from, int to)
        {
            if (packRes[to] == packRes[from] && packRes[to] != null)
            {
                int room = packRes[to].stackSize - packCnt[to];
                int move = Mathf.Min(room, packCnt[from]);
                packCnt[to] += move; packCnt[from] -= move;
                if (packCnt[from] <= 0) { packRes[from] = null; packCnt[from] = 0; }
                return;
            }
            var r = packRes[to]; packRes[to] = packRes[from]; packRes[from] = r;
            int c = packCnt[to]; packCnt[to] = packCnt[from]; packCnt[from] = c;
        }

        // ------------------------------------------------------------ debug
        int debugAmount = 100;

        void BuildDebug(VisualElement body)
        {
            body.Add(UiKit.Text("DEBUG TOOLS - for testing only", "fr-muted", UiKit.Red));

            var give = Card(UiKit.Amber);
            give.Add(UiKit.Text("GIVE ITEMS", "fr-h2"));
            var amt = UiKit.Div("fr-tabs");
            amt.style.marginTop = UiKit.Space.S8;
            foreach (int a in new[] { 10, 100, 1000 })
            {
                int v = a;
                amt.Add(TabButton("star", "x" + a, debugAmount == v, UiKit.Amber, () => debugAmount = v));
            }
            give.Add(amt);
            var res = UiKit.Div("fr-row-wrap");
            foreach (var r in gs.db.resources)
            {
                var rr = r;
                var b = IconBtn(r.displayName.ToUpper(), Icons.ForResource(r), UiKit.Ui(r.color), () => { gs.Add(rr, debugAmount); Sfx.Good(); RefreshModal(); });
                b.AddToClassList("fr-btn-mini");
                b.style.marginBottom = UiKit.Space.S8;
                res.Add(b);
            }
            give.Add(res);
            give.Add(IconBtn("GIVE ONE OF EACH x" + debugAmount, "pack", UiKit.Lime, () =>
            {
                foreach (var r in gs.db.resources) gs.Add(r, debugAmount, false);
                gs.Notify(); Sfx.Good(); RefreshModal();
            }));
            body.Add(give);

            var prog = Card(UiKit.Magenta);
            prog.Add(UiKit.Text("PROGRESS", "fr-h2"));
            prog.Add(UiKit.Text("Player level " + gs.PlayerLevel, "fr-small"));
            prog.Add(IconBtn("LEVEL +1", "star", UiKit.Magenta, () => { gs.DebugSetLevel(gs.PlayerLevel + 1); Sfx.Good(); RefreshModal(); }));
            prog.Add(IconBtn("LEVEL +10", "star", UiKit.Magenta, () => { gs.DebugSetLevel(gs.PlayerLevel + 10); Sfx.Good(); RefreshModal(); }));
            prog.Add(IconBtn("MAX ALL UPGRADES", "up", UiKit.Lime, () =>
            {
                foreach (var u in gs.db.upgrades) if (u != null) gs.DebugSetUpgrade(u, u.maxLevel);
                Sfx.Good(); RefreshModal();
            }));
            prog.Add(IconBtn("RESET UPGRADES", "close", UiKit.Red, () =>
            {
                foreach (var u in gs.db.upgrades) if (u != null) gs.DebugSetUpgrade(u, 0);
                Sfx.Click(); RefreshModal();
            }));
            body.Add(prog);
            BuildTravelDebug(body);

            var tp = Card(UiKit.Cyan);
            tp.Add(UiKit.Text("TELEPORT (free)", "fr-h2"));
            foreach (var p in gs.universe.AuthoredPlanets)
            {
                var pl = p;
                tp.Add(IconBtn(pl.Name.ToUpper() + "  -  " + pl.SystemName, "map", pl.Id == gs.planet.Id ? UiKit.Lime : UiKit.Cyan, () => DebugTeleportTo(pl)));
            }
            body.Add(tp);
            body.Add(BuildGeneratedPicker());
        }

        // ------------------------------------------------------------ debug: generated worlds
        static readonly string[] DebugBiomeLabels = { "ANY", "LUSH", "MINERAL", "GLACIAL", "VOLCANIC", "FUNGAL", "CRYSTAL", "OBSIDIAN", "SHATTERED", "DESERT", "OCEANIC", "GAS GIANT", "BARREN" };
        static readonly string[] DebugSizeLabels = { "ANY", "DWARF", "SMALL", "MEDIUM", "LARGE", "GIANT", "COLOSSAL" };
        static readonly int[] DebugTierChoices = { 0, 2, 4, 6, 8, 12, 20 };
        /// <summary>The menu searches this many sectors around home at most, so a rare combination cannot freeze the game.</summary>
        const int DebugSearchSectors = 3000;
        int debugBiome, debugSize, debugTier, debugSkip;

        VisualElement BuildGeneratedPicker()
        {
            var gen = Card(UiKit.Lime);
            gen.Add(UiKit.Text("GENERATED WORLD (free)", "fr-h2"));
            gen.Add(UiKit.Text("Teleports to the first generated world that matches, searching outwards from home. NEXT cycles through the matches.", "fr-tiny"));
            gen.Add(DebugChoiceRow("BIOME", DebugBiomeLabels, debugBiome, i => debugBiome = i));
            gen.Add(DebugChoiceRow("SIZE", DebugSizeLabels, debugSize, i => debugSize = i));
            var tiers = new string[DebugTierChoices.Length];
            for (int i = 0; i < tiers.Length; i++) tiers[i] = DebugTierChoices[i] + "+";
            gen.Add(DebugChoiceRow("MINIMUM TIER", tiers, debugTier, i => debugTier = i));
            gen.Add(IconBtn("TELEPORT", "map", UiKit.Lime, () => DebugTeleportToGenerated(false)));
            gen.Add(IconBtn("NEXT MATCH", "map", UiKit.Cyan, () => DebugTeleportToGenerated(true)));
            var here = UiKit.Text("Now on: " + gs.planet.Name + "  " + gs.planet.Id, "fr-tiny");
            here.style.marginTop = UiKit.Space.S12;
            gen.Add(here);
            gen.Add(IconBtn("DUMP DESCRIPTOR", "tools", UiKit.Amber, DebugDumpDescriptor));
            return gen;
        }

        VisualElement DebugChoiceRow(string title, string[] labels, int selectedIndex, Action<int> pick)
        {
            var box = UiKit.Div();
            box.Add(SectionLabel(title));
            var row = UiKit.Div("fr-row-wrap");
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                row.Add(DebugChoice(labels[i], i == selectedIndex, () => { pick(index); debugSkip = 0; RefreshModal(); }));
            }
            box.Add(row);
            return box;
        }

        Button DebugChoice(string label, bool on, Action click)
        {
            var b = new Button();
            b.AddToClassList("fr-tab");
            if (on) b.AddToClassList("fr-tab-on");
            UiKit.SetAccent(b, UiKit.Lime);
            // the tab style is for equal width rows; these wrap
            b.style.flexGrow = 0; b.style.flexShrink = 0; b.style.flexBasis = StyleKeyword.Auto;
            b.style.minWidth = UiKit.Size.Touch;
            b.style.paddingLeft = UiKit.Space.S12; b.style.paddingRight = UiKit.Space.S12;
            b.style.marginBottom = UiKit.Space.S8;
            b.Add(UiKit.Text(label, "fr-tab-label"));
            b.clicked += () => { Sfx.Click(); click(); };
            return b;
        }

        void DebugTeleportToGenerated(bool next)
        {
            debugSkip = next ? debugSkip + 1 : 0;
            UBiome? biome = debugBiome == 0 ? (UBiome?)null : (UBiome)(debugBiome - 1);
            USize? size = debugSize == 0 ? (USize?)null : (USize)(debugSize - 1);
            var found = gs.universe.FindGenerated(biome, size, DebugTierChoices[debugTier], debugSkip, DebugSearchSectors);
            if (found == null)
            {
                debugSkip = 0;
                Sfx.Bad();
                Toast("No matching world found nearby", UiKit.Red);
                return;
            }
            Sfx.Good();
            DebugTeleportTo(found);
            Toast(found.Name + "  -  " + found.BiomeName + ", " + found.SizeName.ToLower() + ", tier " + found.Tier, UiKit.Cyan);
        }

        /// <summary>Writes a readable description of the current world's descriptor to the log and the clipboard.</summary>
        void DebugDumpDescriptor()
        {
            string text = gs.planet.Dump();
            Debug.Log("Frontier: planet descriptor\n" + text);
            GUIUtility.systemCopyBuffer = text;
            Sfx.Click();
            Toast("Descriptor copied to the log", UiKit.Amber);
        }

        /// <summary>Moves the player to a world for free and shows it (debug tools).</summary>
        void DebugTeleportTo(PlanetData target)
        {
            CloseModal();
            gs.DebugTeleport(target);
            planetView.Show(target, true, gs.TerraformerStageOn(target));
            selected = target;
        }

        VisualElement Slot(ResourceSO r, int count, bool locked)
        {
            var s = UiKit.Div("fr-slot");
            if (locked) s.AddToClassList("fr-slot-locked");
            if (r != null)
            {
                s.Add(Icons.Make(Icons.ForResource(r), UiKit.Ui(r.color)));
                s.Add(UiKit.Text(count.ToString(), "fr-slot-count"));
            }
            return s;
        }
    }
}
