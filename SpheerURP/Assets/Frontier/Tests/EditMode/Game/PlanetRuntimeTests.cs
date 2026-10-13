using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Frontier.Universe;
using TestLog = Frontier.Universe.Tests.TestLog;

namespace Frontier.Tests
{
    /// <summary>The runtime planet type: authored worlds mirror their assets, generated worlds get valid fields, seeds are stable.</summary>
    [TestFixture]
    public class PlanetRuntimeTests
    {
        GameDatabase db;
        UniverseService service;

        [OneTimeSetUp]
        public void Build()
        {
            db = GameDatabase.Load();
            service = new UniverseService(db);
        }

        // The first draw of System.Random seeded with id.GetHashCode() (times 10) as the legacy planet view saw it, recorded from the old code.
        static readonly string[] OldIds =
        {
            "sylva", "ferrum", "glacia", "cinder", "arbor", "nyx", "aurum", "tide", "zephyr", "pyre", "halcyon", "mire", "obsidian", "vesper", "prism",
            "kaos", "tundra", "coral", "veridian", "calcite", "sulfur", "magma", "lotus", "spore", "eclipse", "phantom", "borealis", "cryo", "aether", "genesis"
        };

        static readonly float[] OldFirstDrawTimesTen =
        {
            0.8434108f, 1.68554008f, 6.59303427f, 1.25893068f, 1.2374599f, 8.444533f, 7.77242565f, 9.39334f, 4.98086f, 0.5601893f, 1.07128286f, 3.50477886f,
            7.324836f, 7.734118f, 7.4161377f, 7.704346f, 7.58646965f, 2.10954881f, 8.71948f, 4.688042f, 3.75401235f, 1.93057346f, 4.58469343f, 8.229631f,
            8.453297f, 9.415876f, 5.740824f, 3.24586582f, 9.40448952f, 8.590513f
        };

        [Test]
        public void AuthoredWorlds_KeepTheExactVisualSeedsOfTheOldPlanetView()
        {
            Assert.AreEqual(30, OldIds.Length);
            for (int i = 0; i < OldIds.Length; i++)
            {
                var p = service.Get(OldIds[i]);
                Assert.IsNotNull(p, OldIds[i]);
                var rng = new System.Random(unchecked((int)p.Seed));
                Assert.AreEqual(OldFirstDrawTimesTen[i], (float)rng.NextDouble() * 10f, OldIds[i] + ": the surface pattern of an authored world must not change");
            }
        }

        [Test]
        public void AuthoredPlanetData_MirrorsItsAsset_FieldForField()
        {
            foreach (var so in db.planets)
            {
                var p = service.Get(so.id);
                Assert.IsTrue(p.IsAuthored);
                Assert.AreSame(so, p.Authored);
                Assert.AreEqual(so.id, p.AuthoredId);
                Assert.AreEqual(so.displayName, p.Name);
                Assert.AreEqual(so.description, p.Description);
                Assert.AreEqual(so.tier, p.Tier);
                Assert.AreEqual(so.danger, p.Danger);
                Assert.AreEqual(so.primary, p.Primary);
                Assert.AreEqual(so.secondary, p.Secondary);
                Assert.AreEqual(so.rim, p.Rim);
                Assert.AreEqual(so.weather, p.Weather);
                Assert.AreEqual(so.mood, p.Mood);
                Assert.AreEqual(so.hasRings, p.HasRings);
                Assert.AreEqual(so.style, p.Style);
                Assert.AreEqual(so.glowIntensity, p.GlowIntensity);
                Assert.AreEqual(so.atmosphereIntensity, p.AtmosphereIntensity);
                Assert.AreEqual(so.surfaceBands, p.SurfaceBands);
                Assert.AreEqual(so.ModelKey, p.ModelKey);
                Assert.AreEqual(so.modelScale, p.ModelScale);
                Assert.AreEqual(so.surfaceRadius, p.SurfaceRadius);
                Assert.AreEqual(so.atmosphereScale, p.AtmosphereScale);
                Assert.AreEqual(so.worldSize, p.WorldSize);
                Assert.AreEqual(so.BuildingSlots, p.BuildingSlots);
                Assert.AreEqual(so.StructureScale, p.StructureScale);
                Assert.AreEqual(so.tier / 2, p.MoonCount);
            }
        }

        // The original GameState.ResourcesFor / SpotsFor, as the oracle for the new PlanetData behaviour.
        List<ResourceSO> OldResourcesFor(PlanetSO p)
        {
            var list = new List<ResourceSO>();
            foreach (var spot in p.spots) if (spot.resource != null && !list.Contains(spot.resource)) list.Add(spot.resource);
            if (db.scrap != null && !list.Contains(db.scrap)) list.Add(db.scrap);
            return list;
        }

        List<GatherSpot> OldSpotsFor(PlanetSO p, MinigameKind game)
        {
            var list = new List<GatherSpot>();
            foreach (var sp in p.spots) if (sp.game == game && sp.resource != null) list.Add(sp);
            if (game == MinigameKind.Mine && db.scrap != null)
                list.Insert(0, new GatherSpot { label = "Surface Scrap", game = MinigameKind.Mine, resource = db.scrap, baseYield = 6, difficulty = 0.2f });
            return list;
        }

        [Test]
        public void AuthoredResourcesAndGatherSpots_BehaveLikeTheOldGameState()
        {
            foreach (var so in db.planets)
            {
                var p = service.Get(so.id);
                var expectedResources = OldResourcesFor(so);
                Assert.AreEqual(expectedResources.Count, p.Resources.Count, so.id);
                for (int i = 0; i < expectedResources.Count; i++) Assert.AreSame(expectedResources[i], p.Resources[i], so.id + " resource order");
                foreach (MinigameKind game in Enum.GetValues(typeof(MinigameKind)))
                {
                    var expected = OldSpotsFor(so, game);
                    var actual = p.SpotsFor(game);
                    Assert.AreEqual(expected.Count, actual.Count, so.id + " " + game);
                    for (int i = 0; i < expected.Count; i++)
                    {
                        Assert.AreEqual(expected[i].label, actual[i].label);
                        Assert.AreSame(expected[i].resource, actual[i].resource);
                        Assert.AreEqual(expected[i].baseYield, actual[i].baseYield);
                        Assert.AreEqual(expected[i].difficulty, actual[i].difficulty);
                        Assert.AreEqual(expected[i].game, actual[i].game);
                    }
                }
            }
        }

        // ---- generated worlds ----------------------------------------------------------------------------------------

        List<PlanetDescriptor> SampleGenerated(int wanted)
        {
            var u = service.Universe;
            var all = new List<PlanetDescriptor>();
            for (int sy = -24; sy <= 24; sy++)
                for (int sx = -24; sx <= 24; sx++)
                    foreach (var s in u.SystemsInSector(new SectorCoord(sx, sy)))
                        if (!s.IsAuthored) all.AddRange(u.PlanetsOf(s));
            Assert.GreaterOrEqual(all.Count, wanted);
            var sample = new List<PlanetDescriptor>();
            double step = all.Count / (double)wanted;
            for (int i = 0; i < wanted; i++) sample.Add(all[(int)(i * step)]);
            return sample;
        }

        static void AssertFinite(Color c, string what)
        {
            Assert.IsFalse(float.IsNaN(c.r) || float.IsNaN(c.g) || float.IsNaN(c.b) || float.IsInfinity(c.r) || float.IsInfinity(c.g) || float.IsInfinity(c.b), what + " is not finite");
            Assert.GreaterOrEqual(c.r, 0f, what);
            Assert.GreaterOrEqual(c.g, 0f, what);
            Assert.GreaterOrEqual(c.b, 0f, what);
        }

        [Test]
        public void FiveHundredGeneratedPlanets_ResolveWithValidFields()
        {
            var sample = SampleGenerated(500);
            var biomes = new HashSet<Biome>();
            var sizes = new HashSet<SizeClass>();
            var seeds = new HashSet<ulong>();
            int withRings = 0, withMoons = 0;
            foreach (var d in sample)
            {
                var p = service.Get(d);
                Assert.IsNotNull(p, d.Id);
                Assert.IsFalse(p.IsAuthored);
                Assert.IsNull(p.Authored);
                Assert.AreEqual(d.Id, p.Id);
                Assert.AreSame(p, service.Get(d.Id), "ids resolve to the same cached object");
                Assert.IsNotEmpty(p.Name);
                Assert.IsNotEmpty(p.Description);
                Assert.IsNull(p.AuthoredId);
                AssertFinite(p.Primary, p.Id + " primary");
                AssertFinite(p.Secondary, p.Id + " secondary");
                AssertFinite(p.Rim, p.Id + " rim");
                Assert.IsNotNull(ModelLibrary.Prefab(p.ModelKey), p.Id + ": model key '" + p.ModelKey + "' is not in the ModelLibrary");
                Assert.GreaterOrEqual(p.BuildingSlots, 12, p.Id);
                Assert.GreaterOrEqual(p.WorldSize, 1f);
                Assert.LessOrEqual(p.WorldSize, 2.5f);
                Assert.AreEqual(1f / p.WorldSize, p.StructureScale, 1e-6f);
                Assert.AreEqual(PlanetSO.StandardSurfaceRadius, p.SurfaceRadius, "generated worlds use the standard 1.5 ground radius");
                Assert.AreEqual(5f, p.AtmosphereScale);
                Assert.AreEqual(1f, p.ModelScale);
                Assert.AreEqual(BackdropMood.Auto, p.Mood);
                Assert.GreaterOrEqual(p.Style, 0);
                Assert.LessOrEqual(p.Style, 4);
                Assert.IsTrue(Enum.IsDefined(typeof(WeatherKind), p.Weather));
                Assert.Greater(p.Spots.Count, 0, p.Id + " offers nothing to harvest");
                Assert.Contains(db.scrap, new List<ResourceSO>(p.Resources), p.Id + " must offer scrap");
                Assert.GreaterOrEqual(p.SpotsFor(MinigameKind.Mine).Count, 1, p.Id + ": scrap can always be mined");
                foreach (var s in p.Spots)
                {
                    Assert.IsNotNull(s.resource);
                    Assert.IsNotEmpty(s.label);
                    Assert.GreaterOrEqual(s.baseYield, 1);
                    Assert.GreaterOrEqual(s.difficulty, 0f);
                    Assert.LessOrEqual(s.difficulty, 1f);
                    Assert.IsTrue(Enum.IsDefined(typeof(MinigameKind), s.game));
                }
                Assert.AreEqual(d.Deposits.Length, p.Spots.Count, "one gather spot per deposit");
                for (int i = 0; i < d.Deposits.Length; i++)
                    Assert.AreEqual(HarvestMapping.ToMinigame(d.Deposits[i].Method), p.Spots[i].game, p.Id + " deposit " + i);
                Assert.AreEqual(PlanetSeeds.VisualSeed(d, service.Seed), p.Seed);
                Assert.AreEqual(p.Seed, service.Get(d.Id).Seed, "the seed is stable");
                biomes.Add(p.Biome);
                sizes.Add(p.Size);
                seeds.Add(p.Seed);
                if (p.HasRings) withRings++;
                if (p.MoonCount > 0) withMoons++;
            }
            TestLog.Info("500 generated planets: " + biomes.Count + " biomes, " + sizes.Count + " size classes, " + withRings + " ringed, " + withMoons + " with moons, " + seeds.Count + " distinct seeds");
            Assert.AreEqual(500, seeds.Count);
            Assert.AreEqual(12, biomes.Count, "all biomes occur");
            Assert.AreEqual(6, sizes.Count, "all size classes occur");
            Assert.Greater(withRings, 5);
            Assert.Greater(withMoons, 50);
        }

        [Test]
        public void EveryModelKeyTheGeneratorCanUse_ExistsInTheModelLibrary()
        {
            foreach (string key in service.Universe.Config.ModelKeys)
                Assert.IsNotNull(ModelLibrary.Prefab(key), "the generator may use model '" + key + "' but the ModelLibrary has no such entry");
            foreach (var so in db.planets) Assert.IsNotNull(ModelLibrary.Prefab(so.ModelKey), so.id);
        }

        [Test]
        public void GeneratedPlanets_MapHarvestMethodsThroughTheSingleMappingFunction()
        {
            var star = new StarSystem(new SystemSpec { Key = new SystemKey(0, 0, 5), Name = "Test", Luminosity = 1f });
            var deposits = new[]
            {
                new Deposit("wood", HarvestMethod.Chop, 1f, 10, 0.2f),
                new Deposit("stone", HarvestMethod.Mine, 1.5f, 10, 0.3f),
                new Deposit("iron", HarvestMethod.Drill, 1f, 6, 0.4f),
                new Deposit("fuel", HarvestMethod.Siphon, 2f, 5, 0.5f),
                new Deposit("crystal", HarvestMethod.Excavate, 0.5f, 8, 0.6f),
                new Deposit("core", HarvestMethod.Salvage, 1f, 3, 0.7f),
                new Deposit("scrap", HarvestMethod.Harvest, 1f, 4, 0.8f)
            };
            var d = new PlanetDescriptor(new PlanetSpec { Key = new PlanetKey(new SystemKey(0, 0, 5), 0), Name = "T", Deposits = deposits, Biome = Biome.Lush });
            var p = new PlanetData(d, star, null, 1UL, db.FindResource, db.scrap);
            var games = new[] { MinigameKind.Chop, MinigameKind.Mine, MinigameKind.Drill, MinigameKind.Drill, MinigameKind.Mine, MinigameKind.Mine, MinigameKind.Chop };
            Assert.AreEqual(deposits.Length, p.Spots.Count);
            for (int i = 0; i < games.Length; i++) Assert.AreEqual(games[i], p.Spots[i].game, deposits[i].Method.ToString());
            Assert.AreEqual(15, p.Spots[1].baseYield, "yield is base yield x richness");
            Assert.AreEqual(10, p.Spots[3].baseYield, "5 x 2.0");
            Assert.AreEqual(4, p.Spots[4].baseYield, "8 x 0.5");
            Assert.AreEqual("Fuel Cell Siphon", p.Spots[3].label);
            Assert.AreEqual("Wood Grove", p.Spots[0].label);
            // the scrap deposit (here harvested) is not duplicated by a synthetic surface scrap spot unless it is a Mine spot of its own
            Assert.IsTrue(p.SpotsFor(MinigameKind.Mine).Exists(s => s.label == "Surface Scrap"), "no scrap deposit uses the Mine game here, so the surface scrap is offered");
        }

        [Test]
        public void ScrapDeposit_InTheMineGame_ReplacesTheSyntheticSurfaceScrap_OnGeneratedWorlds()
        {
            var star = new StarSystem(new SystemSpec { Key = new SystemKey(0, 0, 5), Name = "Test", Luminosity = 1f });
            var d = new PlanetDescriptor(new PlanetSpec
            {
                Key = new PlanetKey(new SystemKey(0, 0, 5), 0), Name = "T", Biome = Biome.Barren,
                Deposits = new[] { new Deposit("stone", HarvestMethod.Mine, 1f, 8, 0.3f), new Deposit("scrap", HarvestMethod.Salvage, 1f, 10, 0.2f) }
            });
            var p = new PlanetData(d, star, null, 1UL, db.FindResource, db.scrap);
            var mine = p.SpotsFor(MinigameKind.Mine);
            Assert.AreEqual(2, mine.Count, "stone and the world's own scrap; no duplicate surface scrap");
            Assert.IsFalse(mine.Exists(s => s.label == "Surface Scrap"));
            Assert.AreEqual(2, new List<ResourceSO>(p.Resources).Count);
        }

        [Test]
        public void Dump_IsReadable()
        {
            var p = service.FindGenerated(Biome.Lush, null);
            Assert.IsNotNull(p);
            string text = p.Dump();
            StringAssert.Contains(p.Id, text);
            StringAssert.Contains(p.Name, text);
            StringAssert.Contains("Lush", text);
            StringAssert.Contains("deposits", text);
            StringAssert.Contains("seed 0x", text);
            TestLog.Info(text);
            StringAssert.Contains("authored Core World 'sylva'", service.Home.Dump());
        }
    }
}
