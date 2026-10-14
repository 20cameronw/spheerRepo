using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    /// <summary>
    /// Icon-driven, responsive UI Toolkit interface. The panel scale is recomputed whenever the screen size changes so that
    /// 1 UI unit = 0.5 dp on phones (type and control tokens are authored in these half-dps, see docs/DESIGN.md).
    /// </summary>
    public partial class UIRoot : MonoBehaviour
    {
        GameState gs;
        DefenseController defense;
        PlanetView planetView;
        SpaceBackdrop backdrop;
        WorldSurface world;
        Camera cam;
        UIDocument doc;
        PanelSettings panelSettings;

        VisualElement root, safe, hud, waveHud, toastLayer, overlayLayer, travelLayer, flash, popupLayer, modalLayer, mapLayer, bannerLayer, scrimLayer;
        VisualElement waveFill, reactorFill, xpFill;
        Label waveLabel, reactorLabel, fuelLabel, packLabel, levelLabel, planetLabel;
        VisualElement planetTag;

        bool busy;
        /// <summary>UI units per dp (1 unit = 0.5 dp).</summary>
        public const float UnitsPerDp = 2f;
        /// <summary>The canvas is never wider than this many units: tablets scale up instead of stretching the layout.</summary>
        public const float MaxCanvasWidth = 900f;
        /// <summary>Space reserved at the top of content layers for the top bar (units, below the safe-area inset).</summary>
        public const float HudTopInset = 132f;
        /// <summary>Space reserved at the bottom of popup layers for the dock (units, above the safe-area inset).</summary>
        public const float DockInset = 176f;
        bool batching;
        Vector4 inset; // left, top, right, bottom in logical px
        int lastW, lastH;
        Rect lastSafe;

        /// <summary>Physical pixels per UI unit: 0.5 dp on phones, capped so the canvas never exceeds <see cref="MaxCanvasWidth"/> units.</summary>
        public static float ComputeScale(float shortSidePx, float density)
        {
            return Mathf.Max(0.2f, Mathf.Max(density / UnitsPerDp, shortSidePx / MaxCanvasWidth));
        }

        public bool IsOverUi(Vector2 screenPos)
        {
            if (root == null || root.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPos.x, ScreenMetrics.Height - screenPos.y));
            return root.panel.Pick(p) != null;
        }

        public void Init(UIDocument document, GameState state, DefenseController def, PlanetView pv, SpaceBackdrop bd, WorldSurface ws, Camera camera)
        {
            doc = document; gs = state; defense = def; planetView = pv; backdrop = bd; world = ws; cam = camera;
            LoadPrefs();
            panelSettings = doc.panelSettings;
            selected = gs.planet;

            root = doc.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.AddToClassList("fr-root");
            UiKit.ApplyStyles(root);

            // Full-screen dim under the HUD: modals keep the top bar and dock live while the world behind them is dimmed evenly.
            scrimLayer = Layer();
            scrimLayer.AddToClassList("fr-scrim");
            scrimLayer.pickingMode = PickingMode.Position;
            scrimLayer.style.display = DisplayStyle.None;
            scrimLayer.RegisterCallback<PointerDownEvent>(e => CloseModal());
            root.Add(scrimLayer);

            safe = Layer(); safe.style.display = DisplayStyle.Flex;
            root.Add(safe);
            BuildHud();
            BuildWaveHud();

            popupLayer = Layer(); modalLayer = Layer(); bannerLayer = Layer(); mapLayer = Layer();
            toastLayer = UiKit.Div("fr-toasts"); toastLayer.pickingMode = PickingMode.Ignore;
            overlayLayer = Layer();
            // Edge-to-edge layer for travel games that draw behind the status bar (no safe-area inset; the game places its own HUD with the insets it is given).
            travelLayer = Layer();
            flash = Layer();
            flash.style.backgroundColor = new Color(0.6f, 1f, 1f, 1f);
            flash.style.opacity = 0f;
            root.Add(popupLayer); root.Add(bannerLayer); root.Add(modalLayer); root.Add(mapLayer);
            root.Add(toastLayer); root.Add(overlayLayer); root.Add(travelLayer); root.Add(flash);

            ApplyLayout(true);

            gs.Changed += OnStateChanged;
            defense.WaveEnded += OnWaveEnded;
            world.StructureTapped += OnStructureTapped;
            world.SlotTapped += OnSlotTapped;
            OnStateChanged();
        }

        VisualElement Layer()
        {
            var e = UiKit.Div("fr-fill");
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        void OnDestroy()
        {
            if (gs != null) gs.Changed -= OnStateChanged;
            if (defense != null) defense.WaveEnded -= OnWaveEnded;
            if (world != null)
            {
                world.StructureTapped -= OnStructureTapped;
                world.SlotTapped -= OnSlotTapped;
            }
        }

        // ------------------------------------------------------------ responsive layout
        void ApplyLayout(bool force)
        {
            var sa = ScreenMetrics.SafeArea;
            int sw = ScreenMetrics.Width, sh = ScreenMetrics.Height;
            if (!force && sw == lastW && sh == lastH && sa == lastSafe) return;
            lastW = sw; lastH = sh; lastSafe = sa;
            float scale = ComputeScale(ScreenMetrics.ShortSide, ScreenMetrics.Density);
            if (panelSettings != null) panelSettings.scale = scale;
            inset = new Vector4(sa.xMin / scale, (sh - sa.yMax) / scale, (sw - sa.xMax) / scale, sa.yMin / scale);
            hud.style.left = inset.x; hud.style.top = inset.y;
            hud.style.right = inset.z; hud.style.bottom = inset.w;
            if (waveHud != null)
            {
                waveHud.style.left = inset.x; waveHud.style.right = inset.z; waveHud.style.top = inset.y; waveHud.style.bottom = inset.w;
                waveHud.style.paddingLeft = UiKit.Space.S32; waveHud.style.paddingRight = UiKit.Space.S32;
                waveHud.style.paddingTop = HudTopInset + UiKit.Space.S16; waveHud.style.paddingBottom = UiKit.Space.S32;
            }
            LayoutContentLayer(popupLayer, inset, false);
            LayoutContentLayer(modalLayer, inset, false);
            LayoutContentLayer(bannerLayer, inset, false);
            LayoutContentLayer(overlayLayer, inset, true);
            toastLayer.style.top = inset.y + HudTopInset + UiKit.Space.S8;
            if (mapOpen) LayoutMapChrome();
        }

        public static void LayoutContentLayer(VisualElement layer, Vector4 safeInsets, bool coverDock)
        {
            layer.style.left = safeInsets.x + UiKit.Space.S16;
            layer.style.right = safeInsets.z + UiKit.Space.S16;
            layer.style.top = safeInsets.y + HudTopInset;
            layer.style.bottom = safeInsets.w + (coverDock ? UiKit.Space.S16 : DockInset);
            layer.style.overflow = Overflow.Hidden;
        }

        // ------------------------------------------------------------ HUD
        void BuildHud()
        {
            hud = Layer();
            safe.Add(hud);

            var top = UiKit.Div("fr-topbar"); top.pickingMode = PickingMode.Ignore;
            planetTag = UiKit.Div("fr-topbar-bar");
            planetTag.pickingMode = PickingMode.Ignore;
            planetLabel = UiKit.Text("", "fr-planet-name");
            planetTag.Add(planetLabel);
            fuelLabel = Chip(planetTag, "fuel", UiKit.Amber, null);
            packLabel = Chip(planetTag, "pack", UiKit.Cyan, null);
            levelLabel = Chip(planetTag, "star", UiKit.Magenta, f => xpFill = f);
            top.Add(planetTag);
            var menuBtn = Icons.IconButton("menu", UiKit.Cyan, OpenMenu, 0f, true);
            menuBtn.AddToClassList("fr-menubtn");
            menuBtn.name = "menu-button";
            top.Add(menuBtn);
            hud.Add(top);

            var dock = UiKit.Div("fr-dock"); dock.pickingMode = PickingMode.Ignore;
            dock.Add(DockButton("research", "COMMAND", UiKit.Magenta, OpenResearch));
            dock.Add(DockButton("map", "MAP", UiKit.Cyan, OpenMap));
            dock.Add(DockButton("pack", "PACK", UiKit.Amber, OpenPack));
            dock.Add(DockButton("build", "BUILD", UiKit.Lime, OpenShop));
            hud.Add(dock);
        }

        /// <summary>A dock button: tonal glass, thin accent outline, icon with a short caption. Accent fill only while pressed.</summary>
        Button DockButton(string icon, string caption, Color accent, Action onClick)
        {
            var b = new Button { name = "dock-" + icon };
            b.AddToClassList("fr-dock-btn");
            UiKit.SetAccent(b, accent);
            b.Add(Icons.Make(icon, accent));
            b.Add(UiKit.Text(caption, "fr-dock-label"));
            b.clicked += () => { Sfx.Click(); onClick(); };
            return b;
        }

        Label Chip(VisualElement parent, string icon, Color color, Action<VisualElement> xp)
        {
            var chip = UiKit.Div("fr-chip"); chip.pickingMode = PickingMode.Ignore;
            Color c = UiKit.Ui(color);
            chip.Add(Icons.Make(icon, c));
            var l = UiKit.Text("0", "fr-chip-text");
            if (xp != null)
            {
                var col = UiKit.Div("fr-chip-col"); col.pickingMode = PickingMode.Ignore;
                col.Add(l);
                VisualElement fill;
                var bar = UiKit.Bar(color, out fill);
                bar.AddToClassList("fr-chip-xp");
                col.Add(bar);
                xp(fill);
                chip.Add(col);
            }
            else chip.Add(l);
            parent.Add(chip);
            return l;
        }

        void BuildWaveHud()
        {
            waveHud = Layer();
            waveHud.style.display = DisplayStyle.None;
            waveHud.style.justifyContent = Justify.SpaceBetween;

            var top = UiKit.Div(); top.pickingMode = PickingMode.Ignore;
            waveLabel = UiKit.Text("", "fr-h2", UiKit.Cyan);
            top.Add(waveLabel);
            top.Add(UiKit.Bar(UiKit.Cyan, out waveFill));
            var hint = UiKit.Text("Tap aliens to zap them", "fr-tiny");
            hint.style.marginTop = UiKit.Space.S8;
            top.Add(hint);
            waveHud.Add(top);

            var bottom = UiKit.Div(); bottom.pickingMode = PickingMode.Ignore;
            reactorLabel = UiKit.Text("", "fr-h2", UiKit.Lime);
            bottom.Add(reactorLabel);
            var rb = UiKit.Bar(UiKit.Lime, out reactorFill);
            rb.style.height = UiKit.Space.S24;
            bottom.Add(rb);
            waveHud.Add(bottom);
            root.Add(waveHud);
        }

        void OnStateChanged()
        {
            if (batching) return;
            fuelLabel.text = gs.Count(gs.db.fuel).ToString();
            packLabel.text = gs.SlotsUsed + "/" + gs.Slots;
            levelLabel.text = "LV " + gs.PlayerLevel;
            planetLabel.text = gs.planet.Name.ToUpper();
            planetLabel.style.color = UiKit.Ui(gs.planet.Rim);
            UiKit.Border(planetTag, UiKit.Tint(gs.planet.Rim, 0.5f), 2f);
            UiKit.SetFill(xpFill, gs.LevelProgress);
            RefreshModal();
            RefreshPopup();
            if (mapOpen) { InvalidateMapPreview(); BuildMapCard(); }
        }

        // ------------------------------------------------------------ frame
        void Update()
        {
            if (root == null) return;
            ApplyLayout(false);

            bool wave = defense.Active;
            world.Locked = wave || busy;
            if (waveHud != null)
            {
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
            }
            hud.style.display = wave ? DisplayStyle.None : DisplayStyle.Flex;
            if (wave && (popup != null || modal != null || buildDef != null)) { CloseAll(); }

            UpdatePopupPosition();
            RefreshBuilderStatus();
            UpdateMapInput();
        }

        void CloseAll()
        {
            ClosePopup();
            CloseModal();
            CloseMap();
            ExitBuild();
        }

        // ------------------------------------------------------------ helpers
        /// <summary>A tonal glass card. Only the selected/equipped one (<paramref name="highlight"/>) gets accent colour.</summary>
        VisualElement Card(Color accent, bool highlight = false)
        {
            var c = UiKit.Div("fr-card");
            if (highlight) { UiKit.SetAccent(c, accent); c.AddToClassList("fr-card-on"); }
            return c;
        }

        /// <summary>A tonal action button with an icon. <paramref name="primary"/> marks the screen's single main action.</summary>
        Button IconBtn(string text, string icon, Color color, Action onClick, bool big = false, bool primary = false)
        {
            var b = UiKit.Btn("", color, onClick, big, primary);
            b.Add(Icons.Make(icon, color, big ? UiKit.Size.IconLg : UiKit.Size.IconMd));
            b.Add(UiKit.Text(text, "fr-btn-label"));
            return b;
        }

        VisualElement ResourceIconChip(ResourceSO r, string text, bool lack = false)
        {
            return UiKit.Chip(Icons.ForResource(r), UiKit.Ui(r.color), text, lack);
        }

        string StructureEffect(StructureSO d, int level)
        {
            float effect = d.EffectAt(level);
            int pct = Mathf.RoundToInt(effect * 100f);
            if (d.kind.IsTurret()) return "dmg " + Mathf.RoundToInt(d.damage * (d.HasLevelRows ? effect : 1f + effect));
            if (d.kind == StructureKind.Warehouse) return d.StorageCapacity(level) + " storage slots";
            if (d.kind == StructureKind.FuelDepot)
                return d.StorageCapacity(level) + " fuel slots" + (pct > 0 ? " · +" + pct + "% fuel stack" : "");
            if (d.kind == StructureKind.ResearchLab) return "Research Lab level " + Mathf.RoundToInt(effect);
            if (d.kind == StructureKind.RadarTower) return "+" + Mathf.RoundToInt(effect) + " ly local survey";
            if (d.kind == StructureKind.ColonyHub) return "+" + Mathf.RoundToInt(effect) + " builder crews";
            if (d.HasLevelRows && d.kind.IsStation()) return "Batch size " + Mathf.RoundToInt(effect) + " (level bonus planned)";
            if (d.kind == StructureKind.Refinery) return "+" + pct + "% output";
            if (d.kind.IsGather()) return "+" + pct + "% yield";
            return "+" + pct + "% rewards, XP";
        }

        string UpgradeEffectText(UpgradeSO u, int level)
        {
            float v = u.ContributionAt(level, gs.db.baseBackpackSlots);
            switch (u.effect)
            {
                case UpgradeEffect.ToolPower: return "x" + (1f + v).ToString("0.0") + " yield";
                case UpgradeEffect.BackpackSlots: return "+" + Mathf.RoundToInt(v) + " slots";
                case UpgradeEffect.TurretFireRate: return "+" + Mathf.RoundToInt(v * 100f) + "% firing speed";
                case UpgradeEffect.ReactorHealth: return "+" + Mathf.RoundToInt(v) + " HP";
                case UpgradeEffect.JumpRange:
                case UpgradeEffect.ScannerRange: return u.ValueAt(level).ToString("0") + " ly";
                case UpgradeEffect.FuelEfficiency: return "-" + Mathf.RoundToInt(v * 100f) + "% fuel use";
                case UpgradeEffect.FuelTankSlots: return u.ValueAt(level).ToString("0") + " tank slots (coming later)";
                case UpgradeEffect.HullPlating: return "Protection level " + level;
                case UpgradeEffect.StackMultiplier: return "x" + u.ValueAt(level, 1f).ToString("0.00") + " stacks (coming later)";
                default: return "+" + Mathf.RoundToInt(v * 100f) + "%";
            }
        }

        static string StructureIcon(StructureKind k) { return Icons.ForStructure(k); }

        // ------------------------------------------------------------ settings
        void LoadPrefs()
        {
            Sfx.Muted = PlayerPrefs.GetInt("fr_muted", 0) == 1;
            world.ShowStructureLabels = PlayerPrefs.GetInt("fr_structure_labels", 1) == 1;
        }

        // ------------------------------------------------------------ gathering
        void StartGather(GatherSpot spot, Transform at, float bonus)
        {
            if (busy || defense.Active) return;
            busy = true;
            Vector3 fxPos = at != null ? at.position : Vector3.zero;
            Minigame g = spot.game == MinigameKind.Chop ? (Minigame)new NodeGame(true)
                       : spot.game == MinigameKind.Mine ? (Minigame)new NodeGame(false)
                       : spot.game == MinigameKind.Siphon ? (Minigame)new SiphonGame()
                       : spot.game == MinigameKind.Excavate ? (Minigame)new ExcavationGame()
                       : spot.game == MinigameKind.Salvage ? (Minigame)new SalvageGame() : new DrillGame();
            g.toolDamage = gs.ToolDamage(spot.game);
            g.toolLevel = gs.ToolLevel(spot.game);
            g.toolName = gs.ToolName(spot.game);
            g.Run(overlayLayer, spot.resource.color, gs.ToolTolerance(spot.game), spot.difficulty, r =>
            {
                busy = false;
                float amount = spot.baseYield * gs.ToolMultiplier(spot.game) * (1f + bonus) * (0.4f + 1.1f * r.quality);
                int n = r.quality > 0.05f ? Mathf.Max(1, Mathf.RoundToInt(amount)) : 0;
                int got = gs.Add(spot.resource, n);
                Color ui = UiKit.Ui(spot.resource.color);
                if (got > 0)
                {
                    Toast("+" + got + " " + spot.resource.displayName, ui);
                    Fx.Burst(fxPos, spot.resource.color, 40, 5f, 0.25f);
                    planetView.Punch();
                    Sfx.Good();
                }
                else Toast(n > 0 ? "Backpack full!" : "Missed everything...", UiKit.Red);
                if (got < n) Toast("Lost " + (n - got) + " (backpack full)", UiKit.Amber);
            });
        }

        // ------------------------------------------------------------ wave end
        void OnWaveEnded(WaveResult r)
        {
            var panel = UiKit.Div("fr-overlay");
            panel.AddToClassList("fr-overlay-clear");
            panel.pickingMode = PickingMode.Position;
            if (scrimLayer != null) scrimLayer.style.display = DisplayStyle.Flex;
            Color c = r.victory ? UiKit.Lime : UiKit.Red;
            UiKit.SetAccent(panel, c);
            panel.Add(UiKit.Text(r.victory ? "WAVE CLEARED" : "REACTOR OVERLOADED", "fr-title", c));
            if (r.victory)
            {
                panel.Add(UiKit.Text("Salvage recovered", "fr-muted"));
                panel.Add(UiKit.CostRow(r.granted, gs, false));
                if (defense.Current != null) panel.Add(UiKit.Chip("star", UiKit.Magenta, "+" + defense.Current.xp + " XP"));
                if (r.lost.Count > 0)
                {
                    panel.Add(UiKit.Text("Backpack full - lost:", "fr-muted", UiKit.Amber));
                    panel.Add(UiKit.CostRow(r.lost, gs, false));
                }
            }
            else panel.Add(UiKit.Text("The aliens broke through. No salvage this time.", "fr-muted"));
            var b = UiKit.Btn("CONTINUE", c, null, true, true);
            b.clicked += () =>
            {
                panel.RemoveFromHierarchy();
                if (scrimLayer != null && modal == null) scrimLayer.style.display = DisplayStyle.None;
            };
            b.style.marginTop = UiKit.Space.S32;
            panel.Add(b);
            overlayLayer.Add(panel);
            OnStateChanged();
        }

        // ------------------------------------------------------------ travel
        // Multi-leg travel is hosted in UIRoot.Travel.cs.

        // ------------------------------------------------------------ toast
        void Toast(string text, Color color)
        {
            var t = UiKit.Div("fr-toast");
            t.pickingMode = PickingMode.Ignore;
            UiKit.Border(t, color, 2f);
            UiKit.Halo(t, color, 24f, 0.25f);
            t.Add(UiKit.Text(text, "fr-h2", color));
            toastLayer.Add(t);
            t.schedule.Execute(() => t.RemoveFromHierarchy()).StartingIn(1800);
        }
    }
}
