namespace Frontier.Hyperspace
{
    public sealed partial class Bot
    {
        struct Threat
        {
            public Hazard h;
            public float tc;
        }

        struct PickupTarget
        {
            public float x, y, tc, value;
        }

        readonly Threat[] threats;
        int threatCount;
        readonly PickupTarget[] pickupsBuf;
        int pickupCount;
        int curUid;
        int decisionTag;

        void Jitter(out double jx, out double jy)
        {
            if (Skill.perceptionNoise <= 0f)
            {
                jx = 0.0;
                jy = 0.0;
                return;
            }
            ulong h = Frontier.Universe.Hash.Combine((ulong)(uint)curUid, (ulong)(uint)decisionTag, 0x4A495454UL);
            jx = (Frontier.Universe.Hash.Unit(h) - 0.5) * 2.0 * Skill.perceptionNoise;
            jy = (Frontier.Universe.Hash.Unit(Frontier.Universe.Hash.Mix(h)) - 0.5) * 2.0 * Skill.perceptionNoise;
        }

        void AddThreat(Hazard h, float tc)
        {
            if (threatCount >= threats.Length) return;
            double jx, jy;
            Jitter(out jx, out jy);
            if (h.shape == HazardShape.Slot) h.a = (float)(h.a + jy);
            else if (h.shape != HazardShape.Bar) h.a = (float)(h.a + jx);
            threats[threatCount++] = new Threat { h = h, tc = tc };
        }

        void AddDisc(float x, float y, float r, float tc)
        {
            if (threatCount >= threats.Length) return;
            double jx, jy;
            Jitter(out jx, out jy);
            threats[threatCount++] = new Threat { h = Hazard.Disc((float)(x + jx), (float)(y + jy), r), tc = tc };
        }

        /// <summary>Keeps only the most imminent threats the bot can attend to.</summary>
        void PruneThreats()
        {
            int keep = Skill.maxThreats;
            if (threatCount <= keep) return;
            for (int k = 0; k < keep; k++)
            {
                int best = k;
                for (int j = k + 1; j < threatCount; j++)
                    if (threats[j].tc < threats[best].tc) best = j;
                if (best != k)
                {
                    Threat tmp = threats[k];
                    threats[k] = threats[best];
                    threats[best] = tmp;
                }
            }
            threatCount = keep;
        }

        void Think(HyperspaceRun run)
        {
            decisionTag = run.StepIndex / 8;
            BuildThreats(run);
            PruneThreats();
            double px = run.Player.x, py = run.Player.y;
            double vmax = (double)run.Player.maxSpeed * Skill.speedFraction;
            double w = t.areaHalfWidth, h = t.areaHalfHeight;
            const double spacing = 0.5;
            int nx = (int)(2.0 * w / spacing) + 1, ny = (int)(2.0 * h / spacing) + 1;
            double bestCost = Evaluate((float)px, (float)py, (float)px, (float)py, (float)vmax), bx = px, by = py;
            for (int j = 0; j < ny; j++)
            {
                double cy = -h + j * spacing;
                for (int i = 0; i < nx; i++)
                {
                    double cx = -w + i * spacing;
                    double cost = Evaluate((float)cx, (float)cy, (float)px, (float)py, (float)vmax);
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bx = cx;
                        by = cy;
                    }
                }
            }
            lastBestCost = (float)bestCost;
            if (Skill.noise > 0f)
            {
                bx += ((double)rng.NextFloat() - 0.5) * 2.0 * Skill.noise;
                by += ((double)rng.NextFloat() - 0.5) * 2.0 * Skill.noise;
            }
            bx = HMath.Clamp(bx, -w, w);
            by = HMath.Clamp(by, -h, h);
            int delay = (int)((double)Skill.reactionDelay * HyperspaceTuning.StepsPerSecond + 0.5);
            Push(run.StepIndex + delay, (float)bx, (float)by);
        }

        float Evaluate(float cx, float cy, float px, float py, float vmax)
        {
            double dx = (double)cx - px, dy = (double)cy - py;
            double d = HMath.Length(dx, dy);
            double ux = d > 1e-4f ? dx / d : 0f, uy = d > 1e-4f ? dy / d : 0f;
            double cost = 0.45 * d + 0.08 * (HMath.Abs(cx) + 0.6 * (double)HMath.Abs(cy));
            double pr = (double)t.playerRadius + Skill.padding;
            double delay = Skill.reactionDelay;
            for (int k = 0; k < threatCount; k++)
            {
                double tMove = threats[k].tc - delay;
                if (tMove < 0f) tMove = 0.0;
                double travelled = vmax * tMove;
                if (travelled > d) travelled = d;
                double qx = px + ux * travelled, qy = py + uy * travelled;
                double cl = threats[k].h.Clearance((float)qx, (float)qy, (float)pr);
                if (cl < 0f) cost += 400.0 * (1.0 + 0.4 / (0.15 + (double)threats[k].tc));
                else if (cl < Skill.comfort)
                {
                    double f = (Skill.comfort - cl) / Skill.comfort;
                    cost += 30.0 * f * f;
                }
            }
            if (cost < 150f)
            {
                for (int k = 0; k < pickupCount; k++)
                {
                    double tMove = pickupsBuf[k].tc - delay;
                    if (tMove < 0f) tMove = 0.0;
                    double travelled = vmax * tMove;
                    if (travelled > d) travelled = d;
                    double qx = px + ux * travelled - pickupsBuf[k].x, qy = py + uy * travelled - pickupsBuf[k].y;
                    double dist = HMath.Length(qx, qy);
                    if (dist < 1.0f) cost -= (double)Skill.pickupGreed * pickupsBuf[k].value * 6.0 * (1.0 - dist);
                }
            }
            return (float)cost;
        }

        void BuildThreats(HyperspaceRun run)
        {
            threatCount = 0;
            pickupCount = 0;
            double hor = Skill.horizon;
            double hz = t.playerHalfDepth;
            for (int i = 0; i < run.EnemyHigh; i++)
            {
                ref Enemy e = ref run.Enemies[i];
                if (!e.active || e.type == EntityType.Guardian || (e.flags & EnemyFlags.Spent) != 0) continue;
                if ((double)e.z + e.halfDepth + hz < 0f) continue;
                curUid = e.uid;
                double closing = (double)run.Speed + e.vzOwn;
                double tc = e.z > 0f ? e.z / closing : 0f;
                if (tc > hor) continue;
                double lead = Skill.exactPrediction ? tc : 0f;
                switch (e.type)
                {
                    case EntityType.LaserGate:
                        AddThreat(Hazards.Gate(e.variant, e.ox, e.oy, e.p0, e.p1, e.p2, e.p3, e.p4, e.p5, (float)(e.age + lead - e.tArrive)), (float)tc);
                        break;
                    case EntityType.Mine:
                        if (e.z > t.mineDetonateZ) AddDisc(e.x, e.y, t.mineBlastRadius, (float)tc);
                        break;
                    case EntityType.Seeker:
                        if (e.state == 0) AddDisc(run.Player.x, run.Player.y, (float)((double)e.radius + 0.6), (float)tc);
                        else AddDisc((float)(e.x + e.vx * tc), (float)(e.y + e.vy * tc), e.radius, (float)tc);
                        break;
                    case EntityType.TurretPod:
                        if (e.state == 1)
                        {
                            double tFire = (double)e.p1 - e.age;
                            if (tFire < 0f) tFire = 0.0;
                            double zFire = e.z - run.Speed * tFire;
                            double flight = zFire > 1f ? zFire / t.BoltSpeedFor(run.Route.tier) : 0.1f;
                            int n = e.variant == (byte)TurretMode.Fan ? t.FanCountFor(run.Route.tier) : 1;
                            for (int b = 0; b < n; b++)
                            {
                                double off = n == 1 ? 0f : (b - (double)(n - 1) * 0.5) * t.turretFanSpread;
                                AddDisc((float)(e.aimX + off), e.aimY, t.boltRadius, (float)(tFire + flight));
                            }
                        }
                        AddDisc(e.x, e.y, e.radius, (float)tc);
                        break;
                    default:
                    {
                        double x = e.x + e.vx * lead, y = e.y + e.vy * lead;
                        if (Skill.exactPrediction && e.path != PathType.Homing && e.path != PathType.Hold)
                            Paths.Position(e, e.age + tc - e.tArrive, out x, out y);
                        AddDisc((float)x, (float)y, e.radius, (float)tc);
                        break;
                    }
                }
            }
            for (int i = 0; i < run.BoltHigh; i++)
            {
                ref Bolt b = ref run.Bolts[i];
                if (!b.active || b.vz >= 0f || b.z < -1f) continue;
                curUid = b.uid;
                double tc = b.z > 0f ? (double)b.z / -b.vz : 0f;
                if (tc > hor) continue;
                AddDisc((float)(b.x + b.vx * tc), (float)(b.y + b.vy * tc), b.radius, (float)tc);
            }
            double eta = run.BossVolleyEta;
            curUid = 7;
            if (eta >= 0f)
            {
                double flight = (double)t.bossZ / t.BoltSpeedFor(run.Route.tier);
                int n = run.BossVolleyCount;
                for (int b = 0; b < n; b++)
                {
                    double off = n == 1 ? 0f : (b - (double)(n - 1) * 0.5) * t.turretFanSpread;
                    if (eta + flight <= hor) AddDisc((float)(run.BossVolleyAimX + off), run.BossVolleyAimY, t.boltRadius, (float)(eta + flight));
                }
            }
            for (int i = 0; i < run.PickupHigh && pickupCount < pickupsBuf.Length; i++)
            {
                ref Pickup pk = ref run.Pickups[i];
                if (!pk.active) continue;
                double tc = (double)pk.z / run.Speed;
                if (tc < 0f || tc > hor) continue;
                double value = PickupValue(run, pk.type);
                if (value <= 0f) continue;
                pickupsBuf[pickupCount++] = new PickupTarget { x = pk.x, y = pk.y, tc = (float)tc, value = (float)value };
            }
        }

        static float PickupValue(HyperspaceRun run, PickupType type)
        {
            switch (type)
            {
                case PickupType.Fuel: return 1.0f;
                case PickupType.LootCrate: return 1.4f;
                case PickupType.Shield: return run.Player.shield < run.Player.maxShield ? 1.6f : 0f;
                case PickupType.Bomb: return run.Player.bombs < run.Player.maxBombs ? 1.0f : 0f;
                case PickupType.Overdrive: return 0.9f;
                default: return 0f;
            }
        }
    }
}
