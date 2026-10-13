using System;

namespace Frontier.Universe
{
    /// <summary>Planet fields that <see cref="UniverseDigest"/> can leave out (used to prove that attribute streams are independent).</summary>
    [Flags]
    public enum PlanetFields
    {
        None = 0,
        Name = 1 << 0,
        Designation = 1 << 1,
        Kind = 1 << 2,
        Biome = 1 << 3,
        Size = 1 << 4,
        WorldSize = 1 << 5,
        BuildingSlots = 1 << 6,
        Tier = 1 << 7,
        Danger = 1 << 8,
        Hazards = 1 << 9,
        Weather = 1 << 10,
        OrbitRadius = 1 << 11,
        OrbitPhase = 1 << 12,
        Temperature = 1 << 13,
        HasRings = 1 << 14,
        MoonCount = 1 << 15,
        Colours = 1 << 16,
        Style = 1 << 17,
        SurfaceBands = 1 << 18,
        Glow = 1 << 19,
        Atmosphere = 1 << 20,
        ModelKey = 1 << 21,
        Deposits = 1 << 22,
        GeneratorVersion = 1 << 23,
        Visual = Colours | Style | SurfaceBands | Glow | Atmosphere,
        All = (1 << 24) - 1
    }

    /// <summary>
    /// Field-by-field digests of generated content. Floating point values are quantised (positions to 1/4096 ly, floats to 1/8192) so a
    /// digest survives last-bit differences between platforms; integers, enums and strings are hashed exactly (strings with FNV-1a).
    /// Digests are order dependent and stable across processes: they are what the golden test freezes.
    /// </summary>
    public static class UniverseDigest
    {
        static ulong Q(double v) { return unchecked((ulong)(long)Math.Floor(v * 4096.0 + 0.5)); }

        static ulong Q(float v) { return unchecked((ulong)(long)Math.Floor(v * 8192.0 + 0.5)); }

        static ulong Fold(ulong h, ulong v) { return Hash.Combine(h, v); }

        static ulong Fold(ulong h, string s) { return Hash.Combine(h, Hash.Fnv1a64(s)); }

        static ulong Fold(ulong h, Rgb c) { return Fold(Fold(Fold(h, Q(c.R)), Q(c.G)), Q(c.B)); }

        /// <summary>Digest of one star system (without its planets).</summary>
        public static ulong OfSystem(StarSystem s)
        {
            ulong h = Fold(0x5359535445UL, s.Key.Id);
            h = Fold(h, s.Id);
            h = Fold(h, s.Name);
            h = Fold(h, s.Designation);
            h = Fold(h, Q(s.X));
            h = Fold(h, Q(s.Y));
            h = Fold(h, (ulong)(int)s.StarClass);
            h = Fold(h, s.Color);
            h = Fold(h, Q(s.Luminosity));
            h = Fold(h, Q(s.SurfaceTemperature));
            h = Fold(h, Q(s.HabitableInner));
            h = Fold(h, Q(s.HabitableOuter));
            h = Fold(h, s.GalaxyId);
            h = Fold(h, (ulong)s.Tier);
            h = Fold(h, (ulong)s.PlanetCount);
            return h;
        }

        /// <summary>Digest of one planet, optionally skipping some fields.</summary>
        public static ulong OfPlanet(PlanetDescriptor p, PlanetFields skip)
        {
            ulong h = Fold(0x504C414E4554UL, p.Key.Id);
            h = Fold(h, p.Id);
            if (p.AuthoredId != null) h = Fold(h, p.AuthoredId);
            if ((skip & PlanetFields.Name) == 0) h = Fold(h, p.Name);
            if ((skip & PlanetFields.Designation) == 0) h = Fold(h, p.Designation);
            if ((skip & PlanetFields.Kind) == 0) h = Fold(h, (ulong)(int)p.Kind);
            if ((skip & PlanetFields.Biome) == 0) h = Fold(h, (ulong)(int)p.Biome);
            if ((skip & PlanetFields.Size) == 0) h = Fold(h, (ulong)(int)p.Size);
            if ((skip & PlanetFields.WorldSize) == 0) h = Fold(h, Q(p.WorldSize));
            if ((skip & PlanetFields.BuildingSlots) == 0) h = Fold(h, (ulong)p.BuildingSlots);
            if ((skip & PlanetFields.Tier) == 0) h = Fold(h, (ulong)p.Tier);
            if ((skip & PlanetFields.Danger) == 0) h = Fold(h, Q(p.Danger));
            if ((skip & PlanetFields.Hazards) == 0) h = Fold(h, (ulong)(int)p.Hazards);
            if ((skip & PlanetFields.Weather) == 0) h = Fold(h, (ulong)(int)p.Weather);
            if ((skip & PlanetFields.OrbitRadius) == 0) h = Fold(h, Q(p.OrbitRadius));
            if ((skip & PlanetFields.OrbitPhase) == 0) h = Fold(h, Q(p.OrbitPhase));
            if ((skip & PlanetFields.Temperature) == 0) h = Fold(h, Q(p.Temperature));
            if ((skip & PlanetFields.HasRings) == 0) h = Fold(h, p.HasRings ? 1UL : 0UL);
            if ((skip & PlanetFields.MoonCount) == 0) h = Fold(h, (ulong)p.MoonCount);
            if ((skip & PlanetFields.Colours) == 0)
            {
                h = Fold(h, p.Primary);
                h = Fold(h, p.Secondary);
                h = Fold(h, p.Rim);
            }
            if ((skip & PlanetFields.Style) == 0) h = Fold(h, (ulong)p.Style);
            if ((skip & PlanetFields.SurfaceBands) == 0) h = Fold(h, Q(p.SurfaceBands));
            if ((skip & PlanetFields.Glow) == 0) h = Fold(h, Q(p.GlowIntensity));
            if ((skip & PlanetFields.Atmosphere) == 0) h = Fold(h, Q(p.AtmosphereIntensity));
            if ((skip & PlanetFields.ModelKey) == 0) h = Fold(h, p.ModelKey);
            if ((skip & PlanetFields.Deposits) == 0)
            {
                h = Fold(h, (ulong)p.Deposits.Length);
                for (int i = 0; i < p.Deposits.Length; i++)
                {
                    Deposit d = p.Deposits[i];
                    h = Fold(h, d.ResourceId);
                    h = Fold(h, (ulong)(int)d.Method);
                    h = Fold(h, Q(d.Richness));
                    h = Fold(h, (ulong)d.BaseYield);
                    h = Fold(h, Q(d.Difficulty));
                }
            }
            if ((skip & PlanetFields.GeneratorVersion) == 0) h = Fold(h, (ulong)p.GeneratorVersion);
            return h;
        }

        public static ulong OfPlanet(PlanetDescriptor p) { return OfPlanet(p, PlanetFields.None); }

        /// <summary>Digest of every system (and optionally every planet) of a sector.</summary>
        public static ulong OfSector(Universe u, SectorCoord c, bool includePlanets, PlanetFields skip)
        {
            ulong h = Fold(0x534543544FUL, c.Id);
            StarSystem[] systems = u.SystemsInSector(c);
            h = Fold(h, (ulong)systems.Length);
            for (int i = 0; i < systems.Length; i++)
            {
                h = Fold(h, OfSystem(systems[i]));
                if (!includePlanets) continue;
                PlanetDescriptor[] planets = u.PlanetsOf(systems[i]);
                h = Fold(h, (ulong)planets.Length);
                for (int p = 0; p < planets.Length; p++) h = Fold(h, OfPlanet(planets[p], skip));
            }
            return h;
        }

        /// <summary>Digest of the sectors [x0, x1) x [y0, y1) in row-major order (the golden test uses a 50 x 40 region).</summary>
        public static ulong OfRegion(Universe u, int sectorX0, int sectorY0, int sectorX1, int sectorY1, bool includePlanets, PlanetFields skip)
        {
            ulong h = 0x524547494F4EUL;
            for (int y = sectorY0; y < sectorY1; y++)
                for (int x = sectorX0; x < sectorX1; x++)
                    h = Fold(h, OfSector(u, new SectorCoord(x, y), includePlanets, skip));
            return h;
        }

        public static ulong OfRegion(Universe u, int sectorX0, int sectorY0, int sectorX1, int sectorY1)
        {
            return OfRegion(u, sectorX0, sectorY0, sectorX1, sectorY1, true, PlanetFields.None);
        }
    }
}
