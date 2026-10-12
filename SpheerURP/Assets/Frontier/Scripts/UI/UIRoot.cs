using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public class UIRoot : MonoBehaviour
    {
        enum Tab { None, Explore, Defend, Shop, Map, Pack }

        GameState gs;
        DefenseController defense;
        PlanetView planetView;
        SpaceBackdrop backdrop;
        UIDocument doc;

        VisualElement root, hud, sheet, nav, waveHud, toastLayer, overlayLayer, flash;
        VisualElement waveFill, reactorFill;
        Label waveLabel, reactorLabel;
        Button[] navButtons;
        VisualElement planetTag;

        Tab tab = Tab.Explore;
        int risk = 1;
        PlanetSO selected;
        bool busy;
        float scrollY;
        ScrollView scroll;
        VisualElement mapElement;
        static readonly Tab[] Tabs = { Tab.Explore, Tab.Defend, Tab.Shop, Tab.Map, Tab.Pack };

        public bool IsOverUi(Vector2 screenPos)
        {
            if (root == null || root.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            return root.panel.Pick(p) != null;
        }

        public void Init(UIDocument document, GameState state, DefenseController def, PlanetView pv, SpaceBackdrop bd)
        {
            doc = document; gs = state; defense = def; planetView = pv; backdrop = bd;
            selected = gs.planet;

            root = doc.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.AddToClassList("fr-root");
            var uss = Resources.Load<StyleSheet>("Frontier/Frontier");
            if (uss != null) root.styleSheets.Add(uss);

            hud = UiKit.Div("fr-hud"); hud.pickingMode = PickingMode.Ignore;
            root.Add(hud);

            sheet = UiKit.Div("fr-sheet");
            root.Add(sheet);

            nav = UiKit.Div("fr-nav");
            navButtons = new Button[Tabs.Length];
            for (int i = 0; i < Tabs.Length; i++)
            {
                var t = Tabs[i];
                var b = new Button { text = t.ToString().ToUpper() };
                b.AddToClassList("fr-nav-btn");
                b.clicked += () => { Sfx.Click(); SetTab(tab == t ? Tab.None : t); };
                navButtons[i] = b;
                nav.Add(b);
            }
            root.Add(nav);

            BuildWaveHud();

            toastLayer = UiKit.Div("fr-fill"); toastLayer.pickingMode = PickingMode.Ignore;
            overlayLayer = UiKit.Div("fr-fill"); overlayLayer.pickingMode = PickingMode.Ignore;
            flash = UiKit.Div("fr-fill"); flash.pickingMode = PickingMode.Ignore;
            flash.style.backgroundColor = new Color(0.6f, 1f, 1f, 1f);
            flash.style.opacity = 0f;
            root.Add(toastLayer); root.Add(overlayLayer); root.Add(flash);

            gs.Changed += Refresh;
            defense.WaveEnded += OnWaveEnded;
            Refresh();

            root.schedule.Execute(Animate).Every(33);
        }

        void OnDestroy()
        {
            if (gs != null) gs.Changed -= Refresh;
            if (defense != null) defense.WaveEnded -= OnWaveEnded;
        }

        // ------------------------------------------------------------ frame
        void Animate()
        {
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 3f);
            if (planetTag != null) UiKit.Border(planetTag, new Color(1f, 0.25f, 0.95f, 0.5f + 0.4f * pulse), 2f);

            bool wave = defense.Active;
            waveHud.style.display = wave ? DisplayStyle.Flex : DisplayStyle.None;
            if (wave)
            {
                int total = defense.Current.aliens.Count;
                UiKit.SetFill(waveFill, total == 0 ? 0f : defense.Killed / (float)total);
                UiKit.SetFill(reactorFill, defense.ReactorHp / Mathf.Max(1f, defense.ReactorMax));
                waveLabel.text = "WAVE " + defense.Current.waveNumber + "   " + defense.Killed + " / " + total;
                reactorLabel.text = "REACTOR " + Mathf.CeilToInt(defense.ReactorHp);
                float hp = defense.ReactorHp / Mathf.Max(1f, defense.ReactorMax);
                reactorFill.style.backgroundColor = Color.Lerp(UiKit.Red, UiKit.Lime, hp);
            }
            sheet.style.display = (tab == Tab.None || wave) ? DisplayStyle.None : DisplayStyle.Flex;
            nav.style.display = wave ? DisplayStyle.None : DisplayStyle.Flex;
            hud.style.display = DisplayStyle.Flex;
        }

        void BuildWaveHud()
        {
            waveHud = UiKit.Div("fr-fill"); waveHud.pickingMode = PickingMode.Ignore;
            waveHud.style.display = DisplayStyle.None;
            waveHud.style.justifyContent = Justify.SpaceBetween;
            waveHud.style.paddingTop = 170; waveHud.style.paddingBottom = 60;
            waveHud.style.paddingLeft = 40; waveHud.style.paddingRight = 40;

            var top = UiKit.Div(); top.pickingMode = PickingMode.Ignore;
            waveLabel = UiKit.Text("", "fr-h2", UiKit.Cyan);
            top.Add(waveLabel);
            var wb = UiKit.Bar(UiKit.Cyan, out waveFill);
            wb.style.width = Length.Percent(100); wb.style.marginLeft = 0; wb.style.marginTop = 8;
            top.Add(wb);
            top.Add(UiKit.Text("Tap aliens to zap them", "fr-tiny"));
            waveHud.Add(top);

            var bottom = UiKit.Div(); bottom.pickingMode = PickingMode.Ignore;
            reactorLabel = UiKit.Text("", "fr-h2", UiKit.Lime);
            bottom.Add(reactorLabel);
            var rb = UiKit.Bar(UiKit.Lime, out reactorFill);
            rb.style.width = Length.Percent(100); rb.style.marginLeft = 0; rb.style.marginTop = 8; rb.style.height = 26;
            bottom.Add(rb);
            waveHud.Add(bottom);
            root.Add(waveHud);
        }

        // ------------------------------------------------------------ refresh
        void SetTab(Tab t)
        {
            if (busy || defense.Active) return;
            tab = t;
            if (t == Tab.Map) selected = gs.planet;
            Refresh();
        }

        void Refresh()
        {
            if (root == null) return;
            RebuildHud();
            for (int i = 0; i < navButtons.Length; i++)
            {
                bool on = Tabs[i] == tab;
                Color c = on ? UiKit.Cyan : new Color(0.5f, 0.65f, 0.8f);
                UiKit.Border(navButtons[i], new Color(c.r, c.g, c.b, on ? 0.95f : 0.35f), 2f);
                navButtons[i].style.color = on ? UiKit.Cyan : new Color(0.75f, 0.88f, 1f, 0.8f);
                navButtons[i].style.backgroundColor = on ? new Color(0.05f, 0.25f, 0.32f, 0.95f) : new Color(0.04f, 0.06f, 0.16f, 0.9f);
            }
            RebuildSheet();
        }

        void RebuildHud()
        {
            hud.Clear();
            var row = UiKit.Div("fr-hud-row"); row.pickingMode = PickingMode.Ignore;

            planetTag = UiKit.Div("fr-planet-tag");
            UiKit.Halo(planetTag, UiKit.Magenta, 30f, 0.25f);
            planetTag.Add(UiKit.Text(gs.planet.displayName.ToUpper(), "fr-title", UiKit.Ui(gs.planet.primary)));
            planetTag.Add(UiKit.Text("TIER " + (gs.planet.tier + 1) + "  -  NEXT WAVE " + gs.NextWave(gs.planet), "fr-tiny"));
            row.Add(planetTag);

            var chips = UiKit.Div("fr-row"); chips.pickingMode = PickingMode.Ignore;
            chips.Add(HudChip(gs.db.fuel.color, "FUEL " + gs.Count(gs.db.fuel)));
            chips.Add(HudChip(new Color(1f, 0.85f, 0.3f), "PACK " + gs.SlotsUsed + "/" + gs.Slots));
            row.Add(chips);
            hud.Add(row);
        }

        VisualElement HudChip(Color c, string text)
        {
            var chip = UiKit.Div("fr-chip");
            Color ui = UiKit.Ui(c);
            UiKit.Border(chip, new Color(ui.r, ui.g, ui.b, 0.8f), 2f);
            var dot = UiKit.Div("fr-dot"); dot.style.backgroundColor = ui;
            chip.Add(dot);
            chip.Add(UiKit.Text(text, "fr-small", ui));
            return chip;
        }

        // ------------------------------------------------------------ sheet
        void RebuildSheet()
        {
            if (scroll != null) scrollY = scroll.scrollOffset.y;
            sheet.Clear();
            scroll = null; mapElement = null;
            if (tab == Tab.None) return;

            var head = UiKit.Div("fr-row");
            head.Add(UiKit.Text(tab.ToString().ToUpper(), "fr-title", UiKit.Cyan));
            sheet.Add(head);

            if (tab == Tab.Map) { BuildMap(); return; }

            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("fr-scroll");
            scroll.style.marginTop = 14;
            sheet.Add(scroll);

            switch (tab)
            {
                case Tab.Explore: BuildExplore(scroll); break;
                case Tab.Defend: BuildDefend(scroll); break;
                case Tab.Shop: BuildShop(scroll); break;
                case Tab.Pack: BuildPack(scroll); break;
            }
            float y = scrollY;
            var sv = scroll;
            sv.schedule.Execute(() => sv.scrollOffset = new Vector2(0f, y)).StartingIn(1);
        }

        VisualElement Card(Color accent, string cls = "fr-card")
        {
            var c = UiKit.Div(cls);
            Color ui = UiKit.Ui(accent);
            UiKit.Border(c, new Color(ui.r, ui.g, ui.b, 0.65f), 2f);
            return c;
        }

        // ------------------------------------------------------------ explore
        void BuildExplore(VisualElement parent)
        {
            parent.Add(UiKit.Text(gs.planet.description, "fr-muted"));
            foreach (var spot in gs.planet.spots)
            {
                var s = spot;
                var card = Card(s.resource.color);
                var top = UiKit.Div("fr-row");
                top.Add(UiKit.Text(s.label, "fr-h2"));
                top.Add(UiKit.Div("fr-spacer"));
                top.Add(UiKit.Text(s.game.ToString().ToUpper(), "fr-small", UiKit.Ui(s.resource.color)));
                card.Add(top);

                float mult = gs.ToolMultiplier(s.game);
                float lo = s.baseYield * mult * 0.4f, hi = s.baseYield * mult * 1.5f;
                var row = UiKit.Div("fr-row-wrap");
                row.Add(UiKit.ItemChip(s.resource, 0));
                row.Add(UiKit.Text(Mathf.RoundToInt(lo) + " - " + Mathf.RoundToInt(hi) + " " + s.resource.displayName, "fr-small"));
                card.Add(row);
                card.Add(UiKit.Text(gs.ToolName(s.game) + "  x" + mult.ToString("0.0") + " yield", "fr-tiny"));

                bool full = gs.FreeSpace(s.resource) <= 0;
                var go = UiKit.Btn(full ? "BACKPACK FULL" : "START", full ? UiKit.Red : UiKit.Ui(s.resource.color), () => StartGather(s));
                UiKit.SetEnabledLook(go, !full);
                card.Add(go);
                parent.Add(card);
            }
        }

        void StartGather(GatherSpot spot)
        {
            if (busy || defense.Active) return;
            busy = true;
            tab = Tab.None;
            Refresh();
            Minigame g = spot.game == MinigameKind.Chop ? (Minigame)new ChopGame()
                       : spot.game == MinigameKind.Mine ? (Minigame)new MineGame() : new DrillGame();
            g.Run(overlayLayer, spot.resource.color, gs.ToolTolerance(spot.game), spot.difficulty, r =>
            {
                busy = false;
                float amount = spot.baseYield * gs.ToolMultiplier(spot.game) * (0.4f + 1.1f * r.quality);
                int n = r.quality > 0.05f ? Mathf.Max(1, Mathf.RoundToInt(amount)) : 0;
                int got = gs.Add(spot.resource, n);
                Color ui = UiKit.Ui(spot.resource.color);
                if (got > 0)
                {
                    Toast("+" + got + " " + spot.resource.displayName, ui);
                    Fx.Burst(new Vector3(0, 1.2f, -2f), spot.resource.color, 40, 5f, 0.25f);
                    planetView.Punch();
                    Sfx.Good();
                }
                else Toast(n > 0 ? "Backpack full!" : "Missed everything...", UiKit.Red);
                if (got < n) Toast("Lost " + (n - got) + " (backpack full)", UiKit.Amber);
                tab = Tab.Explore;
                Refresh();
            });
        }

        // ------------------------------------------------------------ defend
        void BuildDefend(VisualElement parent)
        {
            var info = defense.Preview(risk);
            parent.Add(UiKit.Text("Call in a wave. Danger and size scale with the wave number and this planet's threat.", "fr-muted"));

            var riskRow = UiKit.Div("fr-row"); riskRow.style.marginTop = 10;
            Color[] rc = { UiKit.Cyan, UiKit.Amber, UiKit.Red };
            for (int i = 0; i < DefenseController.RiskName.Length; i++)
            {
                int idx = i;
                var b = UiKit.Btn(DefenseController.RiskName[i] + "\nx" + DefenseController.RiskMult[i].ToString("0.##"), rc[i], () => { risk = idx; Refresh(); });
                b.style.flexGrow = 1; b.style.marginRight = 10;
                if (idx != risk) b.style.opacity = 0.45f;
                riskRow.Add(b);
            }
            parent.Add(riskRow);

            var card = Card(UiKit.Magenta);
            card.style.marginTop = 14;
            card.Add(UiKit.Text("WAVE " + info.waveNumber, "fr-h2", UiKit.Magenta));
            card.Add(UiKit.Text("Threat budget " + info.budget.ToString("0.#") + "  -  " + info.aliens.Count + " aliens  -  HP x" + info.healthMult.ToString("0.00"), "fr-tiny"));

            var counts = new Dictionary<EnemySO, int>();
            foreach (var a in info.aliens) { int c; counts.TryGetValue(a, out c); counts[a] = c + 1; }
            var cr = UiKit.Div("fr-row-wrap"); cr.style.marginTop = 8;
            foreach (var kv in counts)
            {
                var chip = UiKit.Div("fr-itemchip");
                UiKit.Border(chip, UiKit.Ui(kv.Key.color, 0.8f), 2f);
                var dot = UiKit.Div("fr-dot"); dot.style.backgroundColor = UiKit.Ui(kv.Key.color);
                chip.Add(dot);
                chip.Add(UiKit.Text(kv.Value + " x " + kv.Key.displayName, "fr-small"));
                cr.Add(chip);
            }
            card.Add(cr);
            card.Add(UiKit.Text("Rewards", "fr-tiny"));
            card.Add(UiKit.CostRow(info.reward, gs, false));
            parent.Add(card);

            parent.Add(UiKit.Btn("CALL WAVE", UiKit.Magenta, () =>
            {
                tab = Tab.None; Refresh();
                defense.StartWave(risk);
            }, true));
            parent.Add(UiKit.Text("Reactor " + Mathf.RoundToInt(gs.ReactorMax) + "   Turrets " + gs.TurretCount + "   Waves cleared " + gs.totalWavesCleared, "fr-tiny"));
        }

        void OnWaveEnded(WaveResult r)
        {
            var panel = UiKit.Div("fr-overlay");
            panel.pickingMode = PickingMode.Position;
            Color c = r.victory ? UiKit.Lime : UiKit.Red;
            panel.Add(UiKit.Text(r.victory ? "WAVE CLEARED" : "REACTOR OVERLOADED", "fr-title", c));
            if (r.victory)
            {
                panel.Add(UiKit.Text("Salvage recovered", "fr-muted"));
                panel.Add(UiKit.CostRow(r.granted, gs, false));
                if (r.lost.Count > 0)
                {
                    panel.Add(UiKit.Text("Backpack full - lost:", "fr-muted", UiKit.Amber));
                    panel.Add(UiKit.CostRow(r.lost, gs, false));
                }
            }
            else panel.Add(UiKit.Text("The aliens broke through. No salvage this time.", "fr-muted"));
            var b = UiKit.Btn("CONTINUE", c, null, true);
            b.clicked += () => { panel.RemoveFromHierarchy(); tab = Tab.Defend; Refresh(); };
            b.style.marginTop = 30;
            panel.Add(b);
            overlayLayer.Add(panel);
            Refresh();
        }

        // ------------------------------------------------------------ shop
        string EffectText(UpgradeSO u, int level)
        {
            float v = u.valuePerLevel * level;
            switch (u.effect)
            {
                case UpgradeEffect.ToolPower: return "x" + (1f + v).ToString("0.0") + " yield";
                case UpgradeEffect.BackpackSlots: return "+" + Mathf.RoundToInt(v) + " slots";
                case UpgradeEffect.TurretCount: return "+" + Mathf.RoundToInt(v) + " turrets";
                case UpgradeEffect.ReactorHealth: return "+" + Mathf.RoundToInt(v) + " HP";
                default: return "+" + Mathf.RoundToInt(v * 100f) + "%";
            }
        }

        void BuildShop(VisualElement parent)
        {
            foreach (UpgradeCategory cat in System.Enum.GetValues(typeof(UpgradeCategory)))
            {
                var h = UiKit.Text(cat.ToString().ToUpper(), "fr-small", UiKit.Magenta);
                h.style.marginTop = 6; h.style.marginBottom = 8;
                parent.Add(h);
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
                    top.Add(UiKit.Text("Lv " + lvl + "/" + u.maxLevel, "fr-small"));
                    card.Add(top);
                    card.Add(UiKit.Text(u.description, "fr-muted"));
                    card.Add(UiKit.Text("Now " + EffectText(u, lvl) + (max ? "" : "  >  " + EffectText(u, lvl + 1)), "fr-tiny"));
                    if (!max)
                    {
                        var cost = u.CostAt(lvl);
                        card.Add(UiKit.CostRow(cost, gs, true));
                        var buy = UiKit.Btn("UPGRADE", u.color, () =>
                        {
                            if (gs.Buy(u)) { Sfx.Good(); Toast(u.NameAtLevel(gs.Level(u)) + " ready!", UiKit.Ui(u.color)); planetView.Punch(); }
                            else Sfx.Bad();
                        });
                        UiKit.SetEnabledLook(buy, gs.Has(cost));
                        card.Add(buy);
                    }
                    else card.Add(UiKit.Text("MAXED", "fr-small", UiKit.Lime));
                    parent.Add(card);
                }
            }

            var rh = UiKit.Text("REFINERY", "fr-small", UiKit.Magenta);
            rh.style.marginTop = 6; rh.style.marginBottom = 8;
            parent.Add(rh);
            foreach (var rec in gs.db.recipes)
            {
                var r = rec;
                var card = Card(UiKit.Lime);
                card.Add(UiKit.Text(r.displayName, "fr-h2"));
                card.Add(UiKit.CostRow(r.inputs, gs, true));
                card.Add(UiKit.Text("produces", "fr-tiny"));
                card.Add(UiKit.CostRow(r.outputs, gs, false));
                var b = UiKit.Btn("CRAFT", UiKit.Lime, () =>
                {
                    if (gs.Craft(r)) Sfx.Good(); else { Sfx.Bad(); Toast("Not enough room or materials", UiKit.Red); }
                });
                UiKit.SetEnabledLook(b, gs.Has(r.inputs));
                card.Add(b);
                parent.Add(card);
            }
        }

        // ------------------------------------------------------------ pack
        void BuildPack(VisualElement parent)
        {
            parent.Add(UiKit.Text(gs.SlotsUsed + " / " + gs.Slots + " slots used", "fr-muted"));
            var grid = UiKit.Div("fr-grid");
            grid.style.marginTop = 8;
            int used = 0;
            foreach (var r in gs.db.resources)
            {
                int have = gs.Count(r);
                while (have > 0 && used < gs.Slots)
                {
                    int inSlot = Mathf.Min(have, r.stackSize);
                    grid.Add(Slot(r, inSlot));
                    have -= inSlot; used++;
                }
            }
            for (; used < gs.Slots; used++) grid.Add(Slot(null, 0));
            parent.Add(grid);

            var list = UiKit.Div(); list.style.marginTop = 14;
            foreach (var r in gs.db.resources)
                if (gs.Count(r) > 0)
                {
                    var row = UiKit.Div("fr-row");
                    var dot = UiKit.Div("fr-dot"); dot.style.backgroundColor = UiKit.Ui(r.color);
                    row.Add(dot);
                    row.Add(UiKit.Text(r.displayName, "fr-small"));
                    row.Add(UiKit.Div("fr-spacer"));
                    row.Add(UiKit.Text(gs.Count(r).ToString(), "fr-small"));
                    list.Add(row);
                }
            parent.Add(list);
        }

        VisualElement Slot(ResourceSO r, int count)
        {
            var s = UiKit.Div("fr-slot");
            if (r != null)
            {
                Color c = UiKit.Ui(r.color);
                UiKit.Border(s, new Color(c.r, c.g, c.b, 0.9f), 2f);
                UiKit.Halo(s, c, 14f, 0.3f);
                var orb = UiKit.Div(); orb.style.width = 44; orb.style.height = 44;
                UiKit.Radius(orb, 22);
                orb.style.backgroundColor = c;
                s.Add(orb);
                s.Add(UiKit.Text(r.displayName, "fr-tiny"));
                s.Add(UiKit.Text(count.ToString(), "fr-slot-count"));
            }
            return s;
        }

        // ------------------------------------------------------------ map
        void BuildMap()
        {
            mapElement = UiKit.Div();
            mapElement.style.height = 360;
            mapElement.style.marginTop = 10;
            UiKit.Radius(mapElement, 22);
            mapElement.style.backgroundColor = new Color(0.02f, 0.04f, 0.12f, 0.9f);
            UiKit.Border(mapElement, new Color(0.1f, 0.95f, 1f, 0.3f), 2f);
            var me = mapElement;
            mapElement.generateVisualContent += ctx =>
            {
                if (selected == null || selected == gs.planet) return;
                var p = ctx.painter2D;
                Vector2 a = MapPos(gs.planet, me), b = MapPos(selected, me);
                p.lineWidth = 4f;
                p.strokeColor = new Color(1f, 0.25f, 0.95f, 0.9f);
                p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
            };

            foreach (var pl in gs.db.planets)
            {
                var planet = pl;
                var node = UiKit.Div();
                node.style.position = Position.Absolute;
                node.style.width = 70; node.style.height = 70;
                node.style.left = Length.Percent(planet.mapPosition.x * 100f);
                node.style.top = Length.Percent((1f - planet.mapPosition.y) * 100f);
                node.style.marginLeft = -35; node.style.marginTop = -35;
                UiKit.Radius(node, 35);
                node.style.backgroundColor = UiKit.Ui(planet.primary);
                bool cur = planet == gs.planet, sel = planet == selected;
                UiKit.Border(node, cur ? Color.white : sel ? UiKit.Magenta : UiKit.Ui(planet.rim, 0.8f), cur || sel ? 5f : 2f);
                UiKit.Halo(node, UiKit.Ui(planet.rim), 40f, sel ? 0.7f : 0.4f);
                node.RegisterCallback<PointerDownEvent>(e => { Sfx.Click(); selected = planet; Refresh(); });
                var name = UiKit.Text(planet.displayName, "fr-tiny");
                name.style.position = Position.Absolute; name.style.top = 72; name.style.left = -30; name.style.width = 130;
                name.style.unityTextAlign = TextAnchor.UpperCenter;
                node.Add(name);
                mapElement.Add(node);
            }
            sheet.Add(mapElement);
            mapElement.MarkDirtyRepaint();

            var p2 = selected ?? gs.planet;
            var card = Card(p2.primary);
            card.style.marginTop = 14;
            var top = UiKit.Div("fr-row");
            top.Add(UiKit.Text(p2.displayName, "fr-h2", UiKit.Ui(p2.primary)));
            top.Add(UiKit.Div("fr-spacer"));
            top.Add(UiKit.Text("TIER " + (p2.tier + 1) + "  THREAT " + p2.danger.ToString("0.0"), "fr-small"));
            card.Add(top);
            card.Add(UiKit.Text(p2.description, "fr-muted"));
            var res = UiKit.Div("fr-row-wrap");
            var seen = new HashSet<ResourceSO>();
            foreach (var s in p2.spots)
                if (s.resource != null && seen.Add(s.resource)) res.Add(UiKit.ItemChip(s.resource, s.baseYield));
            card.Add(res);
            sheet.Add(card);

            if (p2 == gs.planet) card.Add(UiKit.Text("You are here", "fr-small", UiKit.Cyan));
            else
            {
                int cost = gs.FuelCost(p2);
                bool can = gs.Count(gs.db.fuel) >= cost;
                var fr = UiKit.Div("fr-row-wrap");
                fr.Add(UiKit.ItemChip(gs.db.fuel, cost, gs.Count(gs.db.fuel)));
                card.Add(fr);
                var go = UiKit.Btn(can ? "WARP" : "NOT ENOUGH FUEL", can ? UiKit.Magenta : UiKit.Red, () => StartTravel(p2), true);
                UiKit.SetEnabledLook(go, can);
                card.Add(go);
            }
        }

        static Vector2 MapPos(PlanetSO p, VisualElement map)
        {
            return new Vector2(p.mapPosition.x * map.contentRect.width, (1f - p.mapPosition.y) * map.contentRect.height);
        }

        void StartTravel(PlanetSO target)
        {
            if (busy || defense.Active || backdrop.Warping) return;
            busy = true;
            tab = Tab.None;
            Refresh();
            StartCoroutine(backdrop.DoWarp(() =>
            {
                gs.Travel(target);
                planetView.Show(target, true);
                selected = target;
            }));
            StartCoroutine(TravelFlash());
        }

        IEnumerator TravelFlash()
        {
            float t = 0f;
            while (t < 1.8f)
            {
                t += Time.deltaTime;
                flash.style.opacity = Mathf.Pow(Mathf.Sin(Mathf.Clamp01(t / 1.8f) * Mathf.PI), 6f) * 0.85f;
                yield return null;
            }
            flash.style.opacity = 0f;
            busy = false;
            tab = Tab.Explore;
            Refresh();
            Toast("Arrived at " + gs.planet.displayName, UiKit.Cyan);
        }

        // ------------------------------------------------------------ toast
        void Toast(string text, Color color)
        {
            var t = UiKit.Div("fr-toast");
            UiKit.Border(t, color, 2f);
            UiKit.Halo(t, color, 24f, 0.25f);
            t.Add(UiKit.Text(text, "fr-h2", color));
            t.style.top = 230 + toastLayer.childCount * 90;
            toastLayer.Add(t);
            t.schedule.Execute(() => t.RemoveFromHierarchy()).StartingIn(1800);
        }
    }
}
