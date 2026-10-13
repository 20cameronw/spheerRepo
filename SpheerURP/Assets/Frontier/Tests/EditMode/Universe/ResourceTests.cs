using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    [TestFixture]
    public class ResourceTests
    {
        Dataset _d;

        [OneTimeSetUp]
        public void Setup()
        {
            _d = Dataset.Shared;
        }

        static readonly string[] CurrentIds = { "wood", "stone", "scrap", "iron", "crystal", "fuel", "core" };
        static readonly string[] PlannedIds = { "biomass", "copper_ore", "silica", "ice", "titanium_ore", "methane", "helium3", "radium", "dark_matter" };

        [Test]
        public void DefaultTable_UsesOnlyTodaysResourceIds()
        {
            ResourceTable t = _d.Universe.Resources;
            Assert.AreEqual(CurrentIds.Length, t.Defs.Count);
            foreach (string id in CurrentIds) Assert.IsTrue(t.Contains(id), id);
            foreach (string id in PlannedIds) Assert.IsFalse(t.Contains(id), id + " is planned and must be off by default");
            foreach (ResourceDef d in t.Defs) Assert.IsFalse(d.Planned);
            Assert.IsTrue(t.IsFrozen);
            CollectionAssert.AreEqual(new[] { "scrap" }, t.AlwaysPresent);
            for (int b = 0; b < 12; b++)
            {
                Assert.IsTrue(t.Profiles[b].Contains("scrap"), "scrap missing from " + (Biome)b);
                foreach (ResourceEntry e in t.Profiles[b].Entries) Assert.IsTrue(t.Contains(e.ResourceId));
            }
            Assert.IsTrue(t.BeltProfile.Contains("scrap"));
        }

        [Test]
        public void EveryDepositBelongsToTheProfileTablesAndRespectsMinTier()
        {
            ResourceTable t = _d.Universe.Resources;
            int deposits = 0, injected = 0;
            foreach (PlanetDescriptor p in _d.Planets)
            {
                ResourceProfile profile = t.ProfileFor(p.Biome, p.Kind);
                var seen = new HashSet<string>();
                foreach (Deposit d in p.Deposits)
                {
                    deposits++;
                    Assert.IsTrue(t.Contains(d.ResourceId), "unknown resource " + d.ResourceId + " on " + p.Id);
                    Assert.IsTrue(seen.Add(d.ResourceId), "duplicate deposit " + d.ResourceId + " on " + p.Id);
                    ResourceDef def = t.Find(d.ResourceId);
                    Assert.GreaterOrEqual(p.Tier, def.MinTier, d.ResourceId + " below its min tier on " + p);
                    Assert.IsTrue(d.Richness >= 0.5f && d.Richness <= 2.0f, "richness " + d.Richness);
                    Assert.IsTrue(d.Difficulty >= 0f && d.Difficulty <= 1f, "difficulty " + d.Difficulty);
                    Assert.GreaterOrEqual(d.BaseYield, 1);
                    Assert.IsTrue(Enum.IsDefined(typeof(HarvestMethod), d.Method));
                    if (!profile.Contains(d.ResourceId)) injected++;
                    else
                    {
                        foreach (ResourceEntry e in profile.Entries)
                            if (e.ResourceId == d.ResourceId) Assert.GreaterOrEqual(p.Tier, e.MinTier, d.ResourceId + " on " + p);
                    }
                }
                Assert.GreaterOrEqual(p.Deposits.Length, p.Tier >= 1 ? 2 : 1, p.Id + " should offer scrap plus something once tier 1 resources unlock");
            }
            TestLog.Info(deposits + " deposits checked; " + injected + " are coverage-anchor injections that fall outside the host biome's profile");
        }

        [Test]
        public void Scrap_IsOnEveryWorld()
        {
            int n = 0;
            foreach (PlanetDescriptor p in _d.Planets)
            {
                Deposit d;
                Assert.IsTrue(p.TryGetDeposit("scrap", out d), "no scrap on " + p);
                Assert.AreEqual(HarvestMethod.Salvage, d.Method);
                Assert.AreEqual("scrap", p.Deposits[0].ResourceId, "scrap comes first");
                n++;
            }
            TestLog.Info("scrap present on all " + n + " planets");
            Assert.Greater(n, 5000);
        }

        [Test]
        public void ResourceAvailability_GrowsWithTier()
        {
            var firstTier = new Dictionary<string, int>();
            foreach (PlanetDescriptor p in _d.Planets)
                foreach (Deposit d in p.Deposits)
                {
                    int t;
                    if (!firstTier.TryGetValue(d.ResourceId, out t) || p.Tier < t) firstTier[d.ResourceId] = p.Tier;
                }
            var line = new System.Text.StringBuilder();
            foreach (var kv in firstTier) line.Append(kv.Key + ":" + kv.Value + " ");
            TestLog.Info("lowest tier on which each resource was found: " + line);
            Assert.AreEqual(0, firstTier["wood"]);
            Assert.AreEqual(0, firstTier["stone"]);
            Assert.AreEqual(0, firstTier["scrap"]);
            Assert.GreaterOrEqual(firstTier["iron"], 1);
            Assert.GreaterOrEqual(firstTier["fuel"], 1);
            Assert.GreaterOrEqual(firstTier["crystal"], 2);
            Assert.GreaterOrEqual(firstTier["core"], 3);
        }

        [Test]
        public void HarvestMethods_MatchTheResourceKinds()
        {
            int chopWood = 0, woodTotal = 0, siphonFuel = 0;
            foreach (PlanetDescriptor p in _d.Planets)
            {
                foreach (Deposit d in p.Deposits)
                {
                    if (d.ResourceId == "wood") { woodTotal++; if (d.Method == HarvestMethod.Chop) chopWood++; }
                    if (d.ResourceId == "fuel" && d.Method == HarvestMethod.Siphon) { siphonFuel++; Assert.AreEqual(PlanetKind.GasGiant, p.Kind); }
                    if (d.ResourceId == "core") Assert.AreEqual(HarvestMethod.Drill, d.Method);
                }
            }
            Assert.AreEqual(woodTotal, chopWood);
            Assert.Greater(siphonFuel, 50, "gas giants are siphoned");
        }

        [Test]
        public void PlannedResources_AreOffByDefaultAndEnabledByTheFlag()
        {
            foreach (PlanetDescriptor p in _d.Planets)
                foreach (Deposit d in p.Deposits)
                    Assert.IsFalse(Array.IndexOf(PlannedIds, d.ResourceId) >= 0, d.ResourceId + " leaked into the default universe");

            var cfg = new UniverseConfig();
            cfg.EnablePlannedResources = true;
            var u = new Universe(cfg, Landmarks.CreateDefault());
            Assert.AreEqual(CurrentIds.Length + PlannedIds.Length, u.Resources.Defs.Count);
            var firstTier = new Dictionary<string, int>();
            var count = new Dictionary<string, int>();
            for (int sy = -60; sy < 60; sy++)
            {
                for (int sx = -60; sx < 60; sx++)
                {
                    foreach (StarSystem s in u.SystemsInSector(new SectorCoord(sx, sy)))
                    {
                        foreach (PlanetDescriptor p in u.PlanetsOf(s))
                        {
                            foreach (Deposit d in p.Deposits)
                            {
                                ResourceDef def = u.Resources.Find(d.ResourceId);
                                Assert.IsNotNull(def);
                                Assert.GreaterOrEqual(p.Tier, def.MinTier, d.ResourceId + " on " + p);
                                int t, c;
                                if (!firstTier.TryGetValue(d.ResourceId, out t) || p.Tier < t) firstTier[d.ResourceId] = p.Tier;
                                count.TryGetValue(d.ResourceId, out c);
                                count[d.ResourceId] = c + 1;
                            }
                        }
                    }
                }
            }
            var line = new System.Text.StringBuilder();
            foreach (string id in PlannedIds) line.Append(id + ": min tier " + u.Resources.MinTierOf(id) + ", found " + (count.ContainsKey(id) ? count[id] : 0) + "x  ");
            TestLog.Info("planned resources: " + line);
            foreach (string id in PlannedIds) Assert.IsTrue(count.ContainsKey(id), id + " never generated with the flag on");
            Assert.AreEqual(1, u.Resources.MinTierOf("biomass"));
            Assert.AreEqual(6, u.Resources.MinTierOf("dark_matter"));
            Assert.AreEqual(4, u.Resources.MinTierOf("radium"));
        }

        [Test]
        public void ResourceTables_AreDataAndCanBeExtendedWithoutCodeChanges()
        {
            var cfg = new UniverseConfig();
            var table = ResourceTable.CreateDefault(false);
            var extra = table.AddDef(new ResourceDef("unobtainium", HarvestMethod.Excavate, 2, 3f, 1f, 0.1f));
            table.ProfileFor(Biome.Lush).Add("unobtainium", HarvestMethod.Excavate, 50.0f, 2);
            table.ProfileFor(Biome.Mineral).Add("unobtainium", HarvestMethod.Excavate, 50.0f, 2);
            cfg.Resources = table;
            var u = new Universe(cfg, Landmarks.CreateDefault());
            int found = 0;
            for (int sy = -30; sy < 30 && found < 20; sy++)
                for (int sx = -30; sx < 30 && found < 20; sx++)
                    foreach (StarSystem s in u.SystemsInSector(new SectorCoord(sx, sy)))
                        foreach (PlanetDescriptor p in u.PlanetsOf(s))
                            if (p.HasResource("unobtainium")) { found++; Assert.GreaterOrEqual(p.Tier, 2); }
            Assert.GreaterOrEqual(found, 20, "a data-only resource must show up");

            var bad = ResourceTable.CreateDefault(false);
            bad.ProfileFor(Biome.Lush).Add("ghost", HarvestMethod.Mine, 1f, 0);
            var badCfg = new UniverseConfig { Resources = bad };
            Assert.Throws<ArgumentException>(() => new Universe(badCfg));

            var early = ResourceTable.CreateDefault(false);
            early.ProfileFor(Biome.Lush).Add("crystal", HarvestMethod.Mine, 1f, 0);
            Assert.Throws<ArgumentException>(() => new Universe(new UniverseConfig { Resources = early }), "entry tier below the definition's min tier");
        }
    }
}
