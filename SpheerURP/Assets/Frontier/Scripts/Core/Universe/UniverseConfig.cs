using System;
using System.Collections.Generic;

namespace Frontier.Universe
{
    /// <summary>
    /// Every random attribute of the universe has its own named hash stream. Names (not these enum values) are hashed into the
    /// stream seed, so reordering the enum changes nothing, but a name string must NEVER be edited once released.
    /// To add an attribute: append a value here and a new unique name in <see cref="StreamSalts.NameOf"/>; existing
    /// attributes keep drawing exactly the same numbers.
    /// </summary>
    public enum Attr
    {
        GalaxyExists, GalaxyCentre, GalaxyRadius, GalaxyShape, GalaxyName, GalaxyTint, GalaxyNoise,
        SectorCount, SectorPlacement,
        StarClass, StarLuminosity, StarColour, SystemName, SystemCatalog, PlanetCount, OrbitLayout,
        PlanetKind, PlanetSize, PlanetBiome, TierAnomaly, Danger, OrbitPhase, PlanetName, Weather, Rings, Moons,
        Visual, ModelKey, Deposits, HazardExtra,
        Anchor,
        Count
    }

    /// <summary>
    /// Per-attribute stream seeds of one universe: Combine(universeSeed, Fnv1a64(saltName)). A salt name can be replaced through
    /// <see cref="UniverseConfig.SaltOverrides"/> (used by tests to prove independence, and by future generator versions to re-roll a
    /// single attribute without touching any other).
    /// </summary>
    public sealed class StreamSalts
    {
        readonly ulong[] _seed = new ulong[(int)Attr.Count];

        public StreamSalts(ulong universeSeed, IDictionary<string, string> overrides)
        {
            for (int i = 0; i < _seed.Length; i++)
            {
                string name = NameOf((Attr)i);
                string replaced;
                if (overrides != null && overrides.TryGetValue(name, out replaced) && replaced != null) name = replaced;
                _seed[i] = Hash.SeedFor(universeSeed, Hash.Fnv1a64(name));
            }
        }

        /// <summary>The attribute seed (second argument of <see cref="Hash.Stream(ulong, ulong)"/>).</summary>
        public ulong SeedOf(Attr a) { return _seed[(int)a]; }

        /// <summary>64-bit hash of (universe, attribute, key).</summary>
        public ulong Stream(Attr a, ulong key) { return Hash.Stream(_seed[(int)a], key); }

        public Rng Rng(Attr a, ulong key) { return new Rng(Hash.Stream(_seed[(int)a], key)); }

        public static string NameOf(Attr a)
        {
            switch (a)
            {
                case Attr.GalaxyExists: return "galaxy.exists";
                case Attr.GalaxyCentre: return "galaxy.centre";
                case Attr.GalaxyRadius: return "galaxy.radius";
                case Attr.GalaxyShape: return "galaxy.shape";
                case Attr.GalaxyName: return "galaxy.name";
                case Attr.GalaxyTint: return "galaxy.tint";
                case Attr.GalaxyNoise: return "galaxy.noise";
                case Attr.SectorCount: return "sector.count";
                case Attr.SectorPlacement: return "sector.placement";
                case Attr.StarClass: return "star.class";
                case Attr.StarLuminosity: return "star.luminosity";
                case Attr.StarColour: return "star.colour";
                case Attr.SystemName: return "system.name";
                case Attr.SystemCatalog: return "system.catalog";
                case Attr.PlanetCount: return "system.planet-count";
                case Attr.OrbitLayout: return "system.orbit-layout";
                case Attr.PlanetKind: return "planet.kind";
                case Attr.PlanetSize: return "planet.size";
                case Attr.PlanetBiome: return "planet.biome";
                case Attr.TierAnomaly: return "planet.tier-anomaly";
                case Attr.Danger: return "planet.danger";
                case Attr.OrbitPhase: return "planet.orbit-phase";
                case Attr.PlanetName: return "planet.name";
                case Attr.Weather: return "planet.weather";
                case Attr.Rings: return "planet.rings";
                case Attr.Moons: return "planet.moons";
                case Attr.Visual: return "planet.visual";
                case Attr.ModelKey: return "planet.model";
                case Attr.Deposits: return "planet.deposits";
                case Attr.HazardExtra: return "planet.hazard-extra";
                case Attr.Anchor: return "block.anchor";
                default: throw new ArgumentOutOfRangeException("a");
            }
        }
    }

    /// <summary>
    /// All tunables of the universe generator. Build one (new UniverseConfig() or <see cref="CreateDefault"/>), adjust fields,
    /// and hand it to <see cref="Universe"/>, which takes a private copy. Anything that changes generated content (any field here)
    /// is part of the generator definition: after release, changing a default needs a new GeneratorVersion.
    /// </summary>
    public sealed class UniverseConfig
    {
        /// <summary>Generator version implemented by this code base.</summary>
        public const int CurrentGeneratorVersion = 1;

        public static UniverseConfig CreateDefault() { return new UniverseConfig(); }

        // ---- identity -------------------------------------------------------------------------------------------------
        /// <summary>Universe seed (the default spells "FRONTIER" in ASCII).</summary>
        public ulong Seed = 0x46524F4E54494552UL;
        public int GeneratorVersion = CurrentGeneratorVersion;

        // ---- layout ---------------------------------------------------------------------------------------------------
        /// <summary>Sector edge length in light-years.</summary>
        public double SectorSize = 64.0;
        /// <summary>Home system position; tiers grow with the distance from here.</summary>
        public double HomeX = 0.0, HomeY = 0.0;
        /// <summary>Coverage anchors are assigned per square block of this many sectors per side.</summary>
        public int AnchorBlockSectors = 3;

        // ---- galaxies -------------------------------------------------------------------------------------------------
        /// <summary>Edge of a macro-grid cell; each cell holds at most one random galaxy.</summary>
        public double MacroCellSize = 4096.0;
        public double GalaxyChance = 0.55;
        public double GalaxyRadiusMin = 900.0, GalaxyRadiusMax = 2600.0;
        public int ArmsMin = 2, ArmsMax = 5;
        /// <summary>Spiral tightness 1/tan(pitch): 2.4 is a loose 23 degree spiral, 4.4 a tight 13 degree one.</summary>
        public double WindingMin = 2.4, WindingMax = 4.4;
        /// <summary>Flattening 1 - minor/major; 0 is circular.</summary>
        public double EllipticityMin = 0.0, EllipticityMax = 0.5;
        /// <summary>Bulge scale radius as a fraction of the galaxy radius.</summary>
        public double BulgeFractionMin = 0.10, BulgeFractionMax = 0.20;
        public double EllipticalChance = 0.18, BarredChance = 0.30;
        public double ArmContrastMin = 0.70, ArmContrastMax = 0.90;
        public double BulgeAmplitude = 0.85, DiskAmplitude = 0.55;
        /// <summary>Exponential disk scale length as a fraction of the galaxy radius.</summary>
        public double DiskScaleFraction = 0.26;
        public double ClumpAmplitude = 0.45, ClumpScale = 240.0;
        /// <summary>Density of intergalactic space (rare deep outposts); the field never goes below this.</summary>
        public double HaloDensity = 0.03;
        public double DensityMax = 1.5;
        /// <summary>A random galaxy's radius is capped to this fraction of the distance to each neighbouring galaxy centre.</summary>
        public double GalaxyNeighbourCap = 0.55;
        /// <summary>Galaxy centres are jittered inside [min, max] of their macro cell, which guarantees a minimum spacing.</summary>
        public double GalaxyJitterMin = 0.225, GalaxyJitterMax = 0.775;

        // ---- systems --------------------------------------------------------------------------------------------------
        /// <summary>Expected systems in a sector at density 1.</summary>
        public double SystemsPerSectorMax = 2.6;
        public int MaxSystemsPerSector = 8;
        /// <summary>Minimum distance in light-years between any two systems (random or authored).</summary>
        public double MinSystemSeparation = 8.0;
        public int PlacementAttempts = 12;
        /// <summary>Star class weights per region, indexed by <see cref="StarClass"/> (O B A F G K M Neutron BlackHole).</summary>
        public double[] StarWeightsBulge = { 0.0, 0.0005, 0.004, 0.04, 0.15, 0.33, 0.47, 0.004, 0.002 };
        public double[] StarWeightsDisk = { 0.0006, 0.004, 0.025, 0.10, 0.20, 0.27, 0.40, 0.0008, 0.0003 };
        public double[] StarWeightsArm = { 0.010, 0.050, 0.12, 0.20, 0.22, 0.18, 0.22, 0.0006, 0.0003 };
        public double[] StarWeightsHalo = { 0.0, 0.0, 0.001, 0.02, 0.12, 0.35, 0.50, 0.0003, 0.0001 };
        /// <summary>Neutron star and black hole weights are multiplied by 1 + RemnantCoreBoost * coreProximity.</summary>
        public double RemnantCoreBoost = 14.0;
        /// <summary>Neutron stars and black holes only appear at or beyond this tier (keeps the start area friendly).</summary>
        public int RemnantMinTier = 3;

        // ---- planets --------------------------------------------------------------------------------------------------
        public int MinPlanetsPerSystem = 1;
        public int MaxPlanetsPerSystem = 7;
        /// <summary>tier = clamp(floor(TierScale * log2(1 + distance / TierDistanceUnit)) + anomaly, 0, TierMax).</summary>
        public double TierScale = 1.6;
        public double TierDistanceUnit = 60.0;
        public int TierMax = 40;
        /// <summary>Probability that a planet's tier is shifted by +1 or -1 (half each).</summary>
        public double TierAnomalyChance = 0.12;
        /// <summary>danger = DangerBase + DangerPerTier * tier + noise in [-DangerNoise/2, +DangerNoise/2].</summary>
        public double DangerBase = 0.6, DangerPerTier = 1.1, DangerNoise = 0.6;
        /// <summary>Building slots per <see cref="SizeClass"/> (Dwarf .. Colossal).</summary>
        public int[] BuildingSlotsBySize = { 12, 16, 24, 36, 48, 64 };
        /// <summary>Logical world size multiplier per <see cref="SizeClass"/> (buildings appear smaller on bigger worlds).</summary>
        public float[] WorldSizeBySize = { 1.00f, 1.15f, 1.40f, 1.75f, 2.10f, 2.50f };
        /// <summary>The Void hazard appears from this tier on in intergalactic space and around black holes.</summary>
        public int VoidMinTier = 8;

        // ---- content tables -------------------------------------------------------------------------------------------
        /// <summary>The calibrated model keys that exist in the game's ModelLibrary.</summary>
        public string[] ModelKeys =
        {
            "sylva", "ferrum", "glacia", "cinder", "arbor", "nyx", "aurum", "tide", "zephyr",
            "pyre", "halcyon", "mire", "obsidian", "vesper", "prism", "kaos", "tundra", "coral"
        };

        /// <summary>Model keys allowed per biome (indexed by <see cref="Biome"/>); every key must be listed in <see cref="ModelKeys"/>.</summary>
        public string[][] BiomeModelKeys = CreateDefaultBiomeModelKeys();

        /// <summary>When true the planned resource ids (biomass, copper_ore, silica, ice, titanium_ore, methane, helium3, radium, dark_matter) are generated.</summary>
        public bool EnablePlannedResources;

        /// <summary>Custom resource tables; null uses <see cref="ResourceTable.CreateDefault"/> with <see cref="EnablePlannedResources"/>.</summary>
        public ResourceTable Resources;

        // ---- stream salts (test hook / future versions) ---------------------------------------------------------------
        /// <summary>Maps an attribute stream name (see <see cref="StreamSalts.NameOf"/>) to a replacement salt, re-rolling only that attribute.</summary>
        public Dictionary<string, string> SaltOverrides;

        // ---- caches ---------------------------------------------------------------------------------------------------
        public int SectorCacheCapacity = 4096;
        public int BlockCacheCapacity = 512;
        public int GalaxyCacheCapacity = 4096;

        public static string[][] CreateDefaultBiomeModelKeys()
        {
            var m = new string[12][];
            m[(int)Biome.Lush] = new[] { "sylva", "arbor", "coral" };
            m[(int)Biome.Mineral] = new[] { "ferrum", "aurum" };
            m[(int)Biome.Glacial] = new[] { "glacia", "halcyon", "tundra" };
            m[(int)Biome.Volcanic] = new[] { "cinder", "pyre" };
            m[(int)Biome.Fungal] = new[] { "mire" };
            m[(int)Biome.Crystal] = new[] { "prism" };
            m[(int)Biome.Obsidian] = new[] { "obsidian", "nyx", "vesper" };
            m[(int)Biome.Shattered] = new[] { "kaos" };
            m[(int)Biome.Desert] = new[] { "aurum", "ferrum" };
            m[(int)Biome.Oceanic] = new[] { "tide", "coral" };
            m[(int)Biome.GasGiant] = new[] { "zephyr" };
            m[(int)Biome.Barren] = new[] { "ferrum", "tundra", "nyx" };
            return m;
        }

        /// <summary>The tier for a distance from home (no anomaly).</summary>
        public int TierFromDistance(double distanceLy)
        {
            if (!(distanceLy > 0.0)) return 0;
            double t = TierScale * DetMath.Log2(1.0 + distanceLy / TierDistanceUnit);
            if (!(t < TierMax + 1)) return TierMax;
            return DetMath.Clamp((int)Math.Floor(t), 0, TierMax);
        }

        public UniverseConfig Clone()
        {
            var c = (UniverseConfig)MemberwiseClone();
            c.StarWeightsBulge = (double[])StarWeightsBulge.Clone();
            c.StarWeightsDisk = (double[])StarWeightsDisk.Clone();
            c.StarWeightsArm = (double[])StarWeightsArm.Clone();
            c.StarWeightsHalo = (double[])StarWeightsHalo.Clone();
            c.BuildingSlotsBySize = (int[])BuildingSlotsBySize.Clone();
            c.WorldSizeBySize = (float[])WorldSizeBySize.Clone();
            c.ModelKeys = (string[])ModelKeys.Clone();
            c.BiomeModelKeys = new string[BiomeModelKeys.Length][];
            for (int i = 0; i < BiomeModelKeys.Length; i++) c.BiomeModelKeys[i] = BiomeModelKeys[i] == null ? null : (string[])BiomeModelKeys[i].Clone();
            c.Resources = Resources == null ? null : Resources.Clone();
            c.SaltOverrides = SaltOverrides == null ? null : new Dictionary<string, string>(SaltOverrides);
            return c;
        }

        /// <summary>Throws ArgumentException when a value would break an invariant the generator relies on.</summary>
        public void Validate()
        {
            Require(GeneratorVersion == CurrentGeneratorVersion, "GeneratorVersion " + GeneratorVersion + " is not implemented (this build implements " + CurrentGeneratorVersion + ")");
            Require(SectorSize >= 8.0 && SectorSize <= 4096.0, "SectorSize");
            Require(AnchorBlockSectors >= 1 && AnchorBlockSectors <= 16, "AnchorBlockSectors");
            Require(MacroCellSize >= 2.0 * GalaxyRadiusMax * 0.5 && MacroCellSize > 0.0, "MacroCellSize");
            Require(GalaxyChance >= 0.0 && GalaxyChance <= 1.0, "GalaxyChance");
            Require(GalaxyRadiusMin > 0.0 && GalaxyRadiusMax >= GalaxyRadiusMin, "GalaxyRadius range");
            Require(ArmsMin >= 1 && ArmsMax >= ArmsMin && ArmsMax <= 8, "Arms range");
            Require(EllipticityMin >= 0.0 && EllipticityMax >= EllipticityMin && EllipticityMax < 0.9, "Ellipticity range");
            Require(GalaxyJitterMin >= 0.0 && GalaxyJitterMax <= 1.0 && GalaxyJitterMax >= GalaxyJitterMin, "GalaxyJitter range");
            Require(HaloDensity >= 0.0 && DensityMax > HaloDensity, "HaloDensity/DensityMax");
            Require(SystemsPerSectorMax > 0.0 && MaxSystemsPerSector >= 1 && MaxSystemsPerSector < SystemKey.AuthoredBase, "systems per sector");
            Require(MinSystemSeparation >= 0.0 && MinSystemSeparation < SectorSize * 0.5, "MinSystemSeparation");
            Require(PlacementAttempts >= 1 && PlacementAttempts <= 255, "PlacementAttempts");
            Require(MinPlanetsPerSystem >= 1 && MaxPlanetsPerSystem >= MinPlanetsPerSystem && MaxPlanetsPerSystem <= PlanetKey.MaxIndex + 1, "planets per system");
            Require(TierScale > 0.0 && TierDistanceUnit > 0.0 && TierMax >= 0 && TierMax <= 100, "tier parameters");
            Require(BuildingSlotsBySize != null && BuildingSlotsBySize.Length == 6, "BuildingSlotsBySize must have 6 entries");
            Require(WorldSizeBySize != null && WorldSizeBySize.Length == 6, "WorldSizeBySize must have 6 entries");
            for (int i = 0; i < 6; i++) Require(BuildingSlotsBySize[i] >= 12, "BuildingSlotsBySize must be >= 12");
            Require(StarWeightsBulge != null && StarWeightsBulge.Length == 9, "StarWeightsBulge must have 9 entries");
            Require(StarWeightsDisk != null && StarWeightsDisk.Length == 9, "StarWeightsDisk must have 9 entries");
            Require(StarWeightsArm != null && StarWeightsArm.Length == 9, "StarWeightsArm must have 9 entries");
            Require(StarWeightsHalo != null && StarWeightsHalo.Length == 9, "StarWeightsHalo must have 9 entries");
            Require(ModelKeys != null && ModelKeys.Length > 0, "ModelKeys");
            Require(BiomeModelKeys != null && BiomeModelKeys.Length >= 12, "BiomeModelKeys must cover all 12 biomes");
            for (int b = 0; b < 12; b++)
            {
                Require(BiomeModelKeys[b] != null && BiomeModelKeys[b].Length > 0, "BiomeModelKeys[" + (Biome)b + "] is empty");
                for (int k = 0; k < BiomeModelKeys[b].Length; k++)
                    Require(Array.IndexOf(ModelKeys, BiomeModelKeys[b][k]) >= 0, "BiomeModelKeys[" + (Biome)b + "] uses '" + BiomeModelKeys[b][k] + "' which is not in ModelKeys");
            }
            Require(SectorCacheCapacity >= 16 && BlockCacheCapacity >= 4 && GalaxyCacheCapacity >= 16, "cache capacities");
        }

        static void Require(bool ok, string what)
        {
            if (!ok) throw new ArgumentException("Invalid UniverseConfig: " + what);
        }
    }
}
