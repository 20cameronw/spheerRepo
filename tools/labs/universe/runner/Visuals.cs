using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Frontier.Universe.Runner
{
    /// <summary>Renders the visual checks (density maps, system dots, tier rings) as PNG files.</summary>
    public static class Visuals
    {
        static Universe MakeUniverse()
        {
            var cfg = new UniverseConfig();
            return new Universe(cfg, Frontier.Universe.Tests.FakeAuthored.Create(cfg));
        }

        static Rgb Hsv(double h, double s, double v)
        {
            h = h - Math.Floor(h);
            double h6 = h * 6.0;
            int i = (int)Math.Floor(h6);
            double f = h6 - i;
            double p = v * (1.0 - s), q = v * (1.0 - s * f), t = v * (1.0 - s * (1.0 - f));
            switch (i % 6)
            {
                case 0: return new Rgb((float)v, (float)t, (float)p);
                case 1: return new Rgb((float)q, (float)v, (float)p);
                case 2: return new Rgb((float)p, (float)v, (float)t);
                case 3: return new Rgb((float)p, (float)q, (float)v);
                case 4: return new Rgb((float)t, (float)p, (float)v);
                default: return new Rgb((float)v, (float)p, (float)q);
            }
        }

        /// <summary>Maps a density sample to a colour: black void, tinted galaxies, warm bulges.</summary>
        static void DensityColour(DensitySample s, out float r, out float g, out float b)
        {
            double t = Math.Max(0.0, s.GalaxyDensity - 0.02);
            float v = (float)(t / (t + 0.24));
            Rgb tint = s.Galaxy != null ? s.Galaxy.Tint : new Rgb(0.6f, 0.7f, 1.0f);
            float core = (float)(s.BulgeShare * v);
            float haze = 0.010f;
            r = 0.004f + haze * 0.6f + v * (tint.R * (1f - core) + 1.00f * core);
            g = 0.006f + haze * 0.7f + v * (tint.G * (1f - core) + 0.92f * core);
            b = 0.018f + haze * 1.0f + v * (tint.B * (1f - core) + 0.78f * core);
        }

        static void Put(Canvas img, int x, int y, float r, float g, float b)
        {
            img.Set(x, y, Canvas.ToByte(r * 255f), Canvas.ToByte(g * 255f), Canvas.ToByte(b * 255f));
        }

        static void WorldToPixel(double cx, double cy, double size, int px, double wx, double wy, out double x, out double y)
        {
            x = (wx - cx) / size * px + px * 0.5;
            y = px * 0.5 - (wy - cy) / size * px;
        }

        static void PaintDensity(Universe u, Canvas img, double cx, double cy, double size, float dim)
        {
            int px = img.Width;
            for (int y = 0; y < px; y++)
            {
                for (int x = 0; x < px; x++)
                {
                    double wx = cx + ((x + 0.5) / px - 0.5) * size;
                    double wy = cy + (0.5 - (y + 0.5) / px) * size;
                    DensitySample s;
                    u.SampleDensity(wx, wy, out s);
                    float r, g, b;
                    DensityColour(s, out r, out g, out b);
                    Put(img, x, y, r * dim, g * dim, b * dim);
                }
            }
        }

        static Rgb Boost(Rgb c, float k)
        {
            float m = Math.Max(c.R, Math.Max(c.G, c.B));
            float f = m > 0f ? k / m : 1f;
            return new Rgb(Math.Min(1f, c.R * f), Math.Min(1f, c.G * f), Math.Min(1f, c.B * f));
        }

        static void ScaleBar(Canvas img, double sizeLy, int px, double lengthLy, string label)
        {
            int len = (int)Math.Round(lengthLy / sizeLy * px);
            int x0 = 24, y0 = img.Height - 36;
            for (int x = x0; x <= x0 + len; x++) { img.Blend(x, y0, 1f, 1f, 1f, 0.9f); img.Blend(x, y0 + 1, 1f, 1f, 1f, 0.9f); }
            for (int y = y0 - 5; y <= y0 + 6; y++) { img.Blend(x0, y, 1f, 1f, 1f, 0.9f); img.Blend(x0 + len, y, 1f, 1f, 1f, 0.9f); }
            img.Label(x0, y0 - 22, label, 2, 1f, 1f, 1f);
        }

        // ---- (a) and (b): density maps ----------------------------------------------------------------------------------

        public static void RenderDensity(Universe u, double cx, double cy, double size, int px, string path, string title, bool drawHalo, bool markAuthored)
        {
            var img = new Canvas(px, px);
            PaintDensity(u, img, cx, cy, size, 1f);

            int outposts = 0;
            if (drawHalo)
            {
                int s0x = (int)Math.Floor((cx - size * 0.5) / 64.0), s1x = (int)Math.Floor((cx + size * 0.5) / 64.0);
                int s0y = (int)Math.Floor((cy - size * 0.5) / 64.0), s1y = (int)Math.Floor((cy + size * 0.5) / 64.0);
                for (int sy = s0y; sy <= s1y; sy++)
                {
                    for (int sx = s0x; sx <= s1x; sx++)
                    {
                        foreach (StarSystem s in u.SystemsInSector(new SectorCoord(sx, sy)))
                        {
                            if (s.GalaxyId != 0UL) continue;
                            double x, y;
                            WorldToPixel(cx, cy, size, px, s.X, s.Y, out x, out y);
                            img.Blend((int)x, (int)y, 0.75f, 0.82f, 1f, 0.75f);
                            outposts++;
                        }
                    }
                }
            }

            double hx, hy;
            WorldToPixel(cx, cy, size, px, 0.0, 0.0, out hx, out hy);
            if (hx > 20 && hx < px - 20 && hy > 20 && hy < px - 20)
            {
                img.Ring(hx, hy, size > 10000 ? 9.0 : 14.0, 2.0, 0.3f, 1f, 0.9f, 1f);
                img.Label((int)hx + 16, (int)hy - 16, "HOME", 2, 0.4f, 1f, 0.9f);
            }
            if (markAuthored)
            {
                for (int i = 0; i < u.AuthoredSystems.Count; i++)
                {
                    double x, y;
                    WorldToPixel(cx, cy, size, px, u.AuthoredSystems[i].X, u.AuthoredSystems[i].Y, out x, out y);
                    img.Ring(x, y, 7.0, 1.6, 1f, 0.95f, 0.4f, 1f);
                }
            }
            if (title != null) img.Label(16, 14, title, 3, 0.9f, 0.95f, 1f);
            ScaleBar(img, size, px, size > 10000 ? 5000.0 : 1000.0, size > 10000 ? "5000 LY" : "1000 LY");
            img.Save(path);
            Console.WriteLine("wrote " + path + (drawHalo ? " (" + outposts + " intergalactic outposts drawn)" : ""));
        }

        // ---- (c): systems as dots ---------------------------------------------------------------------------------------

        public static void RenderSystems(Universe u, double cx, double cy, double size, int px, string path)
        {
            var img = new Canvas(px, px);
            PaintDensity(u, img, cx, cy, size, 0.35f);

            for (double g = (Math.Floor((cx - size * 0.5) / 64.0) + 1) * 64.0; g < cx + size * 0.5; g += 64.0)
            {
                double x, y;
                WorldToPixel(cx, cy, size, px, g, cy, out x, out y);
                for (int k = 0; k < px; k++) img.Blend((int)x, k, 0.6f, 0.7f, 1f, 0.07f);
            }
            for (double g = (Math.Floor((cy - size * 0.5) / 64.0) + 1) * 64.0; g < cy + size * 0.5; g += 64.0)
            {
                double x, y;
                WorldToPixel(cx, cy, size, px, cx, g, out x, out y);
                for (int k = 0; k < px; k++) img.Blend(k, (int)y, 0.6f, 0.7f, 1f, 0.07f);
            }

            var list = new List<StarSystem>();
            u.SystemsInRect(cx - size * 0.5, cy - size * 0.5, cx + size * 0.5, cy + size * 0.5, list);
            var classCount = new int[9];
            double[] radius = { 6.0, 5.2, 4.4, 3.8, 3.6, 3.3, 2.7, 4.0, 4.6 };
            foreach (StarSystem s in list)
            {
                classCount[(int)s.StarClass]++;
                double x, y;
                WorldToPixel(cx, cy, size, px, s.X, s.Y, out x, out y);
                Rgb c = Boost(s.Color, 1f);
                double rad = radius[(int)s.StarClass] * px / 1600.0;
                img.FillCircle(x, y, rad * 2.4, c.R, c.G, c.B, 0.10f);
                if (s.StarClass == StarClass.BlackHole)
                {
                    img.FillCircle(x, y, rad, 0.02f, 0.0f, 0.05f, 1f);
                    img.Ring(x, y, rad, 1.6, c.R, c.G, c.B, 1f);
                }
                else
                {
                    img.FillCircle(x, y, rad, c.R, c.G, c.B, 1f);
                    if (s.StarClass == StarClass.Neutron) img.Ring(x, y, rad + 3.0, 1.2, 0.6f, 1f, 1f, 0.9f);
                }
            }
            int n = 1;
            foreach (StarSystem s in u.AuthoredSystems)
            {
                if (Math.Abs(s.X - cx) > size * 0.5 || Math.Abs(s.Y - cy) > size * 0.5) { n++; continue; }
                double x, y;
                WorldToPixel(cx, cy, size, px, s.X, s.Y, out x, out y);
                img.Ring(x, y, 13.0, 2.4, 1f, 1f, 1f, 1f);
                img.Ring(x, y, 19.0, 1.2, 1f, 0.85f, 0.2f, 0.9f);
                img.Label((int)x + 24, (int)y - 8, n + " " + s.Name.ToUpperInvariant(), 2, 1f, 0.95f, 0.6f);
                n++;
            }
            img.Label(16, 14, "2000 LY REGION AROUND HOME (31 X 31 SECTORS)", 3, 0.9f, 0.95f, 1f);

            string[] names = { "O", "B", "A", "F", "G", "K", "M", "NEUTRON", "BLACK HOLE" };
            int ly = px - 24 - 9 * 26;
            img.Label(px - 280, ly - 30, "STAR CLASS (COUNT)", 2, 0.9f, 0.95f, 1f);
            for (int i = 0; i < 9; i++)
            {
                Rgb c = Boost(ClassColour(i), 1f);
                img.FillCircle(px - 266, ly + i * 26 + 8, 6.0, c.R, c.G, c.B, 1f);
                img.Label(px - 250, ly + i * 26, names[i] + " " + classCount[i], 2, 0.85f, 0.9f, 1f);
            }
            ScaleBar(img, size, px, 500.0, "500 LY");
            img.Save(path);
            Console.WriteLine("wrote " + path + " (" + list.Count + " systems, " + u.AuthoredSystems.Count + " authored)");
        }

        static Rgb ClassColour(int cls)
        {
            Rgb[] c =
            {
                new Rgb(0.62f, 0.70f, 1.00f), new Rgb(0.68f, 0.76f, 1.00f), new Rgb(0.80f, 0.85f, 1.00f), new Rgb(0.97f, 0.97f, 1.00f),
                new Rgb(1.00f, 0.95f, 0.70f), new Rgb(1.00f, 0.78f, 0.45f), new Rgb(1.00f, 0.52f, 0.32f), new Rgb(0.50f, 0.95f, 1.00f), new Rgb(0.78f, 0.35f, 1.00f)
            };
            return c[cls];
        }

        // ---- (d): tier rings ------------------------------------------------------------------------------------------------

        static Rgb TierColour(int tier)
        {
            // perceptual-style gradient: purple -> blue -> teal -> green -> yellow -> orange -> red
            float[][] stops =
            {
                new[] { 0.35f, 0.05f, 0.45f }, new[] { 0.22f, 0.28f, 0.70f }, new[] { 0.10f, 0.60f, 0.70f },
                new[] { 0.30f, 0.80f, 0.35f }, new[] { 0.98f, 0.90f, 0.15f }, new[] { 0.96f, 0.50f, 0.10f }, new[] { 0.90f, 0.15f, 0.20f }
            };
            double f = Math.Min(tier, 13) / 13.0 * (stops.Length - 1);
            int i = Math.Min((int)f, stops.Length - 2);
            float t = (float)(f - i);
            return new Rgb(stops[i][0] + (stops[i + 1][0] - stops[i][0]) * t, stops[i][1] + (stops[i + 1][1] - stops[i][1]) * t, stops[i][2] + (stops[i + 1][2] - stops[i][2]) * t);
        }

        public static void RenderTiers(Universe u, double cx, double cy, double size, int px, string path)
        {
            var img = new Canvas(px, px);
            int[] tierRow = new int[px];
            int[] prevRow = null;
            for (int y = 0; y < px; y++)
            {
                for (int x = 0; x < px; x++)
                {
                    double wx = cx + ((x + 0.5) / px - 0.5) * size;
                    double wy = cy + (0.5 - (y + 0.5) / px) * size;
                    int tier = u.TierAt(wx, wy);
                    tierRow[x] = tier;
                    DensitySample s;
                    u.SampleDensity(wx, wy, out s);
                    float tone = (float)(1.0 - Math.Exp(-2.4 * s.GalaxyDensity));
                    Rgb tc = TierColour(tier);
                    bool edge = (x > 0 && tierRow[x - 1] != tier) || (prevRow != null && prevRow[x] != tier);
                    float k = 0.30f + 0.70f * tone;
                    if (edge) { Put(img, x, y, 0.9f, 0.9f, 0.95f); continue; }
                    Put(img, x, y, tc.R * k, tc.G * k, tc.B * k);
                }
                if (prevRow == null) prevRow = new int[px];
                Array.Copy(tierRow, prevRow, px);
            }

            double unit = u.Config.TierDistanceUnit, scale = u.Config.TierScale;
            // colour key with the distance range of every tier
            int kx = px - 330, ky = 24;
            for (int yy = ky - 10; yy < ky + 14 * 24 + 34; yy++)
                for (int xx = kx - 12; xx < px - 8; xx++) img.Blend(xx, yy, 0f, 0f, 0.03f, 0.62f);
            img.Text(kx, ky, "TIER  DISTANCE (LY)", 2, 1f, 1f, 1f);
            for (int t = 0; t < 14; t++)
            {
                double inner = unit * (Math.Pow(2.0, t / scale) - 1.0), outer = unit * (Math.Pow(2.0, (t + 1) / scale) - 1.0);
                Rgb tc = TierColour(t);
                int yy = ky + 26 + t * 24;
                for (int dy = 0; dy < 16; dy++) for (int dx = 0; dx < 30; dx++) Put(img, kx + dx, yy + dy, tc.R, tc.G, tc.B);
                string range = t == 13 ? ">" + ((int)Math.Round(inner)) : ((int)Math.Round(inner)) + "-" + ((int)Math.Round(outer));
                img.Text(kx + 40, yy + 1, "T" + t, 2, 1f, 1f, 1f);
                img.Text(kx + 100, yy + 1, range, 2, 0.85f, 0.9f, 1f);

                // ring label on the map where the ring is wide enough to read
                double x, y;
                WorldToPixel(cx, cy, size, px, 0.5 * (inner + outer), 0.0, out x, out y);
                double widthPx = (outer - inner) / size * px;
                if (widthPx >= 28.0 && x < kx - 40) img.Label((int)x - 8, (int)y - 8, "T" + t, 2, 1f, 1f, 1f);
            }
            double hx, hy;
            WorldToPixel(cx, cy, size, px, 0.0, 0.0, out hx, out hy);
            img.Ring(hx, hy, 6.0, 2.0, 1f, 1f, 1f, 1f);
            img.Label(16, 14, "TIER RINGS: FLOOR(1.6 LOG2(1 + D/60)), HOME AT CENTRE", 3, 1f, 1f, 1f);
            ScaleBar(img, size, px, 5000.0, "5000 LY");
            img.Save(path);
            Console.WriteLine("wrote " + path);
        }

        // ---- commands -----------------------------------------------------------------------------------------------------------

        public static int RenderAll(string[] args)
        {
            string dir = args.Length > 1 ? args[1] : "png";
            Directory.CreateDirectory(dir);
            var sw = Stopwatch.StartNew();
            Universe u = MakeUniverse();
            RenderDensity(u, 0, 0, 30000, 1500, Path.Combine(dir, "a_density_30000ly.png"), "DENSITY 30000 X 30000 LY", true, false);
            RenderDensity(u, 0, 0, 4200, 1500, Path.Combine(dir, "b_home_galaxy_zoom.png"), "HOME GALAXY: THE VERDANT SPIRAL", false, true);
            RenderSystems(u, 0, 0, 2000, 1600, Path.Combine(dir, "c_systems_2000ly.png"));
            RenderTiers(u, 0, 0, 30000, 1500, Path.Combine(dir, "d_tier_rings.png"));
            Console.WriteLine("done in " + sw.ElapsedMilliseconds + " ms");
            return 0;
        }

        public static int QuickDensity(string[] args)
        {
            double cx = double.Parse(args[1], CultureInfo.InvariantCulture), cy = double.Parse(args[2], CultureInfo.InvariantCulture), size = double.Parse(args[3], CultureInfo.InvariantCulture);
            int px = int.Parse(args[4]);
            Universe u = MakeUniverse();
            RenderDensity(u, cx, cy, size, px, args[5], null, size > 10000, false);
            return 0;
        }

        /// <summary>Contact sheet of galaxies of different kinds, to judge variety.</summary>
        public static int RenderGallery(string[] args)
        {
            string path = args.Length > 1 ? args[1] : "png\\galaxy_gallery.png";
            Universe u = MakeUniverse();
            var all = new List<GalaxyInfo>();
            u.Galaxies.GalaxiesInRect(-30000, -30000, 30000, 30000, all);
            var picks = new List<GalaxyInfo>();
            Func<Func<GalaxyInfo, bool>, bool> take = pred =>
            {
                foreach (GalaxyInfo g in all) if (pred(g) && !picks.Contains(g)) { picks.Add(g); return true; }
                return false;
            };
            take(g => g.Name == "Ember Reach");
            take(g => g.Name == "The Fractured Expanse");
            take(g => g.Kind == GalaxyKind.Elliptical && g.Radius > 1500);
            take(g => g.Kind == GalaxyKind.Spiral && g.Arms == 2 && !g.Authored);
            take(g => g.Kind == GalaxyKind.Spiral && g.Arms == 5);
            take(g => g.Kind == GalaxyKind.BarredSpiral && !g.Authored);
            take(g => g.Kind == GalaxyKind.Spiral && g.Ellipticity > 0.35 && !g.Authored);
            take(g => g.Kind == GalaxyKind.Spiral && g.Arms == 4 && !g.Authored);
            take(g => g.Kind == GalaxyKind.Elliptical && g.Radius < 1400);
            const int cell = 480, cols = 3;
            int rows = (picks.Count + cols - 1) / cols;
            var sheet = new Canvas(cell * cols, (cell + 44) * rows);
            for (int i = 0; i < picks.Count; i++)
            {
                GalaxyInfo g = picks[i];
                var img = new Canvas(cell, cell);
                PaintDensity(u, img, g.CenterX, g.CenterY, g.Radius * 2.7, 1f);
                int ox = (i % cols) * cell, oy = (i / cols) * (cell + 44);
                for (int y = 0; y < cell; y++)
                    for (int x = 0; x < cell; x++)
                    {
                        int k = (y * cell + x) * 3;
                        sheet.Set(ox + x, oy + y, img.Rgb[k], img.Rgb[k + 1], img.Rgb[k + 2]);
                    }
                sheet.Text(ox + 6, oy + cell + 6, g.Name.ToUpperInvariant(), 2, 0.95f, 0.95f, 1f);
                sheet.Text(ox + 6, oy + cell + 26, g.Kind.ToString().ToUpperInvariant() + " " + g.Arms + " ARMS R=" + ((int)g.Radius) + " TILT " + g.Ellipticity.ToString("0.00", CultureInfo.InvariantCulture), 2, 0.6f, 0.75f, 0.95f);
            }
            sheet.Save(path);
            Console.WriteLine("wrote " + path + " (" + picks.Count + " galaxies)");
            return 0;
        }
    }
}
