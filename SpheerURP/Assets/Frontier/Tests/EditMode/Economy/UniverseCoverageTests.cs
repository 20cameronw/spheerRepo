using System;
using System.Collections.Generic;
using NUnit.Framework;
using Frontier.Economy;
using Frontier.Universe;
using FUniverse = Frontier.Universe.Universe;

namespace Frontier.Economy.Tests
{
    /// <summary>
    /// The economy's own resource table plugged into the real universe generator, proven with the generator's coverage logic: every raw
    /// resource a tier allows lies within a stated radius of any populated point.
    /// </summary>
    [TestFixture]
    public class UniverseCoverageTests
    {
        /// <summary>The stated guarantee radius in light-years around any populated point (three anchor blocks of 192 ly).</summary>
        public const double StatedRadius = 576.0;

        public const double PopulatedDensity = 0.10;

        EconomyCatalog _c;

        [OneTimeSetUp]
        public void Setup() { _c = Fx.NewCatalog(); }

        sealed class Sampler
        {
            public FUniverse U;
            public List<GalaxyInfo> Galaxies = new List<GalaxyInfo>();
            public Rng Rng;

            public Sampler(FUniverse u, ulong seed)
            {
                U = u;
                Rng = new Rng(seed);
                u.Galaxies.GalaxiesInRect(-16000, -16000, 16000, 16000, Galaxies);
            }

            public void NextPoint(out double x, out double y, out GalaxyInfo galaxy)
            {
                while (true)
                {
                    GalaxyInfo g = Galaxies[Rng.NextInt(Galaxies.Count)];
                    double a = Rng.NextDouble() * DetMath.TwoPi, r = Math.Sqrt(Rng.NextDouble()) * g.Radius;
                    g.FromLocal(Math.Cos(a) * r, Math.Sin(a) * r * g.AxisRatio, out x, out y);
                    if (U.Density(x, y) >= PopulatedDensity) { galaxy = g; return; }
                }
            }
        }

        static bool CarriesNearby(FUniverse u, double x, double y, double radius, string resourceId)
        {
            double r2 = radius * radius;
            int s0x = (int)Math.Floor((x - radius) / 64.0), s1x = (int)Math.Floor((x + radius) / 64.0);
            int s0y = (int)Math.Floor((y - radius) / 64.0), s1y = (int)Math.Floor((y + radius) / 64.0);
            for (int sy = s0y; sy <= s1y; sy++)
                for (int sx = s0x; sx <= s1x; sx++)
                    foreach (StarSystem s in u.SystemsInSector(new SectorCoord(sx, sy)))
                    {
                        double dx = s.X - x, dy = s.Y - y;
                        if (dx * dx + dy * dy > r2) continue;
                        foreach (PlanetDescriptor p in u.PlanetsOf(s)) if (p.HasResource(resourceId)) return true;
                    }
            return false;
        }

        void RunMonteCarlo(ulong universeSeed, int samples, ulong sampleSeed, string label)
        {
            FUniverse u = StarterSystem.CreateUniverse(_c, universeSeed);
            var sampler = new Sampler(u, sampleSeed);
            double[] radii = { 272.0, 400.0, StatedRadius };
            var failures = new int[radii.Length];
            int required = 0, tierMin = int.MaxValue, tierMax = 0;
            var firstFailure = new List<string>();
            for (int i = 0; i < samples; i++)
            {
                double x, y;
                GalaxyInfo g;
                sampler.NextPoint(out x, out y, out g);
                int tier = u.TierAt(x, y);
                tierMin = Math.Min(tierMin, tier);
                tierMax = Math.Max(tierMax, tier);
                foreach (Frontier.Universe.ResourceDef def in u.Resources.Defs)
                {
                    if (def.MinTier > tier) continue;
                    required++;
                    for (int r = 0; r < radii.Length; r++)
                    {
                        if (CarriesNearby(u, x, y, radii[r], def.Id)) continue;
                        failures[r]++;
                        if (r == radii.Length - 1 && firstFailure.Count < 5) firstFailure.Add(def.Id + " missing within " + radii[r] + " ly of (" + x.ToString("0") + ", " + y.ToString("0") + ") tier " + tier + " in " + g.Name);
                    }
                }
            }
            string line = "";
            for (int r = 0; r < radii.Length; r++) line += radii[r].ToString("0") + " ly: " + failures[r] + " misses (" + (100.0 * failures[r] / required).ToString("0.000") + " %)   ";
            TestLog.Info(label + ": " + samples + " random populated points in " + sampler.Galaxies.Count + " galaxies (tiers " + tierMin + ".." + tierMax + "), " + required + " (point, resource) requirements over " + u.Resources.Defs.Count + " raw ids");
            TestLog.Info(label + ": failure rate by search radius -> " + line);
            foreach (string f in firstFailure) TestLog.Info("   FAILURE " + f);
            Assert.AreEqual(0, failures[radii.Length - 1], "coverage guarantee violated within " + StatedRadius + " ly: " + string.Join(", ", firstFailure));
        }

        [Test]
        public void MonteCarlo_EveryTierAllowedRawResourceIsWithin576Ly()
        {
            RunMonteCarlo(UniverseConfigSeed, 8000, 0xEC0A0001UL, "economy table, default seed");
        }

        [Test]
        public void MonteCarlo_HoldsInAnotherUniverseSeed()
        {
            RunMonteCarlo(987654321UL, 3000, 0x5EED5UL, "economy table, seed 987654321");
        }

        const ulong UniverseConfigSeed = 0x46524F4E54494552UL;

        [Test]
        public void BlockLevel_EveryBlockWithAnEligiblePlanetCarriesEveryRawResource()
        {
            FUniverse u = StarterSystem.CreateUniverse(_c, UniverseConfigSeed);
            var sampler = new Sampler(u, 4242UL);
            int B = u.Config.AnchorBlockSectors;
            var seenBlocks = new HashSet<long>();
            int blocks = 0, injectedBlocks = 0, anchors = 0, checkedPairs = 0, skippedNoHost = 0;
            while (blocks < 1200)
            {
                double x, y;
                GalaxyInfo g;
                sampler.NextPoint(out x, out y, out g);
                SectorCoord sc = u.SectorOf(x, y);
                int bx = DetMath.FloorDiv(sc.X, B), by = DetMath.FloorDiv(sc.Y, B);
                if (!seenBlocks.Add(((long)bx << 32) ^ (uint)by)) continue;
                blocks++;
                var planets = new List<PlanetDescriptor>();
                for (int ly = 0; ly < B; ly++)
                    for (int lx = 0; lx < B; lx++)
                        foreach (StarSystem s in u.SystemsInSector(new SectorCoord(bx * B + lx, by * B + ly))) planets.AddRange(u.PlanetsOf(s));
                int inj = u.AnchorsInjectedFor(sc);
                if (inj > 0) injectedBlocks++;
                anchors += inj;
                foreach (Frontier.Universe.ResourceDef def in u.Resources.Defs)
                {
                    bool present = false, host = false;
                    foreach (PlanetDescriptor p in planets)
                    {
                        if (p.HasResource(def.Id)) present = true;
                        int effMin = def.MinTier;
                        foreach (ResourceEntry e in u.Resources.ProfileFor(p.Biome, p.Kind).Entries) if (e.ResourceId == def.Id) effMin = e.MinTier;
                        bool strictGas = p.Kind == PlanetKind.GasGiant && BiomeMask.Has(def.AnchorBiomes, Biome.GasGiant);
                        bool solidOk = p.Kind != PlanetKind.GasGiant && def.AnchorFallbackSolid;
                        bool strictHost = p.Kind != PlanetKind.GasGiant && BiomeMask.Has(def.AnchorBiomes, p.Biome);
                        if (p.Tier >= effMin && (strictGas || strictHost || solidOk)) host = true;
                    }
                    if (!host) { skippedNoHost++; continue; }
                    checkedPairs++;
                    Assert.IsTrue(present, "block (" + bx + "," + by + ") has an eligible planet but no " + def.Id);
                }
            }
            TestLog.Info(blocks + " populated blocks, " + checkedPairs + " (block, resource) pairs satisfied; " + skippedNoHost + " pairs had no eligible host (tier too low or gas-only resource without a gas giant); " + injectedBlocks + " blocks needed " + anchors + " anchor injections");
        }

        [Test]
        public void StarterSystem_IsValidAndTeachesTheLoop()
        {
            FUniverse u = StarterSystem.CreateUniverse(_c, UniverseConfigSeed);
            Assert.AreEqual(5, u.AuthoredSystems[0].PlanetCount);
            PlanetDescriptor sylva;
            Assert.IsTrue(u.TryGetPlanetById(u.PlanetsOf(u.AuthoredSystems[0])[0].Id, out sylva));
            Assert.AreEqual("sylva", sylva.AuthoredId);
            Assert.AreEqual(0, sylva.Tier);
            Assert.IsTrue(sylva.HasResource("wood") && sylva.HasResource("stone") && sylva.HasResource("scrap"), "Landfall needs wood, stone and scrap at home");
            Assert.AreEqual(24, sylva.BuildingSlots);
            foreach (PlanetDescriptor p in u.PlanetsOf(u.AuthoredSystems[0]))
                foreach (Deposit d in p.Deposits)
                {
                    ResourceDef r = _c.Resource(d.ResourceId);
                    Assert.IsNotNull(r, d.ResourceId);
                    Assert.AreEqual(ResourceCategory.Raw, r.category);
                    Assert.GreaterOrEqual(p.Tier, r.tier, d.ResourceId + " on " + p.Name);
                }
            // The four neighbours cover the Foothold and Settlement needs without leaving the system, except what the second system must give.
            var inSystem = new HashSet<string>();
            foreach (PlanetDescriptor p in u.PlanetsOf(u.AuthoredSystems[0])) foreach (Deposit d in p.Deposits) inSystem.Add(d.ResourceId);
            foreach (string id in new[] { "wood", "stone", "scrap", "biomass", "iron", "copper_ore", "silica", "ice", "crystal" }) Assert.IsTrue(inSystem.Contains(id), id + " should exist in the home system");
            foreach (string id in new[] { "titanium_ore", "methane", "helium3", "core" }) Assert.IsFalse(inSystem.Contains(id), id + " must make the player leave home");
        }

        [Test]
        public void ResourceAvailability_GrowsWithTier_AndGasesStayOnGasGiants()
        {
            FUniverse u = StarterSystem.CreateUniverse(_c, UniverseConfigSeed);
            var firstTier = new Dictionary<string, int>();
            int methaneOnSolid = 0, heliumOnSolid = 0, scrap = 0, planets = 0;
            for (int sy = -40; sy < 40; sy++)
                for (int sx = -40; sx < 40; sx++)
                    foreach (StarSystem s in u.SystemsInSector(new SectorCoord(sx, sy)))
                        foreach (PlanetDescriptor p in u.PlanetsOf(s))
                        {
                            planets++;
                            foreach (Deposit d in p.Deposits)
                            {
                                int t;
                                if (!firstTier.TryGetValue(d.ResourceId, out t) || p.Tier < t) firstTier[d.ResourceId] = p.Tier;
                                if (d.ResourceId == "methane" && p.Kind == PlanetKind.Rocky && p.Biome != Biome.Fungal && p.Biome != Biome.Oceanic) methaneOnSolid++;
                                if (d.ResourceId == "helium3" && p.Kind != PlanetKind.GasGiant) heliumOnSolid++;
                                if (d.ResourceId == "scrap") scrap++;
                            }
                        }
            foreach (ResourceDef r in _c.resources)
            {
                if (r.category != ResourceCategory.Raw) continue;
                Assert.IsTrue(firstTier.ContainsKey(r.id), r.id + " never generated");
                Assert.GreaterOrEqual(firstTier[r.id], r.tier, r.id + " appears below its tier");
            }
            Assert.AreEqual(0, heliumOnSolid, "helium-3 only comes from gas giants");
            Assert.AreEqual(0, methaneOnSolid, "methane only from gas giants, fungal and oceanic worlds");
            Assert.AreEqual(planets, scrap, "scrap is on every world");
            TestLog.Info(planets + " planets sampled; lowest tier per resource: " + string.Join(", ", new List<string>(Describe(firstTier))));
        }

        static IEnumerable<string> Describe(Dictionary<string, int> d)
        {
            foreach (KeyValuePair<string, int> kv in d) yield return kv.Key + ":" + kv.Value;
        }

        [Test]
        public void ResourceTable_UsesOnlyCatalogRawIds_AndFreezes()
        {
            ResourceTable t = UniverseTables.BuildResourceTable(_c);
            t.Freeze();
            Assert.AreEqual(15, t.Defs.Count);
            foreach (Frontier.Universe.ResourceDef d in t.Defs)
            {
                ResourceDef r = _c.Resource(d.Id);
                Assert.IsNotNull(r);
                Assert.AreEqual(r.tier, d.MinTier);
                Assert.AreEqual(r.harvestMethods[0], d.DefaultMethod);
            }
            CollectionAssert.AreEqual(new[] { "scrap" }, t.AlwaysPresent);
            for (int b = 0; b < 12; b++) Assert.IsTrue(t.Profiles[b].Contains("scrap"), "scrap in " + (Biome)b);
            Assert.IsTrue(t.BeltProfile.Contains("scrap"));
            Assert.IsFalse(t.Contains("fuel"), "fuel is crafted, never a deposit");
        }
    }
}
