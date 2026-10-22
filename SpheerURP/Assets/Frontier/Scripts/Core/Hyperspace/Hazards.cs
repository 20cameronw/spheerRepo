namespace Frontier.Hyperspace
{
    public enum HazardShape : byte
    {
        /// <summary>Round blocker at (a, b) with radius c.</summary>
        Disc = 0,
        /// <summary>Wall with a vertical slit: gap centre x = a, half width b.</summary>
        Slit = 1,
        /// <summary>Wall with a horizontal slot: gap centre y = a, half height b.</summary>
        Slot = 2,
        /// <summary>Wall with a round hole at (a, b), radius c.</summary>
        Ring = 3,
        /// <summary>Bar through (a, b) with angle c, half length d, half thickness e.</summary>
        Bar = 4
    }

    /// <summary>
    /// The shape an enemy presents to the ship plane at one instant. The run uses it for hit tests and the fairness solver uses it to mark forbidden cells,
    /// so both always agree. Clearance is how far (in world units) the ship disc is from touching: positive is safe, negative is a hit.
    /// </summary>
    public struct Hazard
    {
        public HazardShape shape;
        public float a, b, c, d, e;

        public static Hazard Disc(float x, float y, float r)
        {
            return new Hazard { shape = HazardShape.Disc, a = x, b = y, c = r };
        }

        public float Clearance(float px, float py, float pr)
        {
            switch (shape)
            {
                case HazardShape.Disc:
                    return (float)(HMath.Length((double)px - a, (double)py - b) - c - pr);
                case HazardShape.Slit:
                    return (float)(((double)b - pr) - HMath.Abs((double)px - a));
                case HazardShape.Slot:
                    return (float)(((double)b - pr) - HMath.Abs((double)py - a));
                case HazardShape.Ring:
                    return (float)(((double)c - pr) - HMath.Length((double)px - a, (double)py - b));
                default:
                {
                    double s, co;
                    HMath.SinCos(c, out s, out co);
                    double rx = (double)px - a, ry = (double)py - b;
                    double t = HMath.Clamp(rx * co + ry * s, -d, d);
                    double dx = rx - co * t, dy = ry - s * t;
                    return (float)(HMath.Length(dx, dy) - e - pr);
                }
            }
        }

        public bool Hits(float px, float py, float pr)
        {
            return Clearance(px, py, pr) < 0f;
        }
    }

    public static class Hazards
    {
        /// <summary>Builds the barrier geometry of a laser gate at time u relative to its arrival. Gate parameters: p1 = half size (slit width, slot height, hole radius, bar length), p2 = slide amplitude (bar: half thickness), p3 = angular speed, p4 = phase at arrival, p5 = vertical amplitude (ring), p0 = angle at arrival (bar).</summary>
        public static Hazard Gate(in Enemy e)
        {
            return Gate(e.variant, e.ox, e.oy, e.p0, e.p1, e.p2, e.p3, e.p4, e.p5, (float)((double)e.age - e.tArrive));
        }

        public static Hazard Gate(byte variant, float ox, float oy, float p0, float p1, float p2, float p3, float p4, float p5, float u)
        {
            double ph = (double)p3 * u + p4;
            switch ((GateKind)variant)
            {
                case GateKind.Slit:
                    return new Hazard { shape = HazardShape.Slit, a = (float)(ox + p2 * HMath.Sin(ph)), b = p1 };
                case GateKind.Slot:
                    return new Hazard { shape = HazardShape.Slot, a = (float)(oy + p2 * HMath.Sin(ph)), b = p1 };
                case GateKind.Ring:
                    return new Hazard { shape = HazardShape.Ring, a = (float)(ox + p2 * HMath.Sin(ph)), b = (float)(oy + p5 * HMath.Cos(ph)), c = p1 };
                default:
                    return new Hazard { shape = HazardShape.Bar, a = ox, b = oy, c = (float)(p0 + (double)p3 * u), d = p1, e = p2 };
            }
        }

        /// <summary>Where the gap of a gate is centred right now (x for slits and rings, y for slots; bars report the pivot). For renderers and the HUD telegraph.</summary>
        public static void GapCentre(in Enemy e, out double gx, out double gy)
        {
            Hazard h = Gate(e);
            switch (h.shape)
            {
                case HazardShape.Slit: gx = h.a; gy = 0.0; return;
                case HazardShape.Slot: gx = 0.0; gy = h.a; return;
                default: gx = h.a; gy = h.b; return;
            }
        }
    }
}
