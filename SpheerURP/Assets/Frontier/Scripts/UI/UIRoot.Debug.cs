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
        public Minigame DebugActiveGather { get { return activeGatherMinigame; } }
        public void DebugSetServices(Frontier.Services.FrontierServices serviceSet) { services = serviceSet; }

        public void DebugRelayout() { ApplyLayout(true); }

        /// <summary>Closes everything and restores default tab/page state so each capture starts from a known screen.</summary>
        public void DebugReset()
        {
            CloseAll();
            CloseWaveResult();
            if (root != null) { var oldSheet = root.Q("icon-sheet"); if (oldSheet != null) oldSheet.RemoveFromHierarchy(); }
            if (overlayLayer != null) overlayLayer.Clear();
            if (travelLayer != null) travelLayer.Clear();
            if (toastLayer != null) toastLayer.Clear();
            busy = false;
            activeGatherMinigame = null;
            activeTravelMinigame = null;
            debugToolsOverride = null;
            debugFirstSessionGuideOverride = false;
            menuPage = ""; shopTab = 0; researchPage = ResearchPage.Tech; shipPage = ShipPage.Cargo; packSel = -1; confirmReset = false;
            selected = gs.planet;
            RefreshFirstSessionGuide();
        }

        public void DebugFirstSessionGuide(int step)
        {
            DebugReset();
            debugFirstSessionGuideOverride = true;
            debugFirstSessionGuideStep = step;
            RefreshFirstSessionGuide();
        }

        public void DebugFirstSessionGuideShop(int step)
        {
            DebugFirstSessionGuide(step);
            shopTab = step == 0 ? 1 : step == 6 ? 3 : 0;
            OpenShop();
        }

        public void DebugFirstSessionGuideAction(int step)
        {
            DebugFirstSessionGuide(step);
            RunFirstSessionGuideAction();
        }

        public void DebugShop(int tab) { DebugReset(); shopTab = tab; OpenShop(); }
        public void DebugResourceSource(ResourceSO resource) { DebugReset(); OpenResourceSource(resource); }
        public void DebugProject() { DebugReset(); OpenProjectPanel(); }
        public void DebugGameplayRewardRules(GameplayRewardRules rules) { gameplayRewardRules = rules; }
        public void DebugShortage(ResourceSO resource, int required)
        {
            OpenShortage(Project("upgrade", "pack", gs.Level(gs.db.FindUpgrade("pack")) + 1), resource, required);
        }
        public void DebugAtlas(int tab)
        {
            DebugReset();
            atlasTab = Mathf.Clamp(tab, 0, 4);
            OpenMenu();
            menuPage = "atlas";
            RefreshModal();
        }
        public void DebugResearch(int tab = 0)
        {
            DebugReset();
            researchBranch = "";
            OpenResearch(tab == 0 ? ResearchPage.Tech : ResearchPage.Improvements);
        }
        public void DebugShip(int tab) { DebugReset(); OpenShip((ShipPage)Mathf.Clamp(tab, 0, 2)); }
        public void DebugBuilders() { DebugReset(); OpenBuilders(); }
        public void DebugRewardedConsent() { DebugReset(); _ = RequestRewardedConsent(); }
        public void DebugNetwork() { DebugReset(); OpenNetwork(); }
        public void DebugFuelTanks() { DebugReset(); OpenFuelTanks(); }
        public void DebugNetworkFinale() { DebugReset(); PlayNetworkFinale(); }
        public void DebugPack(int select) { DebugReset(); packSel = select; OpenPack(); }
        public void DebugEmptyPackAction() { RunEmptyPackAction(); }
        public void DebugEmptyPack()
        {
            DebugReset();
            foreach (var resource in gs.db.resources) gs.backpack.Set(resource.id, 0);
            gs.Notify();
            packSel = -1;
            OpenPack();
        }

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

        public void DebugMap(PlanetData pick, float zoom = SystemZoom)
        {
            DebugReset();
            OpenMap();
            if (pick != null) selected = pick;
            mapZoom = zoom >= SystemZoomThreshold ? SystemZoom : zoom >= NeighborZoom ? NeighborZoom : MinZoom;
            if (mapView == null) return;
            BuildMapNodes();
            InvalidateMapPreview();
            BuildMapCard();
            CenterOn(pick != null ? pick : gs.planet);
            mapWorld.MarkDirtyRepaint();
        }

        public RoutePlan DebugMapPreview { get { return mapPreview; } }
        public int DebugMapSystemCount { get { return mapSnapshot == null ? 0 : mapSnapshot.systems.Length; } }
        public double DebugMapCenterX { get { return viewport.CenterX; } }
        public bool DebugMapInSystem { get { return SystemMapView; } }
        public bool DebugMapInNeighbors { get { return NeighborMapView; } }
        public void DebugMapPan(float x, float y) { viewport.Pan(x, y); ApplyMapTransform(); }
        public string DebugMapSelectedId { get { return selected == null ? null : selected.Id; } }
        public bool DebugMapProjectMatch(PlanetData planet) { return mapProjectWorlds.Contains(planet.Id); }
        public bool DebugMapSurveyed(PlanetData planet) { return Surveyed(planet.Star); }
        public double DebugMapSurveyRadius(PlanetData planet)
        {
            int index = surveyCenters.FindIndex(star => star.Id == planet.Star.Id);
            return index < 0 ? 0 : surveyRadii[index];
        }
        public float DebugMapOrbitRadius(PlanetData planet)
        {
            return PlanetOrbitAxes(planet).y;
        }
        public void DebugMapSearch(string text) { mapSearch.value = text; SearchMap(); }
        public void DebugMapAutopilot() { mapAutopilot = true; InvalidateMapPreview(); BuildMapCard(); }
        public void DebugStartTravel(PlanetData target, bool autopilot = false) { StartTravel(target, autopilot); }
        public void DebugAutopilotScreen()
        {
            DebugReset();
            Label progress;
            CreateAutopilotOverlay(out progress);
            progress.text = "Leg 1 / 3 · 12s";
        }

        public void DebugBuild(StructureSO def) { DebugReset(); EnterBuild(def); }
        public void DebugTapBuildSlot(int slot) { OnSlotTapped(slot, planetView.transform); }

        public void DebugToast(string text, Color color) { Toast(text, color); }

        public bool DebugGather(MinigameKind kind)
        {
            DebugReset();
            var spots = gs.SpotsFor(gs.planet, kind);
            if (spots.Count == 0) return false;
            StartGather(spots[0], null, 0f);
            return true;
        }

        public bool DebugPausedGather(MinigameKind kind, bool confirmAbandon = false)
        {
            if (!DebugGather(kind) || activeGatherMinigame == null) return false;
            activeGatherMinigame.Pause();
            if (confirmAbandon) activeGatherMinigame.RequestAbandon();
            return true;
        }

        public bool DebugGatherResult(MinigameKind kind)
        {
            if (!DebugGather(kind) || activeGatherMinigame == null) return false;
            activeGatherMinigame.DebugComplete(0.82f, 7, 10);
            return true;
        }

        public void DebugDrillPreview(bool overheated)
        {
            overlayLayer.schedule.Execute(() =>
            {
                var game = activeGatherMinigame as DrillGame;
                if (game == null || game.DebugRun == null)
                    throw new InvalidOperationException("Drill preview was not ready.");
                var control = overlayLayer.Q("drill-boost");
                foreach (var key in new[] { KeyCode.Space, KeyCode.LeftShift })
                    using (var e = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = key }))
                    { e.target = control; control.SendEvent(e); }
                game.DebugAdvance(overheated ? 3.65f : 0.8f, true, true);
            }).StartingIn(1500);
        }

        public void DebugHarvestPreview(MinigameKind kind)
        {
            overlayLayer.schedule.Execute(() =>
            {
                if (kind == MinigameKind.Excavate)
                {
                    var game = activeGatherMinigame as ExcavationGame;
                    if (game == null || game.DebugRun == null) throw new InvalidOperationException("Excavation preview was not ready.");
                    game.DebugPing((game.DebugRun.Target + 1) % ExcavationHarvestRun.CellCount);
                }
                else if (kind == MinigameKind.Salvage)
                {
                    var game = activeGatherMinigame as SalvageGame;
                    if (game == null) throw new InvalidOperationException("Salvage preview was not ready.");
                    game.DebugSelect(0);
                    var control = overlayLayer.Q("salvage-tether");
                    using (var e = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Space }))
                    { e.target = control; control.SendEvent(e); }
                    game.DebugAdvance(0.7f);
                }
                else if (kind == MinigameKind.Siphon)
                {
                    var game = activeGatherMinigame as SiphonGame;
                    if (game == null || game.DebugRun == null) throw new InvalidOperationException("Siphon preview was not ready.");
                    game.DebugPump(); game.DebugAdvance(0.3f); game.DebugPump();
                }
            }).StartingIn(1500);
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
            activeTravelMinigame = game;
            game.Run(overlayLayer, request, r => { activeTravelMinigame = null; busy = false; if (done != null) done(r); });
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

        public bool DebugTapObstacle(int pad)
        {
            DebugReset();
            return world.SimulateObstacleTap(pad);
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
            string caption = page < 3 ? titles[Mathf.Clamp(page, 0, 2)] :
                (page < 5 ? "ECONOMY RESOURCES" : "ECONOMY STRUCTURES") + "  24 / 32 / 48 / 64 PX";
            var title = UiKit.Text(caption, "fr-t-label");
            title.style.marginBottom = UiKit.Space.S12;
            sheet.Add(title);

            if (page == 2)
            {
                var grid = SheetGrid();
                foreach (var r in gs.db.resources)
                    if (r != null) grid.Add(IconCell(Icons.ForResource(r), r.LocalizedName, UiKit.Ui(r.color), new[] { 32f, 48f }, scale));
                foreach (var s in gs.db.structures)
                    if (s != null) grid.Add(IconCell(Icons.ForStructure(s.kind), s.LocalizedName, UiKit.Ui(s.color), new[] { 32f, 48f }, scale));
                sheet.Add(grid);
            }
            else if (page >= 3)
            {
                var names = Icons.Groups[page < 5 ? 2 : 3];
                int first = ((page - 3) % 2) * 18;
                var grid = SheetGrid();
                for (int i = first; i < Mathf.Min(first + 18, names.Length); i++)
                    grid.Add(IconCell(names[i], names[i], SheetInk, SheetPx, scale));
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
                    int count = 0;
                    foreach (var n in Icons.Groups[g])
                    {
                        if (count++ >= 10) break;
                        grid.Add(IconCell(n, n, SheetInk, SheetPx, scale));
                    }
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
