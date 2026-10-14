using System;
using System.Collections.Generic;

namespace Frontier.Universe
{
    /// <summary>
    /// A galaxy: a fixed centre/radius/shape plus the density model (bulge + exponential disk x logarithmic spiral arms x noise
    /// clumping). Generated galaxies come from the macro grid; the host can create authored landmark galaxies with fixed parameters
    /// (see <see cref="Landmarks"/>). Instances are immutable.
    /// </summary>
    public sealed class GalaxyInfo
    {
        public readonly ulong Id;
        public readonly string Name;
        public readonly GalaxyKind Kind;
        public readonly double CenterX, CenterY;
        /// <summary>Radius in light-years where the disk has faded into the halo (major axis).</summary>
        public readonly double Radius;
        public readonly int Arms;
        /// <summary>Spiral tightness 1/tan(pitch angle).</summary>
        public readonly double Winding;
        /// <summary>Angle of arm 0 at the reference radius, radians.</summary>
        public readonly double ArmPhase;
        /// <summary>+1 or -1: direction the arms wind.</summary>
        public readonly int Chirality;
        /// <summary>Flattening, 1 - minor/major.</summary>
        public readonly double Ellipticity;
        /// <summary>Orientation of the major axis, radians.</summary>
        public readonly double Rotation;
        /// <summary>Bulge scale radius as a fraction of <see cref="Radius"/>.</summary>
        public readonly double BulgeFraction;
        public readonly double BulgeAmplitude, DiskAmplitude, DiskScaleFraction;
        public readonly double ArmContrast;
        public readonly int ArmSharpness;
        /// <summary>Bar half-length as a fraction of the radius (0 = no bar).</summary>
        public readonly double BarLength;
        public readonly double ClumpAmplitude, ClumpScale;
        /// <summary>Display tint for maps.</summary>
        public readonly Rgb Tint;
        /// <summary>Two-letter prefix used for system catalogue designations ("VS" for "The Verdant Spiral").</summary>
        public readonly string CataloguePrefix;
        public readonly bool Authored;
        public readonly ulong NoiseSeed;
        /// <summary>Macro-grid cell of a generated galaxy (zero for authored ones).</summary>
        public readonly int MacroX, MacroY;

        readonly double _cos, _sin, _invAxis, _reach, _invBulge2, _invDiskH, _armR0, _invArmR0, _fadeLo, _fadeHi, _invArmMean;
        readonly double _invBarL, _invBarW, _barAmp, _invClump, _invEllScale;

        /// <summary>Creates an authored (landmark) galaxy with fixed parameters. Only name, centre, radius and arms are required.</summary>
        public GalaxyInfo(string name, double centerX, double centerY, double radius, int arms,
            GalaxyKind kind = GalaxyKind.Spiral, double winding = 3.2, double armPhase = 0.0, int chirality = 1,
            double ellipticity = 0.15, double rotation = 0.4, double bulgeFraction = 0.15,
            double armContrast = 0.85, int armSharpness = 3, double barLength = 0.0,
            double bulgeAmplitude = 0.85, double diskAmplitude = 0.55, double diskScaleFraction = 0.26,
            double clumpAmplitude = 0.55, double clumpScale = 220.0, Rgb? tint = null, ulong id = 0UL, ulong noiseSeed = 0UL)
            : this(name, centerX, centerY, radius, arms, kind, winding, armPhase, chirality, ellipticity, rotation, bulgeFraction,
                armContrast, armSharpness, barLength, bulgeAmplitude, diskAmplitude, diskScaleFraction, clumpAmplitude, clumpScale,
                tint ?? DefaultTint(kind), id != 0UL ? id : AuthoredId(name), noiseSeed != 0UL ? noiseSeed : Hash.Fnv1a64("noise:" + name), true, 0, 0)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("name is required");
            if (!(radius > 0.0)) throw new ArgumentException("radius must be positive");
            if (arms < 0 || arms > 8) throw new ArgumentException("arms must be in [0, 8]");
            if (!(ellipticity >= 0.0 && ellipticity < 0.95)) throw new ArgumentException("ellipticity must be in [0, 0.95)");
            if (!(bulgeFraction > 0.0 && diskScaleFraction > 0.0 && clumpScale > 0.0)) throw new ArgumentException("bulgeFraction, diskScaleFraction and clumpScale must be positive");
            if ((id >> 62) == 2UL) throw new ArgumentException("ids with the top bits 10 are reserved for generated galaxies");
        }

        internal GalaxyInfo(string name, double centerX, double centerY, double radius, int arms,
            GalaxyKind kind, double winding, double armPhase, int chirality, double ellipticity, double rotation, double bulgeFraction,
            double armContrast, int armSharpness, double barLength, double bulgeAmplitude, double diskAmplitude, double diskScaleFraction,
            double clumpAmplitude, double clumpScale, Rgb tint, ulong id, ulong noiseSeed, bool authored, int macroX, int macroY)
        {
            Name = name;
            CenterX = centerX;
            CenterY = centerY;
            Radius = radius;
            Kind = kind;
            Arms = kind == GalaxyKind.Elliptical ? 0 : arms;
            Winding = winding;
            ArmPhase = armPhase;
            Chirality = chirality >= 0 ? 1 : -1;
            Ellipticity = ellipticity;
            Rotation = rotation;
            BulgeFraction = bulgeFraction;
            ArmContrast = armContrast;
            ArmSharpness = armSharpness < 2 ? 2 : (armSharpness > 4 ? 4 : armSharpness);
            BarLength = kind == GalaxyKind.BarredSpiral ? barLength : 0.0;
            BulgeAmplitude = bulgeAmplitude;
            DiskAmplitude = kind == GalaxyKind.Elliptical ? 0.0 : diskAmplitude;
            DiskScaleFraction = diskScaleFraction;
            ClumpAmplitude = clumpAmplitude;
            ClumpScale = clumpScale;
            Tint = tint;
            CataloguePrefix = NameBank.CataloguePrefix(name);
            Id = id;
            NoiseSeed = noiseSeed;
            Authored = authored;
            MacroX = macroX;
            MacroY = macroY;

            double s, c;
            DetMath.SinCos(rotation, out s, out c);
            _cos = c;
            _sin = s;
            _invAxis = 1.0 / (1.0 - ellipticity);
            _reach = 1.3 * radius;
            double rb = bulgeFraction * radius;
            _invBulge2 = 1.0 / (rb * rb);
            _invDiskH = 1.0 / (diskScaleFraction * radius);
            _armR0 = 0.5 * rb;
            _invArmR0 = 1.0 / _armR0;
            _fadeLo = 0.6 * rb;
            _fadeHi = 2.4 * rb;
            _invArmMean = ArmSharpness == 2 ? 1.0 / 0.375 : (ArmSharpness == 3 ? 1.0 / 0.3125 : 1.0 / 0.2734375);
            double barL = BarLength * radius;
            _invBarL = barL > 0.0 ? 1.0 / barL : 0.0;
            _invBarW = barL > 0.0 ? 1.0 / (0.3 * barL) : 0.0;
            _barAmp = 0.6 * bulgeAmplitude;
            _invClump = 1.0 / clumpScale;
            // ellipticals: scale radius of the exp(-sqrt(r/scale)) profile (never wider than 0.09 R, so the envelope fades out before the cutoff)
            _invEllScale = 1.0 / (Math.Min(bulgeFraction, 0.09) * radius);
        }

        static Rgb DefaultTint(GalaxyKind kind)
        {
            return kind == GalaxyKind.Elliptical ? new Rgb(1.0f, 0.82f, 0.55f) : new Rgb(0.65f, 0.80f, 1.0f);
        }

        static ulong AuthoredId(string name)
        {
            return (Hash.Fnv1a64("galaxy:" + name) & 0x3FFFFFFFFFFFFFFFUL) | 1UL;
        }

        internal static ulong MacroId(int mx, int my)
        {
            return (2UL << 62) | ((ulong)((uint)mx & 0x7FFFFFFFu) << 31) | (ulong)((uint)my & 0x7FFFFFFFu);
        }

        internal static bool TryDecodeMacroId(ulong id, out int mx, out int my)
        {
            mx = my = 0;
            if ((id >> 62) != 2UL) return false;
            mx = SignExtend31((uint)((id >> 31) & 0x7FFFFFFFUL));
            my = SignExtend31((uint)(id & 0x7FFFFFFFUL));
            return true;
        }

        static int SignExtend31(uint v)
        {
            return unchecked((int)((v & 0x40000000u) != 0 ? (v | 0x80000000u) : v));
        }

        /// <summary>Axis ratio minor/major.</summary>
        public double AxisRatio { get { return 1.0 - Ellipticity; } }

        /// <summary>Largest distance from the centre at which the density can be non-zero.</summary>
        public double Reach { get { return _reach; } }

        // ---- galaxy-relative helpers --------------------------------------------------------------------------------------

        /// <summary>Galaxy frame in light-years: u along the major axis, v along the minor axis.</summary>
        public void ToLocal(double x, double y, out double u, out double v)
        {
            double dx = x - CenterX, dy = y - CenterY;
            u = dx * _cos + dy * _sin;
            v = dy * _cos - dx * _sin;
        }

        public void FromLocal(double u, double v, out double x, out double y)
        {
            x = CenterX + u * _cos - v * _sin;
            y = CenterY + u * _sin + v * _cos;
        }

        /// <summary>Distance from the centre in units of <see cref="Radius"/> after undoing the flattening (0 at the centre, 1 at the edge).</summary>
        public double NormalizedRadius(double x, double y)
        {
            double u, v;
            ToLocal(x, y, out u, out v);
            v *= _invAxis;
            return Math.Sqrt(u * u + v * v) / Radius;
        }

        /// <summary>Polar angle in the circularised galaxy frame, radians in (-pi, pi].</summary>
        public double AngleOf(double x, double y)
        {
            double u, v;
            ToLocal(x, y, out u, out v);
            return DetMath.Atan2(v * _invAxis, u);
        }

        public bool Contains(double x, double y) { return NormalizedRadius(x, y) <= 1.0; }

        /// <summary>Galaxy-only density at a point (no halo, may exceed DensityMax before the field clamps it).</summary>
        public double DensityAt(double x, double y)
        {
            double d, b, a, r;
            Evaluate(x, y, out d, out b, out a, out r);
            return d;
        }

        /// <summary>Position of the centre line of arm <paramref name="armIndex"/> at normalised radius rn (galaxy frame angle in radians, in the circularised frame).</summary>
        public double ArmAngleAt(int armIndex, double normalizedRadius)
        {
            double r = normalizedRadius * Radius;
            double lnr = r > _armR0 ? DetMath.Log(r * _invArmR0) : 0.0;
            return ArmPhase + Chirality * Winding * lnr + DetMath.TwoPi * armIndex / (Arms > 0 ? Arms : 1);
        }

        internal void Evaluate(double x, double y, out double density, out double bulgePart, out double armStrength, out double rNorm)
        {
            density = 0.0;
            bulgePart = 0.0;
            armStrength = 0.0;
            rNorm = 0.0;
            double reach = _reach;
            double dx = x - CenterX, dy = y - CenterY;
            if (dx > reach || dx < -reach || dy > reach || dy < -reach) return;
            double u = dx * _cos + dy * _sin;
            double v = (dy * _cos - dx * _sin) * _invAxis;
            double r2 = u * u + v * v;
            if (r2 >= reach * reach) return;
            double r = Math.Sqrt(r2);
            rNorm = r / Radius;

            double sb = 1.0 + r2 * _invBulge2;
            double bulge;
            if (Kind == GalaxyKind.Elliptical)
            {
                // cusped core with a long faint envelope: exp(-sqrt(r / scale)) fades into the halo without a visible rim
                bulge = BulgeAmplitude * DetMath.Exp(-Math.Sqrt(r * _invEllScale));
            }
            else
            {
                bulge = BulgeAmplitude / (sb * sb);
            }
            if (_invBarL > 0.0)
            {
                double bx = u * _invBarL, by = v * _invBarW;
                double sbar = 1.0 + bx * bx + by * by;
                bulge += _barAmp / (sbar * sbar);
            }

            double disk = 0.0;
            if (DiskAmplitude > 0.0)
            {
                disk = DiskAmplitude * DetMath.Exp(-r * _invDiskH);
                if (Arms > 0 && r > 1e-6)
                {
                    double lnr = r > _armR0 ? DetMath.Log(r * _invArmR0) : 0.0;
                    double ph = ArmPhase + Chirality * Winding * lnr;
                    double zr = u / r, zi = v / r;
                    double pr = zr, pim = zi;
                    for (int k = 1; k < Arms; k++)
                    {
                        double t = pr * zr - pim * zi;
                        pim = pr * zi + pim * zr;
                        pr = t;
                    }
                    double sn, cs;
                    DetMath.SinCos(Arms * ph, out sn, out cs);
                    double a = 0.5 + 0.5 * (pr * cs + pim * sn);
                    double shape = a * a;
                    if (ArmSharpness >= 3) shape *= a;
                    if (ArmSharpness >= 4) shape *= a;
                    double contrast = ArmContrast * DetMath.SmoothStep(_fadeLo, _fadeHi, r);
                    armStrength = shape * contrast;
                    // sharp ridge plus a soft shoulder (both have mean 1 around the circle)
                    double profile = 0.75 * shape * _invArmMean + 0.25 * (a * 2.0);
                    disk *= (1.0 - contrast) + contrast * profile;
                }
            }

            double n = Noise.Fbm(NoiseSeed, x * _invClump, y * _invClump, 3);
            double clump = 1.0 + ClumpAmplitude * (n - 0.5) * 4.0;
            if (clump < 0.25) clump = 0.25; else if (clump > 2.2) clump = 2.2;
            double edge = r <= Radius ? 1.0 : 1.0 - DetMath.SmoothStep(Radius, reach, r);
            double b2 = bulge * (1.0 + (clump - 1.0) * 0.35) * edge;
            bulgePart = b2;
            density = b2 + disk * clump * edge;
        }

        public override string ToString() { return Name + " (" + Kind + ", r=" + Radius.ToString("0") + " ly, " + Arms + " arms)"; }
    }

    /// <summary>Result of sampling the density field at a point.</summary>
    public struct DensitySample
    {
        /// <summary>Final density: max(HaloDensity, sum of galaxy densities), at most DensityMax.</summary>
        public double Density;
        /// <summary>Dominant galaxy at the point, or null in intergalactic space.</summary>
        public GalaxyInfo Galaxy;
        /// <summary>Sum of all galaxy densities before the halo clamp.</summary>
        public double GalaxyDensity;
        /// <summary>Distance from the dominant galaxy's centre / its radius.</summary>
        public double RadiusNorm;
        /// <summary>Share of the dominant galaxy's density that comes from its bulge (0..1).</summary>
        public double BulgeShare;
        /// <summary>How strongly the point sits on a spiral arm (0..1).</summary>
        public double ArmStrength;
        /// <summary>1 at the very centre of the dominant galaxy, falling off quickly (used for black holes and neutron stars).</summary>
        public double CoreProximity;
    }

    /// <summary>
    /// Deterministic field of galaxies on a macro grid (at most one per cell, jittered centre) plus host supplied authored galaxies.
    /// Thread safety: the macro-galaxy cache is guarded by a lock; all galaxy objects are immutable.
    /// </summary>
    public sealed class GalaxyField
    {
        readonly UniverseConfig _cfg;
        readonly StreamSalts _salts;
        readonly GalaxyInfo[] _authored;
        readonly Dictionary<long, GalaxyInfo> _macro = new Dictionary<long, GalaxyInfo>();
        readonly object _gate = new object();
        readonly int _span;
        readonly double _cell;

        /// <param name="config">Used as is (the Universe passes its private copy).</param>
        public GalaxyField(UniverseConfig config, IList<GalaxyInfo> authored)
        {
            _cfg = config;
            _salts = new StreamSalts(config.Seed, config.SaltOverrides);
            _cell = config.MacroCellSize;
            _authored = new GalaxyInfo[authored == null ? 0 : authored.Count];
            for (int i = 0; i < _authored.Length; i++) _authored[i] = authored[i];
            double reachMax = 1.3 * config.GalaxyRadiusMax;
            _span = Math.Max(1, (int)Math.Floor((1.0 - config.GalaxyJitterMin) + reachMax / _cell));
        }

        public IReadOnlyList<GalaxyInfo> Authored { get { return _authored; } }

        static long CellKey(int mx, int my) { unchecked { return ((long)mx << 32) ^ (long)(uint)my; } }

        static ulong MacroKey(int mx, int my) { unchecked { return ((ulong)(uint)mx << 32) | (ulong)(uint)my; } }

        /// <summary>The random galaxy of a macro-grid cell, or null when the cell has none.</summary>
        public GalaxyInfo GetMacroGalaxy(int mx, int my)
        {
            long k = CellKey(mx, my);
            GalaxyInfo g;
            lock (_gate)
            {
                if (_macro.TryGetValue(k, out g)) return g;
            }
            g = CreateMacro(mx, my);
            lock (_gate)
            {
                GalaxyInfo other;
                if (_macro.TryGetValue(k, out other)) return other;
                if (_macro.Count >= _cfg.GalaxyCacheCapacity) _macro.Clear();
                _macro[k] = g;
            }
            return g;
        }

        bool TryMacroCentre(int mx, int my, out double cx, out double cy)
        {
            ulong key = MacroKey(mx, my);
            cx = cy = 0.0;
            if (!(Hash.Unit(_salts.Stream(Attr.GalaxyExists, key)) < _cfg.GalaxyChance)) return false;
            var r = _salts.Rng(Attr.GalaxyCentre, key);
            double jx = _cfg.GalaxyJitterMin + (_cfg.GalaxyJitterMax - _cfg.GalaxyJitterMin) * r.NextDouble();
            double jy = _cfg.GalaxyJitterMin + (_cfg.GalaxyJitterMax - _cfg.GalaxyJitterMin) * r.NextDouble();
            cx = (mx + jx) * _cell;
            cy = (my + jy) * _cell;
            return true;
        }

        GalaxyInfo CreateMacro(int mx, int my)
        {
            double cx, cy;
            if (!TryMacroCentre(mx, my, out cx, out cy)) return null;
            ulong key = MacroKey(mx, my);

            var rr = _salts.Rng(Attr.GalaxyRadius, key);
            double u = rr.NextDouble();
            double radius = _cfg.GalaxyRadiusMin + (_cfg.GalaxyRadiusMax - _cfg.GalaxyRadiusMin) * u * Math.Sqrt(u);

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    double nx, ny;
                    if (!TryMacroCentre(mx + dx, my + dy, out nx, out ny)) continue;
                    double d = Math.Sqrt((nx - cx) * (nx - cx) + (ny - cy) * (ny - cy));
                    double cap = _cfg.GalaxyNeighbourCap * d;
                    if (cap < radius) radius = cap;
                }
            }
            for (int i = 0; i < _authored.Length; i++)
            {
                var a = _authored[i];
                double d = Math.Sqrt((a.CenterX - cx) * (a.CenterX - cx) + (a.CenterY - cy) * (a.CenterY - cy));
                double cap = d - 0.9 * a.Radius;
                if (cap < radius) radius = cap;
            }
            if (radius < 0.75 * _cfg.GalaxyRadiusMin) return null;

            var sh = _salts.Rng(Attr.GalaxyShape, key);
            double kindRoll = sh.NextDouble();
            GalaxyKind kind = kindRoll < _cfg.EllipticalChance ? GalaxyKind.Elliptical
                : (kindRoll < _cfg.EllipticalChance + _cfg.BarredChance * (1.0 - _cfg.EllipticalChance) ? GalaxyKind.BarredSpiral : GalaxyKind.Spiral);
            int arms = _cfg.ArmsMin + sh.NextInt(_cfg.ArmsMax - _cfg.ArmsMin + 1);
            double winding = sh.Range(_cfg.WindingMin, _cfg.WindingMax);
            double armPhase = sh.NextDouble() * DetMath.TwoPi / (arms > 0 ? arms : 1);
            int chirality = sh.NextDouble() < 0.5 ? 1 : -1;
            double eu = sh.NextDouble();
            double ellipticity = _cfg.EllipticityMin + (_cfg.EllipticityMax - _cfg.EllipticityMin) * eu * eu;
            double rotation = sh.NextDouble() * DetMath.Pi;
            double bulgeFraction = sh.Range(_cfg.BulgeFractionMin, _cfg.BulgeFractionMax);
            double contrast = sh.Range(_cfg.ArmContrastMin, _cfg.ArmContrastMax);
            int sharpness = 2 + sh.NextInt(3);
            double barLength = sh.Range(0.20, 0.36);
            double clumpAmp = _cfg.ClumpAmplitude * sh.Range(0.8, 1.2);
            double clumpScale = _cfg.ClumpScale * sh.Range(0.8, 1.25);
            double bulgeAmp = _cfg.BulgeAmplitude, diskAmp = _cfg.DiskAmplitude;
            if (kind == GalaxyKind.Elliptical)
            {
                bulgeFraction = 0.08;
                bulgeAmp = 1.0;
                diskAmp = 0.0;
                clumpAmp *= 0.45;
                ellipticity = 0.1 + 0.5 * eu;
                arms = 0;
            }

            var tr = _salts.Rng(Attr.GalaxyTint, key);
            Rgb tint = MakeTint(ref tr, kind);
            var nr = _salts.Rng(Attr.GalaxyName, key);
            string name = NameBank.Galaxy(ref nr, kind);
            ulong noise = _salts.Stream(Attr.GalaxyNoise, key);

            return new GalaxyInfo(name, cx, cy, radius, arms, kind, winding, armPhase, chirality, ellipticity, rotation, bulgeFraction,
                contrast, sharpness, barLength, bulgeAmp, diskAmp, _cfg.DiskScaleFraction, clumpAmp, clumpScale, tint,
                GalaxyInfo.MacroId(mx, my), noise, false, mx, my);
        }

        static Rgb MakeTint(ref Rng r, GalaxyKind kind)
        {
            double h;
            double roll = r.NextDouble();
            if (kind == GalaxyKind.Elliptical) h = 0.06 + 0.06 * roll;
            else if (roll < 0.45) h = 0.54 + 0.08 * r.NextDouble();
            else if (roll < 0.70) h = 0.44 + 0.08 * r.NextDouble();
            else if (roll < 0.88) h = 0.72 + 0.10 * r.NextDouble();
            else h = 0.94 + 0.06 * r.NextDouble();
            double s = kind == GalaxyKind.Elliptical ? 0.45 : 0.30 + 0.25 * r.NextDouble();
            return Hsv(h, s, 1.0);
        }

        internal static Rgb Hsv(double h, double s, double v)
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

        // ---- queries --------------------------------------------------------------------------------------------------------

        /// <summary>Density at a point: max(HaloDensity, sum of galaxy densities), clamped to DensityMax. Allocation free.</summary>
        public double Density(double x, double y)
        {
            DensitySample s;
            Sample(x, y, out s);
            return s.Density;
        }

        /// <summary>Full density sample (dominant galaxy, bulge share, arm strength). Allocation free.</summary>
        public void Sample(double x, double y, out DensitySample s)
        {
            s = default(DensitySample);
            double total = 0.0, best = 0.0, bestBulge = 0.0, bestArm = 0.0, bestR = 0.0;
            GalaxyInfo bestG = null;
            double d, b, a, rn;

            for (int i = 0; i < _authored.Length; i++)
            {
                var g = _authored[i];
                g.Evaluate(x, y, out d, out b, out a, out rn);
                if (d <= 0.0) continue;
                total += d;
                if (d > best) { best = d; bestG = g; bestBulge = b; bestArm = a; bestR = rn; }
            }

            int mx0 = (int)Math.Floor(x / _cell), my0 = (int)Math.Floor(y / _cell);
            for (int dy = -_span; dy <= _span; dy++)
            {
                for (int dx = -_span; dx <= _span; dx++)
                {
                    var g = GetMacroGalaxy(mx0 + dx, my0 + dy);
                    if (g == null) continue;
                    g.Evaluate(x, y, out d, out b, out a, out rn);
                    if (d <= 0.0) continue;
                    total += d;
                    if (d > best) { best = d; bestG = g; bestBulge = b; bestArm = a; bestR = rn; }
                }
            }

            s.GalaxyDensity = total;
            double dens = total > _cfg.HaloDensity ? total : _cfg.HaloDensity;
            if (dens > _cfg.DensityMax) dens = _cfg.DensityMax;
            s.Density = dens;
            if (bestG != null && total >= _cfg.HaloDensity)
            {
                s.Galaxy = bestG;
                s.RadiusNorm = bestR;
                s.BulgeShare = best > 0.0 ? DetMath.Clamp01(bestBulge / best) : 0.0;
                s.ArmStrength = DetMath.Clamp01(bestArm);
                s.CoreProximity = DetMath.Exp(-bestR / 0.12);
            }
        }

        /// <summary>The dominant galaxy at a point (the galaxy whose density exceeds the halo), or null in intergalactic space.</summary>
        public GalaxyInfo GalaxyAt(double x, double y)
        {
            DensitySample s;
            Sample(x, y, out s);
            return s.Galaxy;
        }

        /// <summary>The galaxy whose centre is closest to the point (searching the nearby macro cells and all authored galaxies), or null.</summary>
        public GalaxyInfo NearestGalaxy(double x, double y, out double distance)
        {
            GalaxyInfo best = null;
            double bestD = double.MaxValue;
            for (int i = 0; i < _authored.Length; i++)
            {
                double d = Dist(_authored[i], x, y);
                if (d < bestD) { bestD = d; best = _authored[i]; }
            }
            int mx0 = (int)Math.Floor(x / _cell), my0 = (int)Math.Floor(y / _cell);
            for (int dy = -_span; dy <= _span; dy++)
            {
                for (int dx = -_span; dx <= _span; dx++)
                {
                    var g = GetMacroGalaxy(mx0 + dx, my0 + dy);
                    if (g == null) continue;
                    double d = Dist(g, x, y);
                    if (d < bestD) { bestD = d; best = g; }
                }
            }
            distance = best == null ? double.PositiveInfinity : bestD;
            return best;
        }

        static double Dist(GalaxyInfo g, double x, double y)
        {
            double dx = g.CenterX - x, dy = g.CenterY - y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Adds every galaxy whose disk comes within <paramref name="range"/> of the point to <paramref name="results"/>; returns how many were added.</summary>
        public int GalaxiesNear(double x, double y, double range, List<GalaxyInfo> results)
        {
            return GalaxiesInRect(x - range, y - range, x + range, y + range, results, x, y, range);
        }

        /// <summary>Adds every galaxy whose bounding disk overlaps the rectangle to <paramref name="results"/>; returns how many were added.</summary>
        public int GalaxiesInRect(double x0, double y0, double x1, double y1, List<GalaxyInfo> results)
        {
            return GalaxiesInRect(x0, y0, x1, y1, results, 0.0, 0.0, -1.0);
        }

        int GalaxiesInRect(double x0, double y0, double x1, double y1, List<GalaxyInfo> results, double px, double py, double circleRange)
        {
            int added = 0;
            for (int i = 0; i < _authored.Length; i++)
            {
                if (Overlaps(_authored[i], x0, y0, x1, y1, px, py, circleRange)) { results.Add(_authored[i]); added++; }
            }
            double reachMax = 1.3 * _cfg.GalaxyRadiusMax;
            int cx0 = (int)Math.Floor((x0 - reachMax) / _cell), cx1 = (int)Math.Floor((x1 + reachMax) / _cell);
            int cy0 = (int)Math.Floor((y0 - reachMax) / _cell), cy1 = (int)Math.Floor((y1 + reachMax) / _cell);
            for (int cy = cy0; cy <= cy1; cy++)
            {
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    var g = GetMacroGalaxy(cx, cy);
                    if (g != null && Overlaps(g, x0, y0, x1, y1, px, py, circleRange)) { results.Add(g); added++; }
                }
            }
            return added;
        }

        static bool Overlaps(GalaxyInfo g, double x0, double y0, double x1, double y1, double px, double py, double circleRange)
        {
            double r = g.Radius;
            if (circleRange >= 0.0) return Dist(g, px, py) <= circleRange + r;
            double nx = g.CenterX < x0 ? x0 : (g.CenterX > x1 ? x1 : g.CenterX);
            double ny = g.CenterY < y0 ? y0 : (g.CenterY > y1 ? y1 : g.CenterY);
            double dx = g.CenterX - nx, dy = g.CenterY - ny;
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>Finds a galaxy (authored or generated) by its id.</summary>
        public bool TryGetGalaxyById(ulong id, out GalaxyInfo galaxy)
        {
            galaxy = null;
            if (id == 0UL) return false;
            int mx, my;
            if (GalaxyInfo.TryDecodeMacroId(id, out mx, out my))
            {
                var g = GetMacroGalaxy(mx, my);
                if (g != null && g.Id == id) { galaxy = g; return true; }
                return false;
            }
            for (int i = 0; i < _authored.Length; i++)
            {
                if (_authored[i].Id == id) { galaxy = _authored[i]; return true; }
            }
            return false;
        }
    }

    /// <summary>A host supplied system: a system description plus its planets (all planet keys must belong to the system).</summary>
    public sealed class AuthoredSystem
    {
        public readonly SystemSpec Spec;
        public readonly PlanetDescriptor[] Planets;

        public AuthoredSystem(SystemSpec spec, PlanetDescriptor[] planets)
        {
            Spec = spec;
            Planets = planets ?? Array.Empty<PlanetDescriptor>();
        }
    }

    /// <summary>
    /// Everything the game authors by hand (landmark galaxies and Core World systems). It is part of the universe definition:
    /// the same seed with different authored content is a different universe.
    /// </summary>
    public sealed class AuthoredContent
    {
        public readonly List<GalaxyInfo> Galaxies = new List<GalaxyInfo>();
        public readonly List<AuthoredSystem> Systems = new List<AuthoredSystem>();

        public AuthoredContent AddGalaxy(GalaxyInfo galaxy)
        {
            Galaxies.Add(galaxy);
            return this;
        }

        public AuthoredContent AddSystem(SystemSpec spec, PlanetDescriptor[] planets)
        {
            Systems.Add(new AuthoredSystem(spec, planets));
            return this;
        }
    }

    /// <summary>Default landmark galaxies (stand-ins for the game's three authored galaxies). The Home galaxy sits at the origin.</summary>
    public static class Landmarks
    {
        public static GalaxyInfo CreateHomeGalaxy()
        {
            return new GalaxyInfo("The Verdant Spiral", 0.0, 0.0, 1700.0, 3,
                kind: GalaxyKind.Spiral, winding: 3.0, armPhase: 0.6, chirality: 1, ellipticity: 0.18, rotation: 0.5, bulgeFraction: 0.15,
                armContrast: 0.82, clumpAmplitude: 0.45, clumpScale: 240.0, tint: new Rgb(0.55f, 1.0f, 0.75f));
        }

        public static GalaxyInfo CreateEmberReach()
        {
            return new GalaxyInfo("Ember Reach", 4300.0, 400.0, 1400.0, 2,
                kind: GalaxyKind.BarredSpiral, winding: 3.6, armPhase: 1.1, chirality: -1, ellipticity: 0.30, rotation: 2.2, bulgeFraction: 0.14,
                barLength: 0.30, tint: new Rgb(1.0f, 0.62f, 0.35f));
        }

        public static GalaxyInfo CreateFracturedExpanse()
        {
            return new GalaxyInfo("The Fractured Expanse", -3300.0, 4600.0, 1700.0, 4,
                kind: GalaxyKind.Spiral, winding: 2.8, armPhase: 0.2, chirality: 1, ellipticity: 0.10, rotation: 1.2, bulgeFraction: 0.17,
                armSharpness: 4, tint: new Rgb(0.85f, 0.75f, 1.0f));
        }

        /// <summary>The three landmark galaxies and no systems.</summary>
        public static AuthoredContent CreateDefault()
        {
            return new AuthoredContent().AddGalaxy(CreateHomeGalaxy()).AddGalaxy(CreateEmberReach()).AddGalaxy(CreateFracturedExpanse());
        }
    }
}
