#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    /// <summary>Entry points for the screenshot harness and play-mode tests. Not compiled into release builds.</summary>
    public partial class UIRoot
    {
        public VisualElement DebugRoot { get { return root; } }
        public float DebugPanelScale { get { return panelSettings != null ? panelSettings.scale : 1f; } }
        public bool DebugBusy { get { return busy; } }

        public void DebugRelayout() { ApplyLayout(true); }

        /// <summary>Closes everything and restores default tab/page state so each capture starts from a known screen.</summary>
        public void DebugReset()
        {
            CloseAll();
            if (root != null) { var oldSheet = root.Q("icon-sheet"); if (oldSheet != null) oldSheet.RemoveFromHierarchy(); }
            if (overlayLayer != null) overlayLayer.Clear();
            if (travelLayer != null) travelLayer.Clear();
            if (toastLayer != null) toastLayer.Clear();
            busy = false;
            debugToolsOverride = null;
            menuPage = ""; shopTab = 0; researchTab = 0; toolTab = 0; packSel = -1; confirmReset = false;
            selected = gs.planet;
        }

        public void DebugShop(int tab) { DebugReset(); shopTab = tab; OpenShop(); }
        public void DebugResearch(int tab, int tool = 0) { DebugReset(); researchTab = tab; toolTab = tool; OpenResearch(); }
        public void DebugPack(int select) { DebugReset(); packSel = select; OpenPack(); }

        public void DebugMenu(string page, bool scrollToEnd = false)
        {
            DebugReset();
            OpenMenu();
            menuPage = page ?? "";
            RefreshModal();
            if (scrollToEnd && modalScroll != null)
            {
                var scroll = modalScroll;
                scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0f, scroll.contentContainer.layout.height)).StartingIn(60);
            }
        }

        /// <summary>Opens the menu as a release build would see it: no DEBUG tile, and the debug page cannot be reached.</summary>
        public void DebugMenuAsRelease()
        {
            DebugReset();
            debugToolsOverride = false;
            OpenMenu();
            menuPage = "debug";
            RefreshModal();
        }

        public void DebugMap(PlanetData pick, float zoom)
        {
            DebugReset();
            OpenMap();
            if (pick != null) selected = pick;
            mapZoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            if (mapView == null) return;
            BuildMapNodes();
            BuildMapCard();
            CenterOn(pick != null ? pick : gs.planet);
            mapWorld.MarkDirtyRepaint();
        }

        public void DebugBuild(StructureSO def) { DebugReset(); EnterBuild(def); }

        public void DebugToast(string text, Color color) { Toast(text, color); }

        public bool DebugGather(MinigameKind kind)
        {
            DebugReset();
            var spots = gs.SpotsFor(gs.planet, kind);
            if (spots.Count == 0) return false;
            StartGather(spots[0], null, 0f);
            return true;
        }

        public void DebugTravelGame()
        {
            DebugReset();
            busy = true;
            new TravelGame().Run(overlayLayer, UiKit.Magenta, 1f, 0.5f, r => { busy = false; });
        }

        /// <summary>Starts any travel mini-game exactly as the travel flow does (full-bleed host, safe-area insets) for the harness and tests. Returns the request that was used.</summary>
        public TravelRunRequest DebugRunTravel(ITravelMinigame game, TravelRunRequest request, Action<TravelRunResult> done)
        {
            DebugReset();
            busy = true;
            request.fullscreenHost = travelLayer;
            request.safeInsets = inset;
            game.Run(overlayLayer, request, r => { busy = false; if (done != null) done(r); });
            return request;
        }

        /// <summary>A request for a leg from the current world to <paramref name="to"/> as the travel flow would build it (deterministic seed).</summary>
        public TravelRunRequest DebugMakeTravelRequest(PlanetData to, ulong seed, double distanceLy)
        {
            return new TravelRunRequest
            {
                seed = seed, distanceLy = distanceLy, danger = to.Danger, tier = to.Tier, legIndex = 0, legCount = 1,
                from = gs.planet, to = to, fuelCost = gs.FuelCost(to), fullscreenHost = travelLayer, safeInsets = inset
            };
        }

        public void DebugTapStructure(PlacedStructure s)
        {
            DebugReset();
            world.SimulateTap(s);
        }

        // ------------------------------------------------------------ icon contact sheets
        static readonly Color SheetInk = new Color(0.92f, 0.96f, 1f);
        static readonly float[] SheetPx = { 24f, 32f, 48f, 64f };

        /// <summary>
        /// Contact sheet of every icon at 24, 32, 48 and 64 physical pixels on a dark surface, so legibility and consistency
        /// are judged on real pixels. Page 0: UI glyphs. Page 1: resources and structures. Page 2: items in their game colours.
        /// </summary>
        public void DebugIconSheet(int page)
        {
            DebugReset();
            float scale = Mathf.Max(0.1f, DebugPanelScale);
            var sheet = UiKit.Div();
            sheet.pickingMode = PickingMode.Ignore;
            sheet.style.position = Position.Absolute;
            sheet.style.left = 0; sheet.style.top = 0; sheet.style.right = 0; sheet.style.bottom = 0;
            sheet.style.backgroundColor = new Color(0.03f, 0.04f, 0.1f, 1f);
            sheet.style.paddingLeft = inset.x + UiKit.Space.S16; sheet.style.paddingRight = inset.z + UiKit.Space.S16;
            sheet.style.paddingTop = inset.y + UiKit.Space.S16; sheet.style.paddingBottom = inset.w + UiKit.Space.S16;

            string[] titles = { "UI ICONS  24 / 32 / 48 / 64 PX", "ITEM ICONS  24 / 32 / 48 / 64 PX", "ITEM ICONS IN GAME COLOURS" };
            var title = UiKit.Text(titles[Mathf.Clamp(page, 0, 2)], "fr-t-label");
            title.style.marginBottom = UiKit.Space.S12;
            sheet.Add(title);

            if (page == 2)
            {
                var grid = SheetGrid();
                foreach (var r in gs.db.resources)
                    if (r != null) grid.Add(IconCell(Icons.ForResource(r), r.displayName, UiKit.Ui(r.color), new[] { 32f, 48f }, scale));
                foreach (var s in gs.db.structures)
                    if (s != null) grid.Add(IconCell(Icons.ForStructure(s.kind), s.displayName, UiKit.Ui(s.color), new[] { 32f, 48f }, scale));
                sheet.Add(grid);
            }
            else
            {
                string[] names = { "dock, controls", "hud, glyphs", "resources", "structures" };
                for (int g = page == 0 ? 0 : 2; g < (page == 0 ? 2 : 4); g++)
                {
                    var label = UiKit.Text(names[g].ToUpper(), "fr-popup-section");
                    label.style.marginTop = UiKit.Space.S8; label.style.marginBottom = UiKit.Space.S4;
                    sheet.Add(label);
                    var grid = SheetGrid();
                    foreach (var n in Icons.Groups[g]) grid.Add(IconCell(n, n, SheetInk, SheetPx, scale));
                    sheet.Add(grid);
                }
            }
            overlayLayer.Clear();
            sheet.name = "icon-sheet";
            root.Add(sheet);
        }

        static VisualElement SheetGrid()
        {
            var grid = UiKit.Div();
            grid.pickingMode = PickingMode.Ignore;
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.SpaceBetween;
            return grid;
        }

        static VisualElement IconCell(string icon, string label, Color tint, float[] pixelSizes, float scale)
        {
            var cell = UiKit.Div();
            cell.pickingMode = PickingMode.Ignore;
            cell.style.width = Length.Percent(32.3f);
            cell.style.marginBottom = UiKit.Space.S8;
            cell.style.paddingLeft = UiKit.Space.S8; cell.style.paddingRight = UiKit.Space.S8;
            cell.style.paddingTop = UiKit.Space.S4; cell.style.paddingBottom = UiKit.Space.S8;
            cell.style.backgroundColor = new Color(0.07f, 0.1f, 0.22f, 1f);
            UiKit.Radius(cell, UiKit.Space.S12);
            cell.Add(UiKit.Text(label, "fr-tiny"));
            var row = UiKit.Div();
            row.pickingMode = PickingMode.Ignore;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexEnd;
            foreach (float px in pixelSizes)
            {
                var ic = Icons.Make(icon, tint, px / scale);
                ic.style.marginRight = UiKit.Space.S12;
                row.Add(ic);
            }
            cell.Add(row);
            return cell;
        }
    }
}
#endif
