using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Hyperspace
{
    /// <summary>One template placed on the route.</summary>
    public sealed class PlannedEncounter
    {
        public int template;
        public int anchorStep;
        /// <summary>Step after which the encounter is over (last arrival + half a second); the run then emits WaveCleared.</summary>
        public int endStep;
        public bool mirror;
        public float cost;
        public PacingKind kind;
        public int wave;
    }

    /// <summary>The complete, input-independent plan of a run: what spawns when. Built once at run start.</summary>
    public sealed class Schedule
    {
        public SpawnRecord[] records;
        public PlannedEncounter[] encounters;
        /// <summary>Sum of the costs of all placed templates (including the guardian); never exceeds the route budget.</summary>
        public float spent;
        public float budgetTotal;
        public int bossSpawnStep = -1;

        public ulong Digest()
        {
            ulong h = 0x5343484455L;
            for (int i = 0; i < records.Length; i++)
            {
                SpawnRecord r = records[i];
                h = Hash.Combine(h, (ulong)(uint)r.step, (ulong)(uint)r.arrivalStep, ((ulong)(byte)r.type << 16) | ((ulong)(byte)r.pickup << 8) | r.variant);
                h = Hash.Combine(h, Bits(r.x), Bits(r.y), Bits(r.p0));
                h = Hash.Combine(h, Bits(r.p1), Bits(r.p2), Bits(r.p3));
                h = Hash.Combine(h, Bits(r.p4), Bits(r.p5), Bits(r.z0));
            }
            return h;
        }

        static ulong Bits(float v) { return (ulong)(uint)System.BitConverter.SingleToInt32Bits(v); }
    }

    /// <summary>
    /// Fills a route budget with templates. Rules: (1) cumulative cost placed by the end of any template never exceeds the cumulative budget income of the pacing curve (so total spend
    /// never exceeds the route budget); (2) templates are chosen by closeness of their intensity (cost per second) to the current pacing rate, so calm stretches get light formations
    /// and peaks get dense ones; (3) never the same template or family three times in a row, recently used templates are penalised, newly unlocked ones are favoured; (4) templates never
    /// overlap and keep at least the tier's gap (&gt;= entryLead) so each can be analysed on its own by the fairness solver; (5) a Guardian closes long or deep routes.
    /// </summary>
    public static class SpawnDirector
    {
        public static float GapSeconds(int tier, HyperspaceTuning t)
        {
            double f = HMath.Clamp01((double)tier / 8.0);
            double gap = HMath.Lerp(t.gapTier0, t.gapTier12, f);
            return (float)(gap < t.entryLead ? t.entryLead : gap);
        }

        static float RoleFactor(PacingKind kind, EncounterRole role)
        {
            switch (kind)
            {
                case PacingKind.Calm:
                case PacingKind.Relax:
                    return role == EncounterRole.Calm ? 1f : (role == EncounterRole.Build ? 0.15f : 0f);
                case PacingKind.Build:
                    return role == EncounterRole.Build ? 1f : (role == EncounterRole.Calm ? 0.45f : (role == EncounterRole.Peak ? 0.12f : 0f));
                case PacingKind.Peak:
                    return role == EncounterRole.Peak ? 1f : (role == EncounterRole.Build ? 0.55f : (role == EncounterRole.Calm ? 0.1f : 0f));
                default:
                    return 0f;
            }
        }

        public static Schedule Build(RouteSpec route, EncounterLibrary lib, HyperspaceTuning t)
        {
            var rng = new Rng(Hash.Combine(route.run.seed, 0x44495243UL));
            var planned = new List<PlannedEncounter>();
            var records = new List<SpawnRecord>(128);
            double spent = 0.0;
            double gap = GapSeconds(route.tier, t);
            double cursor = t.introSeconds;
            int last1 = -1, last2 = -1;
            EncounterFamily fam1 = EncounterFamily.Boss, fam2 = EncounterFamily.Boss;
            int tier = route.tier;
            double ts = t.TimeScaleFor(tier);
            int count = lib.Count;
            var weights = new float[count];

            while (cursor < (double)route.waveEnd - 0.8)
            {
                PacingSegment seg = route.SegmentAt((float)cursor);
                double rate = seg.rate;
                double total = 0.0;
                for (int pass = 0; pass < 2 && total <= 0f; pass++)
                {
                    bool useFloor = pass == 0 && rate > 1.2f;
                    for (int i = 0; i < count; i++)
                    {
                        weights[i] = 0f;
                        EncounterTemplate tp = lib[i];
                        if (!tp.AllowedAt(tier) || tp.role == EncounterRole.Boss) continue;
                        double rf = RoleFactor(seg.kind, tp.role);
                        if (rf <= 0f) continue;
                        if (i == last1 && i == last2) continue;
                        if (tp.family == fam1 && tp.family == fam2) continue;
                        double span = tp.SpanSecondsAt((float)ts);
                        double anchor = cursor + tp.PreSecondsAt((float)ts);
                        double end = anchor + span;
                        if (end > (double)route.waveEnd - 0.4) continue;
                        if (tp.cost > route.BudgetBy((float)end) - spent + 1e-4) continue;
                        double intensity = tp.cost / (span + gap);
                        if (useFloor && tp.family != EncounterFamily.Reward && intensity < 0.45 * rate) continue;
                        double scale = rate > 0.4f ? rate : 0.4f;
                        double dev = (intensity - rate) / scale;
                        double w = tp.weight * rf / (1.0 + 3.0 * dev * dev);
                        if (tp.minTier >= tier - 1 && tp.minTier > 0) w *= 1.6;
                        if (i == last1 || i == last2) w *= 0.25;
                        if (tp.family == fam1) w *= 0.6;
                        if (tp.family == EncounterFamily.Reward && fam1 == EncounterFamily.Reward) w *= 0.05;
                        weights[i] = (float)w;
                        total += w;
                    }
                }
                if (total <= 0f)
                {
                    cursor += 0.5;
                    continue;
                }
                double pick = rng.NextFloat() * total;
                int chosen = -1;
                for (int i = 0; i < count; i++)
                {
                    if (weights[i] <= 0f) continue;
                    chosen = i;
                    if (pick < weights[i]) break;
                    pick -= weights[i];
                }
                EncounterTemplate ct = lib[chosen];
                bool mirror = ct.mirrorable && rng.NextInt(2) == 0;
                double anchorTime = cursor + ct.PreSecondsAt((float)ts);
                int anchorStep = (int)(anchorTime * HyperspaceTuning.StepsPerSecond + 0.5);
                EncounterInstantiator.Instantiate(ct, mirror, anchorStep, route, t, (short)chosen, records);
                planned.Add(new PlannedEncounter { template = chosen, anchorStep = anchorStep, endStep = anchorStep + (int)((double)ct.SpanSecondsAt((float)ts) * HyperspaceTuning.StepsPerSecond) + 30, mirror = mirror, cost = ct.cost, kind = seg.kind, wave = seg.wave });
                spent += ct.cost;
                last2 = last1;
                last1 = chosen;
                fam2 = fam1;
                fam1 = ct.family;
                cursor = anchorTime + ct.SpanSecondsAt((float)ts) + gap;
            }

            int bossStep = -1;
            if (route.hasBoss)
            {
                EncounterTemplate bt = lib.ById("boss_guardian");
                bossStep = (int)((double)route.bossStart * HyperspaceTuning.StepsPerSecond + 0.5);
                var r = new SpawnRecord
                {
                    step = bossStep, arrivalStep = bossStep + (int)((double)t.bossArriveSeconds * HyperspaceTuning.StepsPerSecond + 0.5), type = EntityType.Guardian,
                    x = 0f, y = 0.6f, z0 = t.spawnDistance, path = PathType.Hold, template = (short)bt.index
                };
                records.Add(r);
                planned.Add(new PlannedEncounter { template = bt.index, anchorStep = r.arrivalStep, endStep = r.arrivalStep, mirror = false, cost = bt.cost, kind = PacingKind.Boss, wave = -1 });
                spent += bt.cost;
            }

            var arr = records.ToArray();
            for (int i = 1; i < arr.Length; i++)
            {
                SpawnRecord key = arr[i];
                int j = i - 1;
                while (j >= 0 && arr[j].step > key.step)
                {
                    arr[j + 1] = arr[j];
                    j--;
                }
                arr[j + 1] = key;
            }
            return new Schedule { records = arr, encounters = planned.ToArray(), spent = (float)spent, budgetTotal = route.budgetTotal, bossSpawnStep = bossStep };
        }
    }
}
