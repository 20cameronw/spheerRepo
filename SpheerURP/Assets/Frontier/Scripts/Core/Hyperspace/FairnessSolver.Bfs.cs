using System;
using System.Collections.Generic;
using System.Text;

namespace Frontier.Hyperspace
{
    public sealed partial class FairnessSolver
    {
        ulong[] cur;
        ulong[] sub;
        ulong[] union;
        ulong[] tmp;

        void Alloc()
        {
            cur = new ulong[rows];
            sub = new ulong[rows];
            union = new ulong[rows];
            tmp = new ulong[rows];
        }

        // ---------------------------------------------------------------- lattice helpers
        float CellX(int i) { return (float)(-t.areaHalfWidth + (double)i * cell); }
        float CellY(int j) { return (float)(-t.areaHalfHeight + (double)j * cell); }

        ulong XRange(float xLo, float xHi)
        {
            int iLo = (int)Math.Ceiling(((double)xLo + t.areaHalfWidth) / cell - 1e-4);
            int iHi = (int)Math.Floor(((double)xHi + t.areaHalfWidth) / cell + 1e-4);
            if (iLo < 0) iLo = 0;
            if (iHi > cols - 1) iHi = cols - 1;
            if (iHi < iLo) return 0UL;
            int w = iHi - iLo + 1;
            return (w >= 64 ? ~0UL : ((1UL << w) - 1UL)) << iLo;
        }

        void ClearDisc(ulong[] mask, float cx, float cy, float r)
        {
            int jLo = (int)Math.Ceiling(((double)cy - r + t.areaHalfHeight) / cell - 1e-4);
            int jHi = (int)Math.Floor(((double)cy + r + t.areaHalfHeight) / cell + 1e-4);
            if (jLo < 0) jLo = 0;
            if (jHi > rows - 1) jHi = rows - 1;
            for (int j = jLo; j <= jHi; j++)
            {
                double dy = (double)CellY(j) - cy;
                double d2 = (double)r * r - dy * dy;
                if (d2 < 0f) continue;
                double half = (float)Math.Sqrt(d2);
                mask[j] &= ~XRange((float)(cx - half), (float)(cx + half));
            }
        }

        void ApplyHazard(ulong[] mask, in Hazard h)
        {
            switch (h.shape)
            {
                case HazardShape.Disc:
                    ClearDisc(mask, h.a, h.b, (float)((double)h.c + reff));
                    break;
                case HazardShape.Slit:
                {
                    double half = (double)h.b - reff;
                    ulong m = half < 0f ? 0UL : XRange((float)(h.a - half), (float)(h.a + half));
                    for (int j = 0; j < rows; j++) mask[j] &= m;
                    break;
                }
                case HazardShape.Slot:
                {
                    double half = (double)h.b - reff;
                    for (int j = 0; j < rows; j++)
                    {
                        double dy = (double)CellY(j) - h.a;
                        if (half < 0f || dy > half || dy < -half) mask[j] = 0UL;
                    }
                    break;
                }
                case HazardShape.Ring:
                {
                    double rr = (double)h.c - reff;
                    for (int j = 0; j < rows; j++)
                    {
                        double dy = (double)CellY(j) - h.b;
                        double d2 = rr * rr - dy * dy;
                        if (rr < 0f || d2 < 0f) { mask[j] = 0UL; continue; }
                        double half = (float)Math.Sqrt(d2);
                        mask[j] &= XRange((float)(h.a - half), (float)(h.a + half));
                    }
                    break;
                }
                default:
                    for (int j = 0; j < rows; j++)
                    {
                        ulong m = mask[j];
                        if (m == 0UL) continue;
                        double y = CellY(j);
                        for (int i = 0; i < cols; i++)
                        {
                            if (((m >> i) & 1UL) == 0UL) continue;
                            if (h.Clearance(CellX(i), (float)y, reff) < 0f) m &= ~(1UL << i);
                        }
                        mask[j] = m;
                    }
                    break;
            }
        }

        ulong[] FullMask()
        {
            var m = new ulong[rows];
            for (int j = 0; j < rows; j++) m[j] = colMask;
            return m;
        }

        /// <summary>One step of travel: 3x3 dilation (horizontal then vertical), exactly the distance the ship covers in one step at the solver's speed.</summary>
        void Dilate(ulong[] a)
        {
            for (int j = 0; j < rows; j++)
            {
                ulong v = a[j];
                tmp[j] = (v | (v << 1) | (v >> 1)) & colMask;
            }
            for (int j = 0; j < rows; j++)
            {
                ulong v = tmp[j];
                if (j > 0) v |= tmp[j - 1];
                if (j < rows - 1) v |= tmp[j + 1];
                a[j] = v;
            }
        }

        static bool IsEmpty(ulong[] a)
        {
            for (int j = 0; j < a.Length; j++) if (a[j] != 0UL) return false;
            return true;
        }

        static int Count(ulong[] a)
        {
            int n = 0;
            for (int j = 0; j < a.Length; j++)
            {
                ulong v = a[j];
                while (v != 0UL)
                {
                    v &= v - 1UL;
                    n++;
                }
            }
            return n;
        }

        static bool MoveAllowed(int k, float f)
        {
            return (int)((double)(k + 1) * f) > (int)((double)k * f);
        }

        // ---------------------------------------------------------------- the search
        sealed class LockGroup
        {
            public int lockK;
            public int endK;
            public List<LockEntry> items = new List<LockEntry>();
        }

        SolveResult Solve(Recording rec, float speedFraction)
        {
            var res = new SolveResult { minSpeedFraction = speedFraction };
            HazardLog log = rec.log;
            int leadSteps = (int)((double)t.entryLead * HyperspaceTuning.StepsPerSecond + 0.5);
            int s0 = rec.firstStep - leadSteps;
            if (s0 < 0) s0 = 0;
            int n = rec.lastStep - s0 + 1;
            res.hazardRecords = log.hazardCount;

            var safe = new ulong[n][];
            int hi = 0;
            for (int k = 0; k < n; k++)
            {
                int step = s0 + k;
                ulong[] m = FullMask();
                while (hi < log.hazardCount && log.hazards[hi].step < step) hi++;
                for (int j = hi; j < log.hazardCount && log.hazards[j].step == step; j++) ApplyHazard(m, log.hazards[j].hazard);
                safe[k] = m;
            }

            List<LockGroup> groups = BuildGroups(log, s0, n);
            res.lockGroups = groups.Count;
            for (int g = 1; g < groups.Count; g++)
            {
                if (groups[g].lockK <= groups[g - 1].endK)
                {
                    res.unsupported = true;
                    return res;
                }
            }

            for (int j = 0; j < rows; j++) cur[j] = colMask & safe[0][j];
            if (IsEmpty(cur))
            {
                res.failTime = (float)((double)(s0 - rec.anchorStep) / 60.0);
                res.failSources = Sources(log, s0);
                return res;
            }
            int gi = 0;
            for (int k = 1; k < n; k++)
            {
                if (MoveAllowed(k, speedFraction)) Dilate(cur);
                for (int j = 0; j < rows; j++) cur[j] &= safe[k][j];
                if (IsEmpty(cur))
                {
                    res.failTime = (float)((double)(s0 + k - rec.anchorStep) / 60.0);
                    res.failSources = Sources(log, s0 + k);
                    return res;
                }
                if (gi < groups.Count && k == groups[gi].lockK)
                {
                    ProcessGroup(groups[gi], safe, speedFraction, s0);
                    k = groups[gi].endK;
                    gi++;
                    if (IsEmpty(cur))
                    {
                        res.failTime = (float)((double)(s0 + k - rec.anchorStep) / 60.0);
                        res.failSources = "lock:" + Sources(log, s0 + k);
                        return res;
                    }
                }
            }
            res.survivors = Count(cur);
            res.solvable = res.survivors > 0;
            return res;
        }

        List<LockGroup> BuildGroups(HazardLog log, int s0, int n)
        {
            var list = new List<LockEntry>();
            for (int i = 0; i < log.lockCount; i++) list.Add(log.locks[i]);
            for (int i = 1; i < list.Count; i++)
            {
                LockEntry key = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].lockStep > key.lockStep)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = key;
            }
            var groups = new List<LockGroup>();
            for (int i = 0; i < list.Count; i++)
            {
                LockGroup g = groups.Count > 0 ? groups[groups.Count - 1] : null;
                int lk = list[i].lockStep - s0;
                if (g == null || g.lockK != lk)
                {
                    g = new LockGroup { lockK = lk, endK = lk };
                    groups.Add(g);
                }
                g.items.Add(list[i]);
                int e = list[i].arriveStep + 3 - s0;
                if (e > g.endK) g.endK = e;
                if (g.endK > n - 1) g.endK = n - 1;
            }
            return groups;
        }

        /// <summary>For every ship position at the lock step (sampled every 3 cells), simulates the window with the ship frozen for the reaction budget and then free, forbids the aimed discs at their
        /// arrival steps, and unions the survivors into the reachable set at the end of the window.</summary>
        void ProcessGroup(LockGroup g, ulong[][] safe, float speedFraction, int s0)
        {
            const int stride = 3;
            for (int j = 0; j < rows; j++) union[j] = 0UL;
            var seeds = new List<int>();
            for (int j = 0; j < rows; j += stride)
                for (int i = 0; i < cols; i += stride)
                    if (((cur[j] >> i) & 1UL) != 0UL) seeds.Add(j * 64 + i);
            for (int sIdx = 0; sIdx < seeds.Count; sIdx++)
            {
                int sj = seeds[sIdx] / 64, si = seeds[sIdx] % 64;
                for (int j = 0; j < rows; j++) sub[j] = 0UL;
                sub[sj] = 1UL << si;
                double lx = CellX(si), ly = CellY(sj);
                bool alive = true;
                for (int m = g.lockK + 1; m <= g.endK && alive; m++)
                {
                    if (m - g.lockK > reactSteps && MoveAllowed(m, speedFraction)) Dilate(sub);
                    for (int j = 0; j < rows; j++) sub[j] &= safe[m][j];
                    int step = s0 + m;
                    for (int c = 0; c < g.items.Count; c++)
                    {
                        LockEntry le = g.items[c];
                        int d = step - le.arriveStep;
                        if (d >= -2 && d <= 2) ClearDisc(sub, (float)(lx + le.offX), (float)(ly + le.offY), (float)((double)le.radius + reff));
                    }
                    alive = !IsEmpty(sub);
                }
                if (alive) for (int j = 0; j < rows; j++) union[j] |= sub[j];
            }
            for (int j = 0; j < rows; j++) cur[j] = union[j];
        }

        /// <summary>Human-readable diagnostics of one template instance: how many hazards cross the plane, how restrictive they are and when.</summary>
        public string Describe(EncounterTemplate tp, int tier, bool mirror, float routeSpeed)
        {
            Recording rec = RecordTemplate(tp, tier, mirror, routeSpeed);
            HazardLog log = rec.log;
            var sb = new StringBuilder();
            sb.Append(tp.id + " tier " + tier + (mirror ? " mirrored" : "") + " speed " + routeSpeed.ToString("0.0") + ": hazards=" + log.hazardCount + " locks=" + log.lockCount
                + " steps " + (rec.firstStep - rec.anchorStep) + ".." + (rec.lastStep - rec.anchorStep) + " (anchor " + rec.anchorStep + ")\n");
            int s0 = rec.firstStep;
            double minFree = 1.0;
            int minStep = s0;
            int hi = 0;
            for (int step = s0; step <= rec.lastStep; step++)
            {
                ulong[] m = FullMask();
                while (hi < log.hazardCount && log.hazards[hi].step < step) hi++;
                bool any = false;
                for (int j = hi; j < log.hazardCount && log.hazards[j].step == step; j++)
                {
                    ApplyHazard(m, log.hazards[j].hazard);
                    any = true;
                }
                if (!any) continue;
                double free = (double)Count(m) / (float)(cols * rows);
                if (free < minFree)
                {
                    minFree = free;
                    minStep = step;
                }
            }
            sb.Append("  most restrictive step " + (minStep - rec.anchorStep) + " free fraction " + minFree.ToString("0.000") + " sources " + Sources(log, minStep) + "\n");
            for (int i = 0; i < log.lockCount && i < 8; i++)
                sb.Append("  lock " + (log.locks[i].lockStep - rec.anchorStep) + " -> " + (log.locks[i].arriveStep - rec.anchorStep) + " off " + log.locks[i].offX.ToString("0.0") + " r " + log.locks[i].radius.ToString("0.00") + "\n");
            return sb.ToString();
        }

        static string Sources(HazardLog log, int step)
        {
            var sb = new StringBuilder();
            var seen = new HashSet<EntityType>();
            for (int i = 0; i < log.hazardCount; i++)
            {
                if (log.hazards[i].step != step || !seen.Add(log.hazards[i].source)) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(log.hazards[i].source);
            }
            return sb.Length == 0 ? "none" : sb.ToString();
        }
    }
}
