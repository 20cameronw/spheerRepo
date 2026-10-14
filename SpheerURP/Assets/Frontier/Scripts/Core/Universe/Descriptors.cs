using System;

namespace Frontier.Universe
{
    /// <summary>One harvestable resource on a planet.</summary>
    public readonly struct Deposit : IEquatable<Deposit>
    {
        public readonly string ResourceId;
        public readonly HarvestMethod Method;
        /// <summary>Quality multiplier, 0.5 to 2.0.</summary>
        public readonly float Richness;
        /// <summary>Yield per harvest before Richness and tool multipliers.</summary>
        public readonly int BaseYield;
        /// <summary>Mini-game difficulty, 0 to 1.</summary>
        public readonly float Difficulty;

        public Deposit(string resourceId, HarvestMethod method, float richness, int baseYield, float difficulty)
        {
            ResourceId = resourceId;
            Method = method;
            Richness = richness;
            BaseYield = baseYield;
            Difficulty = difficulty;
        }

        public bool Equals(Deposit o)
        {
            return ResourceId == o.ResourceId && Method == o.Method && Richness == o.Richness && BaseYield == o.BaseYield && Difficulty == o.Difficulty;
        }

        public override bool Equals(object obj) { return obj is Deposit && Equals((Deposit)obj); }
        public override int GetHashCode() { unchecked { return (int)Hash.Fnv1a64(ResourceId) ^ (int)Method ^ BaseYield; } }
        public override string ToString() { return ResourceId + " (" + Method + ") x" + Richness.ToString("0.00"); }
    }

    /// <summary>Plain data used to build a <see cref="StarSystem"/>; hosts use it for authored systems.</summary>
    public struct SystemSpec
    {
        /// <summary>Use for <see cref="Tier"/> or <see cref="GalaxyId"/> to let the Universe compute the value for authored systems.</summary>
        public const int AutoTier = -1;
        public const ulong AutoGalaxy = ulong.MaxValue;

        public SystemKey Key;
        public string Name;
        public string Designation;
        /// <summary>Position in light-years.</summary>
        public double X, Y;
        public StarClass StarClass;
        public Rgb Color;
        /// <summary>Solar luminosities.</summary>
        public float Luminosity;
        /// <summary>Effective surface temperature in Kelvin.</summary>
        public float SurfaceTemperature;
        /// <summary>Habitable band in AU-like units.</summary>
        public float HabitableInner, HabitableOuter;
        /// <summary>Galaxy the system belongs to; 0 means intergalactic space.</summary>
        public ulong GalaxyId;
        /// <summary>Tier at the system position (distance from home, without planet anomalies).</summary>
        public int Tier;
        public int PlanetCount;
    }

    /// <summary>An immutable star system. Instances are shared between callers; never mutate them.</summary>
    public sealed class StarSystem
    {
        public readonly SystemKey Key;
        /// <summary>Compact string id, "s:X:Y:I".</summary>
        public readonly string Id;
        public readonly string Name;
        /// <summary>Catalogue designation such as "VS-0482913".</summary>
        public readonly string Designation;
        public readonly double X, Y;
        public readonly StarClass StarClass;
        public readonly Rgb Color;
        public readonly float Luminosity;
        public readonly float SurfaceTemperature;
        public readonly float HabitableInner, HabitableOuter;
        public readonly ulong GalaxyId;
        public readonly int Tier;
        public readonly int PlanetCount;

        public StarSystem(SystemSpec s)
        {
            Key = s.Key;
            Id = s.Key.ToString();
            Name = s.Name;
            Designation = s.Designation;
            X = s.X;
            Y = s.Y;
            StarClass = s.StarClass;
            Color = s.Color;
            Luminosity = s.Luminosity;
            SurfaceTemperature = s.SurfaceTemperature;
            HabitableInner = s.HabitableInner;
            HabitableOuter = s.HabitableOuter;
            GalaxyId = s.GalaxyId;
            Tier = s.Tier;
            PlanetCount = s.PlanetCount;
        }

        public bool IsAuthored { get { return Key.IsAuthored; } }

        public SectorCoord Sector { get { return Key.Sector; } }

        public override string ToString() { return Name + " [" + Id + "] " + StarClass; }
    }

    /// <summary>Plain data used to build a <see cref="PlanetDescriptor"/>; hosts use it for authored planets.</summary>
    public struct PlanetSpec
    {
        public PlanetKey Key;
        /// <summary>Host id of an authored world (for example "sylva"); null for generated planets.</summary>
        public string AuthoredId;
        public string Name;
        public string Designation;
        public PlanetKind Kind;
        public Biome Biome;
        public SizeClass Size;
        public float WorldSize;
        public int BuildingSlots;
        public int Tier;
        public float Danger;
        public Hazards Hazards;
        public Weather Weather;
        /// <summary>Orbit radius in AU-like units and phase in radians.</summary>
        public float OrbitRadius, OrbitPhase;
        /// <summary>Equilibrium temperature in Kelvin.</summary>
        public float Temperature;
        public bool HasRings;
        public int MoonCount;
        public Rgb Primary, Secondary, Rim;
        /// <summary>Surface pattern 0..4: bands, swirls, craters, lava veins, crystalline facets.</summary>
        public int Style;
        public float SurfaceBands, GlowIntensity, AtmosphereIntensity;
        public string ModelKey;
        public Deposit[] Deposits;
        /// <summary>Generator version this planet was created with (stored in saves for discovered worlds).</summary>
        public int GeneratorVersion;
    }

    /// <summary>An immutable planet description. Instances are shared between callers; never mutate them or their arrays.</summary>
    public sealed class PlanetDescriptor
    {
        public readonly PlanetKey Key;
        /// <summary>Canonical compact id for saves, "p:X:Y:I:P".</summary>
        public readonly string Id;
        public readonly string AuthoredId;
        public readonly string Name;
        public readonly string Designation;
        public readonly PlanetKind Kind;
        public readonly Biome Biome;
        public readonly SizeClass Size;
        public readonly float WorldSize;
        public readonly int BuildingSlots;
        public readonly int Tier;
        public readonly float Danger;
        public readonly Hazards Hazards;
        public readonly Weather Weather;
        public readonly float OrbitRadius, OrbitPhase;
        public readonly float Temperature;
        public readonly bool HasRings;
        public readonly int MoonCount;
        public readonly Rgb Primary, Secondary, Rim;
        public readonly int Style;
        public readonly float SurfaceBands, GlowIntensity, AtmosphereIntensity;
        public readonly string ModelKey;
        public readonly Deposit[] Deposits;
        public readonly int GeneratorVersion;

        public PlanetDescriptor(PlanetSpec s)
        {
            Key = s.Key;
            Id = s.Key.ToString();
            AuthoredId = s.AuthoredId;
            Name = s.Name;
            Designation = s.Designation;
            Kind = s.Kind;
            Biome = s.Biome;
            Size = s.Size;
            WorldSize = s.WorldSize;
            BuildingSlots = s.BuildingSlots;
            Tier = s.Tier;
            Danger = s.Danger;
            Hazards = s.Hazards;
            Weather = s.Weather;
            OrbitRadius = s.OrbitRadius;
            OrbitPhase = s.OrbitPhase;
            Temperature = s.Temperature;
            HasRings = s.HasRings;
            MoonCount = s.MoonCount;
            Primary = s.Primary;
            Secondary = s.Secondary;
            Rim = s.Rim;
            Style = s.Style;
            SurfaceBands = s.SurfaceBands;
            GlowIntensity = s.GlowIntensity;
            AtmosphereIntensity = s.AtmosphereIntensity;
            ModelKey = s.ModelKey;
            Deposits = s.Deposits ?? Array.Empty<Deposit>();
            GeneratorVersion = s.GeneratorVersion;
        }

        public bool IsAuthored { get { return Key.System.IsAuthored; } }

        public bool IsGasGiant { get { return Kind == PlanetKind.GasGiant; } }

        public bool HasResource(string resourceId)
        {
            for (int i = 0; i < Deposits.Length; i++) if (Deposits[i].ResourceId == resourceId) return true;
            return false;
        }

        public bool TryGetDeposit(string resourceId, out Deposit deposit)
        {
            for (int i = 0; i < Deposits.Length; i++)
            {
                if (Deposits[i].ResourceId == resourceId)
                {
                    deposit = Deposits[i];
                    return true;
                }
            }
            deposit = default(Deposit);
            return false;
        }

        public override string ToString() { return Name + " [" + Id + "] " + Biome + " " + Size + " T" + Tier; }
    }
}
