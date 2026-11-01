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
        const float MinZoom = 1f, NeighborZoom = 4f;
        const float SystemZoom = 12f, SystemZoomThreshold = 8f;
        PlanetData selected;
        bool mapOpen, mapAutopilot, mapMinimal;
        VisualElement mapView, mapWorld, mapCard, mapTools;
        VisualElement mapLocationBar, mapSearchBar;
        Label mapLocation;
        Label mapReadout, mapNotice;
        Button mapViewMode, mapZoomIn, mapZoomOut, mapHideControls;
        TextField mapSearch;
        DropdownField mapFilter;
        readonly MapViewport viewport = new MapViewport();
        float mapZoom = 2f;
        float mapZoomTarget = SystemZoom;
        Vector2 mapZoomAnchor = new Vector2(0.5f, 0.5f);
        bool mapScaleAnimating;
        const float MapWheelSensitivity = 0.12f, MapScaleResponse = 18f;
        int dragPointer = -1;
        Vector2 dragStart, dragLast;
        bool mapDragging, suppressMapClick;
        float lastPinch, mapQueryAt;
        Vector2 lastMapPinchCenter;
        int mapVersion, queryVersion, previewVersion, runningPreviewVersion;
        MapSnapshot mapSnapshot;
        MapQueryBounds mapQueryBounds;
        double mapQueryScale;
        bool mapQueryBoundsKnown;
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
        readonly List<Button> mapSystemButtons = new List<Button>();
        readonly List<StarSystem> mapNodeSystems = new List<StarSystem>();
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
            OpenMap(false);
        }

        void OpenMap(bool minimal)
        {
            if (defense.Active || busy) return;
            if (mapOpen) { CloseMap(); return; }
            ClosePopup(); CloseModal(); ExitBuild();
            mapOpen = true; mapMinimal = minimal;
            suppressMapClick = false; lastPinch = 0f;
            selected = gs.planet; mapAutopilot = false; mapResource = ""; mapZoom = SystemZoom;
            mapZoomTarget = mapZoom; mapScaleAnimating = false;
            planetView.Zoom01 = 0f;
            planetView.MapZoomBlend = minimal ? 0f : 1f;
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
            mapWorld.RegisterCallback<WheelEvent>(e =>
            {
                HandleMapWheel(-e.delta.y / 3f, e.localMousePosition);
                e.StopPropagation();
            });

            mapTools = UiKit.Div("fr-map-tools");
            mapTools.Add(Icons.IconButton("close", UiKit.Muted, ReturnFromMap));
            mapZoomIn = Icons.IconButton("plus", UiKit.Muted, () => StepMapZoom(true));
            mapZoomIn.name = "map-zoom-in";
            mapZoomOut = Icons.IconButton("minus", UiKit.Muted, () => StepMapZoom(false));
            mapZoomOut.name = "map-zoom-out";
            mapTools.Add(mapZoomIn);
            mapTools.Add(mapZoomOut);
            var home = Icons.IconButton("map", UiKit.Cyan, () => { SelectMapWorld(gs.planet); CenterOn(gs.planet); SetMapZoomTarget(SystemZoom); });
            home.tooltip = UiText.Get("map.recenter"); mapTools.Add(home);
            mapView.Add(mapTools);
            mapHideControls = IconBtn(UiText.Get("map.hide-controls"), "eye", UiKit.Cyan, () => SetMapMinimal(true));
            mapHideControls.name = "map-hide-controls";
            mapHideControls.tooltip = UiText.Get("map.hide-controls");
            mapHideControls.style.position = Position.Absolute;
            mapHideControls.style.right = UiKit.Space.S16 + inset.z;
            mapHideControls.style.top = 260 + inset.y;
            mapHideControls.RegisterCallback<GeometryChangedEvent>(e => LayoutMapChrome());
            mapView.Add(mapHideControls);
            mapViewMode = UiKit.Btn("", UiKit.Cyan, () =>
            {
                SetMapZoomTarget(SystemMapView ? NeighborZoom : NeighborMapView ? MinZoom : SystemZoom);
            });
            mapViewMode.name = "map-switch-view";
            mapViewMode.style.position = Position.Absolute;
            mapViewMode.style.left = 16 + inset.x;
            mapViewMode.style.top = 16 + inset.y;
            mapViewMode.style.width = 200;
            mapView.Add(mapViewMode);
            mapLocationBar = UiKit.Div("fr-map-location");
            mapLocationBar.name = "map-location-bar";
            var back = Icons.IconButton("back", UiKit.Muted, ReturnFromMap);
            back.name = "map-return-world";
            back.tooltip = UiText.Get("map.return-world", gs.planet.Name);
            mapLocationBar.Add(back);
            mapLocation = UiKit.Text("", "fr-small");
            mapLocation.name = "map-location";
            mapLocationBar.Add(mapLocation);
            var controls = IconBtn(UiText.Get("map.controls"), "eye", UiKit.Cyan, () => SetMapMinimal(false));
            controls.name = "map-show-controls";
            mapLocationBar.Add(controls);
            mapView.Add(mapLocationBar);
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
            if (mapZoomTransition != null) StopCoroutine(mapZoomTransition);
            mapZoomTransition = null;
            if (planetView != null) planetView.MapZoomBlend = 0f;
            ResetMapPointer();
            if (mapView != null) mapView.RemoveFromHierarchy();
            mapView = null; mapWorld = null; mapCard = null; mapTools = null;
            mapOpen = false; lastPinch = 0f; mapSnapshot = null;
            suppressMapClick = false;
            mapScaleAnimating = false;
            mapQueryBoundsKnown = false;
            mapNameIndex = null;
            mapViewMode = null; mapHideControls = null;
            mapLocationBar = null; mapLocation = null; mapSearchBar = null;
            mapGestureHeld = false;
            mapFilter = null;
            mapVersion++; previewVersion++;
            mapLabels.Clear(); mapPlanets.Clear();
            mapPlanetButtons.Clear();
            mapSystemButtons.Clear(); mapNodeSystems.Clear();
            mapPinPlanets.Clear();
            // Workers never touch UI/GameObjects. Keep their handles until completion so reopening cannot fan out new workers.
        }

        void LayoutMapChrome()
        {
            if (mapTools == null) return;
            foreach (var element in new VisualElement[] { mapViewMode, mapHideControls, mapSearchBar, mapFilter, mapReadout, mapCard })
                if (element != null) element.style.display = mapMinimal ? DisplayStyle.None : DisplayStyle.Flex;
            mapTools.style.display = DisplayStyle.Flex;
            for (int i = 0; i < mapTools.childCount; i++)
                mapTools[i].style.display = mapMinimal && mapTools[i] != mapZoomIn && mapTools[i] != mapZoomOut
                    ? DisplayStyle.None : DisplayStyle.Flex;
            if (mapLocationBar != null)
            {
                mapLocationBar.style.display = mapMinimal ? DisplayStyle.Flex : DisplayStyle.None;
                mapLocationBar.style.top = UiKit.Space.S16 + inset.y;
                mapLocationBar.style.left = UiKit.Space.S16 + inset.x;
                mapLocationBar.style.right = UiKit.Space.S16 + inset.z;
            }
            mapTools.style.right = UiKit.Space.S16 + inset.z;
            mapTools.style.top = (mapMinimal ? UiKit.Size.Touch + UiKit.Space.S32 : UiKit.Space.S16) + inset.y;
            mapTools.style.left = UiKit.Space.S16 + inset.x;
            if (mapReadout != null && mapHideControls != null)
            {
                float hideWidth = mapHideControls.layout.width;
                if (float.IsNaN(hideWidth) || hideWidth <= 0f) hideWidth = UiKit.Size.Touch * 2f;
                mapReadout.style.right = hideWidth + UiKit.Space.S32 + inset.z;
            }
            if (mapCard != null) mapCard.style.marginBottom = UiKit.Space.S16 + inset.w;
            if (mapWorld != null)
            {
                mapWorld.style.top = (mapMinimal ? UiKit.Size.Touch + UiKit.Space.S32 :
                    mapNotice != null && !string.IsNullOrEmpty(mapNotice.text) ? 434 : 380) + inset.y;
                float cardHeight = mapCard != null ? mapCard.layout.height : 0;
                mapWorld.style.bottom = (mapMinimal ? UiKit.Space.S16 :
                    (float.IsNaN(cardHeight) ? 0 : cardHeight) + 32) + inset.w;
            }
            if (mapNotice != null) mapNotice.style.top = (mapMinimal ? UiKit.Size.Touch + UiKit.Space.S24 : 380) + inset.y;
        }

        void SetMapMinimal(bool minimal)
        {
            mapMinimal = minimal;
            LayoutMapChrome();
            ApplyMapTransform();
        }

        bool SystemMapView { get { return viewport.Scale >= SystemZoomThreshold; } }
        bool NeighborMapView { get { return !SystemMapView && viewport.Scale >= NeighborZoom; } }
        float MapOrbitDetail { get { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MinZoom, NeighborZoom, mapZoom)); } }
        float MapLocalDetail { get { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(NeighborZoom, SystemZoom, mapZoom)); } }
        Vector2 Project(double x, double y) { MapPoint p = viewport.Project(x, y); return new Vector2((float)p.x, (float)p.y); }

        void CenterOn(PlanetData p)
        {
            if (mapView == null || p == null) return;
            viewport.Center(p.Star.X, p.Star.Y);
            ApplyMapTransform();
        }

        void StepMapZoom(bool inward)
        {
            if (inward && mapZoom >= SystemZoom - 0.001f && mapMinimal)
            {
                if (selected != null && selected.Id == gs.planet.Id) ReturnFromMap();
                else SetMapMinimal(false);
                return;
            }
            float scale = mapScaleAnimating ? mapZoomTarget : mapZoom;
            SetMapZoomTarget(inward ? (scale < NeighborZoom - 0.001f ? NeighborZoom : SystemZoom)
                : (scale > NeighborZoom + 0.001f ? NeighborZoom : MinZoom));
        }

        void SetMapZoomTarget(float scale)
        {
            mapZoomTarget = Mathf.Clamp(scale, MinZoom, SystemZoom);
            mapZoomAnchor = new Vector2(0.5f, 0.5f);
            mapScaleAnimating = !Mathf.Approximately(mapZoom, mapZoomTarget);
            if (mapWorld != null) mapWorld.MarkDirtyRepaint();
        }

        void ZoomMapAt(float scale, Vector2 anchor)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale)) return;
            mapZoom = Mathf.Clamp(scale, MinZoom, SystemZoom);
            viewport.ZoomAt(anchor.x, anchor.y, mapZoom / viewport.Scale);
            ApplyMapTransform();
        }

        void AdvanceMapScale(float deltaTime)
        {
            if (!mapScaleAnimating) return;
            float logScale = Mathf.Lerp(Mathf.Log(mapZoom), Mathf.Log(mapZoomTarget),
                1f - Mathf.Exp(-MapScaleResponse * deltaTime));
            float scale = Mathf.Exp(logScale);
            if (Mathf.Abs(Mathf.Log(scale / mapZoomTarget)) < 0.0001f)
            {
                scale = mapZoomTarget;
                mapScaleAnimating = false;
            }
            ZoomMapAt(scale, new Vector2(mapZoomAnchor.x * (float)viewport.Width,
                mapZoomAnchor.y * (float)viewport.Height));
        }

        void ApplyMapTransform()
        {
            if (mapWorld == null) return;
            float w = mapWorld.layout.width, h = mapWorld.layout.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0) return;
            viewport.Resize(w, h);
            viewport.ZoomAt(w * 0.5, h * 0.5, mapZoom / viewport.Scale);
            ClampMapCenter();
            if ((!mapQueryBoundsKnown || Math.Abs(Math.Log(mapQueryScale / viewport.Scale)) > 0.14 ||
                !mapQueryBounds.Covers(viewport)) &&
                queryVersion == mapVersion)
            {
                mapVersion++;
                mapQueryAt = Time.unscaledTime + 0.04f;
            }
            if (mapViewMode != null)
                mapViewMode.text = UiText.Get(SystemMapView ? "map.mode.neighbors" :
                    NeighborMapView ? "map.mode.overview" : "map.mode.orbits");
            mapZoomIn.SetEnabled(mapZoom < SystemZoom - 0.001f || mapMinimal);
            mapZoomOut.SetEnabled(mapZoom > MinZoom + 0.001f);
            var legend = mapWorld.Q("map-scanner-legend");
            legend.style.display = mapMinimal ? DisplayStyle.None : DisplayStyle.Flex;
            legend.style.opacity = 1f - MapLocalDetail;
            RefreshMapReadout();
            if (mapLocation != null)
                mapLocation.text = SystemMapView ? UiText.Get("map.location.planet", gs.planet.Name, gs.planet.Star.Name) :
                    NeighborMapView ? UiText.Get("map.location.system", gs.planet.Star.Name) :
                    UiText.Get("map.location.sector", gs.planet.Star.Name,
                        gs.planet.Star.X.ToString("0.#", CultureInfo.InvariantCulture),
                        gs.planet.Star.Y.ToString("0.#", CultureInfo.InvariantCulture));
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
            if (mapZoomTransition != null) return;
            AdvanceMapScale(Time.unscaledDeltaTime);
            if (mapGestureHeld)
            {
                if (Input.touchCount == 0) mapGestureHeld = false;
                lastPinch = 0f;
                return;
            }
            if (Input.mouseScrollDelta.y != 0f && ScreenPointInMap(Input.mousePosition))
                HandleMapWheel(Input.mouseScrollDelta.y, MapScreenPoint(Input.mousePosition));
            if (mapPreview != null && selected != null && selected.Id != gs.planet.Id) mapWorld.MarkDirtyRepaint();
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0).position; var b = Input.GetTouch(1).position;
                float d = Vector2.Distance(a, b);
                if (lastPinch <= 1f && (!ScreenPointInMap(a) || !ScreenPointInMap(b))) return;
                suppressMapClick = true;
                Vector2 pinchCenter = MapScreenPoint((a + b) * 0.5f);
                if (lastPinch > 1 && d > 1)
                {
                    if (mapZoom >= SystemZoom - 0.001f && d > lastPinch && mapMinimal)
                    {
                        if (selected != null && selected.Id == gs.planet.Id) ReturnFromMap();
                        else SetMapMinimal(false);
                    }
                    else
                    {
                        Vector2 movement = pinchCenter - lastMapPinchCenter;
                        viewport.Pan(movement.x, movement.y);
                        ApplyMapPinchRatio(d / lastPinch, pinchCenter);
                    }
                }
                lastPinch = d;
                lastMapPinchCenter = pinchCenter;
            }
            else lastPinch = 0;
        }

        bool ScreenPointInMap(Vector2 screen)
        {
            var point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, ScreenMetrics.Height - screen.y));
            var element = root.panel.Pick(point);
            return element == mapWorld || (element != null && mapWorld.Contains(element));
        }

        Vector2 MapScreenPoint(Vector2 screen)
        {
            var point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, ScreenMetrics.Height - screen.y));
            return mapWorld.WorldToLocal(point);
        }

        void ApplyMapPinchRatio(float ratio, Vector2 anchor)
        {
            if (!(ratio > 0f) || float.IsInfinity(ratio)) return;
            suppressMapClick = true;
            mapScaleAnimating = false;
            ZoomMapAt(mapZoom * ratio, anchor);
            mapZoomTarget = mapZoom;
        }

        int lastMapWheelFrame = -1;
        void HandleMapWheel(float delta, Vector2 anchor)
        {
            if (!mapOpen || mapZoomTransition != null || delta == 0f || float.IsNaN(delta) || float.IsInfinity(delta) ||
                lastMapWheelFrame == Time.frameCount || lastWorldWheelFrame == Time.frameCount) return;
            lastMapWheelFrame = Time.frameCount;
            suppressMapClick = true;
            if (delta > 0f && mapZoom >= SystemZoom - 0.001f && mapMinimal)
            {
                if (selected != null && selected.Id == gs.planet.Id) ReturnFromMap();
                else SetMapMinimal(false);
                return;
            }
            mapZoomTarget = Mathf.Clamp((mapScaleAnimating ? mapZoomTarget : mapZoom) *
                Mathf.Exp(delta * MapWheelSensitivity), MinZoom, SystemZoom);
            mapZoomAnchor = new Vector2(Mathf.Clamp01(anchor.x / Mathf.Max(1f, (float)viewport.Width)),
                Mathf.Clamp01(anchor.y / Mathf.Max(1f, (float)viewport.Height)));
            mapScaleAnimating = !Mathf.Approximately(mapZoom, mapZoomTarget);
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

        void ClampMapCenter()
        {
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            void Include(double cx, double cy, double radius)
            {
                minX = Math.Min(minX, cx - radius); maxX = Math.Max(maxX, cx + radius);
                minY = Math.Min(minY, cy - radius); maxY = Math.Max(maxY, cy + radius);
            }
            for (int i = 0; i < surveyCenters.Count; i++)
                Include(surveyCenters[i].X, surveyCenters[i].Y, surveyRadii[i]);
            foreach (var id in mapRememberedSystems)
                if (gs.universe.Universe.TryGetSystemById(id, out var star)) Include(star.X, star.Y, 0);
            // One stable surveyed-content envelope at every scale: no selected-system clamp
            // or nearest-survey switch can snap the camera during a gesture.
            const double margin = 20;
            if (!double.IsPositiveInfinity(minX))
                viewport.Center(Math.Max(minX - margin, Math.Min(maxX + margin, viewport.CenterX)),
                    Math.Max(minY - margin, Math.Min(maxY + margin, viewport.CenterY)));
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
            if (MapLocalDetail > 0.4f)
            {
                PlanetData nearest = null;
                float nearestDistance = 52;
                foreach (var planet in mapPlanets)
                {
                    float d = Vector2.Distance(PlanetPoint(planet), at);
                    if (d < nearestDistance) { nearest = planet; nearestDistance = d; }
                }
                if (nearest != null) SelectMapWorld(nearest);
                if (nearest != null) return;
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
            if (mapMinimal) SetMapMinimal(false);
            if (otherSystem || !SystemMapView) { CenterOn(planet); SetMapZoomTarget(SystemZoom); }
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
            foreach (var button in mapSystemButtons) button.RemoveFromHierarchy();
            mapPlanetButtons.Clear();
            mapSystemButtons.Clear(); mapNodeSystems.Clear();
            mapPlanets.Clear();
            if (selected != null && Surveyed(selected.Star)) mapPlanets.AddRange(gs.universe.PlanetsOf(selected.Star));
            foreach (var planet in mapPlanets)
            {
                var captured = planet;
                var button = new Button(() =>
                {
                    if (suppressMapClick) { suppressMapClick = false; return; }
                    SelectMapWorld(captured);
                });
                button.name = "map-planet-" + planet.Id;
                button.tooltip = planet.Name;
                button.AddToClassList("fr-map-planet");
                button.RegisterCallback<NavigationSubmitEvent>(e => suppressMapClick = false, TrickleDown.TrickleDown);
                button.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                mapPlanetButtons.Add(button);
                mapWorld.Add(button);
            }
            if (mapSnapshot != null)
                foreach (var star in mapSnapshot.systems)
                {
                    if (!Surveyed(star)) continue;
                    var planets = gs.universe.PlanetsOf(star);
                    if (planets.Count == 0) continue;
                    var captured = planets[0];
                    var button = new Button(() =>
                    {
                        if (suppressMapClick) { suppressMapClick = false; return; }
                        SelectMapWorld(captured);
                    });
                    button.name = "map-system-" + star.Id;
                    button.tooltip = star.Name;
                    button.AddToClassList("fr-map-planet");
                    button.RegisterCallback<NavigationSubmitEvent>(e => suppressMapClick = false, TrickleDown.TrickleDown);
                    button.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                    mapSystemButtons.Add(button); mapNodeSystems.Add(star);
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
            var planets = gs.universe.PlanetsOf(planet.Star);
            int index = 0;
            for (int i = 0; i < planets.Count; i++) if (planets[i].Id == planet.Id) { index = i; break; }
            float fraction = (index + 1f) / Mathf.Max(1, planets.Count);
            const float orbitAspect = 0.65f;
            float outerX = Mathf.Max(80, Mathf.Min((float)viewport.Width * 0.5f - 100,
                ((float)viewport.Height * 0.5f - 72) / orbitAspect));
            // All systems use the same stable schematic lanes, scaled continuously. Neither
            // detail thresholds nor arriving worker snapshots can relocate a planet.
            float radiusX = Mathf.Lerp(64, outerX, fraction) * mapZoom / SystemZoom * MapOrbitDetail;
            return new Vector2(radiusX, radiusX * orbitAspect);
        }

        float NeighborOrbitRadius(StarSystem star)
        {
            var planets = gs.universe.PlanetsOf(star);
            return planets.Count == 0 ? 0f : PlanetOrbitAxes(planets[planets.Count - 1]).x;
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
                button.style.display = MapLocalDetail > 0.4f ? DisplayStyle.Flex : DisplayStyle.None;
                button.style.opacity = MapLocalDetail;
                button.style.left = at.x - 52;
                button.style.top = at.y - 52;
            }
            for (int i = 0; i < mapSystemButtons.Count; i++)
            {
                var star = mapNodeSystems[i];
                Vector2 at = Project(star.X, star.Y);
                var button = mapSystemButtons[i];
                button.style.display = star.Key == selected.Star.Key && MapLocalDetail > 0.4f
                    ? DisplayStyle.None : DisplayStyle.Flex;
                button.style.opacity = 1f - MapLocalDetail * 0.85f;
                button.style.left = at.x - 52;
                button.style.top = at.y - 52;
            }
            if (MapLocalDetail > 0f)
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
            if (mapSnapshot != null && mapSnapshot.densityOnly)
            {
                foreach (var galaxy in mapSnapshot.galaxies)
                {
                    if (used >= 16) break;
                    PlaceMapLabel(ref used, galaxy.Name, Project(galaxy.CenterX, galaxy.CenterY) + new Vector2(0, 16),
                        240, MapTint(UiKit.Cyan, 1f - MapLocalDetail));
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
                    PlaceMapLabel(ref used, star.Name, p + Vector2.Lerp(new Vector2(10, 6),
                        new Vector2(-90, NeighborOrbitRadius(star) * 0.65f + 12), MapOrbitDetail),
                        180, MapTint(star.Key == gs.planet.Star.Key ? UiKit.Cyan : UiKit.Muted, 1f - MapLocalDetail));
                }
            }
            for (int i = used; i < mapLabels.Count; i++) mapLabels[i].style.display = DisplayStyle.None;
        }

        void PlacePlanetLabel(ref int used, PlanetData planet)
        {
            Vector2 at = PlanetPoint(planet);
            string name = planet.Name;
            int before = used;
            Color color = MapTint(planet.Id == gs.planet.Id ? UiKit.Cyan : Color.white, MapLocalDetail);
            PlaceMapLabel(ref used, name, at + new Vector2(-90, 30), 180, color);
            if (used == before)
                PlaceMapLabel(ref used, name, at + new Vector2(-90, -70), 180, color);
        }

        void PlaceMapLabel(ref int used, string text, Vector2 at, float width, Color color)
        {
            if (color.a <= 0.001f) return;
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
            l.style.backgroundColor = MapTint(new Color(0.035f, 0.06f, 0.13f, 0.94f), color.a);
            l.style.paddingLeft = 4f; l.style.paddingRight = 4f;
            l.style.borderTopLeftRadius = l.style.borderTopRightRadius = 4f;
            l.style.borderBottomLeftRadius = l.style.borderBottomRightRadius = 4f;
        }

        void DrawMapBackground(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            DrawScannerCoverage(p);
            DrawPlanetLinks(p);
            if (mapSnapshot != null)
            {
                foreach (var star in mapSnapshot.systems)
                {
                    Vector2 at = Project(star.X, star.Y);
                    if (at.x < -10 || at.y < -10 || at.x > viewport.Width + 10 || at.y > viewport.Height + 10) continue;
                    if (selected != null && star.Key == selected.Star.Key) continue;
                    bool surveyed = Surveyed(star), current = star.Key == gs.planet.Star.Key;
                    Color color = current ? UiKit.Cyan : surveyed ? UiKit.Amber : new Color(0.35f, 0.45f, 0.62f);
                    float visibility = 1f - MapLocalDetail * 0.85f;
                    if (surveyed) DrawNeighborSystem(p, star, visibility);
                    MapDisc(p, at, current ? 12 : star.IsAuthored ? 9 : 6, MapTint(color, visibility));
                    p.strokeColor = MapTint(new Color(0.5f, 0.65f, 0.85f, 0.45f), visibility); p.lineWidth = 2;
                    p.BeginPath(); p.Arc(at, current ? 18 : star.IsAuthored ? 15 : 11, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
                    if (mapResource == ProjectMapFilter && mapProjectSystems.Contains(star.Id))
                        DrawProjectMarker(p, at, (current ? 12f : star.IsAuthored ? 9f : 6f) + 10f, visibility);
                }
            }
            Vector2 here = Project(gs.planet.Star.X, gs.planet.Star.Y);
            p.strokeColor = MapTint(new Color(0.2f, 0.9f, 1, 0.22f), 1f - MapLocalDetail); p.lineWidth = 1;
            float range = (float)(mapJumpRange * viewport.Scale);
            if (range > 0 && range < 12000)
            {
                p.BeginPath(); p.Arc(here, range, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            }
            if (selected != null)
            {
                Vector2 center = Project(selected.Star.X, selected.Star.Y);
                foreach (var planet in mapPlanets)
                {
                    if (MapOrbitDetail <= 0f) break;
                    Vector2 axes = PlanetOrbitAxes(planet);
                    p.strokeColor = MapTint(planet.Id == selected.Id ? new Color(0.5f, 0.85f, 1f, Mathf.Lerp(0.4f, 0.9f, MapLocalDetail)) :
                        new Color(0.4f, 0.56f, 0.78f, Mathf.Lerp(0.28f, 0.6f, MapLocalDetail)), MapOrbitDetail);
                    p.lineWidth = Mathf.Lerp(1f, planet.Id == selected.Id ? 3 : 2, MapLocalDetail);
                    p.BeginPath();
                    for (int i = 0; i <= 80; i++)
                    {
                        float angle = i * Mathf.PI * 2 / 80;
                        Vector2 at = center + new Vector2(Mathf.Cos(angle) * axes.x, Mathf.Sin(angle) * axes.y);
                        if (i == 0) p.MoveTo(at); else p.LineTo(at);
                    }
                    p.ClosePath(); p.Stroke();
                }
                float starRadius = Mathf.Lerp(9f, 21f, MapLocalDetail);
                MapDisc(p, center, starRadius + 4f * MapLocalDetail, new Color(0.2f, 0.12f, 0.23f));
                MapDisc(p, center, starRadius, UiKit.Amber);
                MapDisc(p, center + new Vector2(-6, -7) * MapLocalDetail, 7 * MapLocalDetail, new Color(1, 0.93f, 0.65f));
                if (mapResource == ProjectMapFilter && mapProjectSystems.Contains(selected.Star.Id))
                    DrawProjectMarker(p, center, starRadius + 10f,
                        1f - MapLocalDetail);
                foreach (var planet in mapPlanets) DrawMapPlanet(p, planet);
            }
            foreach (var pinned in mapPinPlanets)
            {
                Vector2 at = PlanetPoint(pinned);
                if (at.x < -12 || at.y < -12 || at.x > viewport.Width + 12 || at.y > viewport.Height + 12) continue;
                float visibility = pinned.Star.Key == selected.Star.Key ? 1f : 1f - MapLocalDetail * 0.85f;
                p.strokeColor = MapTint(UiKit.Magenta, visibility);
                p.lineWidth = 2f;
                float radius = Mathf.Lerp(22f, Mathf.Lerp(10f, 35f, MapLocalDetail), MapOrbitDetail);
                p.BeginPath(); p.Arc(at, radius, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            }
            DrawMapRoute(p);
        }

        static void MapDisc(Painter2D p, Vector2 at, float radius, Color color)
        {
            if (radius <= 0f || color.a <= 0.001f) return;
            p.fillColor = color;
            p.BeginPath(); p.Arc(at, radius, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
        }

        static Color MapTint(Color color, float opacity)
        {
            color.a *= opacity;
            return color;
        }

        void DrawNeighborSystem(Painter2D p, StarSystem star, float opacity)
        {
            if (MapOrbitDetail <= 0f) return;
            Vector2 center = Project(star.X, star.Y);
            foreach (var planet in gs.universe.PlanetsOf(star))
            {
                Vector2 axes = PlanetOrbitAxes(planet);
                p.strokeColor = MapTint(planet.Id == selected.Id ? UiKit.Cyan : new Color(0.4f, 0.56f, 0.78f, 0.28f),
                    opacity * MapOrbitDetail);
                p.lineWidth = planet.Id == selected.Id ? 2f : 1f;
                p.BeginPath();
                int segments = Mathf.RoundToInt(Mathf.Lerp(12f, 48f, MapOrbitDetail));
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    Vector2 at = center + new Vector2(Mathf.Cos(angle) * axes.x, Mathf.Sin(angle) * axes.y);
                    if (i == 0) p.MoveTo(at); else p.LineTo(at);
                }
                p.ClosePath(); p.Stroke();
                MapDisc(p, PlanetPoint(planet), 4f, MapTint(UiKit.Ui(planet.Primary), opacity * MapOrbitDetail));
            }
        }

        static void DrawProjectMarker(Painter2D p, Vector2 at, float offset, float opacity = 1f)
        {
            at += new Vector2(offset, -offset) * 0.7071f;
            MapDisc(p, at, 7f, MapTint(new Color(0.02f, 0.04f, 0.1f, 0.95f), opacity));
            p.fillColor = MapTint(UiKit.Lime, opacity);
            p.BeginPath();
            p.MoveTo(at + new Vector2(0f, -4f));
            p.LineTo(at + new Vector2(4f, 4f));
            p.LineTo(at + new Vector2(-4f, 4f));
            p.ClosePath(); p.Fill();
        }

        void DrawScannerCoverage(Painter2D p)
        {
            p.strokeColor = MapTint(new Color(0.15f, 0.9f, 1f, 0.8f), 1f - MapLocalDetail);
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
                if (!Surveyed(from.Star) || !Surveyed(to.Star)) continue;
                Vector2 a = PlanetPoint(from), b = PlanetPoint(to);
                p.strokeColor = MapTint(UiKit.Lime, from.Star.Key == to.Star.Key ? MapOrbitDetail : 1f - MapLocalDetail * 0.85f);
                p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
            }
        }

        void DrawMapPlanet(Painter2D p, PlanetData planet)
        {
            Vector2 at = PlanetPoint(planet);
            float radius = Mathf.Lerp(4f, planet.Id == selected.Id ? 24 : 19, MapLocalDetail);
            float opacity = MapOrbitDetail;
            if (mapResource == ProjectMapFilter && mapProjectWorlds.Contains(planet.Id))
                DrawProjectMarker(p, at, radius + 10f, opacity);
            if (planet.Id == selected.Id || planet.Id == gs.planet.Id)
            {
                p.strokeColor = MapTint(planet.Id == gs.planet.Id ? UiKit.Cyan : Color.white, opacity); p.lineWidth = 3;
                p.BeginPath(); p.Arc(at, radius + 6, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            }
            MapDisc(p, at + new Vector2(2, 4) * MapLocalDetail, radius + 2 * MapLocalDetail, MapTint(new Color(0.02f, 0.04f, 0.12f), opacity));
            MapDisc(p, at, radius, MapTint(UiKit.Ui(planet.Primary), opacity));
            MapDisc(p, at + new Vector2(radius * 0.25f, radius * 0.2f), radius * 0.52f, MapTint(UiKit.Ui(planet.Secondary), opacity * MapLocalDetail));
            MapDisc(p, at + new Vector2(-radius * 0.32f, -radius * 0.38f), radius * 0.22f, MapTint(new Color(1, 1, 1, 0.55f), opacity * MapLocalDetail));
            if (planet.HasRings)
            {
                p.strokeColor = MapTint(UiKit.Ui(planet.Rim), opacity); p.lineWidth = Mathf.Lerp(1f, 4f, MapLocalDetail);
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
                if (leg.intraSystem) { a = PlanetPoint(gs.planet); b = PlanetPoint(selected); }
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
            if (legs.Length == 0)
            {
                p.BeginPath(); p.MoveTo(Project(gs.planet.Star.X, gs.planet.Star.Y)); p.LineTo(Project(selected.Star.X, selected.Star.Y)); p.Stroke();
            }
        }

        void BuildMapSearch()
        {
            var bar = UiKit.Div("fr-map-search");
            mapSearchBar = bar;
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
            mapReadout.name = "map-scale-readout";
            mapReadout.style.position = Position.Absolute; mapReadout.style.left = 296 + inset.x; mapReadout.style.right = UiKit.Size.Touch + UiKit.Space.S32 + inset.z; mapReadout.style.top = 266 + inset.y;
            mapReadout.style.whiteSpace = WhiteSpace.Normal;
            mapReadout.style.minWidth = 0f;
            mapReadout.style.maxHeight = UiKit.Size.Touch;
            mapReadout.style.overflow = Overflow.Hidden;
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
                mapZoom = MinZoom;
                mapZoomTarget = mapZoom; mapScaleAnimating = false;
                viewport.Center(x, y); ApplyMapTransform();
                bool clamped = Math.Abs(viewport.CenterX - x) > 0.01 || Math.Abs(viewport.CenterY - y) > 0.01;
                SetMapNotice(UiText.Get(clamped ? "map.search.outside" : "map.search.coordinates")); return;
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

        public float DebugMapContinuousScale { get { return mapZoom; } }
        public Vector2 DebugMapPlanetPoint(PlanetData planet) { return PlanetPoint(planet); }
        public Vector2 DebugMapStarPoint(PlanetData planet) { return Project(planet.Star.X, planet.Star.Y); }
        public void DebugMapContinuousZoom(float scale)
        {
            mapScaleAnimating = false;
            ZoomMapAt(scale, new Vector2((float)viewport.Width * 0.5f, (float)viewport.Height * 0.5f));
            mapZoomTarget = mapZoom;
        }
        public void DebugMapPinchRatio(float ratio, Vector2 anchor)
        {
            ApplyMapPinchRatio(ratio, anchor);
        }
    }
}
