using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public enum PlanetBiome
    {
        Lush,
        Mineral,
        Glacial,
        Volcanic,
        Fungal,
        Crystal,
        Obsidian,
        Shattered
    }

    /// <summary>
    /// Procedural planet generation engine for Frontier.
    /// Creates balanced, aesthetically coherent ScriptableObject worlds with calibrated
    /// model references, tiered capacities, visual atmospheres, and minigame spots.
    /// </summary>
    public static class PlanetGenerator
    {
        static readonly string[] CalibratedModels =
        {
            "sylva", "ferrum", "glacia", "cinder", "arbor", "nyx", "aurum",
            "tide", "zephyr", "pyre", "halcyon", "mire", "obsidian", "vesper",
            "prism", "kaos", "tundra", "coral"
        };

        static readonly string[] Prefixes =
        {
            "Kepler", "Gliese", "Aethel", "Vespera", "Zeta", "Titan", "Nova", "Onyx",
            "Helios", "Polaris", "Elysium", "Apex", "Vortex", "Siren", "Calypso",
            "Chronos", "Nebula", "Draco", "Cygnus", "Lyra", "Orion", "Taurus", "Caelum"
        };

        static readonly string[] Suffixes =
        {
            "Prime", "Major", "Minor", "Secundus", "Borealis", "Australis", "IX",
            "IV", "VII", "Station", "Sanctum", "Expanse", "Reach", "Deep", "Rift", "Void"
        };

        public static PlanetSO Create(string id, string name, string desc, int tier, string systemId,
            PlanetBiome biome, int seed, GameDatabase db = null, string preferredModel = null)
        {
            var p = ScriptableObject.CreateInstance<PlanetSO>();
            p.name = id;
            p.id = id;
            p.displayName = name;
            p.description = desc;
            p.tier = tier;
            p.systemId = systemId;

            // Standardized size and slot formulas
            p.worldSize = 1f + tier * 0.16f;
            p.buildingSlots = 12 + tier * 4;
            p.danger = 0.6f + tier * 1.1f;
            p.surfaceRadius = PlanetSO.StandardSurfaceRadius;
            p.modelScale = 1f;
            p.atmosphereScale = 5f;

            var rng = new System.Random(seed);
            ConfigureBiome(p, biome, rng, preferredModel);
            ConfigureSpots(p, biome, tier, db);
            return p;
        }

        static void ConfigureBiome(PlanetSO p, PlanetBiome biome, System.Random rng, string preferredModel)
        {
            switch (biome)
            {
                case PlanetBiome.Lush:
                    p.primary = new Color(0.20f + (float)rng.NextDouble() * 0.15f, 0.55f + (float)rng.NextDouble() * 0.35f, 0.22f);
                    p.secondary = new Color(0.06f, 0.18f + (float)rng.NextDouble() * 0.10f, 0.10f);
                    p.rim = new Color(0.4f, 2.0f, 1.0f);
                    p.weather = rng.NextDouble() < 0.6 ? WeatherKind.Clear : WeatherKind.Dust;
                    p.style = rng.NextDouble() < 0.5 ? 0 : 1;
                    p.surfaceBands = 3f + (float)rng.NextDouble() * 4f;
                    p.glowIntensity = 0.35f; p.atmosphereIntensity = 0.35f;
                    p.modelKey = !string.IsNullOrEmpty(preferredModel) ? preferredModel : "sylva";
                    break;

                case PlanetBiome.Mineral:
                    p.primary = new Color(0.65f + (float)rng.NextDouble() * 0.25f, 0.35f + (float)rng.NextDouble() * 0.15f, 0.18f);
                    p.secondary = new Color(0.22f, 0.12f, 0.08f);
                    p.rim = new Color(2.0f, 1.1f, 0.4f);
                    p.weather = WeatherKind.Dust;
                    p.style = rng.NextDouble() < 0.5 ? 1 : 2;
                    p.surfaceBands = 6f + (float)rng.NextDouble() * 5f;
                    p.glowIntensity = 0.40f; p.atmosphereIntensity = 0.20f;
                    p.modelKey = !string.IsNullOrEmpty(preferredModel) ? preferredModel : "ferrum";
                    break;

                case PlanetBiome.Glacial:
                    p.primary = new Color(0.55f + (float)rng.NextDouble() * 0.20f, 0.80f + (float)rng.NextDouble() * 0.18f, 1.0f);
                    p.secondary = new Color(0.08f, 0.18f, 0.35f);
                    p.rim = new Color(0.6f, 1.8f, 2.6f);
                    p.weather = WeatherKind.Snow;
                    p.style = rng.NextDouble() < 0.5 ? 0 : 4;
                    p.surfaceBands = 2f + (float)rng.NextDouble() * 4f;
                    p.glowIntensity = 0.65f; p.atmosphereIntensity = 0.45f;
                    p.modelKey = !string.IsNullOrEmpty(preferredModel) ? preferredModel : "glacia";
                    break;

                case PlanetBiome.Volcanic:
                    p.primary = new Color(0.95f, 0.22f + (float)rng.NextDouble() * 0.15f, 0.08f);
                    p.secondary = new Color(0.12f, 0.02f, 0.02f);
                    p.rim = new Color(2.6f, 0.6f, 0.2f);
                    p.weather = WeatherKind.Embers;
                    p.style = 3;
                    p.surfaceBands = 10f + (float)rng.NextDouble() * 5f;
                    p.glowIntensity = 0.70f; p.atmosphereIntensity = 0.35f;
                    p.modelKey = !string.IsNullOrEmpty(preferredModel) ? preferredModel : "cinder";
                    break;

                case PlanetBiome.Fungal:
                    p.primary = new Color(0.50f + (float)rng.NextDouble() * 0.20f, 0.70f + (float)rng.NextDouble() * 0.20f, 0.15f);
                    p.secondary = new Color(0.12f, 0.15f, 0.04f);
                    p.rim = new Color(1.8f, 2.4f, 0.5f);
                    p.weather = WeatherKind.Spores;
                    p.style = 2;
                    p.surfaceBands = 5f + (float)rng.NextDouble() * 4f;
                    p.glowIntensity = 0.55f; p.atmosphereIntensity = 0.50f;
                    p.modelKey = !string.IsNullOrEmpty(preferredModel) ? preferredModel : "mire";
                    break;

                case PlanetBiome.Crystal:
                    p.primary = new Color(0.40f + (float)rng.NextDouble() * 0.30f, 0.90f, 1.0f);
                    p.secondary = new Color(0.40f, 0.10f, 0.60f);
                    p.rim = new Color(2.2f, 1.8f, 2.8f);
                    p.weather = rng.NextDouble() < 0.5 ? WeatherKind.Clear : WeatherKind.Snow;
                    p.style = 4;
                    p.surfaceBands = 4f + (float)rng.NextDouble() * 4f;
                    p.glowIntensity = 1.10f; p.atmosphereIntensity = 0.90f;
                    p.hasRings = rng.NextDouble() < 0.4;
                    p.modelKey = !string.IsNullOrEmpty(preferredModel) ? preferredModel : "prism";
                    break;

                case PlanetBiome.Obsidian:
                    p.primary = new Color(0.18f + (float)rng.NextDouble() * 0.10f, 0.10f, 0.28f);
                    p.secondary = new Color(0.03f, 0.01f, 0.06f);
                    p.rim = new Color(1.8f, 0.6f, 2.6f);
                    p.weather = WeatherKind.Storm;
                    p.style = rng.NextDouble() < 0.5 ? 2 : 4;
                    p.surfaceBands = 10f + (float)rng.NextDouble() * 4f;
                    p.glowIntensity = 0.65f; p.atmosphereIntensity = 0.30f;
                    p.hasRings = rng.NextDouble() < 0.5;
                    p.modelKey = !string.IsNullOrEmpty(preferredModel) ? preferredModel : "obsidian";
                    break;

                default: // Shattered / Deep Void
                    p.primary = new Color(0.85f + (float)rng.NextDouble() * 0.15f, 0.85f, 1.0f);
                    p.secondary = new Color(0.04f, 0.04f, 0.08f);
                    p.rim = new Color(2.8f, 2.8f, 3.4f);
                    p.weather = WeatherKind.Storm;
                    p.style = 1;
                    p.surfaceBands = 8f;
                    p.glowIntensity = 1.40f; p.atmosphereIntensity = 1.10f;
                    p.hasRings = true;
                    p.modelKey = !string.IsNullOrEmpty(preferredModel) ? preferredModel : "kaos";
                    break;
            }
        }

        static void ConfigureSpots(PlanetSO p, PlanetBiome biome, int tier, GameDatabase db)
        {
            p.spots = new List<GatherSpot>();
            ResourceSO wood = null, stone = null, iron = null, crystal = null, fuel = null, core = null, scrap = null;
            if (db != null)
            {
                wood = db.FindResource("wood"); stone = db.FindResource("stone"); iron = db.FindResource("iron");
                crystal = db.FindResource("crystal"); fuel = db.FindResource("fuel"); core = db.FindResource("core");
                scrap = db.FindResource("scrap");
            }

            float diff = Mathf.Clamp01(0.20f + tier * 0.09f);
            switch (biome)
            {
                case PlanetBiome.Lush:
                    if (wood != null) p.spots.Add(new GatherSpot { label = "Ancient Canopy", game = MinigameKind.Chop, resource = wood, baseYield = 14 + tier * 6, difficulty = diff });
                    if (stone != null) p.spots.Add(new GatherSpot { label = "River Bed", game = MinigameKind.Mine, resource = stone, baseYield = 8 + tier * 3, difficulty = diff });
                    if (fuel != null) p.spots.Add(new GatherSpot { label = "Amber Bore", game = MinigameKind.Drill, resource = fuel, baseYield = 6 + tier * 2, difficulty = diff + 0.05f });
                    break;

                case PlanetBiome.Mineral:
                    if (iron != null) p.spots.Add(new GatherSpot { label = "Hematite Ridge", game = MinigameKind.Mine, resource = iron, baseYield = 8 + tier * 4, difficulty = diff });
                    if (stone != null) p.spots.Add(new GatherSpot { label = "Basalt Quarry", game = MinigameKind.Mine, resource = stone, baseYield = 10 + tier * 4, difficulty = diff - 0.05f });
                    if (fuel != null) p.spots.Add(new GatherSpot { label = "Thermal Pocket", game = MinigameKind.Drill, resource = fuel, baseYield = 6 + tier * 2, difficulty = diff + 0.05f });
                    break;

                case PlanetBiome.Glacial:
                    if (fuel != null) p.spots.Add(new GatherSpot { label = "Sub-Glacial Vent", game = MinigameKind.Drill, resource = fuel, baseYield = 10 + tier * 3, difficulty = diff });
                    if (crystal != null) p.spots.Add(new GatherSpot { label = "Frost Geodes", game = MinigameKind.Mine, resource = crystal, baseYield = 6 + tier * 2, difficulty = diff + 0.05f });
                    if (wood != null && tier <= 3) p.spots.Add(new GatherSpot { label = "Frozen Timber", game = MinigameKind.Chop, resource = wood, baseYield = 12 + tier * 4, difficulty = diff });
                    break;

                case PlanetBiome.Volcanic:
                    if (iron != null) p.spots.Add(new GatherSpot { label = "Molten Lode", game = MinigameKind.Mine, resource = iron, baseYield = 14 + tier * 4, difficulty = diff });
                    if (fuel != null) p.spots.Add(new GatherSpot { label = "Magma Vent", game = MinigameKind.Drill, resource = fuel, baseYield = 10 + tier * 3, difficulty = diff + 0.05f });
                    if (core != null && tier >= 4) p.spots.Add(new GatherSpot { label = "Caldera Nexus", game = MinigameKind.Drill, resource = core, baseYield = 2 + tier, difficulty = diff + 0.1f });
                    break;

                case PlanetBiome.Fungal:
                    if (wood != null) p.spots.Add(new GatherSpot { label = "Titan Fungi", game = MinigameKind.Chop, resource = wood, baseYield = 24 + tier * 7, difficulty = diff });
                    if (fuel != null) p.spots.Add(new GatherSpot { label = "Methane Bog", game = MinigameKind.Drill, resource = fuel, baseYield = 8 + tier * 2, difficulty = diff + 0.05f });
                    if (core != null) p.spots.Add(new GatherSpot { label = "Spore Hive", game = MinigameKind.Drill, resource = core, baseYield = 2 + tier, difficulty = diff + 0.1f });
                    break;

                case PlanetBiome.Crystal:
                    if (crystal != null) p.spots.Add(new GatherSpot { label = "Prismatic Spire", game = MinigameKind.Mine, resource = crystal, baseYield = 10 + tier * 3, difficulty = diff + 0.05f });
                    if (fuel != null) p.spots.Add(new GatherSpot { label = "Harmonic Bore", game = MinigameKind.Drill, resource = fuel, baseYield = 8 + tier * 2, difficulty = diff });
                    if (iron != null) p.spots.Add(new GatherSpot { label = "Quartz Seam", game = MinigameKind.Mine, resource = iron, baseYield = 12 + tier * 3, difficulty = diff });
                    break;

                case PlanetBiome.Obsidian:
                    if (iron != null) p.spots.Add(new GatherSpot { label = "Black Glass Shelf", game = MinigameKind.Mine, resource = iron, baseYield = 16 + tier * 4, difficulty = diff });
                    if (crystal != null) p.spots.Add(new GatherSpot { label = "Violet Vein", game = MinigameKind.Mine, resource = crystal, baseYield = 8 + tier * 2, difficulty = diff + 0.05f });
                    if (core != null) p.spots.Add(new GatherSpot { label = "Void Rift", game = MinigameKind.Drill, resource = core, baseYield = 3 + tier, difficulty = diff + 0.1f });
                    break;

                default: // Shattered / Void
                    if (crystal != null) p.spots.Add(new GatherSpot { label = "Fracture Core", game = MinigameKind.Mine, resource = crystal, baseYield = 16 + tier * 4, difficulty = diff + 0.05f });
                    if (core != null) p.spots.Add(new GatherSpot { label = "Null Bore", game = MinigameKind.Drill, resource = core, baseYield = 5 + tier * 2, difficulty = diff + 0.1f });
                    if (fuel != null) p.spots.Add(new GatherSpot { label = "Singularity Well", game = MinigameKind.Drill, resource = fuel, baseYield = 12 + tier * 3, difficulty = diff });
                    break;
            }
        }

        public static string GenerateName(System.Random rng)
        {
            string p = Prefixes[rng.Next(Prefixes.Length)];
            string s = Suffixes[rng.Next(Suffixes.Length)];
            return p + " " + s;
        }

        /// <summary>
        /// Generates the expanded roster of 12 deep space planets across all 6 solar systems.
        /// </summary>
        public static List<PlanetSO> GenerateDeepSpaceRoster(GameDatabase db)
        {
            var list = new List<PlanetSO>
            {
                // Sylva System additions
                Create("veridian", "Veridian", "Lush emerald moon dense with ancient mossy canopy.", 1, "sylva", PlanetBiome.Lush, 101, db, "sylva"),
                Create("calcite", "Calcite", "Chalk-white world with deep stone quarries and iron deposits.", 2, "sylva", PlanetBiome.Mineral, 102, db, "ferrum"),

                // Emberfall additions
                Create("sulfur", "Sulfur Basin", "Yellow brimstone plains erupting with pressurized vapor.", 3, "ember", PlanetBiome.Volcanic, 201, db, "cinder"),
                Create("magma", "Magma", "Churning rivers of liquid rock and rich titanium deposits.", 4, "ember", PlanetBiome.Volcanic, 202, db, "pyre"),

                // Verdance additions
                Create("lotus", "Lotus", "Oceanic world of giant bioluminescent floating flora.", 3, "verdance", PlanetBiome.Lush, 301, db, "coral"),
                Create("spore", "Spore Prime", "Subterranean fungal hive where massive caps pierce the crust.", 4, "verdance", PlanetBiome.Fungal, 302, db, "mire"),

                // Umbra additions
                Create("eclipse", "Eclipse", "A dark world locked in permanent transit. Violet lightning storms.", 5, "umbra", PlanetBiome.Obsidian, 401, db, "obsidian"),
                Create("phantom", "Phantom Rift", "Eerie gravitational void world threaded with alien wreckage.", 6, "umbra", PlanetBiome.Obsidian, 402, db, "nyx"),

                // Frostline additions
                Create("borealis", "Borealis", "Atmosphere glows with shifting auroras above frozen seas.", 3, "frost", PlanetBiome.Glacial, 501, db, "halcyon"),
                Create("cryo", "Cryo Trench", "Sub-zero glacial rift where geothermal vents spew concentrated fuel.", 5, "frost", PlanetBiome.Glacial, 502, db, "glacia"),

                // The Fracture additions
                Create("aether", "Aether", "Floating crystalline fragments trapped in an ion field.", 7, "edge", PlanetBiome.Crystal, 601, db, "prism"),
                Create("genesis", "Genesis", "A primordial anomaly pulsating with raw energy. The deepest world.", 8, "edge", PlanetBiome.Shattered, 602, db, "kaos"),
            };
            return list;
        }
    }
}
