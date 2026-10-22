namespace Frontier.Hyperspace
{
    /// <summary>
    /// Closed-form lateral motion of scripted enemies. Every path is a function of u = (seconds since spawn) - (seconds until the nominal arrival at the ship plane),
    /// i.e. u = 0 exactly when the enemy crosses z = 0 without being shot. Authored positions are therefore positions AT ARRIVAL, which keeps formations aligned no
    /// matter how fast the world scrolls. Pure and allocation-free; shared by the run, the bot, the solver and the renders. Results are doubles (the run rounds them to float when it stores them).
    /// </summary>
    public static class Paths
    {
        public static void Position(PathType path, double ox, double oy, double p0, double p1, double p2, double p3, double p4, double p5, double u, out double x, out double y)
        {
            switch (path)
            {
                case PathType.Line:
                    x = ox + p0 * u;
                    y = oy + p1 * u;
                    return;
                case PathType.Sine:
                {
                    double ph = p1 * u + p2;
                    x = ox + p0 * HMath.Sin(ph);
                    y = oy + p3 * HMath.Sin(ph + p4);
                    return;
                }
                case PathType.Curve:
                {
                    double d = p4 > 0.05 ? p4 : 0.05;
                    double s = u + d + p5;
                    if (s <= 0.0)
                    {
                        x = ox;
                        y = oy;
                        return;
                    }
                    if (s >= d)
                    {
                        double vex = 2.0 * (p2 - p0) / d, vey = 2.0 * (p3 - p1) / d;
                        x = p2 + vex * (s - d);
                        y = p3 + vey * (s - d);
                        return;
                    }
                    double k = s / d, w = 1.0 - k;
                    x = w * w * ox + 2.0 * w * k * p0 + k * k * p2;
                    y = w * w * oy + 2.0 * w * k * p1 + k * k * p3;
                    return;
                }
                case PathType.Orbit:
                {
                    double r = p0 + p3 * u;
                    double ph = p1 * u + p2;
                    x = ox + r * HMath.Cos(ph);
                    y = oy + r * HMath.Sin(ph);
                    return;
                }
                default:
                    x = ox;
                    y = oy;
                    return;
            }
        }

        /// <summary>Position of a live enemy at an arbitrary time u relative to its arrival (Homing and Hold enemies report where they are now).</summary>
        public static void Position(in Enemy e, double u, out double x, out double y)
        {
            if (e.path == PathType.Homing || e.path == PathType.Hold)
            {
                x = e.x;
                y = e.y;
                return;
            }
            Position(e.path, e.ox, e.oy, e.p0, e.p1, e.p2, e.p3, e.p4, e.p5, u, out x, out y);
        }

        public static void Position(in SpawnRecord r, double u, out double x, out double y)
        {
            Position(r.path, r.x, r.y, r.p0, r.p1, r.p2, r.p3, r.p4, r.p5, u, out x, out y);
        }
    }
}
