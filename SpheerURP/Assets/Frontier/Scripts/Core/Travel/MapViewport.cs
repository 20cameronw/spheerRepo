using System;

namespace Frontier
{
    /// <summary>Double-precision map coordinates; only projected screen positions should be converted to floats.</summary>
    public readonly struct MapPoint
    {
        public readonly double x, y;
        public MapPoint(double x, double y) { this.x = x; this.y = y; }
        public static MapPoint operator +(MapPoint a, MapPoint b) { return new MapPoint(a.x + b.x, a.y + b.y); }
        public static MapPoint operator -(MapPoint a, MapPoint b) { return new MapPoint(a.x - b.x, a.y - b.y); }
        public static MapPoint operator *(MapPoint a, double k) { return new MapPoint(a.x * k, a.y * k); }
        public double Length { get { return Math.Sqrt(x * x + y * y); } }
    }

    public readonly struct MapQueryBounds
    {
        public readonly MapPoint min, max;

        public MapQueryBounds(MapViewport viewport)
        {
            double padding = Math.Max(192, Math.Max(viewport.Width, viewport.Height) * 0.25);
            min = viewport.Unproject(-padding, viewport.Height + padding);
            max = viewport.Unproject(viewport.Width + padding, -padding);
        }

        public bool Covers(MapViewport viewport)
        {
            double guard = Math.Max(96, Math.Max(viewport.Width, viewport.Height) * 0.125);
            MapPoint a = viewport.Unproject(-guard, viewport.Height + guard);
            MapPoint b = viewport.Unproject(viewport.Width + guard, -guard);
            return a.x >= min.x && a.y >= min.y && b.x <= max.x && b.y <= max.y;
        }
    }

    /// <summary>A viewport, not a giant canvas. Pan is unbounded; zoom preserves the world under the pointer.</summary>
    public sealed class MapViewport
    {
        public const double MinScale = 0.015, MaxScale = 64.0;
        public double CenterX { get; private set; }
        public double CenterY { get; private set; }
        public double Scale { get; private set; } = 2.0;
        public double Width { get; private set; }
        public double Height { get; private set; }

        public void Resize(double width, double height) { Width = Math.Max(0, width); Height = Math.Max(0, height); }
        public void Center(double x, double y) { CenterX = x; CenterY = y; }
        public MapPoint Project(double x, double y) { return new MapPoint((x - CenterX) * Scale + Width * 0.5, (CenterY - y) * Scale + Height * 0.5); }
        public MapPoint Unproject(double x, double y) { return new MapPoint(CenterX + (x - Width * 0.5) / Scale, CenterY - (y - Height * 0.5) / Scale); }
        public void Pan(double dx, double dy) { CenterX -= dx / Scale; CenterY += dy / Scale; }

        public void ZoomAt(double x, double y, double factor)
        {
            if (!(factor > 0) || double.IsInfinity(factor)) return;
            MapPoint before = Unproject(x, y);
            Scale = Math.Max(MinScale, Math.Min(MaxScale, Scale * factor));
            MapPoint after = Unproject(x, y);
            CenterX += before.x - after.x;
            CenterY += before.y - after.y;
        }
    }
}
