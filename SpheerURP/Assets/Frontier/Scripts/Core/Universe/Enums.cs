using System;

namespace Frontier.Universe
{
    /// <summary>Terrain/ecology of a world. Order matches the game's existing PlanetBiome for the first eight values.</summary>
    public enum Biome : byte
    {
        Lush, Mineral, Glacial, Volcanic, Fungal, Crystal, Obsidian, Shattered,
        Desert, Oceanic, GasGiant, Barren
    }

    public enum SizeClass : byte { Dwarf, Small, Medium, Large, Giant, Colossal }

    /// <summary>Rocky worlds, gas giants, and asteroid belts (a belt is a Dwarf, Barren "planet" with its own resource profile).</summary>
    public enum PlanetKind : byte { Rocky, GasGiant, AsteroidBelt }

    public enum StarClass : byte { O, B, A, F, G, K, M, Neutron, BlackHole }

    [Flags]
    public enum Hazards : byte
    {
        None = 0,
        Heat = 1,
        Cold = 2,
        Toxic = 4,
        Radiation = 8,
        Void = 16
    }

    /// <summary>Same value order as the game's WeatherKind (Clear, Rain, Snow, Embers, Spores, Dust, Storm).</summary>
    public enum Weather : byte { Clear, Rain, Snow, Embers, Spores, Dust, Storm }

    /// <summary>The first three values match the game's MinigameKind (Chop, Mine, Drill).</summary>
    public enum HarvestMethod : byte { Chop, Mine, Drill, Siphon, Excavate, Salvage, Harvest }

    public enum GalaxyKind : byte { Spiral, BarredSpiral, Elliptical }

    /// <summary>Bit-mask helpers for sets of biomes.</summary>
    public static class BiomeMask
    {
        public static uint Of(Biome b) { return 1u << (int)b; }

        public static uint Of(params Biome[] biomes)
        {
            uint m = 0;
            for (int i = 0; i < biomes.Length; i++) m |= 1u << (int)biomes[i];
            return m;
        }

        public static bool Has(uint mask, Biome b) { return (mask & (1u << (int)b)) != 0; }

        public const uint All = (1u << 12) - 1;

        /// <summary>Every biome except GasGiant.</summary>
        public const uint Solid = All & ~(1u << (int)Biome.GasGiant);
    }

    /// <summary>Linear RGB colour with float components (rim colours may exceed 1 for HDR glow).</summary>
    public readonly struct Rgb : IEquatable<Rgb>
    {
        public readonly float R, G, B;

        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public bool Equals(Rgb o) { return R == o.R && G == o.G && B == o.B; }
        public override bool Equals(object obj) { return obj is Rgb && Equals((Rgb)obj); }
        public override int GetHashCode() { unchecked { return (R.GetHashCode() * 397) ^ (G.GetHashCode() * 17) ^ B.GetHashCode(); } }
        public override string ToString() { return "(" + R.ToString("0.###") + ", " + G.ToString("0.###") + ", " + B.ToString("0.###") + ")"; }

        public static Rgb Lerp(Rgb a, Rgb b, float t)
        {
            return new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        }
    }
}
