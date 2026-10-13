using System;
using System.Collections.Generic;
using System.Globalization;

namespace Frontier.Universe
{
    /// <summary>Turns a sector coordinate into its star systems: count from the density field, bounded-retry placement, star classes.</summary>
    internal sealed class SystemGenerator
    {
        // Constant per-class tables, indexed by StarClass (O B A F G K M Neutron BlackHole).
        static readonly double[] LumMin = { 30000.0, 200.0, 8.0, 1.8, 0.7, 0.1, 0.002, 0.0005, 0.01 };
        static readonly double[] LumMax = { 1000000.0, 20000.0, 60.0, 6.0, 1.6, 0.6, 0.07, 0.005, 0.2 };
        static readonly double[] TempMin = { 30000.0, 10000.0, 7500.0, 6000.0, 5200.0, 3900.0, 2400.0, 600000.0, 8000.0 };
        static readonly double[] TempMax = { 50000.0, 30000.0, 10000.0, 7500.0, 6000.0, 5200.0, 3900.0, 1000000.0, 40000.0 };
        static readonly double[] PlanetMean = { 2.8, 3.2, 3.8, 4.4, 4.6, 4.2, 3.0, 1.8, 1.6 };
        static readonly double[] LumLogRange = MakeLogRange();

        static readonly Rgb[] BaseColor =
        {
            new Rgb(0.62f, 0.70f, 1.00f), new Rgb(0.68f, 0.76f, 1.00f), new Rgb(0.80f, 0.85f, 1.00f),
            new Rgb(0.97f, 0.97f, 1.00f), new Rgb(1.00f, 0.95f, 0.70f), new Rgb(1.00f, 0.78f, 0.45f),
            new Rgb(1.00f, 0.52f, 0.32f), new Rgb(0.50f, 0.95f, 1.00f), new Rgb(0.78f, 0.35f, 1.00f)
        };

        static double[] MakeLogRange()
        {
            var r = new double[9];
            for (int i = 0; i < 9; i++) r[i] = DetMath.Log(LumMax[i] / LumMin[i]);
            return r;
        }

        readonly UniverseConfig _cfg;
        readonly StreamSalts _salts;
        readonly GalaxyField _field;
        readonly Dictionary<SectorCoord, StarSystem[]> _authored;
        readonly double _sector;

        public SystemGenerator(UniverseConfig cfg, StreamSalts salts, GalaxyField field, Dictionary<SectorCoord, StarSystem[]> authoredBySector)
        {
            _cfg = cfg;
            _salts = salts;
            _field = field;
            _authored = authoredBySector;
            _sector = cfg.SectorSize;
        }

        public StarSystem[] Generate(SectorCoord sc)
        {
            double x0 = sc.X * _sector, y0 = sc.Y * _sector;
            StarSystem[] authoredHere;
            if (_authored.Count == 0 || !_authored.TryGetValue(sc, out authoredHere)) authoredHere = null;

            double density = _field.Density(x0 + _sector * 0.5, y0 + _sector * 0.5);
            double lambda = density * _cfg.SystemsPerSectorMax;
            int n = (int)Math.Floor(lambda);
            double frac = lambda - n;
            ulong sectorId = sc.Id;
            if (Hash.Unit(_salts.Stream(Attr.SectorCount, sectorId)) < frac) n++;
            if (n > _cfg.MaxSystemsPerSector) n = _cfg.MaxSystemsPerSector;

            StarSystem[] placed = null;
            int placedCount = 0;
            if (n > 0)
            {
                placed = new StarSystem[n];
                List<StarSystem> nearAuthored = CollectAuthoredNear(sc);
                ulong placementSeed = _salts.SeedOf(Attr.SectorPlacement);
                double margin = _cfg.MinSystemSeparation * 0.5;
                double inner = _sector - 2.0 * margin;
                double sep2 = _cfg.MinSystemSeparation * _cfg.MinSystemSeparation;
                for (int i = 0; i < n; i++)
                {
                    for (int attempt = 0; attempt < _cfg.PlacementAttempts; attempt++)
                    {
                        // one independent hash per (system, attempt): adding an authored system only displaces the random systems it actually collides with
                        ulong h = Hash.Stream(placementSeed, Hash.Combine(sectorId, (ulong)(i * 256 + attempt)));
                        double px = x0 + margin + Hash.Unit(h) * inner;
                        double py = y0 + margin + Hash.Unit(Hash.Mix(h)) * inner;
                        if (!FarEnough(px, py, sep2, placed, placedCount, nearAuthored)) continue;
                        placed[placedCount++] = MakeSystem(sc, i, px, py);
                        break;
                    }
                }
            }

            int total = placedCount + (authoredHere == null ? 0 : authoredHere.Length);
            if (total == 0) return Array.Empty<StarSystem>();
            if (placedCount == total && placedCount == placed.Length) return placed;
            var result = new StarSystem[total];
            for (int i = 0; i < placedCount; i++) result[i] = placed[i];
            if (authoredHere != null) for (int i = 0; i < authoredHere.Length; i++) result[placedCount + i] = authoredHere[i];
            return result;
        }

        List<StarSystem> CollectAuthoredNear(SectorCoord sc)
        {
            if (_authored.Count == 0) return null;
            List<StarSystem> list = null;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    StarSystem[] a;
                    if (!_authored.TryGetValue(new SectorCoord(sc.X + dx, sc.Y + dy), out a)) continue;
                    if (list == null) list = new List<StarSystem>();
                    list.AddRange(a);
                }
            }
            return list;
        }

        static bool FarEnough(double px, double py, double sep2, StarSystem[] placed, int placedCount, List<StarSystem> authored)
        {
            for (int i = 0; i < placedCount; i++)
            {
                double dx = placed[i].X - px, dy = placed[i].Y - py;
                if (dx * dx + dy * dy < sep2) return false;
            }
            if (authored != null)
            {
                for (int i = 0; i < authored.Count; i++)
                {
                    double dx = authored[i].X - px, dy = authored[i].Y - py;
                    if (dx * dx + dy * dy < sep2) return false;
                }
            }
            return true;
        }

        StarSystem MakeSystem(SectorCoord sc, int index, double x, double y)
        {
            var key = new SystemKey(sc, index);
            ulong id = key.Id;
            DensitySample ds;
            _field.Sample(x, y, out ds);
            double hx = x - _cfg.HomeX, hy = y - _cfg.HomeY;
            int tier = _cfg.TierFromDistance(Math.Sqrt(hx * hx + hy * hy));
            int cls = ChooseClass(id, ref ds, tier);

            var lr = _salts.Rng(Attr.StarLuminosity, id);
            double lum = LumMin[cls] * DetMath.Exp(lr.NextDouble() * LumLogRange[cls]);
            double temp = TempMin[cls] + (TempMax[cls] - TempMin[cls]) * lr.NextDouble();

            var cr = _salts.Rng(Attr.StarColour, id);
            Rgb bc = BaseColor[cls];
            var color = new Rgb(Jitter(bc.R, ref cr), Jitter(bc.G, ref cr), Jitter(bc.B, ref cr));

            var nr = _salts.Rng(Attr.SystemName, id);
            string name = NameBank.Syllabic(ref nr, 2, 3);
            string prefix = ds.Galaxy != null ? ds.Galaxy.CataloguePrefix : "IG";
            string designation = prefix + "-" + Hash.Index(_salts.Stream(Attr.SystemCatalog, id), 10000000).ToString("D7", CultureInfo.InvariantCulture);

            var pr = _salts.Rng(Attr.PlanetCount, id);
            double tri = (pr.NextDouble() + pr.NextDouble() - 1.0) * 2.4;
            int planets = DetMath.Clamp((int)Math.Floor(PlanetMean[cls] + tri + 0.5), _cfg.MinPlanetsPerSystem, _cfg.MaxPlanetsPerSystem);

            double s = Math.Sqrt(lum);
            var spec = new SystemSpec
            {
                Key = key,
                Name = name,
                Designation = designation,
                X = x,
                Y = y,
                StarClass = (StarClass)cls,
                Color = color,
                Luminosity = (float)lum,
                SurfaceTemperature = (float)temp,
                HabitableInner = (float)(0.95 * s),
                HabitableOuter = (float)(1.5 * s),
                GalaxyId = ds.Galaxy != null ? ds.Galaxy.Id : 0UL,
                Tier = tier,
                PlanetCount = planets
            };
            return new StarSystem(spec);
        }

        /// <summary>Double precision arithmetic, one rounding to float at the end (see PlanetGenerator.FillVisual).</summary>
        static float Jitter(float v, ref Rng r)
        {
            double j = (double)v * (0.96 + 0.08 * r.NextDouble());
            return (float)(j > 1.0 ? 1.0 : j);
        }

        int ChooseClass(ulong id, ref DensitySample ds, int tier)
        {
            Span<double> w = stackalloc double[9];
            double total = 0.0;
            for (int i = 0; i < 9; i++)
            {
                double v;
                if (ds.Galaxy == null)
                {
                    v = _cfg.StarWeightsHalo[i];
                }
                else
                {
                    double wb = ds.BulgeShare, arm = ds.ArmStrength;
                    v = wb * _cfg.StarWeightsBulge[i] + (1.0 - wb) * (arm * _cfg.StarWeightsArm[i] + (1.0 - arm) * _cfg.StarWeightsDisk[i]);
                }
                if (i >= 7)
                {
                    if (tier < _cfg.RemnantMinTier) v = 0.0;
                    else if (ds.Galaxy != null) v *= 1.0 + _cfg.RemnantCoreBoost * ds.CoreProximity;
                }
                w[i] = v;
                total += v;
            }
            if (!(total > 0.0)) return (int)StarClass.M;
            double u = Hash.Unit(_salts.Stream(Attr.StarClass, id)) * total;
            double acc = 0.0;
            for (int i = 0; i < 9; i++)
            {
                acc += w[i];
                if (u < acc && w[i] > 0.0) return i;
            }
            return (int)StarClass.M;
        }
    }
}
