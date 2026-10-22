using System;
using System.Collections.Generic;

namespace Frontier.Universe
{
    /// <summary>
    /// Generates the planets of one star system. Every attribute draws from its own hash stream keyed by the planet id, so a planet is a
    /// pure function of (seed, generator version, key) plus the star it orbits.
    /// </summary>
    internal sealed class PlanetGenerator
    {
        sealed class ProfileCache
        {
            public ResourceEntry[] Entries;
            public int[] DefIndex;
            public int[] AlwaysEntry;
        }

        // Weather weights per biome in Weather order: Clear, Rain, Snow, Embers, Spores, Dust, Storm.
        static readonly float[][] WeatherWeights =
        {
            new[] { 0.50f, 0.30f, 0.00f, 0.00f, 0.00f, 0.12f, 0.08f },
            new[] { 0.20f, 0.08f, 0.00f, 0.00f, 0.00f, 0.67f, 0.05f },
            new[] { 0.10f, 0.00f, 0.78f, 0.00f, 0.00f, 0.00f, 0.12f },
            new[] { 0.00f, 0.00f, 0.00f, 0.80f, 0.00f, 0.12f, 0.08f },
            new[] { 0.00f, 0.15f, 0.00f, 0.00f, 0.85f, 0.00f, 0.00f },
            new[] { 0.40f, 0.00f, 0.30f, 0.00f, 0.00f, 0.10f, 0.20f },
            new[] { 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.30f, 0.70f },
            new[] { 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.20f, 0.80f },
            new[] { 0.10f, 0.00f, 0.00f, 0.00f, 0.00f, 0.80f, 0.10f },
            new[] { 0.20f, 0.50f, 0.00f, 0.00f, 0.00f, 0.00f, 0.30f },
            new[] { 0.10f, 0.00f, 0.00f, 0.00f, 0.00f, 0.20f, 0.70f },
            new[] { 0.50f, 0.00f, 0.00f, 0.00f, 0.00f, 0.50f, 0.00f }
        };

        // Deposits per size class (scrap included), before the +1 bonus roll.
        static readonly int[] BaseDepositCount = { 2, 2, 3, 3, 4, 4 };

        // Gas giant palettes: primary, secondary, rim (double precision, see FillVisual).
        static readonly double[][] GasPalettes =
        {
            new[] { 0.95, 0.60, 0.85, 0.30, 0.20, 0.60, 2.2, 1.2, 2.2 },
            new[] { 0.90, 0.70, 0.40, 0.35, 0.20, 0.08, 2.2, 1.6, 0.8 },
            new[] { 0.40, 0.80, 0.85, 0.05, 0.22, 0.35, 0.6, 2.0, 2.4 },
            new[] { 0.45, 0.55, 0.95, 0.08, 0.10, 0.40, 0.8, 1.2, 2.8 },
            new[] { 0.85, 0.45, 0.30, 0.30, 0.08, 0.05, 2.4, 1.0, 0.6 },
            new[] { 0.65, 0.80, 0.50, 0.12, 0.22, 0.10, 1.4, 2.2, 1.0 }
        };

        readonly UniverseConfig _cfg;
        readonly StreamSalts _salts;
        readonly ResourceTable _res;
        readonly ProfileCache[] _profiles = new ProfileCache[13];
        readonly int[] _alwaysDef;
        readonly ulong _anchorSeed;

        public PlanetGenerator(UniverseConfig cfg, StreamSalts salts, ResourceTable resources)
        {
            _cfg = cfg;
            _salts = salts;
            _res = resources;
            _anchorSeed = salts.SeedOf(Attr.Anchor);
            _alwaysDef = new int[resources.AlwaysPresent.Count];
            for (int i = 0; i < _alwaysDef.Length; i++) _alwaysDef[i] = resources.IndexOf(resources.AlwaysPresent[i]);
            for (int b = 0; b < 12; b++) _profiles[b] = BuildCache(resources.Profiles[b]);
            _profiles[12] = BuildCache(resources.BeltProfile);
        }

        ProfileCache BuildCache(ResourceProfile p)
        {
            var c = new ProfileCache();
            c.Entries = p.Entries.ToArray();
            c.DefIndex = new int[c.Entries.Length];
            for (int e = 0; e < c.Entries.Length; e++) c.DefIndex[e] = _res.IndexOf(c.Entries[e].ResourceId);
            c.AlwaysEntry = new int[_alwaysDef.Length];
            for (int a = 0; a < c.AlwaysEntry.Length; a++)
            {
                c.AlwaysEntry[a] = -1;
                for (int e = 0; e < c.Entries.Length; e++) if (c.DefIndex[e] == _alwaysDef[a]) { c.AlwaysEntry[a] = e; break; }
            }
            return c;
        }

        public ResourceTable Resources { get { return _res; } }

        /// <summary>All planets of a system with their natural deposits (coverage anchors are applied later at block level).</summary>
        public PlanetSpec[] GenerateSpecs(StarSystem sys)
        {
            int n = sys.PlanetCount;
            var specs = new PlanetSpec[n];
            var orb = _salts.Rng(Attr.OrbitLayout, sys.Key.Id);
            double m = 0.20 + 0.45 * orb.NextDouble();
            double scale = Math.Sqrt(sys.Luminosity);
            for (int i = 0; i < n; i++)
            {
                if (i > 0) m *= 1.30 + 0.45 * orb.NextDouble();
                specs[i] = BuildSpec(sys, i, m, scale);
            }
            return specs;
        }

        PlanetSpec BuildSpec(StarSystem sys, int index, double m, double scale)
        {
            var key = new PlanetKey(sys.Key, index);
            ulong id = key.Id;
            StarClass cls = sys.StarClass;
            bool remnant = cls == StarClass.Neutron || cls == StarClass.BlackHole;

            double pGas = m > 2.2 ? 0.45 : 0.05;
            if (m > 2.2 && (cls == StarClass.G || cls == StarClass.F)) pGas += 0.08;
            if (remnant) pGas *= 0.3;
            else if (cls == StarClass.M) pGas *= 0.6;
            double uk = Hash.Unit(_salts.Stream(Attr.PlanetKind, id));
            PlanetKind kind = uk < pGas ? PlanetKind.GasGiant : (uk < pGas + 0.07 ? PlanetKind.AsteroidBelt : PlanetKind.Rocky);

            double temp = 295.0 / Math.Sqrt(m);

            int anomaly = 0;
            double ua = Hash.Unit(_salts.Stream(Attr.TierAnomaly, id));
            if (ua < _cfg.TierAnomalyChance * 0.5) anomaly = 1;
            else if (ua < _cfg.TierAnomalyChance) anomaly = -1;
            int tier = DetMath.Clamp(sys.Tier + anomaly, 0, _cfg.TierMax);

            SizeClass size = ChooseSize(id, kind, tier);
            Biome biome = kind == PlanetKind.GasGiant ? Biome.GasGiant : (kind == PlanetKind.AsteroidBelt ? Biome.Barren : ChooseBiome(id, cls, temp, tier, size));

            var p = new PlanetSpec();
            p.Key = key;
            p.Kind = kind;
            p.Biome = biome;
            p.Size = size;
            p.Tier = tier;
            p.Temperature = (float)temp;
            p.OrbitRadius = (float)(scale * m);
            p.OrbitPhase = (float)(Hash.Unit(_salts.Stream(Attr.OrbitPhase, id)) * DetMath.TwoPi);
            p.WorldSize = _cfg.WorldSizeBySize[(int)size];
            p.BuildingSlots = _cfg.BuildingSlotsBySize[(int)size];
            p.Danger = (float)Math.Max(0.1, _cfg.DangerBase + _cfg.DangerPerTier * tier
                + (Hash.Unit(_salts.Stream(Attr.Danger, id)) - 0.5) * _cfg.DangerNoise);
            p.GeneratorVersion = _cfg.GeneratorVersion;

            var nr = _salts.Rng(Attr.PlanetName, id);
            p.Name = NameBank.Syllabic(ref nr, 2, 3);
            p.Designation = sys.Designation + " " + (char)('b' + index);

            p.Weather = ChooseWeather(id, biome);
            p.HasRings = Hash.Unit(_salts.Stream(Attr.Rings, id)) < RingChance(biome, kind, size);
            p.MoonCount = ChooseMoons(id, kind, size);
            FillVisual(ref p, sys, id);
            string[] keys = _cfg.BiomeModelKeys[(int)biome];
            p.ModelKey = keys[Hash.Index(_salts.Stream(Attr.ModelKey, id), keys.Length)];
            p.Hazards = ComputeHazards(sys, biome, tier, temp, id);
            p.Deposits = BuildDeposits(id, biome, kind, size, tier);
            return p;
        }

        // ---- size, biome ---------------------------------------------------------------------------------------------------

        SizeClass ChooseSize(ulong id, PlanetKind kind, int tier)
        {
            if (kind == PlanetKind.AsteroidBelt) return SizeClass.Dwarf;
            double u = Hash.Unit(_salts.Stream(Attr.PlanetSize, id));
            if (kind == PlanetKind.GasGiant) return u < 0.25 ? SizeClass.Large : (u < 0.75 ? SizeClass.Giant : SizeClass.Colossal);
            double t = Math.Min(tier, 14) - 4;
            if (t < 0.0) t = 0.0;
            double w0 = 0.22, w1 = 0.28, w2 = 0.28, w3 = 0.13 * (1.0 + 0.05 * t), w4 = 0.07 * (1.0 + 0.10 * t), w5 = 0.02 * (1.0 + 0.20 * t);
            double total = w0 + w1 + w2 + w3 + w4 + w5;
            double r = u * total;
            if (r < w0) return SizeClass.Dwarf;
            r -= w0;
            if (r < w1) return SizeClass.Small;
            r -= w1;
            if (r < w2) return SizeClass.Medium;
            r -= w2;
            if (r < w3) return SizeClass.Large;
            r -= w3;
            return r < w4 ? SizeClass.Giant : SizeClass.Colossal;
        }

        static double Affinity(double t, double mu, double sigma)
        {
            double z = (t - mu) / sigma;
            return DetMath.Exp(-0.5 * z * z);
        }

        Biome ChooseBiome(ulong id, StarClass cls, double temp, int tier, SizeClass size)
        {
            Span<double> w = stackalloc double[12];
            w[(int)Biome.GasGiant] = 0.0;
            double depth = tier > 4 ? 1.0 + 0.12 * (tier - 4) : 1.0;
            double lushTier = Math.Max(0.4, 1.0 - 0.035 * tier);
            w[(int)Biome.Lush] = 1.50 * Affinity(temp, 285.0, 55.0) * lushTier;
            w[(int)Biome.Oceanic] = 1.00 * Affinity(temp, 290.0, 50.0);
            w[(int)Biome.Fungal] = 0.60 * Affinity(temp, 315.0, 70.0);
            w[(int)Biome.Mineral] = 0.45 * Affinity(temp, 300.0, 140.0);
            w[(int)Biome.Desert] = 0.80 * Affinity(temp, 370.0, 70.0);
            w[(int)Biome.Volcanic] = 0.65 * Affinity(temp, 620.0, 200.0);
            w[(int)Biome.Glacial] = 1.30 * Affinity(temp, 180.0, 60.0);
            w[(int)Biome.Barren] = 0.15 * Affinity(temp, 250.0, 300.0);
            w[(int)Biome.Crystal] = 0.20 * Affinity(temp, 230.0, 200.0) * DetMath.SmoothStep(1.0, 3.0, tier) * depth;
            w[(int)Biome.Obsidian] = 0.15 * Affinity(temp, 300.0, 400.0) * DetMath.SmoothStep(2.0, 4.0, tier) * depth;
            w[(int)Biome.Shattered] = 0.06 * Affinity(temp, 200.0, 400.0) * DetMath.SmoothStep(4.0, 7.0, tier) * depth;

            switch (cls)
            {
                case StarClass.M:
                    w[(int)Biome.Lush] *= 0.65;
                    w[(int)Biome.Oceanic] *= 0.7;
                    break;
                case StarClass.O:
                case StarClass.B:
                case StarClass.A:
                    w[(int)Biome.Lush] *= 0.25;
                    w[(int)Biome.Oceanic] *= 0.3;
                    w[(int)Biome.Fungal] *= 0.25;
                    w[(int)Biome.Volcanic] *= 1.4;
                    w[(int)Biome.Barren] *= 1.6;
                    w[(int)Biome.Crystal] *= 1.4;
                    w[(int)Biome.Shattered] *= 1.5;
                    break;
                case StarClass.Neutron:
                case StarClass.BlackHole:
                    w[(int)Biome.Lush] *= 0.02;
                    w[(int)Biome.Oceanic] *= 0.02;
                    w[(int)Biome.Fungal] *= 0.02;
                    w[(int)Biome.Mineral] *= 0.4;
                    w[(int)Biome.Desert] *= 0.3;
                    w[(int)Biome.Barren] *= 4.0;
                    w[(int)Biome.Crystal] *= 2.0;
                    w[(int)Biome.Obsidian] = Math.Max(w[(int)Biome.Obsidian], 0.6 * 0.15) * 7.0;
                    w[(int)Biome.Shattered] = Math.Max(w[(int)Biome.Shattered], 0.6 * 0.06) * 7.0;
                    break;
            }
            if (size == SizeClass.Dwarf)
            {
                w[(int)Biome.Lush] *= 0.35;
                w[(int)Biome.Oceanic] *= 0.35;
                w[(int)Biome.Fungal] *= 0.35;
                w[(int)Biome.Barren] *= 1.8;
            }

            double total = 0.0;
            for (int i = 0; i < 12; i++)
            {
                if (i == (int)Biome.GasGiant) w[i] = 0.0;
                w[i] += i == (int)Biome.GasGiant ? 0.0 : 0.002;
                total += w[i];
            }
            double u = Hash.Unit(_salts.Stream(Attr.PlanetBiome, id)) * total;
            double acc = 0.0;
            for (int i = 0; i < 12; i++)
            {
                acc += w[i];
                if (u < acc && w[i] > 0.0) return (Biome)i;
            }
            return Biome.Barren;
        }

        Weather ChooseWeather(ulong id, Biome biome)
        {
            float[] w = WeatherWeights[(int)biome];
            double total = 0.0;
            for (int i = 0; i < 7; i++) total += w[i];
            double u = Hash.Unit(_salts.Stream(Attr.Weather, id)) * total;
            double acc = 0.0;
            for (int i = 0; i < 7; i++)
            {
                acc += w[i];
                if (u < acc && w[i] > 0f) return (Weather)i;
            }
            return Weather.Clear;
        }

        static double RingChance(Biome biome, PlanetKind kind, SizeClass size)
        {
            if (kind == PlanetKind.AsteroidBelt) return 0.0;
            if (kind == PlanetKind.GasGiant) return size == SizeClass.Colossal ? 0.55 : (size == SizeClass.Giant ? 0.45 : 0.30);
            switch (biome)
            {
                case Biome.Crystal: return 0.25;
                case Biome.Obsidian: return 0.20;
                case Biome.Shattered: return 0.35;
                case Biome.Glacial: return 0.08;
                default: return 0.03;
            }
        }

        int ChooseMoons(ulong id, PlanetKind kind, SizeClass size)
        {
            if (kind == PlanetKind.AsteroidBelt) return 0;
            double u = Hash.Unit(_salts.Stream(Attr.Moons, id));
            if (kind == PlanetKind.GasGiant) return Math.Min(7, (int)Math.Floor(u * 7.0) + (size >= SizeClass.Giant ? 1 : 0));
            return Math.Min(4, (int)Math.Floor(u * u * ((int)size + 1) * 1.3));
        }

        // ---- hazards ---------------------------------------------------------------------------------------------------------

        Hazards ComputeHazards(StarSystem sys, Biome biome, int tier, double temp, ulong id)
        {
            Hazards h = Hazards.None;
            switch (biome)
            {
                case Biome.Volcanic:
                case Biome.Desert:
                    h |= Hazards.Heat;
                    break;
                case Biome.Glacial:
                    h |= Hazards.Cold;
                    break;
                case Biome.Fungal:
                case Biome.GasGiant:
                    h |= Hazards.Toxic;
                    break;
                case Biome.Obsidian:
                case Biome.Shattered:
                    h |= Hazards.Radiation;
                    break;
                case Biome.Crystal:
                    if (tier >= 6) h |= Hazards.Radiation;
                    break;
            }
            if (temp > 480.0) h |= Hazards.Heat;
            if (temp < 150.0) h |= Hazards.Cold;

            StarClass cls = sys.StarClass;
            if (cls == StarClass.O || cls == StarClass.B || cls == StarClass.Neutron || cls == StarClass.BlackHole) h |= Hazards.Radiation;
            else if (cls == StarClass.A && Hash.Unit(_salts.Stream(Attr.HazardExtra, id ^ 0xA5A5A5UL)) < 0.25) h |= Hazards.Radiation;

            if ((tier >= _cfg.VoidMinTier && (sys.GalaxyId == 0UL || cls == StarClass.BlackHole)) || (biome == Biome.Shattered && tier >= 6)) h |= Hazards.Void;

            double pExtra = Math.Min(0.25, 0.03 + 0.01 * tier);
            ulong he = _salts.Stream(Attr.HazardExtra, id);
            if (Hash.Unit(he) < pExtra) h |= (he & 0x100UL) != 0UL ? Hazards.Toxic : Hazards.Radiation;
            return h;
        }

        // ---- visuals ---------------------------------------------------------------------------------------------------------

        void FillVisual(ref PlanetSpec p, StarSystem sys, ulong id)
        {
            // All arithmetic is done in double precision and rounded to float exactly once at the end: float arithmetic is evaluated
            // with different intermediate precision by different runtimes (Mono in the editor, IL2CPP on devices, CoreCLR on a server).
            var r = _salts.Rng(Attr.Visual, id);
            double u0 = r.NextDouble(), u1 = r.NextDouble(), u2 = r.NextDouble(), u3 = r.NextDouble(), u4 = r.NextDouble(), u6 = r.NextDouble();
            double pr, pg, pb, sr, sg, sb, rr, rg, rb, bands, glow, atmo;
            int style;
            switch (p.Biome)
            {
                case Biome.Lush:
                    pr = 0.20 + 0.15 * u0; pg = 0.55 + 0.35 * u1; pb = 0.22;
                    sr = 0.06; sg = 0.18 + 0.10 * u2; sb = 0.10;
                    rr = 0.4; rg = 2.0; rb = 1.0;
                    style = u4 < 0.5 ? 0 : 1; bands = 3.0 + 4.0 * u3; glow = 0.35; atmo = 0.35;
                    break;
                case Biome.Mineral:
                    pr = 0.65 + 0.25 * u0; pg = 0.35 + 0.15 * u1; pb = 0.18;
                    sr = 0.22; sg = 0.12; sb = 0.08;
                    rr = 2.0; rg = 1.1; rb = 0.4;
                    style = u4 < 0.5 ? 1 : 2; bands = 6.0 + 5.0 * u3; glow = 0.40; atmo = 0.20;
                    break;
                case Biome.Glacial:
                    pr = 0.55 + 0.20 * u0; pg = 0.80 + 0.18 * u1; pb = 1.0;
                    sr = 0.08; sg = 0.18; sb = 0.35;
                    rr = 0.6; rg = 1.8; rb = 2.6;
                    style = u4 < 0.5 ? 0 : 4; bands = 2.0 + 4.0 * u3; glow = 0.65; atmo = 0.45;
                    break;
                case Biome.Volcanic:
                    pr = 0.95; pg = 0.22 + 0.15 * u1; pb = 0.08;
                    sr = 0.12; sg = 0.02; sb = 0.02;
                    rr = 2.6; rg = 0.6; rb = 0.2;
                    style = 3; bands = 10.0 + 5.0 * u3; glow = 0.70; atmo = 0.35;
                    break;
                case Biome.Fungal:
                    pr = 0.50 + 0.20 * u0; pg = 0.70 + 0.20 * u1; pb = 0.15;
                    sr = 0.12; sg = 0.15; sb = 0.04;
                    rr = 1.8; rg = 2.4; rb = 0.5;
                    style = 2; bands = 5.0 + 4.0 * u3; glow = 0.55; atmo = 0.50;
                    break;
                case Biome.Crystal:
                    pr = 0.40 + 0.30 * u0; pg = 0.90; pb = 1.0;
                    sr = 0.40; sg = 0.10; sb = 0.60;
                    rr = 2.2; rg = 1.8; rb = 2.8;
                    style = 4; bands = 4.0 + 4.0 * u3; glow = 1.10; atmo = 0.90;
                    break;
                case Biome.Obsidian:
                    pr = 0.18 + 0.10 * u0; pg = 0.10; pb = 0.28;
                    sr = 0.03; sg = 0.01; sb = 0.06;
                    rr = 1.8; rg = 0.6; rb = 2.6;
                    style = u4 < 0.5 ? 2 : 4; bands = 10.0 + 4.0 * u3; glow = 0.65; atmo = 0.30;
                    break;
                case Biome.Shattered:
                    pr = 0.85 + 0.15 * u0; pg = 0.85; pb = 1.0;
                    sr = 0.04; sg = 0.04; sb = 0.08;
                    rr = 2.8; rg = 2.8; rb = 3.4;
                    style = 1; bands = 8.0; glow = 1.40; atmo = 1.10;
                    break;
                case Biome.Desert:
                    pr = 0.90 + 0.08 * u0; pg = 0.68 + 0.14 * u1; pb = 0.22 + 0.12 * u2;
                    sr = 0.35; sg = 0.16; sb = 0.03;
                    rr = 2.4; rg = 1.6; rb = 0.4;
                    style = 1; bands = 5.0 + 4.0 * u3; glow = 0.40; atmo = 0.25;
                    break;
                case Biome.Oceanic:
                    pr = 0.06 + 0.08 * u0; pg = 0.42 + 0.16 * u1; pb = 0.92;
                    sr = 0.02; sg = 0.08; sb = 0.30;
                    rr = 0.3; rg = 1.6; rb = 2.6;
                    style = 1; bands = 3.0 + 3.0 * u3; glow = 0.55; atmo = 0.65;
                    break;
                case Biome.GasGiant:
                    {
                        double[] pal = GasPalettes[(int)(u6 * 5.999)];
                        pr = Clamp1(pal[0] + 0.10 * (u0 - 0.5)); pg = Clamp1(pal[1] + 0.10 * (u1 - 0.5)); pb = Clamp1(pal[2] + 0.10 * (u2 - 0.5));
                        sr = pal[3]; sg = pal[4]; sb = pal[5];
                        rr = pal[6]; rg = pal[7]; rb = pal[8];
                        style = u4 < 0.7 ? 0 : 1; bands = 8.0 + 8.0 * u3; glow = 0.50; atmo = 0.80;
                    }
                    break;
                default:
                    pr = 0.50 + 0.12 * u0; pg = 0.46 + 0.10 * u1; pb = 0.42;
                    sr = 0.14; sg = 0.13; sb = 0.12;
                    rr = 1.0; rg = 1.0; rb = 1.3;
                    style = 2; bands = 3.0 + 3.0 * u3; glow = 0.20; atmo = 0.05;
                    break;
            }
            const double tint = 0.08;
            double cr = sys.Color.R, cg = sys.Color.G, cb = sys.Color.B;
            pr += (cr - pr) * tint;
            pg += (cg - pg) * tint;
            pb += (cb - pb) * tint;
            p.Primary = new Rgb((float)pr, (float)pg, (float)pb);
            p.Secondary = new Rgb((float)sr, (float)sg, (float)sb);
            p.Rim = new Rgb((float)rr, (float)rg, (float)rb);
            p.Style = style;
            p.SurfaceBands = (float)bands;
            p.GlowIntensity = (float)glow;
            p.AtmosphereIntensity = (float)atmo;
        }

        static double Clamp1(double v) { return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v); }

        // ---- deposits --------------------------------------------------------------------------------------------------------

        internal Deposit MakeDeposit(ResourceDef def, HarvestMethod method, int tier, float richness, double difficultyNoise)
        {
            int t = Math.Min(tier, 20);
            int baseYield = (int)((double)def.BaseYield + (double)def.YieldPerTier * t + 0.5);
            double d = 0.20 + 0.09 * Math.Min(tier, 8) + (double)def.DifficultyOffset + (difficultyNoise - 0.5) * 0.12;
            return new Deposit(def.Id, method, richness, baseYield < 1 ? 1 : baseYield, (float)DetMath.Clamp01(d));
        }

        /// <summary>The harvest method a biome profile uses for a resource, or the resource's default method.</summary>
        internal HarvestMethod MethodFor(Biome biome, PlanetKind kind, int defIndex)
        {
            ProfileCache pc = _profiles[kind == PlanetKind.AsteroidBelt ? 12 : (int)biome];
            for (int e = 0; e < pc.Entries.Length; e++) if (pc.DefIndex[e] == defIndex) return pc.Entries[e].Method;
            return _res.Defs[defIndex].DefaultMethod;
        }

        /// <summary>Lowest tier at which this kind of world may carry the resource: the profile entry's tier, else the resource's own.</summary>
        internal int MinTierFor(Biome biome, PlanetKind kind, int defIndex)
        {
            ProfileCache pc = _profiles[kind == PlanetKind.AsteroidBelt ? 12 : (int)biome];
            for (int e = 0; e < pc.Entries.Length; e++) if (pc.DefIndex[e] == defIndex) return pc.Entries[e].MinTier;
            return _res.Defs[defIndex].MinTier;
        }

        /// <summary>Anchor deposit for coverage: a solid, slightly better than average vein.</summary>
        internal Deposit MakeAnchorDeposit(int defIndex, Biome biome, PlanetKind kind, int tier, ulong planetId)
        {
            ResourceDef def = _res.Defs[defIndex];
            ulong h = Hash.Stream(_anchorSeed, Hash.Combine(planetId, Hash.Fnv1a64(def.Id), 0x7E57UL));
            double u = Hash.Unit(h);
            double u2 = Hash.Unit(Hash.Mix(h));
            return MakeDeposit(def, MethodFor(biome, kind, defIndex), tier, (float)(0.8 + 0.8 * u), u2);
        }

        internal ulong AnchorSeed { get { return _anchorSeed; } }

        Deposit[] BuildDeposits(ulong id, Biome biome, PlanetKind kind, SizeClass size, int tier)
        {
            ProfileCache pc = _profiles[kind == PlanetKind.AsteroidBelt ? 12 : (int)biome];
            ResourceEntry[] entries = pc.Entries;
            int nEntries = entries.Length;
            var rng = _salts.Rng(Attr.Deposits, id);

            double bonus = rng.NextDouble();
            int want = kind == PlanetKind.AsteroidBelt ? 3 : (kind == PlanetKind.GasGiant ? 2 : BaseDepositCount[(int)size]);
            if (bonus < 0.4) want++;

            Span<int> cEntry = stackalloc int[16];
            Span<int> cDef = stackalloc int[16];
            int nc = 0;
            for (int a = 0; a < _alwaysDef.Length && nc < 16; a++)
            {
                if (_alwaysDef[a] < 0) continue;
                cEntry[nc] = pc.AlwaysEntry[a];
                cDef[nc] = _alwaysDef[a];
                nc++;
            }
            int alwaysCount = nc;

            Span<float> wts = nEntries <= 32 ? stackalloc float[32] : new float[nEntries];
            double total = 0.0;
            for (int e = 0; e < nEntries; e++)
            {
                float w = entries[e].Weight;
                bool always = false;
                for (int a = 0; a < alwaysCount; a++) if (cDef[a] == pc.DefIndex[e]) always = true;
                if (always || entries[e].MinTier > tier || w <= 0f) w = 0f;
                wts[e] = w;
                total += w;
            }
            for (int pick = alwaysCount; pick < want && nc < 16; pick++)
            {
                if (!(total > 0.0)) break;
                double r = rng.NextDouble() * total;
                double acc = 0.0;
                int chosen = -1;
                for (int e = 0; e < nEntries; e++)
                {
                    if (wts[e] <= 0f) continue;
                    acc += wts[e];
                    chosen = e;
                    if (r < acc) break;
                }
                if (chosen < 0) break;
                total -= wts[chosen];
                wts[chosen] = 0f;
                cEntry[nc] = chosen;
                cDef[nc] = pc.DefIndex[chosen];
                nc++;
            }

            var result = new Deposit[nc];
            for (int i = 0; i < nc; i++)
            {
                ResourceDef def = _res.Defs[cDef[i]];
                HarvestMethod method = cEntry[i] >= 0 ? entries[cEntry[i]].Method : def.DefaultMethod;
                double u = rng.NextDouble();
                float richness = (float)(0.5 + 1.5 * u * Math.Sqrt(u));
                result[i] = MakeDeposit(def, method, tier, richness, rng.NextDouble());
            }
            return result;
        }
    }
}
