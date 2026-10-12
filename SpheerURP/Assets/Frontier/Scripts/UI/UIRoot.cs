using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    /// <summary>
    /// Icon-driven, responsive UI Toolkit interface. Layout is authored for a 1080 logical-pixel short side
    /// and the panel scale is recomputed whenever the screen size changes.
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

        VisualElement root, safe, hud, waveHud, toastLayer, overlayLayer, flash, popupLayer, modalLayer, mapLayer, bannerLayer;
        VisualElement waveFill, reactorFill, xpFill;
        Label waveLabel, reactorLabel, fuelLabel, packLabel, levelLabel, planetLabel;
        VisualElement planetTag;

        bool busy;
        bool batching;
        Vector4 inset; // left, top, right, bottom in logical px
        int lastW, lastH;
        Rect lastSafe;

        public bool IsOverUi(Vector2 screenPos)
        {
            if (root == null || root.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
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
            var uss = Resources.Load<StyleSheet>("Frontier/Frontier");
            if (uss != null) root.styleSheets.Add(uss);

            safe = Layer(); safe.style.display = DisplayStyle.Flex;
            root.Add(safe);
            BuildHud();
            BuildWaveHud();

            popupLayer = Layer(); modalLayer = Layer(); bannerLayer = Layer(); mapLayer = Layer();
            toastLayer = UiKit.Div("fr-toasts"); toastLayer.pickingMode = PickingMode.Ignore;
            overlayLayer = Layer();
            flash = Layer();
            flash.style.backgroundColor = new Color(0.6f, 1f, 1f, 1f);
            flash.style.opacity = 0f;
            root.Add(popupLayer); root.Add(bannerLayer); root.Add(modalLayer); root.Add(mapLayer);
            root.Add(toastLayer); root.Add(overlayLayer); root.Add(flash);

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
            var sa = Screen.safeArea;
            if (!force && Screen.width == lastW && Screen.height == lastH && sa == lastSafe) return;
            lastW = Screen.width; lastH = Screen.height; lastSafe = sa;
            float scale = Mathf.Max(0.2f, Mathf.Min(Screen.width, Screen.height) / 1080f) * uiScale;
            if (panelSettings != null) panelSettings.scale = scale;
            inset = new Vector4(sa.xMin / scale, (Screen.height - sa.yMax) / scale, (Screen.width - sa.xMax) / scale, sa.yMin / scale);
            hud.style.left = inset.x; hud.style.top = inset.y;
            hud.style.right = inset.z; hud.style.bottom = inset.w;
            if (waveHud != null)
            {
                waveHud.style.left = inset.x; waveHud.style.right = inset.z; waveHud.style.top = inset.y; waveHud.style.bottom = inset.w;
                waveHud.style.paddingLeft = 40; waveHud.style.paddingRight = 40;
                waveHud.style.paddingTop = 150; waveHud.style.paddingBottom = 40;
            }
            if (mapOpen) LayoutMapChrome();
        }

        // ------------------------------------------------------------ HUD
        void BuildHud()
        {
            hud = Layer();
            safe.Add(hud);

            var top = UiKit.Div("fr-topbar"); top.pickingMode = PickingMode.Ignore;
            planetTag = UiKit.Div("fr-topbar-bar");
            planetTag.pickingMode = PickingMode.Ignore;
            fuelLabel = Chip(planetTag, "fuel", UiKit.Amber, null);
            packLabel = Chip(planetTag, "pack", UiKit.Cyan, null);
            levelLabel = Chip(planetTag, "star", UiKit.Magenta, f => xpFill = f);
            planetLabel = UiKit.Text("", "fr-planet-name");
            planetTag.Add(planetLabel);
            top.Add(planetTag);

            var map = Icons.IconButton("map", UiKit.Cyan, OpenMap, 0f, true);
            map.AddToClassList("fr-topbar-corner"); map.AddToClassList("fr-topbar-left");
            top.Add(map);
            var menu = Icons.IconButton("grid", UiKit.Cyan, OpenMenu, 0f, true);
            menu.AddToClassList("fr-topbar-corner"); menu.AddToClassList("fr-topbar-right");
            top.Add(menu);
            hud.Add(top);

            var research = Icons.IconButton("research", UiKit.Magenta, OpenResearch, 0f, true);
            research.AddToClassList("fr-corner"); research.AddToClassList("fr-corner-left");
            hud.Add(research);
            var shop = Icons.IconButton("build", UiKit.Lime, OpenShop, 0f, true);
            shop.AddToClassList("fr-corner"); shop.AddToClassList("fr-corner-right");
            hud.Add(shop);
        }

        Label Chip(VisualElement parent, string icon, Color color, Action<VisualElement> xp)
        {
            var chip = UiKit.Div("fr-chip"); chip.pickingMode = PickingMode.Ignore;
            Color c = UiKit.Ui(color);
            chip.Add(Icons.Make(icon, c));
            var l = UiKit.Text("0", "fr-chip-text");
            chip.Add(l);
            if (xp != null)
            {
                VisualElement fill;
                var bar = UiKit.Bar(color, out fill);
                bar.AddToClassList("fr-chip-xp");
                chip.Add(bar);
                xp(fill);
            }
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
            top.Add(UiKit.Text("Tap aliens to zap them", "fr-tiny"));
            waveHud.Add(top);

            var bottom = UiKit.Div(); bottom.pickingMode = PickingMode.Ignore;
            reactorLabel = UiKit.Text("", "fr-h2", UiKit.Lime);
            bottom.Add(reactorLabel);
            var rb = UiKit.Bar(UiKit.Lime, out reactorFill);
            rb.style.height = 24;
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
            planetLabel.text = gs.planet.displayName.ToUpper();
            planetLabel.style.color = UiKit.Ui(gs.planet.rim);
            UiKit.SetFill(xpFill, gs.LevelProgress);
            RefreshModal();
            RefreshPopup();
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

            if (planetTag != null)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 3f);
                Color c = UiKit.Ui(gs.planet.rim, 0.5f + 0.4f * pulse);
                UiKit.Border(planetTag, c, 2f);
            }

            UpdatePopupPosition();
            UpdateMapInput();
            SweepText();
        }

        void CloseAll()
        {
            ClosePopup();
            CloseModal();
            CloseMap();
            ExitBuild();
        }

        // ------------------------------------------------------------ helpers
        VisualElement Card(Color accent)
        {
            var c = UiKit.Div("fr-card");
            UiKit.Border(c, UiKit.Ui(accent, 0.55f), 2f);
            return c;
        }

        Button IconBtn(string text, string icon, Color color, Action onClick, bool big = false)
        {
            var b = UiKit.Btn(text, color, onClick, big);
            b.Insert(0, Icons.Make(icon, color, big ? 44f : 34f));
            return b;
        }

        VisualElement ResourceIconChip(ResourceSO r, string text, bool lack = false)
        {
            var chip = UiKit.Div("fr-itemchip");
            Color c = UiKit.Ui(r.color);
            UiKit.Border(chip, new Color(c.r, c.g, c.b, 0.8f), 2f);
            chip.Add(Icons.Make(Icons.ForResource(r), c));
            chip.Add(UiKit.Text(text, "fr-small", lack ? UiKit.Red : Color.white));
            return chip;
        }

        string StructureEffect(StructureSO d, int level)
        {
            int pct = Mathf.RoundToInt(d.bonusPerLevel * (level - 1) * 100f);
            if (d.kind.IsTurret()) return "dmg " + Mathf.RoundToInt(d.damage * (1f + d.bonusPerLevel * (level - 1)));
            if (d.kind == StructureKind.Refinery) return "+" + pct + "% output";
            if (d.kind.IsGather()) return "+" + pct + "% yield";
            return "+" + pct + "% rewards, XP";
        }

        string UpgradeEffectText(UpgradeSO u, int level)
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

        static string StructureIcon(StructureKind k)
        {
            switch (k)
            {
                case StructureKind.Refinery: return "refinery";
                case StructureKind.Lumber: return "wood";
                case StructureKind.Mine: return "iron";
                case StructureKind.Drill: return "tools";
                case StructureKind.Gunner: return "alien";
                case StructureKind.Laser: return "bolt";
                case StructureKind.Satellite: return "warp";
                default: return "alarm";
            }
        }

        // ------------------------------------------------------------ settings (UI scale, text scale, sound)
        float uiScale = 1f, textScale = 1f;
        float nextSweep;

        void LoadPrefs()
        {
            uiScale = Mathf.Clamp(PlayerPrefs.GetFloat("fr_uiscale", 1f), 0.7f, 1.5f);
            textScale = Mathf.Clamp(PlayerPrefs.GetFloat("fr_textscale", 1f), 0.8f, 1.6f);
            Sfx.Muted = PlayerPrefs.GetInt("fr_muted", 0) == 1;
        }

        void SetUiScale(float v) { uiScale = v; PlayerPrefs.SetFloat("fr_uiscale", v); ApplyLayout(true); }
        void SetTextScale(float v) { textScale = v; PlayerPrefs.SetFloat("fr_textscale", v); nextSweep = 0f; }

        // Labels keep their USS size in userData; a periodic sweep rescales them so new popups pick it up too.
        void SweepText()
        {
            if (Time.unscaledTime < nextSweep) return;
            nextSweep = Time.unscaledTime + 0.15f;
            ScaleLabels(root);
        }

        void ScaleLabels(VisualElement ve)
        {
            var l = ve as Label;
            if (l != null)
            {
                if (!(l.userData is float))
                {
                    float f = l.resolvedStyle.fontSize;
                    if (!(f > 0f)) { goto children; }
                    l.userData = f;
                }
                float want = (float)l.userData * textScale;
                if (Mathf.Abs(l.style.fontSize.value.value - want) > 0.01f || l.style.fontSize.keyword != StyleKeyword.Undefined)
                    l.style.fontSize = want;
            }
            children:
            foreach (var c in ve.Children()) ScaleLabels(c);
        }

        // ------------------------------------------------------------ gathering
        void StartGather(GatherSpot spot, Transform at, float bonus)
        {
            if (busy || defense.Active) return;
            busy = true;
            Vector3 fxPos = at != null ? at.position : Vector3.zero;
            Minigame g = spot.game == MinigameKind.Chop ? (Minigame)new ChopGame()
                       : spot.game == MinigameKind.Mine ? (Minigame)new MineGame() : new DrillGame();
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
            panel.pickingMode = PickingMode.Position;
            Color c = r.victory ? UiKit.Lime : UiKit.Red;
            panel.Add(UiKit.Text(r.victory ? "WAVE CLEARED" : "REACTOR OVERLOADED", "fr-title", c));
            if (r.victory)
            {
                panel.Add(UiKit.Text("Salvage recovered", "fr-muted"));
                panel.Add(UiKit.CostRow(r.granted, gs, false));
                if (defense.Current != null)
                {
                    var xp = UiKit.Div("fr-itemchip");
                    UiKit.Border(xp, UiKit.Ui(UiKit.Magenta, 0.8f), 2f);
                    xp.Add(Icons.Make("star", UiKit.Magenta));
                    xp.Add(UiKit.Text("+" + defense.Current.xp + " XP", "fr-small"));
                    panel.Add(xp);
                }
                if (r.lost.Count > 0)
                {
                    panel.Add(UiKit.Text("Backpack full - lost:", "fr-muted", UiKit.Amber));
                    panel.Add(UiKit.CostRow(r.lost, gs, false));
                }
            }
            else panel.Add(UiKit.Text("The aliens broke through. No salvage this time.", "fr-muted"));
            var b = UiKit.Btn("CONTINUE", c, null, true);
            b.clicked += () => panel.RemoveFromHierarchy();
            b.style.marginTop = 30;
            panel.Add(b);
            overlayLayer.Add(panel);
            OnStateChanged();
        }

        // ------------------------------------------------------------ travel
        void StartTravel(PlanetSO target)
        {
            if (busy || defense.Active || backdrop.Warping) return;
            if (!gs.Travel(target)) { Sfx.Bad(); return; }
            busy = true;
            CloseAll();
            StartCoroutine(backdrop.DoWarp(() =>
            {
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
            Toast("Arrived at " + gs.planet.displayName, UiKit.Cyan);
        }

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
