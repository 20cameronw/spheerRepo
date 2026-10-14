using System;

namespace Frontier
{
    /// <summary>One continuous orbital-tangent cubic, shared by route previews and their moving ship marker.</summary>
    public readonly struct RouteCurve
    {
        public readonly MapPoint start, control1, control2, end;

        public RouteCurve(MapPoint start, MapPoint end, MapPoint departureCenter, MapPoint arrivalCenter)
        {
            this.start = start; this.end = end;
            MapPoint delta = end - start;
            double length = delta.Length;
            MapPoint fallback = length > 1e-12 ? delta * (1.0 / length) : new MapPoint(1, 0);
            MapPoint a = start - departureCenter, b = end - arrivalCenter;
            MapPoint ta = Tangent(a, fallback), tb = Tangent(b, fallback);
            double bend = length * 0.32;
            control1 = start + ta * bend;
            control2 = end - tb * bend;
        }

        static MapPoint Tangent(MapPoint radial, MapPoint fallback)
        {
            double n = radial.Length;
            return n > 1e-12 ? new MapPoint(-radial.y / n, radial.x / n) : fallback;
        }

        public MapPoint At(double time)
        {
            double t = Math.Max(0, Math.Min(1, time)), u = 1 - t;
            return start * (u * u * u) + control1 * (3 * u * u * t) + control2 * (3 * u * t * t) + end * (t * t * t);
        }

        public MapPoint Direction(double time)
        {
            double t = Math.Max(0, Math.Min(1, time)), u = 1 - t;
            MapPoint d = (control1 - start) * (3 * u * u) + (control2 - control1) * (6 * u * t) + (end - control2) * (3 * t * t);
            double n = d.Length;
            return n > 1e-12 ? d * (1.0 / n) : new MapPoint(1, 0);
        }
    }
}
