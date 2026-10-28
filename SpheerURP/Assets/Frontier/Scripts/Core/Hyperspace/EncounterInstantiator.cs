using System.Collections.Generic;

namespace Frontier.Hyperspace
{
    /// <summary>Turns an authored template into concrete spawn records for a tier: mirroring, tier scaling of gaps and speeds, and exact spawn steps from the route's speed profile.</summary>
    public static class EncounterInstantiator
    {
        /// <summary>Smallest opening half size the instantiator will ever produce at a tier (ship radius + fairness margin + slack).</summary>
        public static float MinGapHalf(HyperspaceTuning t, int tier) { return (float)((double)t.playerRadius + t.FairnessMarginFor(tier) + t.GapSlackFor(tier)); }

        public static SpawnEntry Mirror(SpawnEntry e)
        {
            e.x = -e.x;
            switch (e.type)
            {
                case EntityType.LaserGate:
                    switch ((GateKind)e.variant)
                    {
                        case GateKind.Slit: e.p4 = (float)((double)e.p4 + HMath.Pi); break;
                        case GateKind.Ring: e.p4 = (float)((double)e.p4 + HMath.Pi); e.p5 = -e.p5; break;
                        case GateKind.Bar: e.p0 = (float)(HMath.Pi - (double)e.p0); e.p3 = -e.p3; break;
                    }
                    return e;
            }
            switch (e.path)
            {
                case PathType.Line: e.p0 = -e.p0; break;
                case PathType.Sine: e.p2 = (float)((double)e.p2 + HMath.Pi); e.p4 = (float)((double)e.p4 - HMath.Pi); break;
                case PathType.Curve: e.p0 = -e.p0; e.p2 = -e.p2; break;
                case PathType.Orbit: e.p1 = -e.p1; e.p2 = (float)(HMath.Pi - (double)e.p2); break;
            }
            return e;
        }

        /// <summary>Applies the tier scaling that both the director and the solver use.</summary>
        public static SpawnEntry ScaleForTier(SpawnEntry e, int tier, HyperspaceTuning t)
        {
            double k = tier;
            if ((e.flags & EntryFlags.ScaleGap) != 0)
            {
                double half = (double)e.p1 * t.GapScaleFor(tier);
                double min = MinGapHalf(t, tier);
                e.p1 = (float)(half < min ? (e.p1 < min ? e.p1 : min) : half);
            }
            switch (e.type)
            {
                case EntityType.Zigzag:
                    e.p0 = (float)(e.p0 * (1.0 + t.zigAmpPerTier * k));
                    e.p1 = (float)(e.p1 * (1.0 + t.zigRatePerTier * k));
                    break;
                case EntityType.LaserGate:
                    e.p2 = (float)(e.p2 * (1.0 + t.gateSlidePerTier * k));
                    e.p3 = (float)(e.p3 * (1.0 + t.gateSpinPerTier * k));
                    break;
                case EntityType.Swooper:
                    e.p4 = (float)(e.p4 * (1.0 - t.swooperCurvePerTier * k));
                    break;
            }
            if (e.path == PathType.Line)
            {
                e.p0 = (float)(e.p0 * (1.0 + t.linePathSpeedPerTier * k));
                e.p1 = (float)(e.p1 * (1.0 + t.linePathSpeedPerTier * k));
            }
            if (e.type == EntityType.Drone || e.type == EntityType.Zigzag || e.type == EntityType.Swooper) e.vzOwn = (float)(e.vzOwn * (1.0 + t.dartSpeedPerTier * k));
            if (e.type == EntityType.Seeker && e.vzOwn <= 0f) e.vzOwn = t.seekerClosing;
            return e;
        }

        /// <summary>Appends the records of one placed template. anchorStep is the step at which the first entry arrives at the ship plane.</summary>
        public static void Instantiate(EncounterTemplate template, bool mirror, int anchorStep, RouteSpec route, HyperspaceTuning t, short templateIndex, List<SpawnRecord> output)
        {
            double timeScale = t.TimeScaleFor(route.tier);
            for (int i = 0; i < template.entries.Length; i++)
            {
                SpawnEntry e = template.entries[i];
                if (mirror && template.mirrorable) e = Mirror(e);
                e = ScaleForTier(e, route.tier, t);
                int arrival = anchorStep + (int)(e.zOffset * timeScale / t.nominalSpeed * HyperspaceTuning.StepsPerSecond + 0.5);
                var r = new SpawnRecord
                {
                    arrivalStep = arrival, type = e.type, pickup = e.pickup, variant = e.variant, flags = e.flags, x = e.x, y = e.y, vzOwn = e.vzOwn,
                    path = e.path, p0 = e.p0, p1 = e.p1, p2 = e.p2, p3 = e.p3, p4 = e.p4, p5 = e.p5, template = templateIndex, group = e.group
                };
                SpawnStep(ref r, route, t);
                output.Add(r);
            }
        }

        /// <summary>Finds the step at which a record must appear so that it reaches z = 0 at arrivalStep, by integrating the same recurrence the run uses, backward.</summary>
        public static void SpawnStep(ref SpawnRecord r, RouteSpec route, HyperspaceTuning t)
        {
            double z = 0.0;
            int k = r.arrivalStep;
            if (k > route.totalSteps) k = route.totalSteps;
            if (r.type == EntityType.None)
            {
                // pickups drift with the scroll only
                while (k > 0 && z < t.spawnDistance)
                {
                    k--;
                    z += (double)route.speed[k] * HyperspaceTuning.Dt;
                }
            }
            else
            {
                while (k > 0 && z < t.spawnDistance)
                {
                    k--;
                    z += ((double)route.speed[k] + r.vzOwn) * HyperspaceTuning.Dt;
                }
            }
            r.step = k;
            r.z0 = (float)z;
        }
    }
}
