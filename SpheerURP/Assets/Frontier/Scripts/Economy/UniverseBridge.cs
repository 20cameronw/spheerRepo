using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>Builds the generator's resource tables and the authored starter system from the economy catalog.</summary>
    public static class UniverseTables
    {
        /// <summary>The biome-inherent hazards (the generator adds star, temperature and random extras on top).</summary>
        public static Hazards BiomeHazards(Biome biome, int tier)
        {
            switch (biome)
            {
                case Biome.Volcanic:
                case Biome.Desert: return Hazards.Heat;
                case Biome.Glacial: return Hazards.Cold;
                case Biome.Fungal:
                case Biome.GasGiant: return Hazards.Toxic;
                case Biome.Obsidian: return Hazards.Radiation;
                case Biome.Shattered: return tier >= 6 ? (Hazards.Radiation | Hazards.Void) : Hazards.Radiation;
                case Biome.Crystal: return tier >= 6 ? Hazards.Radiation : Hazards.None;
                default: return Hazards.None;
            }
        }

        /// <summary>A <see cref="ResourceTable"/> with every raw resource of the catalog, per-biome profiles and the belt profile. Scrap is always present.</summary>
        public static ResourceTable BuildResourceTable(EconomyCatalog cat)
        {
            var table = new ResourceTable();
            for (int i = 0; i < cat.resources.Length; i++)
            {
                ResourceDef r = cat.resources[i];
                if (r.category != ResourceCategory.Raw) continue;
                var def = new Frontier.Universe.ResourceDef(r.id, r.harvestMethods[0], r.tier, r.baseYield, r.yieldPerTier, r.difficultyOffset);
                // Anchors (the coverage guarantee) may only inject a deposit where it makes sense: belts are re-enabled by Freeze for the
                // resources of the belt profile; trees, plants and gases never fall back onto an arbitrary solid world.
                def.AnchorOnBelts = false;
                if (r.id == "wood" || r.id == "biomass" || r.id == "methane" || r.id == "helium3") def.AnchorFallbackSolid = false;
                table.AddDef(def);
            }
            table.AlwaysPresent.Add("scrap");
            for (int i = 0; i < cat.habitats.Length; i++)
            {
                HabitatDef h = cat.habitats[i];
                ResourceProfile profile = h.belt ? table.BeltProfile : table.ProfileFor(h.biome);
                profile.Add(h.resourceId, h.method, h.weight, h.minTier);
            }
            return table;
        }

        public static UniverseConfig CreateConfig(EconomyCatalog cat)
        {
            var cfg = new UniverseConfig();
            cfg.Resources = BuildResourceTable(cat);
            return cfg;
        }

        public static UniverseConfig CreateConfig(EconomyCatalog cat, ulong seed)
        {
            UniverseConfig cfg = CreateConfig(cat);
            cfg.Seed = seed;
            return cfg;
        }
    }

    /// <summary>
    /// The authored home system (Sylva and four neighbours), written with the new resource ids. It is a recommendation for the Core World
    /// authoring pass and the stand-in the simulator uses for Landfall and Foothold.
    /// </summary>
    public static class StarterSystem
    {
        public const string HomePlanetId = "sylva";

        static Deposit D(EconomyCatalog cat, string id, HarvestMethod m, double richness, int tier, double difficulty)
        {
            ResourceDef r = cat.Resource(id);
            int baseYield = (int)((double)r.baseYield + (double)r.yieldPerTier * tier + 0.5);
            return new Deposit(id, m, (float)richness, baseYield < 1 ? 1 : baseYield, (float)difficulty);
        }

        static PlanetDescriptor P(SystemKey sys, int index, string authoredId, string name, Biome biome, SizeClass size, int slots, float worldSize, int tier, double orbit,
            Hazards hazards, Weather weather, Rgb primary, Rgb secondary, Rgb rim, string model, Deposit[] deposits)
        {
            var s = new PlanetSpec();
            s.Key = new PlanetKey(sys, index);
            s.AuthoredId = authoredId;
            s.Name = name;
            s.Designation = "SYL-" + index;
            s.Kind = PlanetKind.Rocky;
            s.Biome = biome;
            s.Size = size;
            s.WorldSize = worldSize;
            s.BuildingSlots = slots;
            s.Tier = tier;
            s.Danger = (float)(0.6 + 1.1 * tier);
            s.Hazards = hazards;
            s.Weather = weather;
            s.OrbitRadius = (float)orbit;
            s.OrbitPhase = (float)(index * 1.1);
            s.Temperature = 290f;
            s.Primary = primary;
            s.Secondary = secondary;
            s.Rim = rim;
            s.ModelKey = model;
            s.Deposits = deposits;
            s.GeneratorVersion = 1;
            return new PlanetDescriptor(s);
        }

        /// <summary>Landmark galaxies plus the home system at the origin.</summary>
        public static AuthoredContent Create(EconomyCatalog cat)
        {
            var key = new SystemKey(SectorCoord.FromLy(0.0, 0.0, 64.0), SystemKey.AuthoredBase);
            var green = new Rgb(0.3f, 0.7f, 0.3f);
            var dark = new Rgb(0.05f, 0.15f, 0.08f);
            var rim = new Rgb(0.4f, 2f, 1f);
            var planets = new[]
            {
                P(key, 0, "sylva", "Sylva", Biome.Lush, SizeClass.Medium, 24, 1.4f, 0, 1.00, Hazards.None, Weather.Clear, green, dark, rim, "sylva", new[]
                {
                    D(cat, "scrap", HarvestMethod.Salvage, 1.0, 0, 0.12), D(cat, "wood", HarvestMethod.Chop, 1.0, 0, 0.18), D(cat, "stone", HarvestMethod.Mine, 1.0, 0, 0.15)
                }),
                P(key, 1, "veridian", "Veridian", Biome.Lush, SizeClass.Dwarf, 12, 1.0f, 1, 1.05, Hazards.None, Weather.Clear, green, dark, rim, "sylva", new[]
                {
                    D(cat, "scrap", HarvestMethod.Salvage, 1.0, 1, 0.20), D(cat, "wood", HarvestMethod.Chop, 1.5, 1, 0.26), D(cat, "stone", HarvestMethod.Mine, 1.0, 1, 0.25), D(cat, "biomass", HarvestMethod.Harvest, 1.0, 1, 0.28)
                }),
                P(key, 2, "ferrum", "Ferrum", Biome.Mineral, SizeClass.Medium, 24, 1.4f, 1, 1.60, Hazards.None, Weather.Dust, new Rgb(0.7f, 0.4f, 0.2f), new Rgb(0.2f, 0.1f, 0.05f), new Rgb(2f, 0.9f, 0.3f), "ferrum", new[]
                {
                    D(cat, "scrap", HarvestMethod.Salvage, 1.0, 1, 0.20), D(cat, "stone", HarvestMethod.Mine, 1.4, 1, 0.25), D(cat, "iron", HarvestMethod.Mine, 1.0, 1, 0.30),
                    D(cat, "copper_ore", HarvestMethod.Mine, 0.8, 1, 0.30), D(cat, "wood", HarvestMethod.Chop, 0.6, 1, 0.26)
                }),
                P(key, 3, "calcite", "Calcite", Biome.Barren, SizeClass.Small, 16, 1.15f, 2, 2.40, Hazards.None, Weather.Dust, new Rgb(0.8f, 0.78f, 0.7f), new Rgb(0.25f, 0.22f, 0.2f), new Rgb(1.8f, 1.6f, 1.2f), "ferrum", new[]
                {
                    D(cat, "scrap", HarvestMethod.Salvage, 1.0, 2, 0.28), D(cat, "stone", HarvestMethod.Mine, 1.6, 2, 0.33), D(cat, "iron", HarvestMethod.Mine, 1.0, 2, 0.38),
                    D(cat, "silica", HarvestMethod.Mine, 1.0, 2, 0.35), D(cat, "ice", HarvestMethod.Excavate, 0.7, 2, 0.40)
                }),
                P(key, 4, "glacia", "Glacia", Biome.Glacial, SizeClass.Medium, 24, 1.4f, 2, 3.80, Hazards.Cold, Weather.Snow, new Rgb(0.6f, 0.75f, 0.8f), new Rgb(0.05f, 0.12f, 0.3f), new Rgb(0.5f, 1.8f, 2.4f), "glacia", new[]
                {
                    D(cat, "scrap", HarvestMethod.Salvage, 1.0, 2, 0.28), D(cat, "ice", HarvestMethod.Excavate, 1.5, 2, 0.40), D(cat, "crystal", HarvestMethod.Mine, 1.0, 2, 0.45),
                    D(cat, "wood", HarvestMethod.Chop, 0.4, 2, 0.34), D(cat, "stone", HarvestMethod.Mine, 0.9, 2, 0.33)
                }),
            };
            var sys = new SystemSpec();
            sys.Key = key;
            sys.Name = "Sylva";
            sys.Designation = "HOME-0";
            sys.X = 0.0;
            sys.Y = 0.0;
            sys.StarClass = StarClass.G;
            sys.Color = new Rgb(1f, 0.95f, 0.7f);
            sys.Luminosity = 1f;
            sys.SurfaceTemperature = 5778f;
            sys.HabitableInner = 0.95f;
            sys.HabitableOuter = 1.5f;
            sys.GalaxyId = SystemSpec.AutoGalaxy;
            sys.Tier = 0;
            var content = Landmarks.CreateDefault();
            content.AddSystem(sys, planets);
            return content;
        }

        /// <summary>A universe made from the catalog's resource table and the starter system.</summary>
        public static Frontier.Universe.Universe CreateUniverse(EconomyCatalog cat, ulong seed)
        {
            return new Frontier.Universe.Universe(UniverseTables.CreateConfig(cat, seed), Create(cat));
        }
    }
}
