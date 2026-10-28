using System;
using System.Collections.Generic;
using Frontier.Universe;
using Uni = Frontier.Universe.Universe;

namespace Frontier
{
    public readonly struct MapDensityCell
    {
        public readonly double x, y, width, height;
        public readonly float density;
        public MapDensityCell(double x, double y, double width, double height, float density)
        { this.x = x; this.y = y; this.width = width; this.height = height; this.density = density; }
    }

    public readonly struct MapSurveyPoint
    {
        public readonly double x, y, radius;
        public MapSurveyPoint(double x, double y, double radius)
        {
            this.x = x;
            this.y = y;
            this.radius = Math.Max(0, radius);
        }
    }

    public sealed class MapSnapshot
    {
        public MapDensityCell[] density;
        public StarSystem[] systems;
        public StarSystem[] surveyedSystems;
        public GalaxyInfo[] galaxies;
        public bool densityOnly, truncated;
    }

    /// <summary>Exact-name lookup for authored and surveyed destinations only.</summary>
    public sealed class MapNameIndex
    {
        readonly Dictionary<string, string> planetIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static MapNameIndex Build(Uni universe, IEnumerable<string> discoveredSystemIds, IEnumerable<string> pinnedPlanetIds = null)
        {
            if (universe == null) throw new ArgumentNullException(nameof(universe));
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var system in universe.AuthoredSystems) ids.Add(system.Id);
            if (discoveredSystemIds != null)
                foreach (string id in discoveredSystemIds)
                    if (!string.IsNullOrEmpty(id)) ids.Add(id);

            var systems = new List<StarSystem>(ids.Count);
            foreach (string id in ids)
            {
                StarSystem system;
                if (universe.TryGetSystemById(id, out system)) systems.Add(system);
            }
            var result = new MapNameIndex();
            result.AddSystems(universe, systems);
            result.AddPlanets(universe, pinnedPlanetIds);
            return result;
        }

        public void AddSystems(Uni universe, IEnumerable<StarSystem> systems)
        {
            if (universe == null) throw new ArgumentNullException(nameof(universe));
            if (systems == null) return;
            foreach (StarSystem system in systems)
            {
                if (system == null) continue;
                StarSystem known;
                if (!universe.TryGetSystem(system.Key, out known) || known.Id != system.Id) continue;
                PlanetDescriptor[] planets = universe.PlanetsOf(known);
                if (planets.Length == 0) continue;
                AddName(known.Name, planets[0].Id);
                AddName(known.Designation, planets[0].Id);
                AddName(known.Id, planets[0].Id);
                foreach (PlanetDescriptor planet in planets) AddPlanet(planet);
            }
        }

        void AddPlanets(Uni universe, IEnumerable<string> planetIds)
        {
            if (planetIds == null) return;
            foreach (string id in planetIds)
            {
                PlanetKey key;
                PlanetDescriptor planet;
                if (PlanetKey.TryParse(id, out key) && universe.TryGetPlanet(key, out planet))
                {
                    AddPlanet(planet);
                    StarSystem system;
                    if (universe.TryGetSystem(key.System, out system))
                    {
                        AddName(system.Name, planet.Id);
                        AddName(system.Designation, planet.Id);
                        AddName(system.Id, planet.Id);
                    }
                }
            }
        }

        void AddPlanet(PlanetDescriptor planet)
        {
            AddName(planet.Name, planet.Id);
            AddName(planet.Designation, planet.Id);
            AddName(planet.Id, planet.Id);
            AddName(planet.AuthoredId, planet.Id);
        }

        public bool TryFind(string name, out string planetId)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                planetId = null;
                return false;
            }
            return planetIds.TryGetValue(name.Trim(), out planetId);
        }

        void AddName(string name, string planetId)
        {
            if (!string.IsNullOrWhiteSpace(name) && !planetIds.ContainsKey(name))
                planetIds.Add(name, planetId);
        }
    }

    /// <summary>Bounded, engine-free map sampling. Call on a worker; no Unity objects or session state are accessed.</summary>
    public static class MapQuery
    {
        public const int Columns = 32, Rows = 24, MaxSystems = 800;
        public const double MaxSystemSpanLy = 2400;

        public static bool Surveyed(StarSystem star, MapPoint[] centers, double radius)
        {
            if (star.IsAuthored) return true;
            foreach (var center in centers)
            {
                double dx = star.X - center.x, dy = star.Y - center.y;
                if (dx * dx + dy * dy <= radius * radius) return true;
            }
            return false;
        }

        public static bool Surveyed(StarSystem star, MapSurveyPoint[] centers)
        {
            return Surveyed(star, centers, null);
        }

        public static bool Surveyed(StarSystem star, MapSurveyPoint[] centers, ISet<string> discovered)
        {
            if (star.IsAuthored || (discovered != null && discovered.Contains(star.Id))) return true;
            foreach (var center in centers)
            {
                double dx = star.X - center.x, dy = star.Y - center.y;
                if (dx * dx + dy * dy <= center.radius * center.radius) return true;
            }
            return false;
        }

        public static MapSnapshot Build(Uni universe, double x0, double y0, double x1, double y1, string resource, MapPoint[] centers, double surveyRadius)
        {
            var points = new MapSurveyPoint[centers.Length];
            for (int i = 0; i < points.Length; i++) points[i] = new MapSurveyPoint(centers[i].x, centers[i].y, surveyRadius);
            return Build(universe, x0, y0, x1, y1, resource, points);
        }

        public static MapSnapshot Build(Uni universe, double x0, double y0, double x1, double y1, string resource, MapSurveyPoint[] centers)
        {
            return Build(universe, x0, y0, x1, y1, resource, centers, new string[0]);
        }

        public static MapSnapshot Build(Uni universe, double x0, double y0, double x1, double y1, string resource,
            MapSurveyPoint[] centers, string[] discoveredSystemIds)
        {
            var discovered = new HashSet<string>(discoveredSystemIds ?? new string[0], StringComparer.Ordinal);
            double width = x1 - x0, height = y1 - y0;
            var result = new MapSnapshot { density = new MapDensityCell[Columns * Rows] };
            var galaxies = new List<GalaxyInfo>();
            universe.Galaxies.GalaxiesInRect(x0, y0, x1, y1, galaxies);
            result.galaxies = galaxies.ToArray();
            double dx = width / Columns, dy = height / Rows;
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Columns; x++)
                {
                    double px = x0 + x * dx, py = y0 + y * dy;
                    result.density[y * Columns + x] = new MapDensityCell(px, py, dx, dy, (float)universe.Density(px + dx * 0.5, py + dy * 0.5));
                }
            var systems = new List<StarSystem>();
            result.densityOnly = width > MaxSystemSpanLy || height > MaxSystemSpanLy;
            if (!result.densityOnly) universe.SystemsInRect(x0, y0, x1, y1, systems);
            else
                foreach (var star in universe.AuthoredSystems)
                    if (star.X >= x0 && star.X <= x1 && star.Y >= y0 && star.Y <= y1) systems.Add(star);
            var surveyedSystems = new List<StarSystem>();
            foreach (var star in systems)
                if (Surveyed(star, centers)) surveyedSystems.Add(star);
            result.surveyedSystems = surveyedSystems.ToArray();
            if (!string.IsNullOrEmpty(resource))
                systems.RemoveAll(star =>
                {
                    if (!Surveyed(star, centers, discovered)) return true;
                    foreach (var world in universe.PlanetsOf(star)) if (world.HasResource(resource)) return false;
                    return true;
                });
            double cx = (x0 + x1) * 0.5, cy = (y0 + y1) * 0.5;
            systems.Sort((a, b) =>
            {
                if (a.IsAuthored != b.IsAuthored) return a.IsAuthored ? -1 : 1;
                double da = (a.X - cx) * (a.X - cx) + (a.Y - cy) * (a.Y - cy);
                double db = (b.X - cx) * (b.X - cx) + (b.Y - cy) * (b.Y - cy);
                int c = da.CompareTo(db);
                return c != 0 ? c : a.Key.Id.CompareTo(b.Key.Id);
            });
            result.truncated = systems.Count > MaxSystems;
            if (result.truncated) systems.RemoveRange(MaxSystems, systems.Count - MaxSystems);
            result.systems = systems.ToArray();
            return result;
        }
    }
}
