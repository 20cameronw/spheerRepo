using System;

namespace Frontier
{
    /// <summary>Why a trip cannot be taken (or what the player must know about it). Several can apply at once.</summary>
    [Flags]
    public enum RouteReasons
    {
        None = 0,
        /// <summary>The destination is the world the player is on.</summary>
        AlreadyHere = 1,
        /// <summary>The ship has no jump drive (jump level 0): it can only hop inside its home system.</summary>
        NoJumpDrive = 2,
        /// <summary>No chain of jumps within the jump range reaches the destination.</summary>
        OutOfRange = 4,
        /// <summary>The hull plating does not cover a hazard of the destination.</summary>
        Hazard = 8,
        /// <summary>The route needs more energy than the ship carries.</summary>
        NotEnoughFuel = 16,
        /// <summary>The destination does not exist in the universe.</summary>
        Unknown = 32,
        /// <summary>The bounded search exhausted its work budget; this is not proof that no route exists.</summary>
        SearchLimit = 64
    }

    /// <summary>One flight of a route: a hop inside a system or one jump between two systems (one travel mini-game run).</summary>
    [Serializable]
    public struct RouteLeg
    {
        /// <summary>Compact ids of the systems at the start and the end of the leg ("s:X:Y:I").</summary>
        public string fromSystemId, toSystemId;
        /// <summary>Start and end in light-years (the star positions).</summary>
        public double x0, y0, x1, y1;
        /// <summary>Straight length in light-years (0 for a hop inside a system).</summary>
        public double lengthLy;
        /// <summary>Orbit difference in AU-like units for a hop inside a system, else 0.</summary>
        public double deltaAu;
        public bool intraSystem;
        /// <summary>Energy units of this leg; the legs of a plan add up to <see cref="RoutePlan.totalFuel"/>.</summary>
        public int fuel;
        /// <summary>Seconds the leg takes (a mini-game run, or an autopilot warp).</summary>
        public int seconds;
    }

    /// <summary>
    /// A planned trip. Always safe to read: an unreachable plan has no legs, <see cref="reachable"/> false and at least one reason.
    /// Built by the route planner, drawn by the map (see <see cref="RouteCurve"/>), paid and flown by the travel flow.
    /// </summary>
    [Serializable]
    public sealed class RoutePlan
    {
        public static readonly RouteLeg[] NoLegs = new RouteLeg[0];

        public RouteLeg[] legs = NoLegs;
        public bool reachable;
        public bool instant;
        public RouteReasons reasons;
        public bool autopilot;
        public string fromPlanetId, toPlanetId;
        /// <summary>Total energy units of the trip with every modifier applied.</summary>
        public int totalFuel;
        public int totalSeconds;
        /// <summary>Summed length of the jump legs in light-years.</summary>
        public double totalLengthLy;
        /// <summary>Straight distance between the start and the end in light-years.</summary>
        public double straightLy;
        /// <summary>True when the whole trip is a hop inside one system.</summary>
        public bool intraSystem;
        /// <summary>Hazards that block landing on the destination (empty when the plating covers them).</summary>
        public HazardGap[] hazardGaps = NoGaps;
        /// <summary>Energy missing for this trip (0 when the stock covers it or the stock was not given).</summary>
        public int fuelShort;
        /// <summary>For an unreachable trip: how far the ship can get toward the destination in one jump (the red part of the preview line), in light-years.</summary>
        public double reachLy;

        public static readonly HazardGap[] NoGaps = new HazardGap[0];

        public int LegCount { get { return legs.Length; } }

        public bool Has(RouteReasons r) { return (reasons & r) != 0; }

        public static RoutePlan Blocked(RouteReasons reasons)
        {
            return new RoutePlan { reachable = false, reasons = reasons };
        }
    }

    /// <summary>A hazard the destination has and the hull plating level the ship lacks for it.</summary>
    [Serializable]
    public struct HazardGap
    {
        /// <summary>The single hazard flag.</summary>
        public Frontier.Universe.Hazards hazard;
        public int have, need;
    }
}
