using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Frontier.Economy;
using Frontier.Universe;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        const float MinZoom = 0.015f, MaxZoom = 64f;
        const float SystemZoom = 12f, SystemZoomThreshold = 8f;
        PlanetData selected;
        bool mapOpen, mapAutopilot;
        VisualElement mapView, mapWorld, mapCard, mapTools;
        Label mapReadout, mapNotice;
        Button mapViewMode;
        TextField mapSearch;
        DropdownField mapFilter;
        readonly MapViewport viewport = new MapViewport();
        float mapZoom = 2f;
        int dragPointer = -1;
        Vector2 dragStart, dragLast;
        bool mapDragging, suppressMapClick;
        float lastPinch, mapQueryAt;
        int mapVersion, queryVersion, previewVersion, runningPreviewVersion;
        MapSnapshot mapSnapshot;
        MapNameIndex mapNameIndex;
        string mapNextStopPlanetId;
        Task<MapSnapshot> mapQuery;
        Task<MapPreviewResult> mapPreviewTask;
        RoutePlan mapPreview;
        readonly List<StarSystem> surveyCenters = new List<StarSystem>();
        readonly List<double> surveyRadii = new List<double>();
        readonly List<PlanetData> mapPlanets = new List<PlanetData>();
        readonly List<PlanetData> mapPinPlanets = new List<PlanetData>();
        readonly List<Label> mapLabels = new List<Label>();
        readonly List<Button> mapPlanetButtons = new List<Button>();
        readonly List<Rect> mapLabelBounds = new List<Rect>();
        string mapResource = "";
        double mapJumpRange;
        readonly List<string> mapResourceIds = new List<string>();
        const string ProjectMapFilter = "@project";
        readonly HashSet<string> mapProjectResources = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> mapProjectWorlds = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> mapProjectSystems = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> mapRememberedSystems = new HashSet<string>(StringComparer.Ordinal);

        struct MapPreviewResult
        {
            public RoutePlan plan;
            public string nextStopPlanetId;
        }

        void OpenMap()
        {
            if (defense.Active || busy) return;
            if (mapOpen) { CloseMap(); return; }
            ClosePopup(); CloseModal(); ExitBuild();
            mapOpen = true; selected = gs.planet; mapAutopilot = false; mapResource = ""; mapZoom = SystemZoom;
            RefreshMapSurvey();
            mapNameIndex = MapNameIndex.Build(gs.universe.Universe, KnownMapSystems(), gs.MapPins);
            mapView = UiKit.Div("fr-map");
            mapLayer.Add(mapView);
            mapWorld = UiKit.Div("fr-map-viewport");
            mapWorld.name = "map-viewport";
            mapWorld.generateVisualContent += DrawMapBackground;
            mapView.Add(mapWorld);
            mapWorld.RegisterCallback<GeometryChangedEvent>(e => ApplyMapTransform());
            mapWorld.RegisterCallback<PointerDownEvent>(OnMapDown);
            mapWorld.RegisterCallback<PointerMoveEvent>(OnMapMove);
            mapWorld.RegisterCallback<PointerUpEvent>(OnMapUp);
            mapWorld.RegisterCallback<PointerCancelEvent>(e => ResetMapPointer());
            mapWorld.RegisterCallback<PointerCaptureOutEvent>(e => { dragPointer = -1; mapDragging = false; });
            mapWorld.RegisterCallback<ClickEvent>(e => PickMap(e.localPosition));
            mapWorld.RegisterCallback<WheelEvent>(e => { ZoomAt(e.localMousePosition, Mathf.Exp(-e.delta.y * 0.08f)); e.StopPropagation(); });

            mapTools = UiKit.Div("fr-map-tools");
            mapTools.Add(Icons.IconButton("close", UiKit.Muted, CloseMap));
            mapTools.Add(Icons.IconButton("plus", UiKit.Muted, () => ZoomAt(ViewCenter(), 1.4f)));
            mapTools.Add(Icons.IconButton("minus", UiKit.Muted, () => ZoomAt(ViewCenter(), 1f / 1.4f)));
            var home = Icons.IconButton("map", UiKit.Cyan, () => { mapZoom = SystemZoom; SelectMapWorld(gs.planet); CenterOn(gs.planet); });
            home.tooltip = UiText.Get("map.recenter"); mapTools.Add(home);
            mapView.Add(mapTools);
            mapViewMode = UiKit.Btn("", UiKit.Cyan, () =>
            {
                mapZoom = SystemMapView ? 2f : SystemZoom;
                CenterOn(selected);
            });
            mapViewMode.name = "map-switch-view";
            mapViewMode.style.position = Position.Absolute;
            mapViewMode.style.left = 16 + inset.x;
            mapViewMode.style.top = 16 + inset.y;
            mapViewMode.style.width = 200;
            mapView.Add(mapViewMode);
            RefreshMapProjectResources();
            BuildMapSearch();
            var scanLegend = UiKit.Text(UiText.Get("map.scanner.legend"), "fr-tiny", UiKit.Cyan);
            scanLegend.name = "map-scanner-legend";
            scanLegend.tooltip = UiText.Get("map.scanner.help");
            scanLegend.pickingMode = PickingMode.Ignore;
            scanLegend.style.position = Position.Absolute;
            scanLegend.style.left = UiKit.Space.S16;
            scanLegend.style.top = UiKit.Space.S8;
            mapWorld.Add(scanLegend);
            RefreshMapPinPlanets();
            BuildMapNodes();
            InvalidateMapPreview();
            BuildMapCard();
            LayoutMapChrome();
            viewport.Center(gs.planet.Star.X, gs.planet.Star.Y);
            ApplyMapTransform();
        }

        void OpenMapForResource(string resourceId)
        {
            OpenMap();
            if (!mapOpen || mapFilter == null) return;
            int index = mapResourceIds.IndexOf(resourceId);
            if (index < 1) return;
            mapFilter.index = index;
            mapResource = resourceId;
            mapVersion++;
            mapQueryAt = 0;
        }

        void CloseMap()
        {
            ResetMapPointer();
            if (mapView != null) mapView.RemoveFromHierarchy();
            mapView = null; mapWorld = null; mapCard = null; mapTools = null;
            mapOpen = false; lastPinch = 0f; mapSnapshot = null;
            mapNameIndex = null;
            mapViewMode = null;
            mapFilter = null;
            mapVersion++; previewVersion++;
            mapLabels.Clear(); mapPlanets.Clear();
            mapPlanetButtons.Clear();
            mapPinPlanets.Clear();
            // Workers never touch UI/GameObjects. Keep their handles until completion so reopening cannot fan out new workers.
        }

        void LayoutMapChrome()
        {
            if (mapTools == null) return;
            mapTools.style.right = UiKit.Space.S16 + inset.z;
            mapTools.style.top = UiKit.Space.S16 + inset.y;
            mapTools.style.left = UiKit.Space.S16 + inset.x;
            if (mapCard != null) mapCard.style.marginBottom = UiKit.Space.S16 + inset.w;
            if (mapWorld != null)
            {
                mapWorld.style.top = (mapNotice != null && !string.IsNullOrEmpty(mapNotice.text) ? 390 : 336) + inset.y;
                float cardHeight = mapCard != null ? mapCard.layout.height : 0;
                mapWorld.style.bottom = (float.IsNaN(cardHeight) ? 0 : cardHeight) + 32 + inset.w;
            }
        }

        Vector2 ViewCenter() { return new Vector2((float)viewport.Width * 0.5f, (float)viewport.Height * 0.5f); }
        bool SystemMapView { get { return viewport.Scale >= SystemZoomThreshold; } }
        Vector2 Project(double x, double y) { MapPoint p = viewport.Project(x, y); return new Vector2((float)p.x, (float)p.y); }

        void CenterOn(PlanetData p)
        {
            if (mapView == null || p == null) return;
            viewport.Center(p.Star.X, p.Star.Y);
            ApplyMapTransform();
        }

        void ZoomAt(Vector2 at, float factor)
        {
            viewport.ZoomAt(at.x, at.y, factor);
            mapZoom = (float)viewport.Scale;
            ApplyMapTransform();
        }

        void ApplyMapTransform()
        {
            if (mapWorld == null) return;
            float w = mapWorld.layout.width, h = mapWorld.layout.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0) return;
            viewport.Resize(w, h);
            viewport.ZoomAt(w * 0.5, h * 0.5, mapZoom / viewport.Scale);
            mapVersion++;
            mapQueryAt = Time.unscaledTime + 0.12f;
            if (mapViewMode != null)
                mapViewMode.text = UiText.Get(SystemMapView ? "map.mode.universe" : "map.mode.orbits");
            mapWorld.Q("map-scanner-legend").style.display = SystemMapView ? DisplayStyle.None : DisplayStyle.Flex;
            PositionMapLabels();
            mapWorld.MarkDirtyRepaint();
        }

        void OnMapDown(PointerDownEvent e)
        {
            if (dragPointer >= 0 || e.button != 0) return;
            dragPointer = e.pointerId; dragStart = dragLast = e.position;
            mapDragging = false; suppressMapClick = false;
        }

        void OnMapMove(PointerMoveEvent e)
        {
            if (e.pointerId != dragPointer) return;
            if (Input.touchCount >= 2) { suppressMapClick = true; dragLast = e.position; return; }
            Vector2 pos = e.position;
            if (!mapDragging && (pos - dragStart).magnitude > 12)
            {
                mapDragging = true; suppressMapClick = true; mapWorld.CapturePointer(e.pointerId);
            }
            if (mapDragging) { viewport.Pan(pos.x - dragLast.x, pos.y - dragLast.y); ApplyMapTransform(); }
            dragLast = pos;
        }

        void OnMapUp(PointerUpEvent e) { if (e.pointerId == dragPointer) ResetMapPointer(); }
        void ResetMapPointer()
        {
            if (mapWorld != null && dragPointer >= 0 && mapWorld.HasPointerCapture(dragPointer)) mapWorld.ReleasePointer(dragPointer);
            dragPointer = -1; mapDragging = false;
        }

        void UpdateMapInput()
        {
            if (!mapOpen || mapWorld == null) return;
            PollMapWorkers();
            if (mapPreview != null && selected != null && selected.Id != gs.planet.Id) mapWorld.MarkDirtyRepaint();
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0).position; var b = Input.GetTouch(1).position;
                float d = Vector2.Distance(a, b);
                suppressMapClick = true;
                if (lastPinch > 1 && d > 1)
                {
                    Vector2 mid = (a + b) * 0.5f;
                    Vector2 pp = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(mid.x, ScreenMetrics.Height - mid.y));
                    ZoomAt(mapWorld.WorldToLocal(pp), d / lastPinch);
                }
                lastPinch = d;
            }
            else lastPinch = 0;
        }

        void RefreshMapSurvey()
        {
            surveyCenters.Clear();
            surveyRadii.Clear();
            mapRememberedSystems.Clear();
            mapRememberedSystems.UnionWith(gs.MapDiscoveryIds());
            foreach (var id in gs.MapPins)
            {
                var pinned = gs.universe.Get(id);
                if (pinned != null) mapRememberedSystems.Add(pinned.Star.Id);
            }
            var centerIndices = new Dictionary<SystemKey, int>();
            foreach (var record in gs.world.All)
            {
                if (record.visits <= 0 || !PlanetKey.TryParse(record.planetId, out var key) ||
                    !gs.universe.Universe.TryGetSystem(key.System, out var star)) continue;
                var visited = gs.universe.Get(record.planetId);
                mapRememberedSystems.Add(star.Id);
                double radius = gs.ScannerRangeLy + gs.RadarRangeBonus(visited);
                if (!centerIndices.TryGetValue(key.System, out int index))
                {
                    centerIndices.Add(key.System, surveyCenters.Count);
                    surveyCenters.Add(star);
                    surveyRadii.Add(radius);
                }
                else surveyRadii[index] = Math.Max(surveyRadii[index], radius);
            }
            if (!centerIndices.TryGetValue(gs.planet.Star.Key, out int currentIndex))
            {
                surveyCenters.Add(gs.planet.Star);
                surveyRadii.Add(gs.ScannerRangeLy + gs.RadarRangeBonus(gs.planet));
            }
            else surveyRadii[currentIndex] = Math.Max(surveyRadii[currentIndex], gs.ScannerRangeLy + gs.RadarRangeBonus(gs.planet));
            mapRememberedSystems.Add(gs.planet.Star.Id);
        }

        string[] KnownMapSystems()
        {
            var ids = new string[mapRememberedSystems.Count];
            mapRememberedSystems.CopyTo(ids);
            return ids;
        }

        bool Surveyed(StarSystem star)
        {
            if (mapRememberedSystems.Contains(star.Id)) return true;
            for (int i = 0; i < surveyCenters.Count; i++)
            {
                var center = surveyCenters[i];
                double dx = star.X - center.X, dy = star.Y - center.Y;
                if (dx * dx + dy * dy <= surveyRadii[i] * surveyRadii[i]) return true;
            }
            return false;
        }

        void PickMap(Vector2 at)
        {
            if (suppressMapClick) { suppressMapClick = false; return; }
            if (SystemMapView)
            {
                PlanetData nearest = null;
                float nearestDistance = 52;
                foreach (var planet in mapPlanets)
                {
                    float d = Vector2.Distance(PlanetPoint(planet), at);
                    if (d < nearestDistance) { nearest = planet; nearestDistance = d; }
                }
                if (nearest != null) SelectMapWorld(nearest);
                return;
            }
            if (mapSnapshot == null) return;
            StarSystem best = null;
            float distance = 30;
            foreach (var star in mapSnapshot.systems)
            {
                float d = Vector2.Distance(Project(star.X, star.Y), at);
                if (d < distance) { best = star; distance = d; }
            }
            if (best == null) return;
            var worlds = gs.universe.PlanetsOf(best);
            if (worlds.Count == 0) return;
            SelectMapWorld(worlds[0]);
            mapZoom = SystemZoom;
            CenterOn(selected);
        }

        void SelectMapWorld(PlanetData planet)
        {
            if (planet == null || !Surveyed(planet.Star))
            {
                SetMapNotice(UiText.Get("map.unscanned"));
                return;
            }
            bool otherSystem = selected == null || selected.Star.Key != planet.Star.Key;
            selected = planet;
            BuildMapNodes(); InvalidateMapPreview(); BuildMapCard();
            if (otherSystem || !SystemMapView) { mapZoom = SystemZoom; CenterOn(planet); }
            mapWorld.MarkDirtyRepaint();
        }

        void InvalidateMapPreview()
        {
            mapPreview = null; mapNextStopPlanetId = null; previewVersion++;
            var caps = gs.Ship;
            mapJumpRange = gs.Rules.JumpRangeLy(caps);
        }

        void BuildMapNodes()
        {
            foreach (var button in mapPlanetButtons) button.RemoveFromHierarchy();
            mapPlanetButtons.Clear();
            mapPlanets.Clear();
            if (selected != null && Surveyed(selected.Star)) mapPlanets.AddRange(gs.universe.PlanetsOf(selected.Star));
            foreach (var planet in mapPlanets)
            {
                var captured = planet;
                var button = new Button(() => SelectMapWorld(captured));
                button.name = "map-planet-" + planet.Id;
                button.tooltip = planet.Name;
                button.AddToClassList("fr-map-planet");
                button.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                mapPlanetButtons.Add(button);
                mapWorld.Add(button);
            }
            RefreshMapProjectMatches();
            PositionMapLabels();
        }

        void RefreshMapProjectResources()
        {
            mapProjectResources.Clear();
            var project = gs.TrackedProject;
            if (project != null)
            {
                var status = gs.ProjectState(project);
                var totals = new Dictionary<ResourceSO, long>();
                foreach (var cost in status.costs)
                {
                    if (cost.resource == null) continue;
                    totals.TryGetValue(cost.resource, out long amount);
                    totals[cost.resource] = amount + cost.amount;
                }
                foreach (var cost in totals)
                    if (gs.ProjectResourceCount(project, cost.Key) < cost.Value)
                        mapProjectResources.Add(cost.Key.id);
            }
            RefreshMapProjectMatches();
        }

        void RefreshMapProjectMatches()
        {
            mapProjectWorlds.Clear();
            mapProjectSystems.Clear();
            void MatchWorld(PlanetData planet)
            {
                if (!Surveyed(planet.Star)) return;
                foreach (var deposit in planet.Descriptor.Deposits)
                    if (mapProjectResources.Contains(deposit.ResourceId))
                    {
                        mapProjectWorlds.Add(planet.Id);
                        mapProjectSystems.Add(planet.Star.Id);
                        break;
                    }
            }
            foreach (var planet in mapPlanets) MatchWorld(planet);
            if (mapSnapshot != null)
                foreach (var star in mapSnapshot.systems)
                    if (Surveyed(star))
                        foreach (var planet in gs.universe.PlanetsOf(star)) MatchWorld(planet);
            for (int i = 0; i < mapPlanetButtons.Count; i++)
                mapPlanetButtons[i].EnableInClassList("fr-map-project-world",
                    mapResource == ProjectMapFilter && mapProjectWorlds.Contains(mapPlanets[i].Id));
        }

        void RefreshMapPinPlanets()
        {
            mapPinPlanets.Clear();
            foreach (string id in gs.MapPins)
            {
                PlanetData pinned = gs.universe.Get(id);
                if (pinned != null) mapPinPlanets.Add(pinned);
            }
        }

        Vector2 PlanetPoint(PlanetData planet)
        {
            Vector2 center = Project(planet.Star.X, planet.Star.Y);
            Vector2 axes = PlanetOrbitAxes(planet);
            return center + new Vector2(Mathf.Cos(planet.Descriptor.OrbitPhase) * axes.x, -Mathf.Sin(planet.Descriptor.OrbitPhase) * axes.y);
        }

        Vector2 PlanetOrbitAxes(PlanetData planet)
        {
            int index = mapPlanets.FindIndex(p => p.Id == planet.Id);
            // The chart is deliberately schematic: evenly spaced lanes keep inner planets readable.
            float fraction = (index + 1f) / Mathf.Max(1, mapPlanets.Count);
            float outerX = Mathf.Max(80, (float)viewport.Width * 0.5f - 100);
            float outerY = Mathf.Max(64, (float)viewport.Height * 0.5f - 72);
            return new Vector2(Mathf.Lerp(64, outerX, fraction), Mathf.Lerp(48, outerY, fraction));
        }

        void PositionMapLabels()
        {
            if (mapWorld == null) return;
            int used = 0;
            mapLabelBounds.Clear();
            for (int i = 0; i < mapPlanetButtons.Count; i++)
            {
                var button = mapPlanetButtons[i];
                Vector2 at = PlanetPoint(mapPlanets[i]);
                button.style.display = SystemMapView ? DisplayStyle.Flex : DisplayStyle.None;
                button.style.left = at.x - 52;
                button.style.top = at.y - 52;
            }
            if (SystemMapView)
            {
                if (selected != null) PlacePlanetLabel(ref used, selected);
                if (selected != null && selected.Id != gs.planet.Id && selected.Star.Key == gs.planet.Star.Key)
                    PlacePlanetLabel(ref used, gs.planet);
                foreach (var planet in mapPlanets)
                {
                    if (planet.Id == selected.Id || planet.Id == gs.planet.Id) continue;
                    PlacePlanetLabel(ref used, planet);
                }
            }
            else if (mapSnapshot != null && mapSnapshot.densityOnly)
            {
                foreach (var galaxy in mapSnapshot.galaxies)
                {
                    if (used >= 16) break;
                    PlaceMapLabel(ref used, galaxy.Name, Project(galaxy.CenterX, galaxy.CenterY) + new Vector2(0, 16), 240, UiKit.Cyan);
                }
            }
            else if (mapSnapshot != null && viewport.Scale >= 0.5)
            {
                foreach (var star in mapSnapshot.systems)
                {
                    if (used >= 45) break;
                    Vector2 p = Project(star.X, star.Y);
                    if (p.x < 0 || p.y < 0 || p.x > viewport.Width || p.y > viewport.Height) continue;
                    if (!Surveyed(star) && star.Key != selected.Star.Key) continue;
                    PlaceMapLabel(ref used, star.Name, p + new Vector2(10, 6), 180, star.Key == gs.planet.Star.Key ? UiKit.Cyan : UiKit.Muted);
                }
            }
            for (int i = used; i < mapLabels.Count; i++) mapLabels[i].style.display = DisplayStyle.None;
        }

        void PlacePlanetLabel(ref int used, PlanetData planet)
        {
            Vector2 at = PlanetPoint(planet);
            string name = planet.Name;
            int before = used;
            PlaceMapLabel(ref used, name, at + new Vector2(-90, 30), 180, planet.Id == gs.planet.Id ? UiKit.Cyan : Color.white);
            if (used == before)
                PlaceMapLabel(ref used, name, at + new Vector2(-90, -70), 180, planet.Id == gs.planet.Id ? UiKit.Cyan : Color.white);
        }

        void PlaceMapLabel(ref int used, string text, Vector2 at, float width, Color color)
        {
            var bounds = new Rect(at.x, at.y, width, 40);
            if (bounds.xMin < 0 || bounds.yMin < 0 || bounds.xMax > viewport.Width || bounds.yMax > viewport.Height) return;
            foreach (var previous in mapLabelBounds) if (previous.Overlaps(bounds)) return;
            mapLabelBounds.Add(bounds);
            if (used == mapLabels.Count)
            {
                var label = UiKit.Text("", "fr-tiny");
                label.AddToClassList("fr-map-label");
                label.pickingMode = PickingMode.Ignore; label.style.position = Position.Absolute;
                mapLabels.Add(label); mapWorld.Add(label);
            }
            var l = mapLabels[used++];
            l.text = text; l.style.color = color; l.style.left = at.x; l.style.top = at.y; l.style.width = width; l.style.display = DisplayStyle.Flex;
        }

        void DrawMapBackground(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            if (!SystemMapView) DrawScannerCoverage(p);
            DrawPlanetLinks(p);
            if (mapSnapshot != null && !SystemMapView)
            {
                foreach (var star in mapSnapshot.systems)
                {
                    Vector2 at = Project(star.X, star.Y);
                    if (at.x < -10 || at.y < -10 || at.x > viewport.Width + 10 || at.y > viewport.Height + 10) continue;
                    bool surveyed = Surveyed(star), current = star.Key == gs.planet.Star.Key;
                    Color color = current ? UiKit.Cyan : surveyed ? UiKit.Amber : new Color(0.35f, 0.45f, 0.62f);
                    MapDisc(p, at, current ? 12 : star.IsAuthored ? 9 : 6, color);
                    p.strokeColor = new Color(0.5f, 0.65f, 0.85f, 0.45f); p.lineWidth = 2;
                    p.BeginPath(); p.Arc(at, current ? 18 : star.IsAuthored ? 15 : 11, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
                    if (mapResource == ProjectMapFilter && mapProjectSystems.Contains(star.Id))
                        DrawProjectMarker(p, at, current ? 25 : 21);
                }
            }
            Vector2 here = Project(gs.planet.Star.X, gs.planet.Star.Y);
            p.strokeColor = new Color(0.2f, 0.9f, 1, 0.22f); p.lineWidth = 1;
            float range = (float)(mapJumpRange * viewport.Scale);
            if (!SystemMapView && range > 0 && range < 12000)
            {
                p.BeginPath(); p.Arc(here, range, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            }
            if (SystemMapView && selected != null)
            {
                Vector2 center = Project(selected.Star.X, selected.Star.Y);
                foreach (var planet in mapPlanets)
                {
                    Vector2 axes = PlanetOrbitAxes(planet);
                    p.strokeColor = planet.Id == selected.Id ? new Color(0.5f, 0.85f, 1f, 0.9f) : new Color(0.4f, 0.56f, 0.78f, 0.6f);
                    p.lineWidth = planet.Id == selected.Id ? 3 : 2;
                    p.BeginPath();
                    for (int i = 0; i <= 80; i++)
                    {
                        float angle = i * Mathf.PI * 2 / 80;
                        Vector2 at = center + new Vector2(Mathf.Cos(angle) * axes.x, Mathf.Sin(angle) * axes.y);
                        if (i == 0) p.MoveTo(at); else p.LineTo(at);
                    }
                    p.ClosePath(); p.Stroke();
                }
                MapDisc(p, center, 25, new Color(0.2f, 0.12f, 0.23f));
                MapDisc(p, center, 21, UiKit.Amber);
                MapDisc(p, center + new Vector2(-6, -7), 7, new Color(1, 0.93f, 0.65f));
                foreach (var planet in mapPlanets) DrawMapPlanet(p, planet);
            }
            foreach (var pinned in mapPinPlanets)
            {
                if (SystemMapView && pinned.Star.Key != selected.Star.Key) continue;
                Vector2 at = SystemMapView ? PlanetPoint(pinned) : Project(pinned.Star.X, pinned.Star.Y);
                if (at.x < -12 || at.y < -12 || at.x > viewport.Width + 12 || at.y > viewport.Height + 12) continue;
                p.strokeColor = UiKit.Magenta;
                p.lineWidth = 2f;
                p.BeginPath(); p.Arc(at, SystemMapView ? 35f : 22f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            }
            DrawMapRoute(p);
        }

        static void MapDisc(Painter2D p, Vector2 at, float radius, Color color)
        {
            p.fillColor = color;
            p.BeginPath(); p.Arc(at, radius, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
        }

        static void DrawProjectMarker(Painter2D p, Vector2 at, float radius)
        {
            p.strokeColor = UiKit.Lime;
            p.lineWidth = 3;
            p.BeginPath();
            p.MoveTo(at + Vector2.up * radius);
            p.LineTo(at + Vector2.right * radius);
            p.LineTo(at + Vector2.down * radius);
            p.LineTo(at + Vector2.left * radius);
            p.ClosePath(); p.Stroke();
        }

        void DrawScannerCoverage(Painter2D p)
        {
            p.strokeColor = new Color(0.15f, 0.9f, 1f, 0.8f);
            p.lineWidth = 2;
            for (int i = 0; i < surveyCenters.Count; i++)
            {
                var center = Project(surveyCenters[i].X, surveyCenters[i].Y);
                float radius = (float)(surveyRadii[i] * viewport.Scale);
                if (radius <= 0 || radius > 12000 || center.x + radius < 0 || center.y + radius < 0 ||
                    center.x - radius > viewport.Width || center.y - radius > viewport.Height) continue;
                for (int angle = 0; angle < 360; angle += 12)
                {
                    p.BeginPath();
                    p.Arc(center, radius, Angle.Degrees(angle), Angle.Degrees(angle + 7));
                    p.Stroke();
                }
            }
        }

        void DrawPlanetLinks(Painter2D p)
        {
            p.strokeColor = UiKit.Lime;
            p.lineWidth = 3;
            foreach (var link in gs.PlanetLinks)
            {
                var from = gs.universe.Get(link.fromPlanetId);
                var to = gs.universe.Get(link.toPlanetId);
                if (from == null || to == null) continue;
                Vector2 a, b;
                if (SystemMapView)
                {
                    if (selected == null || from.Star.Key != selected.Star.Key || to.Star.Key != selected.Star.Key) continue;
                    a = PlanetPoint(from); b = PlanetPoint(to);
                }
                else
                {
                    if (from.Star.Key == to.Star.Key) continue;
                    a = Project(from.Star.X, from.Star.Y); b = Project(to.Star.X, to.Star.Y);
                }
                p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
            }
        }

        void DrawMapPlanet(Painter2D p, PlanetData planet)
        {
            Vector2 at = PlanetPoint(planet);
            float radius = planet.Id == selected.Id ? 24 : 19;
            if (mapResource == ProjectMapFilter && mapProjectWorlds.Contains(planet.Id))
                DrawProjectMarker(p, at, radius + 16);
            if (planet.Id == selected.Id || planet.Id == gs.planet.Id)
            {
                p.strokeColor = planet.Id == gs.planet.Id ? UiKit.Cyan : Color.white; p.lineWidth = 3;
                p.BeginPath(); p.Arc(at, radius + 6, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            }
            MapDisc(p, at + new Vector2(2, 4), radius + 2, new Color(0.02f, 0.04f, 0.12f));
            MapDisc(p, at, radius, UiKit.Ui(planet.Primary));
            MapDisc(p, at + new Vector2(radius * 0.25f, radius * 0.2f), radius * 0.52f, UiKit.Ui(planet.Secondary));
            MapDisc(p, at + new Vector2(-radius * 0.32f, -radius * 0.38f), radius * 0.22f, new Color(1, 1, 1, 0.55f));
            if (planet.HasRings)
            {
                p.strokeColor = UiKit.Ui(planet.Rim); p.lineWidth = 4;
                p.BeginPath(); p.MoveTo(at + new Vector2(-radius * 1.5f, radius * 0.35f));
                p.BezierCurveTo(at + new Vector2(-radius, radius), at + new Vector2(radius, radius * 0.65f),
                    at + new Vector2(radius * 1.5f, -radius * 0.35f)); p.Stroke();
            }
        }

        void DrawMapRoute(Painter2D p)
        {
            if (mapPreview == null || selected == null || selected.Id == gs.planet.Id) return;
            p.lineWidth = 2.5f; p.strokeColor = mapPreview.reachable ? UiKit.Magenta : UiKit.Red;
            var legs = mapPreview.legs;
            for (int i = 0; i < legs.Length; i++)
            {
                RouteLeg leg = legs[i];
                Vector2 a = Project(leg.x0, leg.y0), b = Project(leg.x1, leg.y1);
                if (leg.intraSystem && SystemMapView) { a = PlanetPoint(gs.planet); b = PlanetPoint(selected); }
                if (SystemMapView && !leg.intraSystem) continue;
                Vector2 ac = Project(leg.x0, leg.y0), bc = Project(leg.x1, leg.y1);
                if (!leg.intraSystem)
                {
                    Vector2 d = b - a;
                    Vector2 perpendicular = new Vector2(-d.y, d.x);
                    ac = a - (-perpendicular + d * 0.25f);
                    bc = b - (-perpendicular - d * 0.25f);
                }
                var curve = new RouteCurve(new MapPoint(a.x, a.y), new MapPoint(b.x, b.y), new MapPoint(ac.x, ac.y), new MapPoint(bc.x, bc.y));
                p.BeginPath(); p.MoveTo(a);
                for (int k = 1; k <= 24; k++) { MapPoint q = curve.At(k / 24.0); p.LineTo(new Vector2((float)q.x, (float)q.y)); }
                p.Stroke();
                float time = Mathf.Repeat(Time.unscaledTime * 0.12f, 1);
                if ((int)(time * legs.Length) == i)
                {
                    MapPoint q = curve.At(time * legs.Length - i), d = curve.Direction(time * legs.Length - i);
                    Vector2 ship = new Vector2((float)q.x, (float)q.y), forward = new Vector2((float)d.x, (float)d.y), side = new Vector2(-forward.y, forward.x);
                    p.fillColor = Color.white;
                    p.BeginPath(); p.MoveTo(ship + forward * 10); p.LineTo(ship - forward * 6 + side * 5); p.LineTo(ship - forward * 6 - side * 5); p.ClosePath(); p.Fill();
                }
            }
            if (legs.Length == 0 && !SystemMapView)
            {
                p.BeginPath(); p.MoveTo(Project(gs.planet.Star.X, gs.planet.Star.Y)); p.LineTo(Project(selected.Star.X, selected.Star.Y)); p.Stroke();
            }
        }

        void BuildMapSearch()
        {
            var bar = UiKit.Div("fr-map-search");
            bar.style.top = 140 + inset.y; bar.style.left = 16 + inset.x; bar.style.right = 16 + inset.z;
            mapSearch = new TextField { tooltip = UiText.Get("map.search.tooltip") };
            mapSearch.textEdition.placeholder = UiText.Get("map.search.placeholder");
            mapSearch.style.flexGrow = 1;
            mapSearch.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return) { SearchMap(); e.StopPropagation(); } });
            bar.Add(mapSearch); bar.Add(UiKit.Btn(UiText.Get("map.search.find"), UiKit.Cyan, SearchMap));
            mapView.Add(bar);
            var filter = new DropdownField();
            mapFilter = filter;
            filter.AddToClassList("fr-map-filter");
            filter.tooltip = UiText.Get("map.filter.tooltip");
            filter.choices = new List<string> { UiText.Get("map.filter.all"), UiText.Get("map.filter.project") };
            mapResourceIds.Clear(); mapResourceIds.Add(""); mapResourceIds.Add(ProjectMapFilter);
            foreach (var r in gs.db.resources) { filter.choices.Add(r.LocalizedName); mapResourceIds.Add(r.id); }
            filter.index = mapProjectResources.Count > 0 ? 1 : 0;
            mapResource = mapResourceIds[filter.index];
            filter.RegisterValueChangedCallback(e =>
            {
                mapResource = mapResourceIds[filter.index]; mapVersion++; mapQueryAt = 0;
                RefreshMapProjectMatches();
                BuildMapCard();
                mapWorld.MarkDirtyRepaint();
            });
            filter.style.position = Position.Absolute; filter.style.left = 16 + inset.x; filter.style.top = 260 + inset.y; filter.style.width = 260;
            mapView.Add(filter);
            mapReadout = UiKit.Text("", "fr-tiny", UiKit.Muted);
            mapReadout.style.position = Position.Absolute; mapReadout.style.left = 296 + inset.x; mapReadout.style.right = 16 + inset.z; mapReadout.style.top = 266 + inset.y;
            mapView.Add(mapReadout);
            mapNotice = UiKit.Text("", "fr-tiny", UiKit.Muted);
            mapNotice.style.position = Position.Absolute; mapNotice.style.left = 16 + inset.x;
            mapNotice.style.top = 336 + inset.y; mapNotice.style.right = 16 + inset.z;
            mapView.Add(mapNotice);
        }

        void SetMapNotice(string message)
        {
            mapNotice.text = message;
            LayoutMapChrome();
        }

        void SearchMap()
        {
            string text = mapSearch.value.Trim();
            SetMapNotice("");
            if (text.Length == 0) { SetMapNotice(UiText.Get("map.search.empty")); return; }
            string[] xy = text.Split(',');
            double x, y;
            if (xy.Length == 2 && double.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && double.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                && !double.IsNaN(x) && !double.IsNaN(y) && Math.Abs(x) <= 1e7 && Math.Abs(y) <= 1e7)
            {
                mapZoom = 2f;
                viewport.Center(x, y); ApplyMapTransform(); SetMapNotice(UiText.Get("map.search.coordinates")); return;
            }
            var byPlanetId = gs.universe.Get(text);
            if (byPlanetId != null && Surveyed(byPlanetId.Star)) { SelectMapWorld(byPlanetId); CenterOn(byPlanetId); return; }
            foreach (var world in gs.universe.AuthoredPlanets)
                if (Surveyed(world.Star) && (string.Equals(world.Name, text, StringComparison.OrdinalIgnoreCase) || world.Id == text || world.Authored.id == text))
                { SelectMapWorld(world); CenterOn(world); return; }
            foreach (var record in gs.world.All)
            {
                var world = gs.universe.Get(record.planetId);
                if (world != null && Surveyed(world.Star) && (string.Equals(world.Name, text, StringComparison.OrdinalIgnoreCase) || world.Id == text))
                { SelectMapWorld(world); CenterOn(world); return; }
            }
            StarSystem byId;
            if (gs.universe.Universe.TryGetSystemById(text, out byId) && Surveyed(byId))
            {
                var worlds = gs.universe.PlanetsOf(byId);
                if (worlds.Count > 0) { SelectMapWorld(worlds[0]); CenterOn(selected); return; }
            }
            if (mapSnapshot != null)
                foreach (var star in mapSnapshot.systems)
                    if (string.Equals(star.Name, text, StringComparison.OrdinalIgnoreCase) || star.Id == text || string.Equals(star.Designation, text, StringComparison.OrdinalIgnoreCase))
                    { var worlds = gs.universe.PlanetsOf(star); if (worlds.Count > 0) { SelectMapWorld(worlds[0]); CenterOn(selected); return; } }
            string knownPlanetId;
            if (mapNameIndex != null && mapNameIndex.TryFind(text, out knownPlanetId))
            {
                var knownWorld = gs.universe.Get(knownPlanetId);
                if (knownWorld != null && Surveyed(knownWorld.Star)) { SelectMapWorld(knownWorld); CenterOn(knownWorld); return; }
            }
            SetMapNotice(UiText.Get("map.search.not-found"));
        }
    }
}
