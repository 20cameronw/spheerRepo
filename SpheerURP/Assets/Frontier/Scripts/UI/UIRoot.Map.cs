using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        const float MapSize = 9000f;
        const float MinZoom = 0.07f, MaxZoom = 2.4f;

        PlanetData selected;
        bool mapOpen;
        VisualElement mapView, mapWorld, mapCard, mapTools;
        Vector2 mapPan;
        float mapZoom = 0.7f;
        int dragPointer = -1;
        Vector2 dragStart, dragLast;
        bool mapDragging;
        float lastPinch;
        readonly List<Vector2> stars = new List<Vector2>();

        void OpenMap()
        {
            if (defense.Active || busy) return;
            if (mapOpen) { CloseMap(); return; }
            ClosePopup(); CloseModal(); ExitBuild();
            mapOpen = true;
            selected = gs.planet;
            mapZoom = 0.7f;

            if (stars.Count == 0)
            {
                var rng = new System.Random(99);
                for (int i = 0; i < 350; i++) stars.Add(new Vector2((float)rng.NextDouble() * MapSize, (float)rng.NextDouble() * MapSize));
                for (int galaxy = 0; galaxy < 3; galaxy++)
                    for (int i = 0; i < 900; i++)
                    {
                        float radius = Mathf.Sqrt((float)rng.NextDouble()) * (galaxy == 1 ? 1400f : 1700f);
                        float angle = radius * 0.0024f + (i % (galaxy + 2)) * Mathf.PI * 2f / (galaxy + 2);
                        float spread = ((float)rng.NextDouble() - 0.5f) * 0.5f;
                        stars.Add(GalaxyCenter(galaxy) + new Vector2(Mathf.Cos(angle + spread), Mathf.Sin(angle + spread) * 0.65f) * radius);
                    }
            }

            mapView = UiKit.Div("fr-map");
            mapView.pickingMode = PickingMode.Position;
            mapLayer.Add(mapView);

            mapWorld = UiKit.Div("fr-map-world");
            mapWorld.style.width = MapSize; mapWorld.style.height = MapSize;
            mapWorld.pickingMode = PickingMode.Position;
            mapWorld.generateVisualContent += DrawMapBackground;
            mapView.Add(mapWorld);

            mapView.RegisterCallback<PointerDownEvent>(OnMapDown);
            mapView.RegisterCallback<PointerMoveEvent>(OnMapMove);
            mapView.RegisterCallback<PointerUpEvent>(OnMapUp);
            mapView.RegisterCallback<WheelEvent>(e =>
            {
                ZoomAt(e.localMousePosition, Mathf.Exp(-e.delta.y * 0.08f));
                e.StopPropagation();
            });
            mapView.RegisterCallback<GeometryChangedEvent>(e => { ApplyMapTransform(); });

            mapTools = UiKit.Div("fr-map-tools"); mapTools.pickingMode = PickingMode.Ignore;
            mapTools.Add(Icons.IconButton("close", UiKit.Muted, CloseMap));
            mapTools.Add(Icons.IconButton("plus", UiKit.Muted, () => ZoomAt(ViewCenter(), 1.3f)));
            mapTools.Add(Icons.IconButton("minus", UiKit.Muted, () => ZoomAt(ViewCenter(), 1f / 1.3f)));
            mapView.Add(mapTools);
            LayoutMapChrome();

            BuildMapNodes();
            BuildMapCard();

            // Center on the current planet once the viewport has a size.
            mapView.schedule.Execute(() => { CenterOn(gs.planet); }).StartingIn(30);
        }

        void CloseMap()
        {
            if (mapView != null) mapView.RemoveFromHierarchy();
            mapView = null; mapWorld = null; mapCard = null; mapTools = null;
            mapOpen = false; dragPointer = -1; lastPinch = 0f;
        }

        void LayoutMapChrome()
        {
            if (mapTools == null) return;
            mapTools.style.right = UiKit.Space.S16 + inset.z;
            mapTools.style.top = UiKit.Space.S16 + inset.y;
            if (mapCard != null) mapCard.style.marginBottom = UiKit.Space.S16 + inset.w;
        }

        Vector2 ViewCenter()
        {
            return new Vector2(mapView.resolvedStyle.width * 0.5f, mapView.resolvedStyle.height * 0.5f);
        }

        static Vector2 GalaxyCenter(int galaxy)
        {
            return galaxy == 0 ? new Vector2(2200f, 2400f) : galaxy == 1 ? new Vector2(6500f, 2100f) : new Vector2(5600f, 6800f);
        }

        static int SystemIndex(string id)
        {
            for (int i = 0; i < PlanetSO.Systems.Length; i++) if (PlanetSO.Systems[i].id == id) return i;
            return -1;
        }

        static Vector2 SystemPoint(PlanetSO.StarSystem system)
        {
            int i = SystemIndex(system.id);
            Vector2 offset = i % 2 == 0 ? new Vector2(-620f, -260f) : new Vector2(570f, 350f);
            return GalaxyCenter(i / 2) + offset;
        }

        static Vector2 MapPoint(PlanetData planet)
        {
            var so = planet.Authored;
            if (so == null) return GeneratedMapPoint(planet);
            var system = PlanetSO.SystemOf(so.systemId);
            if (system == null) return new Vector2(so.mapPosition.x, 1f - so.mapPosition.y) * MapSize;
            Vector2 offset = so.mapPosition - system.center;
            return SystemPoint(system) + new Vector2(offset.x, -offset.y) * 6200f;
        }

        /// <summary>Map pixels per light-year for the marker of a generated world (the full star map for generated space comes later).</summary>
        const float GeneratedPxPerLy = 3.5f;
        const float GeneratedMarkerMargin = 320f;

        // A generated world is shown as a single marker: its light-year offset from the home system, scaled onto the old map and kept inside it.
        static Vector2 GeneratedMapPoint(PlanetData planet)
        {
            Vector2 home = SystemPoint(PlanetSO.Systems[0]);
            Vector2 point = home + new Vector2((float)planet.Star.X, -(float)planet.Star.Y) * GeneratedPxPerLy;
            float lo = GeneratedMarkerMargin, hi = MapSize - GeneratedMarkerMargin;
            return new Vector2(Mathf.Clamp(point.x, lo, hi), Mathf.Clamp(point.y, lo, hi));
        }

        static PlanetSO.StarSystem SystemOfPlanet(PlanetData planet)
        {
            return planet.Authored != null ? PlanetSO.SystemOf(planet.Authored.systemId) : null;
        }

        static Vector2 RoutePoint(PlanetData from, PlanetData to, float time)
        {
            Vector2 a = MapPoint(from), b = MapPoint(to);
            var fromSystem = SystemOfPlanet(from);
            var toSystem = SystemOfPlanet(to);
            Vector2 ar = fromSystem != null ? a - SystemPoint(fromSystem) : a - b;
            Vector2 br = toSystem != null ? b - SystemPoint(toSystem) : b - a;
            Vector2 ac = a - ar, bc = b - br;
            const float orbitAngle = 0.65f;
            if (time < 0.15f) return ac + RotateMapVector(ar, time / 0.15f * orbitAngle);
            if (time > 0.85f) return bc + RotateMapVector(br, -(1f - time) / 0.15f * orbitAngle);
            ar = RotateMapVector(ar, orbitAngle);
            br = RotateMapVector(br, -orbitAngle);
            a = ac + ar; b = bc + br;
            time = (time - 0.15f) / 0.7f;
            Vector2 startTangent = new Vector2(-ar.y, ar.x).normalized;
            Vector2 endTangent = new Vector2(-br.y, br.x).normalized;
            float bend = Mathf.Clamp(Vector2.Distance(a, b) * 0.4f, 180f, 1300f);
            Vector2 c1 = a + startTangent * bend, c2 = b - endTangent * bend;
            float u = 1f - time;
            return u * u * u * a + 3f * u * u * time * c1 + 3f * u * time * time * c2 + time * time * time * b;
        }

        static Vector2 RotateMapVector(Vector2 point, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(point.x * c - point.y * s, point.x * s + point.y * c);
        }

        void CenterOn(PlanetData p)
        {
            if (mapView == null) return;
            mapPan = ViewCenter() - MapPoint(p) * mapZoom;
            ApplyMapTransform();
        }

        void ZoomAt(Vector2 viewPoint, float factor)
        {
            if (mapView == null) return;
            float nz = Mathf.Clamp(mapZoom * factor, MinZoom, MaxZoom);
            Vector2 worldPt = (viewPoint - mapPan) / mapZoom;
            mapZoom = nz;
            mapPan = viewPoint - worldPt * mapZoom;
            ApplyMapTransform();
        }

        void ApplyMapTransform()
        {
            if (mapView == null || mapWorld == null) return;
            float vw = mapView.resolvedStyle.width, vh = mapView.resolvedStyle.height;
            if (!float.IsNaN(vw) && !float.IsNaN(vh))
            {
                float ws = MapSize * mapZoom;
                float slack = 200f;
                mapPan.x = ws >= vw ? Mathf.Clamp(mapPan.x, vw - ws - slack, slack) : (vw - ws) * 0.5f;
                mapPan.y = ws >= vh ? Mathf.Clamp(mapPan.y, vh - ws - slack, slack) : (vh - ws) * 0.5f;
            }
            mapWorld.style.translate = new Translate(mapPan.x, mapPan.y);
            mapWorld.style.scale = new Scale(new Vector3(mapZoom, mapZoom, 1f));
        }

        // ------------------------------------------------------------ pan input
        void OnMapDown(PointerDownEvent e)
        {
            if (dragPointer >= 0) return;
            dragPointer = e.pointerId;
            dragStart = dragLast = e.position;
            mapDragging = false;
        }

        void OnMapMove(PointerMoveEvent e)
        {
            if (e.pointerId != dragPointer) return;
            if (Input.touchCount >= 2) { dragLast = e.position; return; }
            Vector2 pos = e.position;
            if (!mapDragging && (pos - dragStart).magnitude > 12f)
            {
                mapDragging = true;
                mapView.CapturePointer(e.pointerId);
            }
            if (mapDragging)
            {
                mapPan += pos - dragLast;
                ApplyMapTransform();
            }
            dragLast = pos;
        }

        void OnMapUp(PointerUpEvent e)
        {
            if (e.pointerId != dragPointer) return;
            if (mapDragging && mapView.HasPointerCapture(e.pointerId)) mapView.ReleasePointer(e.pointerId);
            dragPointer = -1; mapDragging = false;
        }

        void UpdateMapInput()
        {
            if (!mapOpen || mapView == null) return;
            if (selected == null || selected.Id != gs.planet.Id) mapWorld.MarkDirtyRepaint();
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0).position; var b = Input.GetTouch(1).position;
                float d = Vector2.Distance(a, b);
                if (lastPinch > 1f && d > 1f)
                {
                    Vector2 mid = (a + b) * 0.5f;
                    Vector2 pp = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(mid.x, ScreenMetrics.Height - mid.y));
                    ZoomAt(mapView.WorldToLocal(pp), d / lastPinch);
                }
                lastPinch = d;
            }
            else lastPinch = 0f;
        }

        // ------------------------------------------------------------ drawing
        void DrawMapBackground(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.lineWidth = 2f;
            // Painter2D alphas look about twice as strong as authored here (linear colour space), so these are kept very low.
            p.strokeColor = new Color(0.1f, 0.9f, 1f, 0.025f);
            for (int i = 0; i <= 30; i++)
            {
                float v = i * 300f;
                p.BeginPath(); p.MoveTo(new Vector2(v, 0f)); p.LineTo(new Vector2(v, MapSize)); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(0f, v)); p.LineTo(new Vector2(MapSize, v)); p.Stroke();
            }
            p.fillColor = new Color(0.8f, 0.9f, 1f, 0.5f);
            foreach (var s in stars)
            {
                p.BeginPath();
                p.MoveTo(s); p.LineTo(s + new Vector2(3f, 0f)); p.LineTo(s + new Vector2(3f, 3f)); p.LineTo(s + new Vector2(0f, 3f));
                p.ClosePath(); p.Fill();
            }
            for (int galaxy = 0; galaxy < 3; galaxy++)
            {
                Color tint = galaxy == 0 ? UiKit.Cyan : galaxy == 1 ? UiKit.Magenta : UiKit.Amber;
                p.strokeColor = new Color(tint.r, tint.g, tint.b, 0.012f);
                p.lineWidth = 85f;
                for (int arm = 0; arm < galaxy + 2; arm++)
                {
                    p.BeginPath();
                    for (int i = 0; i < 100; i++)
                    {
                        float radius = i * 17f;
                        float angle = radius * 0.0024f + arm * Mathf.PI * 2f / (galaxy + 2);
                        Vector2 point = GalaxyCenter(galaxy) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.65f) * radius;
                        if (i == 0) p.MoveTo(point); else p.LineTo(point);
                    }
                    p.Stroke();
                }
            }
            foreach (var sys in PlanetSO.Systems)
            {
                Vector2 c = SystemPoint(sys);
                float maxR = 0f;
                foreach (var pl in gs.universe.AuthoredPlanets)
                {
                    if (pl.Authored.systemId != sys.id) continue;
                    float d = (MapPoint(pl) - c).magnitude;
                    if (d <= 900f) maxR = Mathf.Max(maxR, d); // far-flung members keep their orbit ring but do not inflate the halo
                }
                if (maxR <= 0f) continue;
                p.fillColor = new Color(sys.color.r, sys.color.g, sys.color.b, 0.012f);
                p.BeginPath(); p.Arc(c, maxR + 150f, Angle.Degrees(0f), Angle.Degrees(360f)); p.Fill();
                p.lineWidth = 3f;
                foreach (var pl in gs.universe.AuthoredPlanets)
                {
                    if (pl.Authored.systemId != sys.id) continue;
                    p.strokeColor = new Color(sys.color.r, sys.color.g, sys.color.b, 0.25f);
                    p.BeginPath(); p.Arc(c, (MapPoint(pl) - c).magnitude, Angle.Degrees(0f), Angle.Degrees(360f)); p.Stroke();
                }
                for (int k = 3; k >= 1; k--)
                {
                    p.fillColor = new Color(sys.color.r, sys.color.g, sys.color.b, 0.16f * (4 - k) / 3f);
                    p.BeginPath(); p.Arc(c, 28f + k * 22f, Angle.Degrees(0f), Angle.Degrees(360f)); p.Fill();
                }
                p.fillColor = Color.Lerp(sys.color, Color.white, 0.6f);
                p.BeginPath(); p.Arc(c, 30f, Angle.Degrees(0f), Angle.Degrees(360f)); p.Fill();
            }
            if (selected != null && selected.Id != gs.planet.Id)
            {
                p.lineWidth = 6f;
                p.strokeColor = new Color(1f, 0.25f, 0.95f, 0.9f);
                p.BeginPath(); p.MoveTo(MapPoint(gs.planet));
                for (int i = 1; i <= 100; i++) p.LineTo(RoutePoint(gs.planet, selected, i / 100f));
                p.Stroke();
                float time = Mathf.Repeat(Time.unscaledTime * 0.15f, 1f);
                Vector2 ship = RoutePoint(gs.planet, selected, time);
                Vector2 forward = (RoutePoint(gs.planet, selected, Mathf.Min(1f, time + 0.01f)) - ship).normalized;
                Vector2 side = new Vector2(-forward.y, forward.x);
                p.fillColor = Color.white;
                p.BeginPath(); p.MoveTo(ship + forward * 20f);
                p.LineTo(ship - forward * 12f + side * 10f); p.LineTo(ship - forward * 12f - side * 10f);
                p.ClosePath(); p.Fill();
            }
        }

        // ------------------------------------------------------------ nodes + card
        void BuildMapNodes()
        {
            for (int i = mapWorld.childCount - 1; i >= 0; i--) mapWorld.RemoveAt(i);
            string[] galaxies = { "THE VERDANT SPIRAL", "EMBER REACH", "THE FRACTURED EXPANSE" };
            for (int i = 0; i < galaxies.Length; i++)
            {
                var label = UiKit.Text(galaxies[i], "fr-title", new Color(0.7f, 0.8f, 0.9f, 0.65f));
                label.pickingMode = PickingMode.Ignore;
                label.style.position = Position.Absolute;
                label.style.left = GalaxyCenter(i).x - 600f;
                label.style.top = GalaxyCenter(i).y - 1250f;
                mapWorld.Add(label);
            }
            foreach (var sys in PlanetSO.Systems)
            {
                var lbl = UiKit.Text(sys.name.ToUpper(), "fr-small", UiKit.Ui(sys.color));
                lbl.pickingMode = PickingMode.Ignore;
                lbl.style.position = Position.Absolute;
                lbl.style.left = SystemPoint(sys).x - 200f;
                lbl.style.top = SystemPoint(sys).y + 70f;
                lbl.style.width = 400f;
                lbl.style.unityTextAlign = TextAnchor.UpperCenter;
                mapWorld.Add(lbl);
            }
            foreach (var pl in gs.universe.AuthoredPlanets) AddMapNode(pl);
            // A generated world is not part of the hand made map: it only gets a marker, and only while the player is on it.
            if (!gs.planet.IsAuthored) AddMapNode(gs.planet);
        }

        void AddMapNode(PlanetData planet)
        {
            var node = UiKit.Div("fr-map-node");
            Vector2 mp = MapPoint(planet);
            node.style.left = mp.x; node.style.top = mp.y;
            node.style.backgroundColor = UiKit.Ui(planet.Primary);
            bool cur = planet.Id == gs.planet.Id, sel = selected != null && planet.Id == selected.Id;
            UiKit.Border(node, cur ? Color.white : sel ? UiKit.Magenta : UiKit.Ui(planet.Rim, 0.8f), cur || sel ? 6f : 3f);
            UiKit.Halo(node, UiKit.Ui(planet.Rim), 60f, sel ? 0.7f : 0.3f);
            node.RegisterCallback<ClickEvent>(e =>
            {
                Sfx.Click();
                selected = planet;
                mapView.schedule.Execute(() => { BuildMapNodes(); BuildMapCard(); mapWorld.MarkDirtyRepaint(); }).StartingIn(1);
            });
            if (gs.HasBase(planet))
            {
                var b = Icons.Make("base", UiKit.Lime, 52f);
                b.style.position = Position.Absolute; b.style.left = 29; b.style.top = -46;
                node.Add(b);
            }
            var name = UiKit.Text(planet.Name, "fr-small");
            name.style.position = Position.Absolute; name.style.top = 116; name.style.left = -45; name.style.width = 200;
            name.style.unityTextAlign = TextAnchor.UpperCenter;
            node.Add(name);
            mapWorld.Add(node);
        }

        void BuildMapCard()
        {
            if (mapCard != null) mapCard.RemoveFromHierarchy();
            var p2 = selected ?? gs.planet;
            Color pc = UiKit.Ui(p2.Rim);
            mapCard = UiKit.Div("fr-map-card");
            UiKit.SetAccent(mapCard, pc);
            var top = UiKit.Div("fr-row");
            top.Add(UiKit.Text(p2.Name.ToUpper(), "fr-h2", pc));
            top.Add(UiKit.Div("fr-spacer"));
            var skull = Icons.Make("skull", UiKit.Red, UiKit.Size.IconSm);
            skull.style.marginRight = UiKit.Space.S8;
            top.Add(skull);
            top.Add(UiKit.Text(p2.Danger.ToString("0.0"), "fr-small"));
            mapCard.Add(top);
            mapCard.Add(UiKit.Text(p2.Description, "fr-tiny"));
            if (!p2.IsAuthored)
            {
                var kind = UiKit.Text(p2.Star.Name + "  -  " + p2.BiomeName + ", " + p2.SizeName.ToLower() + "  -  tier " + p2.Tier, "fr-tiny", UiKit.Cyan);
                kind.style.marginTop = UiKit.Space.S4;
                mapCard.Add(kind);
            }
            var slots = UiKit.Text(p2.BuildingSlots + " building slots  -  world size x" + p2.WorldSize.ToString("0.00"), "fr-small");
            slots.style.marginTop = UiKit.Space.S8;
            mapCard.Add(slots);

            mapCard.Add(SectionLabel("AVAILABLE RESOURCES"));
            var res = UiKit.Div("fr-row-wrap");
            foreach (var resource in gs.ResourcesFor(p2))
                res.Add(ResourceIconChip(resource, resource.displayName));
            if (gs.HasBase(p2)) res.Add(UiKit.Chip("base", UiKit.Lime, "BASE"));
            mapCard.Add(res);

            if (p2.Id == gs.planet.Id)
            {
                var here = UiKit.Text("You are here", "fr-small", UiKit.Cyan);
                here.style.marginTop = UiKit.Space.S12;
                mapCard.Add(here);
            }
            else
            {
                int cost = gs.FuelCost(p2);
                bool can = gs.Count(gs.db.fuel) >= cost;
                var row = UiKit.Div("fr-row-wrap");
                row.style.marginTop = UiKit.Space.S8;
                row.Add(ResourceIconChip(gs.db.fuel, cost + " / " + gs.Count(gs.db.fuel), !can));
                mapCard.Add(row);
                var go = IconBtn(can ? "WARP" : "NEED FUEL", "warp", can ? UiKit.Magenta : UiKit.Red, () => StartTravel(p2), true, can);
                UiKit.SetEnabledLook(go, can);
                mapCard.Add(go);
            }
            mapView.Add(mapCard);
            LayoutMapChrome();
        }
    }
}
