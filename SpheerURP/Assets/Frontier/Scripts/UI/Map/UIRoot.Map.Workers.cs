using System.Threading.Tasks;
using Frontier.Universe;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        MapSurveyPoint[] SurveyPoints()
        {
            var points = new MapSurveyPoint[surveyCenters.Count];
            for (int i = 0; i < points.Length; i++)
                points[i] = new MapSurveyPoint(surveyCenters[i].X, surveyCenters[i].Y, surveyRadii[i]);
            return points;
        }

        void PollMapWorkers()
        {
            if (mapQuery != null && mapQuery.IsCompleted)
            {
                if (mapQuery.IsFaulted) { Debug.LogException(mapQuery.Exception); SetMapNotice(UiText.Get("map.unavailable")); }
                else if (queryVersion == mapVersion)
                {
                    mapSnapshot = mapQuery.Result;
                    gs.RecordMapDiscoveries(mapSnapshot.surveyedSystems);
                    foreach (var star in mapSnapshot.surveyedSystems) mapRememberedSystems.Add(star.Id);
                    RefreshMapProjectMatches();
                    if (mapNameIndex != null) mapNameIndex.AddSystems(gs.universe.Universe, mapSnapshot.surveyedSystems);
                    BuildMapNodes();
                    mapWorld.MarkDirtyRepaint();
                    string readout = SystemMapView ? UiText.Get("map.view.system")
                        : NeighborMapView ? UiText.Get("map.view.neighbors")
                        : mapSnapshot.densityOnly ? UiText.Get("map.view.galaxy")
                        : UiText.Get("map.view.sector", mapSnapshot.systems.Length);
                    mapReadout.text = mapSnapshot.truncated ? UiText.Get("map.view.truncated", readout) : readout;
                }
                mapQuery = null;
            }
            if (mapQuery == null && queryVersion != mapVersion && Time.unscaledTime >= mapQueryAt && viewport.Width > 0 && viewport.Height > 0)
            {
                queryVersion = mapVersion;
                mapQueryBounds = new MapQueryBounds(viewport);
                mapQueryScale = viewport.Scale;
                mapQueryBoundsKnown = true;
                MapPoint a = mapQueryBounds.min, b = mapQueryBounds.max;
                var universe = gs.universe.Universe;
                var points = SurveyPoints();
                var discovered = KnownMapSystems();
                string resource = mapResource == ProjectMapFilter ? "" : mapResource;
                double visibleSpan = System.Math.Max(viewport.Width, viewport.Height) / viewport.Scale;
                mapQuery = Task.Run(() => MapQuery.Build(universe, a.x, a.y, b.x, b.y, resource, points, discovered, visibleSpan));
            }
            if (mapPreviewTask != null && mapPreviewTask.IsCompleted)
            {
                if (mapPreviewTask.IsFaulted)
                {
                    Debug.LogException(mapPreviewTask.Exception);
                    if (runningPreviewVersion == previewVersion)
                    {
                        mapPreview = RoutePlan.Blocked(RouteReasons.Unknown);
                        mapNextStopPlanetId = null;
                    }
                }
                else if (runningPreviewVersion == previewVersion)
                {
                    MapPreviewResult result = mapPreviewTask.Result;
                    mapPreview = result.plan;
                    mapNextStopPlanetId = result.nextStopPlanetId;
                }
                mapPreviewTask = null;
                if (mapPreview != null) { BuildMapCard(); mapWorld.MarkDirtyRepaint(); }
            }
            if (mapPreviewTask == null && mapPreview == null && selected != null)
            {
                if (gs.CanInstantTravel(selected))
                {
                    mapPreview = gs.PlanRoute(selected, mapAutopilot);
                    mapNextStopPlanetId = null;
                    BuildMapCard();
                    mapWorld.MarkDirtyRepaint();
                    return;
                }
                runningPreviewVersion = previewVersion;
                var planner = gs.universe.NewPlanner();
                var from = gs.universe.EndpointOf(gs.planet); var to = gs.universe.EndpointOf(selected);
                var caps = gs.Ship; var stock = gs.FuelOnBoard; bool auto = mapAutopilot;
                var fromSystem = gs.planet.Star;
                double targetX = selected.Star.X, targetY = selected.Star.Y;
                mapPreviewTask = Task.Run(() =>
                {
                    var plan = planner.Plan(from, to, caps, auto, stock);
                    string nextStopPlanetId = null;
                    if (!plan.reachable && (plan.Has(RouteReasons.OutOfRange) || plan.Has(RouteReasons.SearchLimit)))
                    {
                        StarSystem next = planner.NextStopToward(fromSystem, targetX, targetY, caps);
                        var worlds = planner.Universe.PlanetsOf(next);
                        if (worlds.Length > 0) nextStopPlanetId = worlds[0].Id;
                    }
                    return new MapPreviewResult { plan = plan, nextStopPlanetId = nextStopPlanetId };
                });
            }
        }

    }
}
