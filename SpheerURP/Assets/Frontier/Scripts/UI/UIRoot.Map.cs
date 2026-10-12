using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        const float MapSize = 4000f;
        const float MinZoom = 0.14f, MaxZoom = 2.4f;

        PlanetSO selected;
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
                for (int i = 0; i < 600; i++) stars.Add(new Vector2((float)rng.NextDouble() * MapSize, (float)rng.NextDouble() * MapSize));
            }

            mapView = UiKit.Div("fr-map");
            mapView.pickingMode = PickingMode.Position;
            mapLayer.Add(mapView);

            mapWorld = UiKit.Div("fr-map-world");
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
            mapTools.Add(Icons.IconButton("close", UiKit.Red, CloseMap, 96f));
            mapTools.Add(Icons.IconButton("plus", UiKit.Cyan, () => ZoomAt(ViewCenter(), 1.3f), 96f));
            mapTools.Add(Icons.IconButton("minus", UiKit.Cyan, () => ZoomAt(ViewCenter(), 1f / 1.3f), 96f));
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
            mapTools.style.right = 16 + inset.z;
            mapTools.style.top = 16 + inset.y;
            if (mapCard != null) mapCard.style.marginBottom = 16 + inset.w;
        }

        Vector2 ViewCenter()
        {
            return new Vector2(mapView.resolvedStyle.width * 0.5f, mapView.resolvedStyle.height * 0.5f);
        }

        static Vector2 MapPoint(PlanetSO p) { return new Vector2(p.mapPosition.x * MapSize, (1f - p.mapPosition.y) * MapSize); }

        void CenterOn(PlanetSO p)
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
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0).position; var b = Input.GetTouch(1).position;
                float d = Vector2.Distance(a, b);
                if (lastPinch > 1f && d > 1f)
                {
                    Vector2 mid = (a + b) * 0.5f;
                    Vector2 pp = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(mid.x, Screen.height - mid.y));
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
            p.strokeColor = new Color(0.1f, 0.9f, 1f, 0.07f);
            for (int i = 0; i <= 40; i++)
            {
                float v = i * 100f;
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
            foreach (var sys in PlanetSO.Systems)
            {
                Vector2 c = new Vector2(sys.center.x * MapSize, (1f - sys.center.y) * MapSize);
                float maxR = 0f;
                foreach (var pl in gs.db.planets)
                    if (pl.systemId == sys.id) maxR = Mathf.Max(maxR, (MapPoint(pl) - c).magnitude);
                if (maxR <= 0f) continue;
                p.fillColor = new Color(sys.color.r, sys.color.g, sys.color.b, 0.05f);
                p.BeginPath(); p.Arc(c, maxR + 150f, Angle.Degrees(0f), Angle.Degrees(360f)); p.Fill();
                p.lineWidth = 3f;
                foreach (var pl in gs.db.planets)
                {
                    if (pl.systemId != sys.id) continue;
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
            if (selected != null && selected != gs.planet)
            {
                p.lineWidth = 6f;
                p.strokeColor = new Color(1f, 0.25f, 0.95f, 0.9f);
                p.BeginPath(); p.MoveTo(MapPoint(gs.planet)); p.LineTo(MapPoint(selected)); p.Stroke();
            }
        }

        // ------------------------------------------------------------ nodes + card
        void BuildMapNodes()
        {
            for (int i = mapWorld.childCount - 1; i >= 0; i--) mapWorld.RemoveAt(i);
            foreach (var sys in PlanetSO.Systems)
            {
                var lbl = UiKit.Text(sys.name.ToUpper(), "fr-small", UiKit.Ui(sys.color));
                lbl.pickingMode = PickingMode.Ignore;
                lbl.style.position = Position.Absolute;
                lbl.style.left = sys.center.x * MapSize - 200f;
                lbl.style.top = (1f - sys.center.y) * MapSize + 70f;
                lbl.style.width = 400f;
                lbl.style.unityTextAlign = TextAnchor.UpperCenter;
                mapWorld.Add(lbl);
            }
            foreach (var pl in gs.db.planets)
            {
                var planet = pl;
                var node = UiKit.Div("fr-map-node");
                Vector2 mp = MapPoint(planet);
                node.style.left = mp.x; node.style.top = mp.y;
                node.style.backgroundColor = UiKit.Ui(planet.primary);
                bool cur = planet == gs.planet, sel = planet == selected;
                UiKit.Border(node, cur ? Color.white : sel ? UiKit.Magenta : UiKit.Ui(planet.rim, 0.8f), cur || sel ? 6f : 3f);
                UiKit.Halo(node, UiKit.Ui(planet.rim), 60f, sel ? 0.7f : 0.4f);
                node.RegisterCallback<ClickEvent>(e =>
                {
                    Sfx.Click();
                    selected = planet;
                    mapView.schedule.Execute(() => { BuildMapNodes(); BuildMapCard(); mapWorld.MarkDirtyRepaint(); }).StartingIn(1);
                });
                bool hasBase = gs.structures.Exists(s => s.planetId == planet.id);
                if (hasBase)
                {
                    var b = Icons.Make("base", UiKit.Lime, 52f);
                    b.style.position = Position.Absolute; b.style.left = 29; b.style.top = -46;
                    node.Add(b);
                }
                var name = UiKit.Text(planet.displayName, "fr-small");
                name.style.position = Position.Absolute; name.style.top = 116; name.style.left = -45; name.style.width = 200;
                name.style.unityTextAlign = TextAnchor.UpperCenter;
                node.Add(name);
                mapWorld.Add(node);
            }
        }

        void BuildMapCard()
        {
            if (mapCard != null) mapCard.RemoveFromHierarchy();
            var p2 = selected ?? gs.planet;
            Color pc = UiKit.Ui(p2.rim);
            mapCard = UiKit.Div("fr-map-card");
            UiKit.Border(mapCard, pc, 3f);
            UiKit.Halo(mapCard, pc, 28f, 0.2f);
            var top = UiKit.Div("fr-row");
            top.Add(UiKit.Text(p2.displayName.ToUpper(), "fr-h2", pc));
            top.Add(UiKit.Div("fr-spacer"));
            top.Add(Icons.Make("skull", UiKit.Red, 32f));
            top.Add(UiKit.Text(p2.danger.ToString("0.0"), "fr-small"));
            mapCard.Add(top);
            mapCard.Add(UiKit.Text(p2.description, "fr-tiny"));

            mapCard.Add(UiKit.Text("AVAILABLE RESOURCES", "fr-small"));
            var res = UiKit.Div("fr-row-wrap");
            foreach (var resource in gs.ResourcesFor(p2))
                res.Add(ResourceIconChip(resource, resource.displayName));
            if (gs.structures.Exists(s => s.planetId == p2.id))
            {
                var bc = UiKit.Div("fr-itemchip");
                UiKit.Border(bc, UiKit.Ui(UiKit.Lime, 0.8f), 2f);
                bc.Add(Icons.Make("base", UiKit.Lime));
                res.Add(bc);
            }
            mapCard.Add(res);

            if (p2 == gs.planet) mapCard.Add(UiKit.Text("You are here", "fr-small", UiKit.Cyan));
            else
            {
                int cost = gs.FuelCost(p2);
                bool can = gs.Count(gs.db.fuel) >= cost;
                var row = UiKit.Div("fr-row-wrap");
                row.Add(ResourceIconChip(gs.db.fuel, cost + " / " + gs.Count(gs.db.fuel), !can));
                mapCard.Add(row);
                var go = IconBtn(can ? "WARP" : "NEED FUEL", "warp", can ? UiKit.Magenta : UiKit.Red, () => StartTravel(p2), true);
                UiKit.SetEnabledLook(go, can);
                mapCard.Add(go);
            }
            mapView.Add(mapCard);
            LayoutMapChrome();
        }
    }
}
