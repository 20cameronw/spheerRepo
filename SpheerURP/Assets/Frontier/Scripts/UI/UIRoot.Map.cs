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
        PlanetData selected;
        bool mapOpen, mapAutopilot;
        VisualElement mapView, mapWorld, mapCard, mapTools;
        Label mapReadout, mapNotice;
        TextField mapSearch;
        readonly MapViewport viewport = new MapViewport();
        float mapZoom = 2f;
        int dragPointer = -1;
        Vector2 dragStart, dragLast;
        bool mapDragging, suppressMapClick;
        float lastPinch, mapQueryAt;
        int mapVersion, queryVersion, previewVersion, runningPreviewVersion;
        MapSnapshot mapSnapshot;
        Task<MapSnapshot> mapQuery;
        Task<RoutePlan> mapPreviewTask;
        RoutePlan mapPreview;
        readonly List<StarSystem> surveyCenters = new List<StarSystem>();
        readonly List<PlanetData> mapPlanets = new List<PlanetData>();
        readonly List<Label> mapLabels = new List<Label>();
        readonly List<Rect> mapLabelBounds = new List<Rect>();
        string mapResource = "";
        double mapSurveyRadius, mapJumpRange;
        readonly List<string> mapResourceIds = new List<string>();

        void OpenMap()
        {
            if (defense.Active || busy) return;
            if (mapOpen) { CloseMap(); return; }
            ClosePopup(); CloseModal(); ExitBuild();
            mapOpen = true; selected = gs.planet; mapAutopilot = false; mapResource = ""; mapZoom = 2f;
            surveyCenters.Clear();
            var seen = new HashSet<SystemKey>();
            foreach (var record in gs.world.All)
            {
                PlanetKey key;
                StarSystem star;
                if (record.visits > 0 && PlanetKey.TryParse(record.planetId, out key) && seen.Add(key.System) && gs.universe.Universe.TryGetSystem(key.System, out star))
                    surveyCenters.Add(star);
            }
            if (seen.Add(gs.planet.Star.Key)) surveyCenters.Add(gs.planet.Star);
            mapView = UiKit.Div("fr-map");
            mapLayer.Add(mapView);
            mapWorld = UiKit.Div("fr-map-viewport");
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
            var home = Icons.IconButton("map", UiKit.Cyan, () => { SelectMapWorld(gs.planet); CenterOn(gs.planet); });
            home.tooltip = "Recenter on your ship"; mapTools.Add(home);
            mapView.Add(mapTools);
            BuildMapSearch();
            BuildMapNodes();
            InvalidateMapPreview();
            BuildMapCard();
            LayoutMapChrome();
            viewport.Center(gs.planet.Star.X, gs.planet.Star.Y);
            ApplyMapTransform();
        }

        void CloseMap()
        {
            ResetMapPointer();
            if (mapView != null) mapView.RemoveFromHierarchy();
            mapView = null; mapWorld = null; mapCard = null; mapTools = null;
            mapOpen = false; lastPinch = 0f; mapSnapshot = null;
            mapVersion++; previewVersion++;
            mapLabels.Clear(); mapPlanets.Clear();
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
                mapWorld.style.top = 340 + inset.y;
                float cardHeight = mapCard != null ? mapCard.layout.height : 0;
                mapWorld.style.bottom = (float.IsNaN(cardHeight) ? 0 : cardHeight) + 32 + inset.w;
            }
        }

        Vector2 ViewCenter() { return new Vector2((float)viewport.Width * 0.5f, (float)viewport.Height * 0.5f); }
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

        bool Surveyed(StarSystem star)
        {
            if (star.IsAuthored) return true;
            double radius = mapSurveyRadius;
            foreach (var center in surveyCenters)
            {
                double dx = star.X - center.X, dy = star.Y - center.Y;
                if (dx * dx + dy * dy <= radius * radius) return true;
            }
            return false;
        }

        void PickMap(Vector2 at)
        {
            if (suppressMapClick) { suppressMapClick = false; return; }
            if (viewport.Scale >= 8)
                foreach (var planet in mapPlanets)
                    if (Vector2.Distance(PlanetPoint(planet), at) < 30) { SelectMapWorld(planet); return; }
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
            selected = planet;
            BuildMapNodes(); InvalidateMapPreview(); BuildMapCard();
            mapWorld.MarkDirtyRepaint();
        }

        void InvalidateMapPreview()
        {
            mapPreview = null; previewVersion++;
            var caps = gs.Ship;
            mapSurveyRadius = gs.Rules.ScannerRangeLy(caps);
            mapJumpRange = gs.Rules.JumpRangeLy(caps);
        }

        void BuildMapNodes()
        {
            mapPlanets.Clear();
            if (selected != null) mapPlanets.AddRange(gs.universe.PlanetsOf(selected.Star));
            PositionMapLabels();
        }

        Vector2 PlanetPoint(PlanetData planet)
        {
            Vector2 center = Project(planet.Star.X, planet.Star.Y);
            double maxOrbit = 1;
            foreach (var p in mapPlanets) maxOrbit = Math.Max(maxOrbit, p.Descriptor.OrbitRadius);
            float radius = 26 + (float)(planet.Descriptor.OrbitRadius / maxOrbit) * Mathf.Min((float)viewport.Width, (float)viewport.Height) * 0.25f;
            return center + new Vector2(Mathf.Cos(planet.Descriptor.OrbitPhase), -Mathf.Sin(planet.Descriptor.OrbitPhase)) * radius;
        }

        void PositionMapLabels()
        {
            if (mapWorld == null) return;
            int used = 0;
            mapLabelBounds.Clear();
            if (viewport.Scale >= 8)
            {
                foreach (var planet in mapPlanets)
                {
                    Vector2 p = PlanetPoint(planet);
                    PlaceMapLabel(ref used, planet.Name, p + new Vector2(-70, 22), 140, planet.Id == gs.planet.Id ? UiKit.Cyan : Color.white);
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

        void PlaceMapLabel(ref int used, string text, Vector2 at, float width, Color color)
        {
            var bounds = new Rect(at.x, at.y, width, 40);
            if (bounds.xMin < 0 || bounds.yMin < 0 || bounds.xMax > viewport.Width || bounds.yMax > viewport.Height) return;
            foreach (var previous in mapLabelBounds) if (previous.Overlaps(bounds)) return;
            mapLabelBounds.Add(bounds);
            if (used == mapLabels.Count)
            {
                var label = UiKit.Text("", "fr-tiny");
                label.pickingMode = PickingMode.Ignore; label.style.position = Position.Absolute;
                mapLabels.Add(label); mapWorld.Add(label);
            }
            var l = mapLabels[used++];
            l.text = text; l.style.color = color; l.style.left = at.x; l.style.top = at.y; l.style.width = width; l.style.display = DisplayStyle.Flex;
        }

        void DrawMapBackground(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            if (mapSnapshot != null)
            {
                DrawMapDensity(p);
                foreach (var star in mapSnapshot.systems)
                {
                    Vector2 at = Project(star.X, star.Y);
                    if (at.x < -10 || at.y < -10 || at.x > viewport.Width + 10 || at.y > viewport.Height + 10) continue;
                    bool surveyed = Surveyed(star), current = star.Key == gs.planet.Star.Key;
                    p.fillColor = current ? UiKit.Cyan : surveyed ? new Color(star.Color.R, star.Color.G, star.Color.B) : new Color(0.25f, 0.32f, 0.42f);
                    p.BeginPath(); p.Arc(at, current ? 7 : star.IsAuthored ? 5 : 3, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                }
            }
            Vector2 here = Project(gs.planet.Star.X, gs.planet.Star.Y);
            p.strokeColor = new Color(0.2f, 0.9f, 1, 0.22f); p.lineWidth = 1;
            float range = (float)(mapJumpRange * viewport.Scale);
            if (range > 0 && range < 12000)
            {
                p.BeginPath(); p.Arc(here, range, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            }
            if (viewport.Scale >= 8 && selected != null)
            {
                Vector2 center = Project(selected.Star.X, selected.Star.Y);
                foreach (var planet in mapPlanets)
                {
                    Vector2 at = PlanetPoint(planet);
                    p.strokeColor = new Color(0.3f, 0.65f, 0.9f, 0.15f); p.lineWidth = 1;
                    p.BeginPath(); p.Arc(center, (at - center).magnitude, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
                    p.fillColor = UiKit.Ui(planet.Primary);
                    p.BeginPath(); p.Arc(at, planet.Id == selected.Id ? 13 : 9, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                }
            }
            DrawMapRoute(p);
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
                if (leg.intraSystem && viewport.Scale >= 8) { a = PlanetPoint(gs.planet); b = PlanetPoint(selected); }
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
            bar.style.top = 140 + inset.y; bar.style.left = 16 + inset.x; bar.style.right = 16 + inset.z;
            mapSearch = new TextField { tooltip = "Known system/world name, canonical id, or x,y light-years" };
            mapSearch.textEdition.placeholder = "World / system name or x,y";
            mapSearch.style.flexGrow = 1;
            mapSearch.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return) { SearchMap(); e.StopPropagation(); } });
            bar.Add(mapSearch); bar.Add(UiKit.Btn("FIND", UiKit.Cyan, SearchMap));
            mapView.Add(bar);
            var filter = new DropdownField();
            filter.AddToClassList("fr-map-filter");
            filter.tooltip = "Filter systems by surveyed deposits";
            filter.choices = new List<string> { "All deposits" }; mapResourceIds.Clear(); mapResourceIds.Add("");
            foreach (var r in gs.db.resources) { filter.choices.Add(r.displayName); mapResourceIds.Add(r.id); }
            filter.index = 0;
            filter.RegisterValueChangedCallback(e => { mapResource = mapResourceIds[filter.index]; mapVersion++; mapQueryAt = 0; });
            filter.style.position = Position.Absolute; filter.style.left = 16 + inset.x; filter.style.top = 260 + inset.y; filter.style.width = 260;
            mapView.Add(filter);
            mapReadout = UiKit.Text("", "fr-tiny", UiKit.Muted);
            mapReadout.style.position = Position.Absolute; mapReadout.style.left = 296 + inset.x; mapReadout.style.right = 16 + inset.z; mapReadout.style.top = 266 + inset.y;
            mapView.Add(mapReadout);
            mapNotice = UiKit.Text("STAR MAP\nDrag · pinch to zoom", "fr-tiny", UiKit.Muted);
            mapNotice.style.position = Position.Absolute; mapNotice.style.left = 16 + inset.x; mapNotice.style.top = 24 + inset.y; mapNotice.style.width = 260;
            mapView.Add(mapNotice);
        }

        void SearchMap()
        {
            string text = mapSearch.value.Trim();
            if (text.Length == 0) { mapNotice.text = "Enter a world name or x,y"; return; }
            string[] xy = text.Split(',');
            double x, y;
            if (xy.Length == 2 && double.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && double.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                && !double.IsNaN(x) && !double.IsNaN(y) && Math.Abs(x) <= 1e7 && Math.Abs(y) <= 1e7)
            {
                viewport.Center(x, y); ApplyMapTransform(); mapNotice.text = "Coordinates in light-years · zoom out to explore"; return;
            }
            var byPlanetId = gs.universe.Get(text);
            if (byPlanetId != null) { SelectMapWorld(byPlanetId); CenterOn(byPlanetId); return; }
            foreach (var world in gs.universe.AuthoredPlanets)
                if (string.Equals(world.Name, text, StringComparison.OrdinalIgnoreCase) || world.Id == text || world.Authored.id == text)
                { SelectMapWorld(world); CenterOn(world); return; }
            foreach (var record in gs.world.All)
            {
                var world = gs.universe.Get(record.planetId);
                if (world != null && (string.Equals(world.Name, text, StringComparison.OrdinalIgnoreCase) || world.Id == text))
                { SelectMapWorld(world); CenterOn(world); return; }
            }
            StarSystem byId;
            if (gs.universe.Universe.TryGetSystemById(text, out byId))
            {
                var worlds = gs.universe.PlanetsOf(byId);
                if (worlds.Count > 0) { SelectMapWorld(worlds[0]); CenterOn(selected); return; }
            }
            if (mapSnapshot != null)
                foreach (var star in mapSnapshot.systems)
                    if (string.Equals(star.Name, text, StringComparison.OrdinalIgnoreCase) || star.Id == text || string.Equals(star.Designation, text, StringComparison.OrdinalIgnoreCase))
                    { var worlds = gs.universe.PlanetsOf(star); if (worlds.Count > 0) { SelectMapWorld(worlds[0]); CenterOn(selected); return; } }
            mapNotice.text = "Not found · use an exact Core World / visible system name or x,y";
        }
    }
}
