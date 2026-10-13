using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>Time of build, upgrade and clear jobs: a geometric curve over a level index (0 = instant ... 11 = a day), shifted per structure.</summary>
    public sealed class JobTimes
    {
        public const int AnchorCount = 12;
        readonly double[] _anchors = new double[AnchorCount];
        readonly double _cap;
        readonly double[] _mega = new double[5];
        readonly double _megaCap;

        public JobTimes(Tuning t)
        {
            for (int i = 0; i < AnchorCount; i++) _anchors[i] = t.D("time.anchor." + i);
            _cap = t.D("time.cap_seconds");
            for (int i = 0; i < 5; i++) _mega[i] = t.D("time.mega.stage" + (i + 1));
            _megaCap = t.D("time.mega_cap_seconds");
        }

        public static void RegisterKeys(Tuning t)
        {
            double[] def = { 0, 5, 20, 60, 180, 600, 1800, 3600, 10800, 28800, 57600, 86400 };
            for (int i = 0; i < AnchorCount; i++)
                t.Register("time", "time.anchor." + i, def[i], 0, 604800, "Seconds of a job at level index " + i + " of the shared time curve (index 0 is instant, 11 is one day). Intermediate indices interpolate geometrically.");
            t.Register("time", "time.cap_seconds", 86400, 3600, 604800, "Hard cap for ordinary build/upgrade jobs (about one day).");
            double[] mega = { 172800, 259200, 345600, 432000, 604800 };
            for (int i = 0; i < 5; i++)
                t.Register("time", "time.mega.stage" + (i + 1), mega[i], 3600, 2592000, "Seconds of megaproject stage " + (i + 1) + " (before the per-project multiplier).");
            t.Register("time", "time.mega_cap_seconds", 864000, 86400, 2592000, "Hard cap for a megaproject stage.");
            t.Register("time", "time.scale", 1.0, 0.1, 10, "Global multiplier on every structure job time (1 = as designed).");
        }

        /// <summary>Seconds at a fractional level index of the shared curve (0 below index 0, capped at the ordinary cap).</summary>
        public int Seconds(double index)
        {
            if (!(index > 0.0)) return 0;
            double s;
            if (index >= AnchorCount - 1) s = _anchors[AnchorCount - 1];
            else
            {
                int i = (int)Math.Floor(index);
                double f = index - i;
                double a = _anchors[i], b = _anchors[i + 1];
                s = i == 0 ? b * f : a * DetMath.Pow(b / a, f);
            }
            if (s > _cap) s = _cap;
            return CostMath.NiceSeconds(s);
        }

        /// <summary>Seconds of level <paramref name="level"/> of a structure whose level 1 sits at index <paramref name="start"/> and climbs <paramref name="step"/> per level.</summary>
        public int StructureSeconds(double start, double step, int level, double scale)
        {
            double s = Seconds(start + step * (level - 1));
            double scaled = s * scale;
            if (scaled > _cap) scaled = _cap;
            return CostMath.NiceSeconds(scaled);
        }

        /// <summary>Seconds of a megaproject stage (1 based) with a per-project multiplier, capped.</summary>
        public int MegaStageSeconds(int stage, double projectMult)
        {
            double s = _mega[Math.Max(0, Math.Min(4, stage - 1))] * projectMult;
            if (s > _megaCap) s = _megaCap;
            return CostMath.NiceSeconds(s);
        }

        public double Cap { get { return _cap; } }

        public double MegaCap { get { return _megaCap; } }
    }
}
