using System;
using Frontier.Universe;

namespace Frontier
{
    /// <summary>
    /// Where a planet is for travel purposes: its system (id and position in light-years) and its place on the orbit inside the
    /// system (radius in AU-like units, phase in radians). Built by the host from a planet descriptor.
    /// </summary>
    public readonly struct TravelNode
    {
        public readonly string SystemId;
        public readonly double X, Y;
        public readonly double OrbitRadius, OrbitPhase;

        public TravelNode(string systemId, double xLy, double yLy, double orbitRadius, double orbitPhase)
        {
            SystemId = systemId;
            X = xLy;
            Y = yLy;
            OrbitRadius = orbitRadius;
            OrbitPhase = orbitPhase;
        }
    }

    /// <summary>
    /// The interim rule for what a trip costs. Everything that prices travel goes through this one interface so the later economy
    /// model (jump range, energy-dense fuels, multi-hop routes) can replace it without touching callers.
    /// </summary>
    public interface ITravelCostModel
    {
        /// <summary>Distance of a leg: AU between the two orbits inside a system, light-years between different systems.</summary>
        double Distance(TravelNode from, TravelNode to);

        /// <summary>Fuel for a leg, at least the model's minimum. <paramref name="efficiency"/> multiplies the cost (1 = no upgrades, 0.25 = best).</summary>
        int FuelCost(TravelNode from, TravelNode to, float efficiency);
    }

    /// <summary>
    /// Interim travel cost. Inside a system: <c>min(sameBase + perAu * orbitDistance, sameMax)</c>, small and bounded. Between systems:
    /// <c>interBase + perLy * distanceLy</c>. Both are scaled by the engine efficiency and never below the minimum. Calibrated so the
    /// 30 authored worlds cost about what the old map-distance formula charged (Sylva to Glacia stays 5 fuel).
    /// </summary>
    public sealed class TravelModel : ITravelCostModel
    {
        public readonly double SameSystemBase, SameSystemPerAu, SameSystemMax;
        public readonly double InterSystemBase, CostPerLy;
        public readonly int MinCost;

        public TravelModel() : this(3.0, 1.0, 8.0, 11.0, 0.017, 1) { }

        public TravelModel(double sameSystemBase, double sameSystemPerAu, double sameSystemMax, double interSystemBase, double costPerLy, int minCost)
        {
            if (sameSystemMax < sameSystemBase) throw new ArgumentException("sameSystemMax must not be below sameSystemBase");
            if (interSystemBase < sameSystemMax) throw new ArgumentException("an inter-system hop must never be cheaper than a same-system hop");
            SameSystemBase = sameSystemBase;
            SameSystemPerAu = sameSystemPerAu;
            SameSystemMax = sameSystemMax;
            InterSystemBase = interSystemBase;
            CostPerLy = costPerLy;
            MinCost = Math.Max(1, minCost);
        }

        public static bool IsSameSystem(TravelNode a, TravelNode b)
        {
            return string.Equals(a.SystemId, b.SystemId, StringComparison.Ordinal);
        }

        /// <summary>Straight-line distance in the orbital plane of one system, in AU-like units (law of cosines on the two orbit positions).</summary>
        public static double OrbitDistanceAu(TravelNode a, TravelNode b)
        {
            double ra = Math.Max(0.0, a.OrbitRadius), rb = Math.Max(0.0, b.OrbitRadius);
            double d2 = ra * ra + rb * rb - 2.0 * ra * rb * DetMath.Cos(a.OrbitPhase - b.OrbitPhase);
            return d2 > 0.0 ? Math.Sqrt(d2) : 0.0;
        }

        public static double DistanceLy(TravelNode a, TravelNode b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public double Distance(TravelNode from, TravelNode to)
        {
            return IsSameSystem(from, to) ? OrbitDistanceAu(from, to) : DistanceLy(from, to);
        }

        /// <summary>The cost before the engine efficiency is applied.</summary>
        public double RawCost(TravelNode from, TravelNode to)
        {
            double d = Distance(from, to);
            if (IsSameSystem(from, to)) return Math.Min(SameSystemBase + SameSystemPerAu * d, SameSystemMax);
            return InterSystemBase + CostPerLy * d;
        }

        public int FuelCost(TravelNode from, TravelNode to, float efficiency)
        {
            double scaled = RawCost(from, to) * Math.Max(0.0, (double)efficiency);
            int fuel = (int)Math.Ceiling(scaled - 1e-9);
            return fuel < MinCost ? MinCost : fuel;
        }
    }
}
