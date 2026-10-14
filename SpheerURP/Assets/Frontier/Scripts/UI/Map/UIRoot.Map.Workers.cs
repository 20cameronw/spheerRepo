using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        MapPoint[] SurveyPoints()
        {
            var points = new MapPoint[surveyCenters.Count];
            for (int i = 0; i < points.Length; i++) points[i] = new MapPoint(surveyCenters[i].X, surveyCenters[i].Y);
            return points;
        }

        void PollMapWorkers()
        {
            if (mapQuery != null && mapQuery.IsCompleted)
            {
                if (mapQuery.IsFaulted) { Debug.LogException(mapQuery.Exception); mapNotice.text = "Map unavailable · pan or zoom to retry"; }
                else if (queryVersion == mapVersion)
                {
                    mapSnapshot = mapQuery.Result;
                    PositionMapLabels();
                    mapWorld.MarkDirtyRepaint();
                    mapReadout.text = (mapSnapshot.densityOnly ? "Galaxy · zoom in for stars" : viewport.Scale >= 8 ? "System · orbits schematic" : "Sector · " + mapSnapshot.systems.Length + " stars")
                        + (mapSnapshot.truncated ? " · zoom in for more" : "");
                }
                mapQuery = null;
            }
            if (mapQuery == null && queryVersion != mapVersion && Time.unscaledTime >= mapQueryAt && viewport.Width > 0 && viewport.Height > 0)
            {
                queryVersion = mapVersion;
                MapPoint a = viewport.Unproject(0, viewport.Height), b = viewport.Unproject(viewport.Width, 0);
                var universe = gs.universe.Universe;
                var points = SurveyPoints();
                double radius = gs.ScannerRangeLy;
                string resource = mapResource;
                mapQuery = Task.Run(() => MapQuery.Build(universe, a.x, a.y, b.x, b.y, resource, points, radius));
            }
            if (mapPreviewTask != null && mapPreviewTask.IsCompleted)
            {
                if (mapPreviewTask.IsFaulted)
                {
                    Debug.LogException(mapPreviewTask.Exception);
                    if (runningPreviewVersion == previewVersion) mapPreview = RoutePlan.Blocked(RouteReasons.Unknown);
                }
                else if (runningPreviewVersion == previewVersion) mapPreview = mapPreviewTask.Result;
                mapPreviewTask = null;
                if (mapPreview != null) { BuildMapCard(); mapWorld.MarkDirtyRepaint(); }
            }
            if (mapPreviewTask == null && mapPreview == null && selected != null)
            {
                runningPreviewVersion = previewVersion;
                var planner = gs.universe.NewPlanner();
                var from = gs.universe.EndpointOf(gs.planet); var to = gs.universe.EndpointOf(selected);
                var caps = gs.Ship; var stock = gs.FuelOnBoard; bool auto = mapAutopilot;
                mapPreviewTask = Task.Run(() => planner.Plan(from, to, caps, auto, stock));
            }
        }

        void DrawMapDensity(Painter2D p)
        {
            foreach (var cell in mapSnapshot.density)
            {
                if (cell.density < 0.02f) continue;
                Vector2 a = Project(cell.x, cell.y), b = Project(cell.x + cell.width, cell.y + cell.height);
                p.fillColor = new Color(0.12f, 0.3f, 0.55f, Mathf.Min(0.35f, cell.density * 0.3f));
                p.BeginPath(); p.MoveTo(a); p.LineTo(new Vector2(b.x, a.y)); p.LineTo(b); p.LineTo(new Vector2(a.x, b.y)); p.ClosePath(); p.Fill();
            }
        }
    }
}
