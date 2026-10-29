using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Frontier.Services;

namespace Frontier
{
    /// <summary>
    /// Icon-driven, responsive UI Toolkit interface. The panel scale is recomputed whenever the screen size changes so that
    /// 1 UI unit = 0.5 dp on phones (type and control tokens are authored in these half-dps, see docs/DESIGN.md).
    /// </summary>
    public partial class UIRoot : MonoBehaviour
    {
        GameState gs;
        UniverseService observedUniverse;
        DefenseController defense;
        PlanetView planetView;
        SpaceBackdrop backdrop;
        WorldSurface world;
        Camera cam;
        UIDocument doc;
        PanelSettings panelSettings;
        FrontierServices services;

        VisualElement root, safe, hud, waveHud, toastLayer, overlayLayer, travelLayer, flash, popupLayer, modalLayer, mapLayer, bannerLayer, scrimLayer;
        VisualElement waveFill, reactorFill, xpFill;
        Label waveLabel, waveModifierLabel, reactorLabel, fuelLabel, shardLabel, packLabel, levelLabel, planetLabel;
        WaveInfo waveHudInfo;
        VisualElement planetTag;
        Button surfaceViewButton;
        VisualElement surfaceViewIcon;
        Button packDock;
        readonly List<VisualElement> packSegments = new List<VisualElement>();

        bool busy, introActive;
        public bool IsTravelling { get; private set; }
        Minigame activeGatherMinigame;
        VisualElement waveResultPanel;
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

        public void Init(UIDocument document, GameState state, DefenseController def, PlanetView pv, SpaceBackdrop bd, WorldSurface ws, Camera camera, FrontierServices serviceSet)
        {
            doc = document; gs = state; defense = def; planetView = pv; backdrop = bd; world = ws; cam = camera;
            services = serviceSet;
            gameplayRewardRules = Resources.Load<GameplayRewardRules>("Frontier/GameplayRewardRules");
            observedUniverse = gs.universe;
            LoadPrefs();
            panelSettings = doc.panelSettings;
            PresentationQuality.Changed += ApplyPanelBudget;
            ApplyPanelBudget();
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
            BuildFirstSessionGuide();
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
            NotificationPreferences.StatusChanged += OnNotificationStatusChanged;
            defense.WaveEnded += OnWaveEnded;
            world.StructureTapped += OnStructureTapped;
            world.SlotTapped += OnSlotTapped;
            world.ObstacleTapped += OnObstacleTapped;
            OnStateChanged();
            BuildProjectResume();
        }

        VisualElement Layer()
        {
            var e = UiKit.Div("fr-fill");
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        void OnDestroy()
        {
            PresentationQuality.Changed -= ApplyPanelBudget;
            NotificationPreferences.StatusChanged -= OnNotificationStatusChanged;
            if (gs != null) gs.Changed -= OnStateChanged;
            if (defense != null) defense.WaveEnded -= OnWaveEnded;
            if (world != null)
            {
                world.StructureTapped -= OnStructureTapped;
                world.SlotTapped -= OnSlotTapped;
                world.ObstacleTapped -= OnObstacleTapped;
            }

        }

        void ApplyPanelBudget()
        {
            if (panelSettings != null) PresentationQuality.ApplyPanelBudget(panelSettings);
        }

        void OnNotificationStatusChanged()
        {
            if (root != null && modalKey == "menu" && menuPage == "settings") RefreshModal();
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
            top.usageHints = UsageHints.DynamicTransform;
            planetTag = UiKit.Div("fr-topbar-bar");
            planetTag.pickingMode = PickingMode.Ignore;
            planetLabel = UiKit.Text("", "fr-planet-name");
            planetTag.Add(planetLabel);
            fuelLabel = Chip(planetTag, "fuel", UiKit.Amber, null);
            shardLabel = Chip(planetTag, "crystal", UiKit.Cyan, null);
            shardLabel.name = "hud-nova-shards";
            levelLabel = Chip(planetTag, "star", UiKit.Magenta, f => xpFill = f);
            top.Add(planetTag);
            var menuBtn = Icons.IconButton("menu", UiKit.Cyan, OpenMenu, 0f, true);
            menuBtn.AddToClassList("fr-menubtn");
            menuBtn.name = "menu-button";
            top.Add(menuBtn);
            hud.Add(top);
            surfaceViewButton = Icons.IconButton("base", UiKit.Cyan, () =>
            {
                if (busy || introActive || defense.Active) return;
                CloseModal();
                ClosePopup();
                planetView.CloseView = !planetView.CloseView;
                RefreshViewButton();
            });
            surfaceViewIcon = surfaceViewButton.Q(className: "fr-icon");
            surfaceViewButton.name = "world-view-toggle";
            surfaceViewButton.style.position = Position.Absolute;
            surfaceViewButton.style.right = UiKit.Space.S24;
            surfaceViewButton.style.bottom = DockInset + UiKit.Space.S16;
            RefreshViewButton();
            hud.Add(surfaceViewButton);

            var dock = UiKit.Div("fr-dock"); dock.pickingMode = PickingMode.Ignore;
            dock.usageHints = UsageHints.DynamicTransform;
            dock.Add(DockButton("research", UiText.Get("nav.command"), UiKit.Magenta, OpenResearch));
            dock.Add(DockButton("map", UiText.Get("nav.map"), UiKit.Cyan, OpenMap));
            packDock = DockButton("ship", UiText.Get("nav.pack"), UiKit.Amber, OpenPack);
            var capacity = UiKit.Div("fr-pack-capacity");
            capacity.name = "pack-capacity-gauge";
            capacity.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 8; i++)
            {
                var segment = UiKit.Div("fr-pack-segment");
                segment.pickingMode = PickingMode.Ignore;
                packSegments.Add(segment);
                capacity.Add(segment);
            }
            packDock.Add(capacity);
            packLabel = UiKit.Text("", "fr-pack-capacity-text");
            packLabel.name = "pack-capacity-text";
            packDock.Add(packLabel);
            dock.Add(packDock);
            dock.Add(DockButton("build", UiText.Get("nav.build"), UiKit.Lime, OpenShop));
            hud.Add(dock);
        }

        void RefreshViewButton()
        {
            if (surfaceViewButton == null) return;
            surfaceViewButton.tooltip = UiText.Get(planetView.CloseView ? "world.view.orbit" : "world.view.close");
            if (surfaceViewIcon != null) Icons.Apply(surfaceViewIcon, planetView.CloseView ? "map" : "base", UiKit.Cyan);
        }

        void RefreshPackCapacity()
        {
            packLabel.text = gs.SlotsUsed + "/" + gs.Slots;
            packDock.tooltip = UiText.Get("pack.capacity", gs.SlotsUsed, gs.Slots);
            float fullness = gs.Slots <= 0 ? 1 : Mathf.Clamp01(gs.SlotsUsed / (float)gs.Slots);
            int filled = Mathf.CeilToInt(fullness * packSegments.Count);
            for (int i = 0; i < packSegments.Count; i++)
            {
                float fraction = i / (float)(packSegments.Count - 1);
                Color color = fraction < 0.5f ? Color.Lerp(UiKit.Lime, UiKit.Amber, fraction * 2)
                    : Color.Lerp(UiKit.Amber, UiKit.Red, (fraction - 0.5f) * 2);
                packSegments[i].style.backgroundColor = i < filled ? color : UiKit.Tint(UiKit.Muted, 0.18f);
                packSegments[i].EnableInClassList("fr-pack-segment-filled", i < filled);
            }
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
            waveModifierLabel = UiKit.Text("", "fr-small", UiKit.Amber);
            waveModifierLabel.style.whiteSpace = WhiteSpace.Normal;
            top.Add(waveModifierLabel);
            var hint = UiKit.Text(UiText.Get("wave.tap-help"), "fr-tiny");
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
            ResetProjectContextIfNeeded();
            RefreshViewButton();
            if (observedUniverse != gs.universe)
            {
                observedUniverse = gs.universe;
                CloseMap();
                ClosePopup();
                ExitBuild();
                selected = gs.planet;
                planetView.Show(gs.planet, false, gs.TerraformerStageOn(gs.planet));
            }
            if (planetView.Current != null && planetView.Current.Id == gs.planet.Id)
                planetView.SetTerraformingStage(gs.TerraformerStageOn(gs.planet));
            fuelLabel.text = UiKit.FuelLabel(gs.FuelGallons);
            fuelLabel.tooltip = UiText.Get("fuel.summary", gs.FuelTankUsed, gs.FuelTankSlots);
            shardLabel.text = gs.ShardBalance.ToString("N0");
            shardLabel.tooltip = UiText.Get("hyperspace.revive.balance", gs.ShardBalance);
            RefreshPackCapacity();
            levelLabel.text = UiText.Get("hud.player-level", gs.PlayerLevel);
            planetLabel.text = gs.planet.Name.ToUpper();
            planetLabel.style.color = UiKit.Ui(gs.planet.Rim);
            UiKit.Border(planetTag, UiKit.Tint(gs.AscensionRank > 0 ? AscensionAccent(gs.AscensionRank) : gs.planet.Rim, 0.5f), 2f);
            UiKit.SetFill(xpFill, gs.LevelProgress);
            RefreshFirstSessionGuide();
            RefreshModal();
            RefreshPopup();
            if (mapOpen) { RefreshMapPinPlanets(); InvalidateMapPreview(); BuildMapCard(); }
            RefreshShortage();
        }

        // ------------------------------------------------------------ frame
        void Update()
        {
            if (root == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();
            ApplyLayout(false);

            bool wave = defense.Active;
            if (wave && planetView.CloseView)
            {
                planetView.CloseView = false;
                RefreshViewButton();
            }
            world.Locked = wave || busy;
            UpdateFirstSessionGuideVisibility();
            RefreshProjectResume();
            if (waveHud != null)
            {
                waveHud.style.display = wave ? DisplayStyle.Flex : DisplayStyle.None;
                if (wave)
                {
                    if (waveHudInfo != defense.Current)
                    {
                        waveHudInfo = defense.Current;
                        waveModifierLabel.text = waveHudInfo.modifier == null ? "" :
                            UiText.Get("wave.modifier", gs.db.formula.ModifierName(waveHudInfo.modifier).ToUpperInvariant(),
                                gs.db.formula.ModifierDescription(waveHudInfo.modifier));
                        waveModifierLabel.style.display = waveHudInfo.modifier == null ? DisplayStyle.None : DisplayStyle.Flex;
                    }
                    int total = defense.Current.aliens.Count;
                    UiKit.SetFill(waveFill, total == 0 ? 0f : defense.Killed / (float)total);
                    UiKit.SetFill(reactorFill, defense.ReactorHp / Mathf.Max(1f, defense.ReactorMax));
                    waveLabel.text = UiText.Get("wave.progress", defense.Current.waveNumber, defense.Killed, total);
                    reactorLabel.text = UiText.Get("wave.reactor", Mathf.CeilToInt(defense.ReactorHp));
                    float hp = defense.ReactorHp / Mathf.Max(1f, defense.ReactorMax);
                    reactorFill.style.backgroundColor = Color.Lerp(UiKit.Red, UiKit.Lime, hp);
                }
            }
            hud.style.display = wave || introActive || networkFinaleActive ? DisplayStyle.None : DisplayStyle.Flex;
            if (wave && (popup != null || modal != null || buildDef != null || shortageOverlay != null)) { CloseAll(); }

            UpdatePopupPosition();
            RefreshBuilderStatus();
            RefreshObstacleStatus();
            UpdateMapInput();
        }

        void CloseAll()
        {
            CloseShortage();
            ClosePopup();
            CloseModal();
            CloseMap();
            ExitBuild();
        }

        public bool HandleBack()
        {
            if (skipShipMoment != null) { skipShipMoment(); return true; }
            if (activeGatherMinigame != null) return activeGatherMinigame.HandleBack();
            if (activeTravelMinigame is ITravelBackHandler travel) return travel.HandleBack();
            if (busy || defense.Active) return true;
            if (waveResultPanel != null) { CloseWaveResult(); return true; }
            if (shortageOverlay != null) { CloseShortage(); return true; }
            if (popup != null) { ClosePopup(); return true; }
            if (modal != null)
            {
                if (modalKey == "menu" && !string.IsNullOrEmpty(menuPage))
                {
                    menuPage = "";
                    RefreshModal();
                }
                else CloseModal();
                return true;
            }
            if (mapOpen) { CloseMap(); return true; }
            if (buildDef != null) { ExitBuild(); return true; }
            return false;
        }

        // ------------------------------------------------------------ helpers
        /// <summary>A tonal glass card. Only the selected/equipped one (<paramref name="highlight"/>) gets accent colour.</summary>
        VisualElement Card(Color accent, bool highlight = false)
        {
            return new FrontierCard { Accent = accent, Highlighted = highlight };
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
            if (d.kind.IsTurret()) return UiText.Get("effect.damage", Mathf.RoundToInt(d.damage * (d.HasLevelRows ? effect : 1f + effect)));
            if (d.kind == StructureKind.Warehouse) return UiText.Get("effect.storage", d.StorageCapacity(level));
            if (d.kind == StructureKind.FuelDepot)
                return UiText.Get(pct > 0 ? "effect.fuel-storage-stack" : "effect.fuel-storage", d.StorageCapacity(level), pct);
            if (d.kind == StructureKind.AutoHarvester)
                return UiText.Get("effect.offline-harvest", effect, d.CapacityAt(level), d.StorageCapacity(level));
            if (d.kind == StructureKind.Satellite)
                return UiText.Get(d.CapacityAt(level) > 0 ? "effect.beam.range" : "effect.beam.system",
                    d.CapacityAt(level), Mathf.RoundToInt(effect * 100f));
            if (d.kind == StructureKind.ResearchLab) return UiText.Get("effect.lab", Mathf.RoundToInt(effect));
            if (d.kind == StructureKind.RadarTower) return UiText.Get("effect.survey", Mathf.RoundToInt(effect));
            if (d.kind == StructureKind.ColonyHub) return UiText.Get("effect.builders-discount",
                Mathf.RoundToInt(effect), Mathf.Clamp(d.CapacityAt(level), 0, 90));
            if (d.kind == StructureKind.WarpGate) return UiText.Get("effect.warp-gate", pct);
            if (d.kind == StructureKind.DysonCollector)
                return UiText.Get("effect.dyson", effect, d.CapacityAt(level));
            if (d.kind == StructureKind.Terraformer) return UiText.Get("effect.terraformer", pct);
            if (d.HasLevelRows && d.kind.IsStation()) return UiText.Get("effect.batch", Mathf.RoundToInt(effect));
            if (d.kind == StructureKind.Refinery) return UiText.Get("effect.output", pct);
            if (d.kind.IsGather()) return UiText.Get("effect.yield", pct);
            return UiText.Get("effect.rewards", pct);
        }

        string UpgradeEffectText(UpgradeSO u, int level)
        {
            float v = u.ContributionAt(level, gs.db.baseBackpackSlots);
            switch (u.effect)
            {
                case UpgradeEffect.ToolPower: return UiText.Get("effect.tool-power", 1f + v);
                case UpgradeEffect.BackpackSlots: return UiText.Get("effect.pack-slots", Mathf.RoundToInt(v));
                case UpgradeEffect.TurretFireRate: return UiText.Get("effect.fire-rate", Mathf.RoundToInt(v * 100f));
                case UpgradeEffect.ReactorHealth: return UiText.Get("effect.health", Mathf.RoundToInt(v));
                case UpgradeEffect.JumpRange:
                case UpgradeEffect.ScannerRange: return UiText.Get("effect.range", u.ValueAt(level));
                case UpgradeEffect.FuelEfficiency: return UiText.Get("effect.fuel-use", Mathf.RoundToInt(v * 100f));
                case UpgradeEffect.FuelTankSlots: return UiText.Get("effect.tank-slots", u.ValueAt(level));
                case UpgradeEffect.HullPlating: return UiText.Get("effect.plating", level);
                case UpgradeEffect.StackMultiplier: return UiText.Get("effect.stack-multiplier", u.ValueAt(level, 1f));
                default: return UiText.Get("effect.percent", Mathf.RoundToInt(v * 100f));
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
                       : spot.game == MinigameKind.Mine ? (Minigame)new VeinGame()
                       : spot.game == MinigameKind.Siphon ? (Minigame)new SiphonGame()
                       : spot.game == MinigameKind.Excavate ? (Minigame)new ExcavationGame()
                       : spot.game == MinigameKind.Salvage ? (Minigame)new SalvageGame() : new DrillGame();
            g.toolDamage = gs.ToolDamage(spot.game);
            g.toolLevel = gs.ToolLevel(spot.game);
            g.toolName = gs.ToolName(spot.game);
            activeGatherMinigame = g;
            MinigameResult previewResult = default;
            bool baseCommitted = false;
            Func<Task<ServiceResult<RewardedAdResult>>> offerDoubleYield =
                gameplayRewardRules != null && gs.stats.harvestRunsCompleted > 0 &&
                GameplayOfferAllowed(gameplayRewardRules.harvestEnabled, "double-yield")
                    ? (Func<Task<ServiceResult<RewardedAdResult>>>) (async () =>
                    {
                        if (baseCommitted || !GameplayOfferAllowed(gameplayRewardRules.harvestEnabled, "double-yield"))
                            return ServiceResult<RewardedAdResult>.Failed(ServiceMessage.Get("reward.changed"));
                        int quote = HarvestAmount(spot, bonus, previewResult);
                        if (!gs.PrepareGameplayReward("double-yield", spot.resource, quote, null, out var ticket, out var error))
                            return ServiceResult<RewardedAdResult>.Failed(error);
                        baseCommitted = true;
                        return await ShowGameplayReward(ticket);
                    })
                    : null;
            g.Run(overlayLayer, spot.resource.color, gs.ToolTolerance(spot.game), spot.difficulty, r =>
            {
                busy = false;
                if (activeGatherMinigame == g) activeGatherMinigame = null;
                if (r.abandoned)
                {
                    Toast(UiText.Get("harvest.abandoned"), UiKit.Amber);
                    return;
                }
                int n = HarvestAmount(spot, bonus, r);
                if (!baseCommitted && r.quality > 0.05f) gs.RecordHarvestRunCompleted();
                int got = baseCommitted ? n : gs.Add(spot.resource, n);
                Color ui = UiKit.Ui(spot.resource.color);
                if (got > 0)
                {
                    Toast(UiText.Get("harvest.reward", got, spot.resource.LocalizedName), ui);
                    Fx.Burst(fxPos, spot.resource.color, 40, 5f, 0.25f);
                    planetView.Punch();
                    Sfx.Good();
                }
                else Toast(UiText.Get(n > 0 ? "harvest.backpack-full" : "harvest.missed"), UiKit.Red);
                if (got < n) Toast(UiText.Get("harvest.overflow", n - got), UiKit.Amber);
                if (r.rewardedDoubleYield && baseCommitted)
                    Toast(UiText.Get("reward.saved"), UiKit.Lime);
            }, offerDoubleYield, r =>
            {
                previewResult = r;
                int expected = HarvestAmount(spot, bonus, r);
                int free = gs.FreeSpace(spot.resource);
                return expected > 0 && (long)free >= (long)expected * 2;
            }, r => UiText.Get("reward.harvest", HarvestAmount(spot, bonus, r), spot.resource.LocalizedName));
        }

        int HarvestAmount(GatherSpot spot, float bonus, MinigameResult result)
        {
            if (result.quality <= 0.05f) return 0;
            float amount = spot.baseYield * gs.ToolMultiplier(spot.game) *
                (1f + bonus + gs.TerraformerHarvestBonus) * (0.4f + 1.1f * result.quality);
            return Mathf.Max(1, Mathf.RoundToInt(amount));
        }

        // ------------------------------------------------------------ wave end
        void OnWaveEnded(WaveResult r)
        {
            CloseWaveResult();
            var panel = UiKit.Div("fr-overlay");
            waveResultPanel = panel;
            panel.AddToClassList("fr-overlay-clear");
            panel.pickingMode = PickingMode.Position;
            if (scrimLayer != null) scrimLayer.style.display = DisplayStyle.Flex;
            Color c = r.victory ? UiKit.Lime : UiKit.Red;
            UiKit.SetAccent(panel, c);
            panel.Add(UiKit.Text(UiText.Get(r.victory ? "wave.victory" : "wave.defeat"), "fr-title", c));
            if (r.victory)
            {
                panel.Add(UiKit.Text(UiText.Get("wave.salvage"), "fr-muted"));
                panel.Add(UiKit.CostRow(r.granted, gs, false));
                if (defense.Current != null) panel.Add(UiKit.Chip("star", UiKit.Magenta, UiText.Get("wave.xp", defense.Current.xp)));
                if (r.lost.Count > 0)
                {
                    panel.Add(UiKit.Text(UiText.Get("wave.overflow"), "fr-muted", UiKit.Amber));
                    panel.Add(UiKit.CostRow(r.lost, gs, false));
                }
            }
            else panel.Add(UiKit.Text(UiText.Get("wave.defeat.help"), "fr-muted"));
            var b = UiKit.Btn(UiText.Get("common.continue"), c, null, true, true);
            b.clicked += CloseWaveResult;
            b.style.marginTop = UiKit.Space.S32;
            panel.Add(b);
            overlayLayer.Add(panel);
            OnStateChanged();
        }

        void CloseWaveResult()
        {
            if (waveResultPanel != null) waveResultPanel.RemoveFromHierarchy();
            waveResultPanel = null;
            if (scrimLayer != null && modal == null) scrimLayer.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------ travel
        // Multi-leg travel is hosted in UIRoot.Travel.cs.

        // ------------------------------------------------------------ toast
        void Toast(string text, Color color)
        {
            var t = new FrontierToast { Text = text, Tint = color };
            UiKit.Halo(t, color, 24f, 0.25f);
            toastLayer.Add(t);
            t.schedule.Execute(() => t.RemoveFromHierarchy()).StartingIn(1800);
        }
    }
}
