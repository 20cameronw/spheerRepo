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
        public GalaxyInfo[] galaxies;
        public bool densityOnly, truncated;
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
            if (star.IsAuthored) return true;
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
            if (!string.IsNullOrEmpty(resource))
                systems.RemoveAll(star =>
                {
                    if (!Surveyed(star, centers)) return true;
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
