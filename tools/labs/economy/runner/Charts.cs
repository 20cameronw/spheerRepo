using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Frontier.Economy;
using Frontier.Universe;
using Frontier.Universe.Runner;

namespace Frontier.Economy.Runner
{
    /// <summary>Balance charts drawn from the simulator timelines (PNG, uses the universe sandbox canvas).</summary>
    public static class Charts
    {
        static readonly float[][] ArchColor =
        {
            new[] { 1.00f, 0.60f, 0.24f }, // casual
            new[] { 0.20f, 0.78f, 0.91f }, // regular
            new[] { 0.89f, 0.30f, 0.61f }  // hardcore
        };

        static readonly float[][] EraColor =
        {
            new[] { 0.55f, 0.70f, 0.40f }, new[] { 0.35f, 0.75f, 0.60f }, new[] { 0.25f, 0.65f, 0.85f },
            new[] { 0.45f, 0.45f, 0.95f }, new[] { 0.75f, 0.40f, 0.90f }, new[] { 0.95f, 0.45f, 0.55f }
        };

        const float Bg = 0.055f;

        static float[] Hex(string h)
        {
            return new[] { Convert.ToInt32(h.Substring(1, 2), 16) / 255f, Convert.ToInt32(h.Substring(3, 2), 16) / 255f, Convert.ToInt32(h.Substring(5, 2), 16) / 255f };
        }

        static void Fill(Canvas c, int x0, int y0, int x1, int y1, float[] col, float a)
        {
            for (int y = Math.Max(0, y0); y < Math.Min(c.Height, y1); y++)
                for (int x = Math.Max(0, x0); x < Math.Min(c.Width, x1); x++) c.Blend(x, y, col[0], col[1], col[2], a);
        }

        static void Line(Canvas c, double x0, double y0, double x1, double y1, float[] col, float a, int thick)
        {
            double dx = x1 - x0, dy = y1 - y0;
            int steps = (int)Math.Max(1.0, Math.Max(Math.Abs(dx), Math.Abs(dy)));
            for (int i = 0; i <= steps; i++)
            {
                double x = x0 + dx * i / steps, y = y0 + dy * i / steps;
                for (int ty = -thick / 2; ty <= thick / 2; ty++)
                    for (int tx = -thick / 2; tx <= thick / 2; tx++) c.Blend((int)Math.Round(x) + tx, (int)Math.Round(y) + ty, col[0], col[1], col[2], a);
            }
        }

        static void Dashed(Canvas c, double x0, double y0, double x1, double y1, float[] col, float a)
        {
            double len = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int n = (int)(len / 6.0);
            for (int i = 0; i < n; i += 2)
            {
                double t0 = (double)i / n, t1 = (double)(i + 1) / n;
                Line(c, x0 + (x1 - x0) * t0, y0 + (y1 - y0) * t0, x0 + (x1 - x0) * t1, y0 + (y1 - y0) * t1, col, a, 1);
            }
        }

        static string Days(double d)
        {
            if (d < 1.0 / 24.0) return (d * 1440.0).ToString("0") + " MIN";
            if (d < 1.0) return (d * 24.0).ToString("0") + " H";
            return d.ToString("0") + " D";
        }

        sealed class Axes
        {
            public int X0, Y0, X1, Y1;
            public double LogMin, LogMax, YMin, YMax;
            public bool YLog;

            public double PX(double days) { double l = Math.Log10(Math.Max(days, Math.Pow(10.0, LogMin))); return X0 + (l - LogMin) / (LogMax - LogMin) * (X1 - X0); }

            public double PY(double v)
            {
                double t = YLog ? (Math.Log10(Math.Max(v, Math.Pow(10.0, YMin))) - YMin) / (YMax - YMin) : (v - YMin) / (YMax - YMin);
                if (t < 0) t = 0; if (t > 1) t = 1;
                return Y1 - t * (Y1 - Y0);
            }
        }

        static void DrawFrame(Canvas c, Axes a, bool xTicks)
        {
            Fill(c, a.X0, a.Y0, a.X1, a.Y1, new[] { 0.09f, 0.10f, 0.14f }, 1f);
            double[] marks = { 0.001, 1.0 / 24.0, 1.0, 7.0, 30.0, 100.0, 365.0, 730.0 };
            foreach (double m in marks)
            {
                if (Math.Log10(m) < a.LogMin || Math.Log10(m) > a.LogMax) continue;
                double x = a.PX(m);
                Line(c, x, a.Y0, x, a.Y1, new[] { 0.25f, 0.27f, 0.33f }, 0.5f, 1);
                if (xTicks) c.Text((int)x - 12, a.Y1 + 6, m < 0.01 ? "1 MIN" : (m < 0.5 ? "1 H" : (m == 7.0 ? "1 WEEK" : (m == 30.0 ? "1 MONTH" : (m == 365.0 ? "1 YEAR" : (m == 730.0 ? "2 YEARS" : m.ToString("0") + " D"))))), 1, 0.7f, 0.74f, 0.82f);
            }
            Line(c, a.X0, a.Y1, a.X1, a.Y1, new[] { 0.6f, 0.62f, 0.7f }, 1f, 1);
            Line(c, a.X0, a.Y0, a.X0, a.Y1, new[] { 0.6f, 0.62f, 0.7f }, 1f, 1);
        }

        public static int Run(string[] args)
        {
            string outDir = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "..", "reports");
            Directory.CreateDirectory(outDir);
            var t = Tuning.CreateDefault();
            EconomyCatalog cat = CatalogBuilder.Build(t);
            SimWorld world = SimCommand.BuildWorld(cat, 1UL, 3600.0);
            var results = new List<SimResult>();
            foreach (Archetype a in Archetype.All()) results.Add(SimEngine.Run(cat, t, world, a, new SimSettings { seed = 1UL, horizonDays = 730.0 }));
            ResourcesOverTime(Path.Combine(outDir, "chart_resources_over_time.png"), cat, results);
            TierOverTime(Path.Combine(outDir, "chart_tier_over_time.png"), results);
            TimePerEra(Path.Combine(outDir, "chart_time_per_era.png"), results);
            ScoreOverTime(Path.Combine(outDir, "chart_score_over_time.png"), results);
            Console.WriteLine("charts written to " + Path.GetFullPath(outDir));
            return 0;
        }

        // ---------------------------------------------------------------------------------------------- resources over time
        static void ResourcesOverTime(string path, EconomyCatalog cat, List<SimResult> results)
        {
            int w = 1500, panelH = 330, gap = 60, top = 70;
            var c = new Canvas(w, top + results.Count * (panelH + gap) + 20);
            Fill(c, 0, 0, c.Width, c.Height, new[] { Bg, Bg, Bg + 0.03f }, 1f);
            c.Text(30, 18, "RESOURCES HELD OVER TIME (PEAK VALUE IN SCRAP-EQUIVALENTS, LOG SCALE; THIN WHITE LINE = EVERYTHING GATHERED SO FAR)", 2, 0.95f, 0.95f, 1f);
            string[] names = { "RAW", "PROCESSED", "ALLOY", "COMPONENT", "ENERGY" };
            for (int k = 0; k < names.Length; k++)
            {
                float[] col = Hex(cat.categoryHex[k]);
                Fill(c, 30 + k * 150, 46, 46 + k * 150, 58, col, 1f);
                c.Text(52 + k * 150, 48, names[k], 1, 0.85f, 0.88f, 0.95f);
            }
            for (int i = 0; i < results.Count; i++)
            {
                SimResult r = results[i];
                var a = new Axes { X0 = 90, X1 = w - 40, Y0 = top + i * (panelH + gap) + 20, Y1 = top + i * (panelH + gap) + panelH, LogMin = -3.0, LogMax = Math.Log10(730.0), YMin = 0.0, YMax = 9.0, YLog = true };
                DrawFrame(c, a, i == results.Count - 1 || true);
                for (int e = 0; e <= 9; e += 3)
                {
                    int y = (int)a.PY(Math.Pow(10.0, e));
                    Line(c, a.X0, y, a.X1, y, new[] { 0.25f, 0.27f, 0.33f }, 0.5f, 1);
                    c.Text(a.X0 - 44, y - 3, "1E" + e, 1, 0.7f, 0.74f, 0.82f);
                }
                for (int cat2 = 0; cat2 < 5; cat2++)
                {
                    float[] col = Hex(cat.categoryHex[cat2]);
                    double px = 0, py = 0;
                    bool first = true;
                    for (int si = 0; si < r.samples.Count; si++)
                    {
                        SimSample s = r.samples[si];
                        // stock is spent as fast as it is gathered, so the line shows the peak held over the last 40 % of the elapsed time
                        double peak = s.categoryValue[cat2];
                        for (int sj = si - 1; sj >= 0 && r.samples[sj].days >= s.days / 1.4; sj--) peak = Math.Max(peak, r.samples[sj].categoryValue[cat2]);
                        double x = a.PX(Math.Max(s.days, 0.001)), y = a.PY(Math.Max(1.0, peak));
                        if (!first) Line(c, px, py, x, y, col, 0.9f, 2);
                        px = x; py = y; first = false;
                    }
                }
                {
                    double px = 0, py = 0;
                    bool first = true;
                    foreach (SimSample s in r.samples)
                    {
                        double x = a.PX(Math.Max(s.days, 0.001)), y = a.PY(Math.Max(1.0, s.harvestedValue));
                        if (!first) Line(c, px, py, x, y, new[] { 0.95f, 0.95f, 0.98f }, 0.55f, 1);
                        px = x; py = y; first = false;
                    }
                }
                EraMarkers(c, a, r);
                c.Text(a.X0 + 8, a.Y0 + 6, r.archetype.ToUpperInvariant() + " (" + Archetype.All()[i].sessionsPerDay + " X " + Archetype.All()[i].minutesPerSession.ToString("0") + " MIN A DAY)", 2, ArchColor[i][0], ArchColor[i][1], ArchColor[i][2]);
            }
            c.Save(path);
        }

        static void EraMarkers(Canvas c, Axes a, SimResult r)
        {
            string[] ms = SimBands.EraMilestones;
            for (int e = 1; e < ms.Length; e++)
            {
                double d = r.Days(ms[e]);
                if (double.IsNaN(d)) continue;
                double x = a.PX(d);
                Dashed(c, x, a.Y0, x, a.Y1, EraColor[e], 0.9f);
                c.Text((int)x + 3, a.Y1 - 12, SimBands.EraNames[e].ToUpperInvariant(), 1, EraColor[e][0], EraColor[e][1], EraColor[e][2]);
            }
        }

        // ---------------------------------------------------------------------------------------------- tier over time
        static void TierOverTime(string path, List<SimResult> results)
        {
            int w = 1500, h = 620;
            var c = new Canvas(w, h);
            Fill(c, 0, 0, w, h, new[] { Bg, Bg, Bg + 0.03f }, 1f);
            c.Text(30, 18, "HIGHEST PLANET TIER REACHED (TIME ON A LOG SCALE)", 2, 0.95f, 0.95f, 1f);
            var a = new Axes { X0 = 90, X1 = w - 50, Y0 = 80, Y1 = h - 60, LogMin = -3.0, LogMax = Math.Log10(730.0), YMin = 0.0, YMax = 10.0, YLog = false };
            DrawFrame(c, a, true);
            for (int tier = 0; tier <= 10; tier++)
            {
                double y = a.PY(tier);
                Line(c, a.X0, y, a.X1, y, new[] { 0.25f, 0.27f, 0.33f }, 0.5f, 1);
                c.Text(a.X0 - 24, (int)y - 3, "T" + tier, 1, 0.7f, 0.74f, 0.82f);
            }
            for (int i = 0; i < results.Count; i++)
            {
                SimResult r = results[i];
                double px = 0, py = 0;
                bool first = true;
                foreach (SimSample s in r.samples)
                {
                    double x = a.PX(Math.Max(s.days, 0.001)), y = a.PY(s.reachTier);
                    if (!first) { Line(c, px, py, x, py, ArchColor[i], 0.95f, 3); Line(c, x, py, x, y, ArchColor[i], 0.95f, 3); }
                    px = x; py = y; first = false;
                }
                Fill(c, 110 + i * 200, 52, 126 + i * 200, 64, ArchColor[i], 1f);
                c.Text(132 + i * 200, 54, r.archetype.ToUpperInvariant(), 1, 0.9f, 0.92f, 0.98f);
            }
            for (int e = 1; e < SimBands.EraMilestones.Length; e++)
            {
                // era names across the top, positioned at the regular player's entry
                double d = results[1].Days(SimBands.EraMilestones[e]);
                if (double.IsNaN(d)) continue;
                double x = a.PX(d);
                Dashed(c, x, a.Y0, x, a.Y1, EraColor[e], 0.8f);
                c.Text((int)x + 3, a.Y0 + 4 + e * 10, SimBands.EraNames[e].ToUpperInvariant() + " (REGULAR)", 1, EraColor[e][0], EraColor[e][1], EraColor[e][2]);
            }
            c.Save(path);
        }

        // ---------------------------------------------------------------------------------------------- time per era
        static void TimePerEra(string path, List<SimResult> results)
        {
            int w = 1500, h = 900;
            var c = new Canvas(w, h);
            Fill(c, 0, 0, w, h, new[] { Bg, Bg, Bg + 0.03f }, 1f);
            c.Text(30, 18, "TIME SPENT IN EACH ERA (DAYS)", 2, 0.95f, 0.95f, 1f);
            for (int e = 0; e < 6; e++)
            {
                Fill(c, 30 + e * 190, 50, 46 + e * 190, 62, EraColor[e], 1f);
                c.Text(52 + e * 190, 52, SimBands.EraNames[e].ToUpperInvariant(), 1, 0.9f, 0.92f, 0.98f);
            }
            // panel 1: absolute days, sqrt scale so the hardcore bar stays readable
            c.Text(30, 90, "ABSOLUTE (SQUARE ROOT SCALE OF DAYS)", 1, 0.7f, 0.74f, 0.82f);
            int bx0 = 160, bx1 = w - 60;
            double maxDays = 730.0;
            for (int i = 0; i < results.Count; i++)
            {
                SimResult r = results[i];
                int y = 120 + i * 90;
                c.Text(30, y + 14, r.archetype.ToUpperInvariant(), 2, ArchColor[i][0], ArchColor[i][1], ArchColor[i][2]);
                double prev = 0.0;
                for (int e = 0; e < 6; e++)
                {
                    double entry = e == 0 ? 0.0 : r.Days(SimBands.EraMilestones[e]);
                    double next = e == 5 ? maxDays : r.Days(SimBands.EraMilestones[e + 1]);
                    if (double.IsNaN(entry)) break;
                    if (double.IsNaN(next)) next = maxDays;
                    double xa = bx0 + Math.Sqrt(entry / maxDays) * (bx1 - bx0), xb = bx0 + Math.Sqrt(next / maxDays) * (bx1 - bx0);
                    Fill(c, (int)xa, y, (int)xb, y + 44, EraColor[e], 0.95f);
                    double len = next - entry;
                    if (xb - xa > 50) c.Text((int)xa + 4, y + 16, (len < 1.0 ? len.ToString("0.0") : len.ToString("0")) + " D", 1, 0.02f, 0.02f, 0.08f);
                    prev = next;
                }
                if (prev <= 0.0) prev = 0.0;
            }
            foreach (double tick in new[] { 1.0, 7.0, 30.0, 100.0, 365.0, 730.0 })
            {
                int x = (int)(bx0 + Math.Sqrt(tick / maxDays) * (bx1 - bx0));
                Line(c, x, 112, x, 112 + 3 * 90 - 10, new[] { 0.6f, 0.62f, 0.7f }, 0.35f, 1);
                c.Text(x - 12, 112 + 3 * 90 - 4, tick == 730.0 ? "2 YEARS" : (tick == 365.0 ? "1 YEAR" : (tick == 7.0 ? "1 WEEK" : tick.ToString("0") + " D")), 1, 0.7f, 0.74f, 0.82f);
            }
            // panel 2: share of the time until the Ascension era is entered
            int top2 = 480;
            c.Text(30, top2 - 30, "SHARE OF THE TIME UNTIL THE ASCENSION ERA BEGINS", 1, 0.7f, 0.74f, 0.82f);
            for (int i = 0; i < results.Count; i++)
            {
                SimResult r = results[i];
                double total = r.Days("era_ascension");
                if (double.IsNaN(total)) total = 730.0;
                int y = top2 + i * 90;
                c.Text(30, y + 14, r.archetype.ToUpperInvariant(), 2, ArchColor[i][0], ArchColor[i][1], ArchColor[i][2]);
                for (int e = 0; e < 5; e++)
                {
                    double entry = e == 0 ? 0.0 : r.Days(SimBands.EraMilestones[e]);
                    double next = r.Days(SimBands.EraMilestones[e + 1]);
                    if (double.IsNaN(entry) || double.IsNaN(next)) continue;
                    int xa = (int)(bx0 + entry / total * (bx1 - bx0)), xb = (int)(bx0 + Math.Min(1.0, next / total) * (bx1 - bx0));
                    Fill(c, xa, y, xb, y + 44, EraColor[e], 0.95f);
                    double share = (next - entry) / total * 100.0;
                    if (xb - xa > 40) c.Text(xa + 4, y + 16, share.ToString("0") + " %", 1, 0.02f, 0.02f, 0.08f);
                }
                c.Text(bx1 - 150, y + 52, "TOTAL " + total.ToString("0") + " DAYS", 1, 0.8f, 0.84f, 0.92f);
            }
            c.Save(path);
        }

        // ---------------------------------------------------------------------------------------------- score over time
        static void ScoreOverTime(string path, List<SimResult> results)
        {
            int w = 1500, h = 620;
            var c = new Canvas(w, h);
            Fill(c, 0, 0, w, h, new[] { Bg, Bg, Bg + 0.03f }, 1f);
            c.Text(30, 18, "FRONTIER SCORE OVER TIME (LOG SCALE) AND THE PLAN TARGETS", 2, 0.95f, 0.95f, 1f);
            var a = new Axes { X0 = 90, X1 = w - 50, Y0 = 80, Y1 = h - 60, LogMin = -3.0, LogMax = Math.Log10(730.0), YMin = 2.0, YMax = 7.0, YLog = true };
            DrawFrame(c, a, true);
            for (int e = 2; e <= 7; e++)
            {
                double y = a.PY(Math.Pow(10.0, e));
                Line(c, a.X0, y, a.X1, y, new[] { 0.25f, 0.27f, 0.33f }, 0.5f, 1);
                c.Text(a.X0 - 44, (int)y - 3, "1E" + e, 1, 0.7f, 0.74f, 0.82f);
            }
            foreach (double target in new[] { 5000.0, 10000.0, 100000.0, 1000000.0 })
            {
                double y = a.PY(target);
                Dashed(c, a.X0, y, a.X1, y, new[] { 0.95f, 0.85f, 0.3f }, 0.7f);
                c.Text(a.X1 - 130, (int)y - 10, "TARGET " + (target >= 1e6 ? "1M" : (target / 1000.0).ToString("0") + "K"), 1, 0.95f, 0.85f, 0.3f);
            }
            for (int i = 0; i < results.Count; i++)
            {
                double px = 0, py = 0;
                bool first = true;
                foreach (SimSample s in results[i].samples)
                {
                    double x = a.PX(Math.Max(s.days, 0.001)), y = a.PY(Math.Max(100.0, s.score));
                    if (!first) Line(c, px, py, x, y, ArchColor[i], 0.95f, 3);
                    px = x; py = y; first = false;
                }
                Fill(c, 110 + i * 200, 52, 126 + i * 200, 64, ArchColor[i], 1f);
                c.Text(132 + i * 200, 54, results[i].archetype.ToUpperInvariant(), 1, 0.9f, 0.92f, 0.98f);
            }
            c.Save(path);
        }
    }
}
