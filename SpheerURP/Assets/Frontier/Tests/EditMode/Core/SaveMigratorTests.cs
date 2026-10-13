using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Tests
{
    /// <summary>A stand-in for the game's authored worlds: a few legacy ids with canonical ids and distances.</summary>
    public sealed class FakeWorldMap : IWorldIdMap
    {
        public readonly Dictionary<string, string> Canonical = new Dictionary<string, string>
        {
            { "sylva", "p:0:0:1000:0" }, { "glacia", "p:0:0:1000:1" }, { "ferrum", "p:0:0:1000:2" }, { "veridian", "p:0:0:1000:3" },
            { "cinder", "p:3:0:1001:0" }, { "prism", "p:14:-15:1005:0" }
        };

        public readonly Dictionary<string, double> Distance = new Dictionary<string, double>
        {
            { "p:0:0:1000:0", 0.0 }, { "p:0:0:1000:1", 0.0 }, { "p:0:0:1000:2", 0.0 }, { "p:0:0:1000:3", 0.0 },
            { "p:3:0:1001:0", 213.3 }, { "p:14:-15:1005:0", 1290.5 }
        };

        public string HomeId { get { return "p:0:0:1000:0"; } }

        public bool TryCanonical(string id, out string canonicalId)
        {
            if (id != null && Canonical.TryGetValue(id, out canonicalId)) return true;
            if (id != null && Distance.ContainsKey(id)) { canonicalId = id; return true; }
            canonicalId = null;
            return false;
        }

        public double DistanceFromHomeLy(string canonicalId)
        {
            double d;
            return canonicalId != null && Distance.TryGetValue(canonicalId, out d) ? d : -1.0;
        }
    }

    /// <summary>The v1 to v2 migration, as a pure function.</summary>
    [TestFixture]
    public class SaveMigratorTests
    {
        static readonly DateTime Now = new DateTime(2026, 10, 13, 20, 15, 0, DateTimeKind.Utc);

        static MigrationContext Context()
        {
            return new MigrationContext { AppVersion = "0.9.1", UniverseSeed = 0x46524F4E54494552UL, GeneratorVersion = 1, NowUtc = Now, SaveId = "save-1" };
        }

        // The same content as Tests/EditMode/Fixtures/save_v1_midgame.json (a real file written by the legacy game), built by hand.
        static SaveDataV1 MidGame()
        {
            var v1 = new SaveDataV1
            {
                itemIds = new List<string> { "fuel", "wood", "scrap", "stone", "iron", "crystal", "core", "alloy", "plasma" },
                itemCounts = new List<int> { 30, 30, 89, 26, 30, 5, 7, 5, 2 },
                upgradeIds = new List<string> { "pack", "axe", "pick", "engine" },
                upgradeLevels = new List<int> { 2, 1, 1, 1 },
                waveIds = new List<string> { "sylva", "glacia", "veridian" },
                waveCounts = new List<int> { 3, 1, 2 },
                structPlanets = new List<string> { "sylva", "sylva", "sylva", "sylva", "sylva", "glacia", "glacia", "glacia", "veridian" },
                structIds = new List<string> { "lumber", "refinery", "warehouse", "alarm", "gunner", "driller", "mine", "warehouse", "lumber" },
                structSlots = new List<int> { 0, 3, 5, 7, 9, 2, 4, 1, 6 },
                structLevels = new List<int> { 1, 2, 1, 1, 1, 2, 1, 1, 1 },
                xp = 212,
                planetId = "glacia",
                totalWavesCleared = 6
            };
            var w1 = new WarehouseStorageV1 { planetId = "sylva", structureSlot = 5 };
            for (int i = 0; i < 18; i++) w1.slots.Add(new StoredStackV1 { resourceId = "", count = 0 });
            w1.slots[1] = new StoredStackV1 { resourceId = "scrap", count = 50 };
            w1.slots[2] = new StoredStackV1 { resourceId = "stone", count = 12 };
            w1.slots[4] = new StoredStackV1 { resourceId = "wood", count = 40 };
            var w2 = new WarehouseStorageV1 { planetId = "glacia", structureSlot = 1 };
            for (int i = 0; i < 18; i++) w2.slots.Add(new StoredStackV1 { resourceId = "", count = 0 });
            w2.slots[3] = new StoredStackV1 { resourceId = "crystal", count = 6 };
            v1.warehouses.Add(w1);
            v1.warehouses.Add(w2);
            return v1;
        }

        [Test]
        public void MidGameSave_MigratesEverythingToCanonicalIds()
        {
            var warnings = new List<string>();
            var v2 = SaveMigrator.FromV1(MidGame(), new FakeWorldMap(), Context(), warnings);

            Assert.AreEqual(SaveSchema.Current, v2.schemaVersion);
            Assert.AreEqual("save-1", v2.saveId);
            Assert.AreEqual("0.9.1", v2.createdWith.appVersion);
            Assert.AreEqual(1, v2.createdWith.generatorVersion);
            Assert.AreEqual("46524F4E54494552", v2.createdWith.universeSeed);
            Assert.AreEqual(UtcText.Format(Now), v2.clock.lastSavedUtc);
            Assert.AreEqual(212, v2.player.xp);
            Assert.AreEqual("p:0:0:1000:1", v2.location.currentPlanetId, "glacia");
            Assert.AreEqual(0, warnings.Count, string.Join("; ", warnings));

            Assert.AreEqual(9, v2.player.inventory.Count);
            int scrap = 0;
            foreach (var s in v2.player.inventory) if (s.resourceId == "scrap") scrap = s.count;
            Assert.AreEqual(89, scrap);
            Assert.AreEqual(4, v2.player.upgrades.Count);
            foreach (var u in v2.player.upgrades) if (u.id == "pack") Assert.AreEqual(2, u.level);

            Assert.AreEqual(3, v2.worlds.Count, "only planets the player changed");
            Assert.AreEqual("p:0:0:1000:0", v2.worlds[0].planetId, "sorted by id");
            var sylva = v2.worlds[0];
            Assert.AreEqual(5, sylva.structures.Count);
            Assert.AreEqual(3, sylva.wavesCleared);
            Assert.AreEqual("refinery", sylva.structures[1].defId);
            Assert.AreEqual(2, sylva.structures[1].level);
            Assert.AreEqual(1, sylva.storages.Count);
            Assert.AreEqual(5, sylva.storages[0].structureSlot);
            Assert.AreEqual(18, sylva.storages[0].slots.Count);
            Assert.AreEqual("scrap", sylva.storages[0].slots[1].resourceId);
            Assert.AreEqual(50, sylva.storages[0].slots[1].count);
            Assert.IsNull(sylva.storages[0].slots[0].resourceId, "empty v1 slots (empty text) become empty slots");
            Assert.AreEqual(40, sylva.storages[0].slots[4].count);
            var glacia = v2.worlds[1];
            Assert.AreEqual(3, glacia.structures.Count);
            Assert.AreEqual(1, glacia.wavesCleared);
            Assert.AreEqual(6, glacia.storages[0].slots[3].count);
            var veridian = v2.worlds[2];
            Assert.AreEqual("p:0:0:1000:3", veridian.planetId);
            Assert.AreEqual(2, veridian.wavesCleared);

            Assert.AreEqual(6, v2.stats.totalWavesCleared);
            Assert.AreEqual(3, v2.stats.worldsLanded, "sylva, glacia and veridian");
            Assert.AreEqual(0.0, v2.stats.maxDistanceFromHomeLy, 1e-9);
            foreach (var d in v2.worlds) { Assert.IsTrue(d.discovered); Assert.GreaterOrEqual(d.visits, 1); }
        }

        [Test]
        public void Migration_IsDeterministic()
        {
            var a = SaveMigrator.FromV1(MidGame(), new FakeWorldMap(), Context(), null);
            var b = SaveMigrator.FromV1(MidGame(), new FakeWorldMap(), Context(), null);
            Assert.AreEqual(a.worlds.Count, b.worlds.Count);
            for (int i = 0; i < a.worlds.Count; i++)
            {
                Assert.AreEqual(a.worlds[i].planetId, b.worlds[i].planetId);
                Assert.AreEqual(a.worlds[i].structures.Count, b.worlds[i].structures.Count);
            }
            Assert.AreEqual(a.stats.worldsLanded, b.stats.worldsLanded);
        }

        [Test]
        public void FarWorlds_SetTheMaximumDistance()
        {
            var v1 = new SaveDataV1 { planetId = "prism" };
            v1.structPlanets.Add("cinder"); v1.structIds.Add("mine"); v1.structSlots.Add(1); v1.structLevels.Add(1);
            var v2 = SaveMigrator.FromV1(v1, new FakeWorldMap(), Context(), null);
            Assert.AreEqual(1290.5, v2.stats.maxDistanceFromHomeLy, 1e-9);
            Assert.AreEqual(3, v2.stats.worldsLanded, "home, the world with a structure and the world the player is on");
            Assert.AreEqual("p:14:-15:1005:0", v2.location.currentPlanetId);
        }

        [Test]
        public void FreshGame_MigratesToTheHomeWorld()
        {
            var v1 = new SaveDataV1
            {
                itemIds = new List<string> { "fuel", "wood", "scrap" },
                itemCounts = new List<int> { 10, 24, 30 },
                planetId = "sylva"
            };
            var v2 = SaveMigrator.FromV1(v1, new FakeWorldMap(), Context(), null);
            Assert.AreEqual("p:0:0:1000:0", v2.location.currentPlanetId);
            Assert.AreEqual(3, v2.player.inventory.Count);
            Assert.AreEqual(1, v2.worlds.Count, "the home world records the first landing");
            Assert.AreEqual(1, v2.stats.worldsLanded);
            Assert.AreEqual(0, v2.stats.totalWavesCleared);
            Assert.AreEqual(0, v2.worlds[0].structures.Count);
        }

        [Test]
        public void MissingPlanetId_StartsAtHome()
        {
            var v2 = SaveMigrator.FromV1(new SaveDataV1 { planetId = null }, new FakeWorldMap(), Context(), null);
            Assert.AreEqual("p:0:0:1000:0", v2.location.currentPlanetId);
        }

        [Test]
        public void UnknownWorlds_AreKeptVerbatim_AndTheCurrentOneFallsBackToHome()
        {
            var v1 = new SaveDataV1 { planetId = "atlantis" };
            v1.structPlanets.Add("atlantis"); v1.structIds.Add("lumber"); v1.structSlots.Add(2); v1.structLevels.Add(3);
            var warnings = new List<string>();
            var v2 = SaveMigrator.FromV1(v1, new FakeWorldMap(), Context(), warnings);
            Assert.AreEqual("p:0:0:1000:0", v2.location.currentPlanetId);
            Assert.IsTrue(warnings.Exists(w => w.Contains("atlantis")));
            var atlantis = v2.worlds.Find(w => w.planetId == "atlantis");
            Assert.IsNotNull(atlantis, "no player data is destroyed");
            Assert.AreEqual(3, atlantis.structures[0].level);
        }

        [Test]
        public void AlreadyCanonicalIds_PassThrough()
        {
            var v1 = new SaveDataV1 { planetId = "p:3:0:1001:0" };
            v1.waveIds.Add("p:3:0:1001:0"); v1.waveCounts.Add(4);
            var v2 = SaveMigrator.FromV1(v1, new FakeWorldMap(), Context(), null);
            Assert.AreEqual("p:3:0:1001:0", v2.location.currentPlanetId);
            Assert.AreEqual(4, v2.worlds.Find(w => w.planetId == "p:3:0:1001:0").wavesCleared);
        }

        [Test]
        public void OddLists_AreCutToTheShortestAndBadRecordsDropped()
        {
            var v1 = new SaveDataV1
            {
                itemIds = new List<string> { "wood", "", "stone", "wood" },
                itemCounts = new List<int> { 5, 9, -3 },       // one entry short, an empty id, a negative count
                upgradeIds = new List<string> { "pack", "pack", "axe" },
                upgradeLevels = new List<int> { 1, 3, 0 },
                waveIds = new List<string> { "sylva" },
                waveCounts = new List<int> { 2, 7 },
                totalWavesCleared = 1,                         // smaller than the per-planet sum: the sum wins
                planetId = "sylva"
            };
            v1.structPlanets.AddRange(new[] { "sylva", "sylva", "sylva", "" });
            v1.structIds.AddRange(new[] { "lumber", "mine", "gunner", "x" });
            v1.structSlots.AddRange(new[] { 1, 1, -4, 2 });    // the second shares a pad with the first; the third has a negative pad
            v1.structLevels.AddRange(new[] { 0, 2, 1, 1 });
            var warnings = new List<string>();
            var v2 = SaveMigrator.FromV1(v1, new FakeWorldMap(), Context(), warnings);
            Assert.AreEqual(1, v2.player.inventory.Count);
            Assert.AreEqual(5, v2.player.inventory[0].count);
            Assert.AreEqual(1, v2.player.upgrades.Count);
            Assert.AreEqual(3, v2.player.upgrades[0].level, "the later duplicate wins");
            var home = v2.worlds[0];
            Assert.AreEqual(1, home.structures.Count);
            Assert.AreEqual("lumber", home.structures[0].defId);
            Assert.AreEqual(1, home.structures[0].level, "level 0 is repaired to 1");
            Assert.AreEqual(2, home.wavesCleared);
            Assert.AreEqual(2, v2.stats.totalWavesCleared);
            Assert.GreaterOrEqual(warnings.Count, 2);
        }

        [Test]
        public void NullListsInTheLegacyFile_AreTolerated()
        {
            var v1 = new SaveDataV1
            {
                itemIds = null, itemCounts = null, upgradeIds = null, upgradeLevels = null, waveIds = null, waveCounts = null,
                structPlanets = null, structIds = null, structSlots = null, structLevels = null, warehouses = null, planetId = "sylva"
            };
            var v2 = SaveMigrator.FromV1(v1, new FakeWorldMap(), Context(), null);
            Assert.AreEqual(0, v2.player.inventory.Count);
            Assert.AreEqual("p:0:0:1000:0", v2.location.currentPlanetId);
        }

        [Test]
        public void ReservedSections_AreEmptyListsWithDefaults()
        {
            var v2 = SaveMigrator.FromV1(MidGame(), new FakeWorldMap(), Context(), null);
            Assert.AreEqual(0, v2.wallet.Count);
            Assert.AreEqual(0, v2.builders.Count);
            Assert.AreEqual(0, v2.atlas.Count);
            Assert.AreEqual(0, v2.settings.Count);
            Assert.AreEqual(0.0, v2.stats.playSeconds);
        }

        [Test]
        public void Normalize_RepairsNullsAndDropsEmptyEntries()
        {
            var s = new SaveV2 { createdWith = null, player = null, location = null, worlds = null, stats = null, clock = null, wallet = null, builders = null, atlas = null, settings = null };
            s.Normalize();
            Assert.IsNotNull(s.player.inventory);
            Assert.IsNotNull(s.worlds);
            Assert.IsNotNull(s.stats);
            Assert.AreEqual("", s.location.currentPlanetId);
            s.player.inventory.Add(new StackRecord { resourceId = "wood", count = 0 });
            s.player.inventory.Add(new StackRecord { resourceId = "wood", count = 2 });
            s.player.inventory.Add(null);
            s.player.upgrades.Add(new IdLevel { id = "", level = 3 });
            s.player.xp = -5;
            s.Normalize();
            Assert.AreEqual(1, s.player.inventory.Count);
            Assert.AreEqual(0, s.player.upgrades.Count);
            Assert.AreEqual(0, s.player.xp);
        }

        [Test]
        public void SeedText_RoundTrips()
        {
            Assert.AreEqual("46524F4E54494552", SeedText.Format(0x46524F4E54494552UL));
            ulong back;
            Assert.IsTrue(SeedText.TryParse("46524F4E54494552", out back));
            Assert.AreEqual(0x46524F4E54494552UL, back);
            Assert.IsFalse(SeedText.TryParse("", out back));
            Assert.IsFalse(SeedText.TryParse("xyz", out back));
            Assert.AreEqual("00000000000000FF", SeedText.Format(255UL));
        }
    }
}
