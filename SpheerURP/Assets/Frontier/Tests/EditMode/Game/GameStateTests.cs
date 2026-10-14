using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    /// <summary>
    /// The session facade end to end: debounced writes with a fake clock, immediate flushes, travel and visit bookkeeping, loading a real
    /// v1 save, recovery from a damaged primary, read-only handling of newer saves, unknown content and generated worlds.
    /// </summary>
    [TestFixture]
    public class GameStateTests
    {
        SaveSandbox sandbox;
        GameDatabase db;
        FakeClock clock;

        [SetUp]
        public void SetUp()
        {
            sandbox = new SaveSandbox();
            db = GameDatabase.Load();
            clock = new FakeClock();
        }

        [TearDown]
        public void TearDown()
        {
            sandbox.Dispose();
        }

        GameState NewState(bool load = false) { return new GameState(db, load, clock); }

        string PrimaryPath { get { return sandbox.SaveFile; } }
        string BackupPath { get { return sandbox.SaveFile + SaveSchema.BackupSuffix; } }
        string TempPath { get { return sandbox.SaveFile + SaveSchema.TempSuffix; } }

        static SaveV2 ReadSave(string path) { return SaveSerializer.Instance.DeserializeV2(File.ReadAllText(path)).Normalize(); }

        void GiveFuel(GameState state, int amount) { state.backpack.Set(db.fuel.id, amount); }

        [Test]
        public void Biodome_OnlyOffersResourcesWithTheHarvestMethod()
        {
            var state = NewState();
            var authored = ScriptableObject.CreateInstance<PlanetSO>();
            try
            {
                var biomass = db.FindResource("biomass");
                var wood = db.FindResource("wood");
                authored.spots.Add(new GatherSpot { game = MinigameKind.Chop, resource = biomass, baseYield = 10 });
                authored.spots.Add(new GatherSpot { game = MinigameKind.Chop, resource = wood, baseYield = 10 });
                var mixedWorld = new PlanetData(state.planet.Descriptor, state.planet.Star, authored,
                    state.universe.Seed, id => db.FindResource(id), db.scrap);

                var biodome = db.FindStructure("biodome");
                Assert.IsNotNull(biodome);
                Assert.IsTrue(biodome.Implemented);
                Assert.IsTrue(biodome.kind.IsGather());
                var harvestSpots = state.SpotsFor(mixedWorld, StructureKind.Biodome);
                Assert.AreEqual(1, harvestSpots.Count);
                Assert.AreEqual("biomass", harvestSpots[0].resource.id);

                var lumberSpots = state.SpotsFor(mixedWorld, StructureKind.Lumber);
                Assert.AreEqual(1, lumberSpots.Count);
                Assert.AreEqual("wood", lumberSpots[0].resource.id);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(authored);
            }
        }

        [Test]
        public void AutoHarvester_AccruesCappedOfflineOutputIntoItsPersistentHopper()
        {
            var state = NewState();
            var definition = db.FindStructure("auto_harvester");
            Assert.IsNotNull(definition);
            Assert.IsTrue(definition.Implemented);
            Assert.AreEqual(8, definition.CapacityAt(1), "level one caps a single offline interval at eight hours");
            Assert.AreEqual(18, definition.StorageCapacity(1), "offline-hour capacity is not the storage-slot capacity");
            Assert.IsNotNull(state.DebugPlace(definition, 0));
            state.Save();

            clock.Advance(12 * 60 * 60);
            var loaded = NewState(true);
            var harvester = loaded.StructureAt(loaded.planet, 0);
            Assert.IsNotNull(harvester);
            var hopper = loaded.StorageFor(harvester);
            int stored = 0;
            foreach (var stack in hopper.slots) stored += stack.count;
            Assert.Greater(stored, 0, "the stored save gap produces offline raw materials");
            Assert.LessOrEqual(WarehouseOps.UsedSlots(hopper), definition.StorageCapacity(1));

            var reloaded = NewState(true);
            var reloadedHopper = reloaded.StorageFor(reloaded.StructureAt(reloaded.planet, 0));
            int storedAgain = 0;
            foreach (var stack in reloadedHopper.slots) storedAgain += stack.count;
            Assert.AreEqual(stored, storedAgain, "the launch-time accrual is flushed and is not replayed");
        }

        [Test]
        public void AutoHarvester_CarriesFractionalRunsAcrossShortOfflineIntervals()
        {
            var state = NewState();
            Assert.IsNotNull(state.DebugPlace(db.FindStructure("auto_harvester"), 0));
            state.Save();

            clock.Advance(60);
            var firstLoad = NewState(true);
            var firstRecord = firstLoad.StructureAt(firstLoad.planet, 0).record;
            Assert.AreEqual(0.1, firstRecord.offlineRunRemainder, 0.0001);

            clock.Advance(11 * 60);
            var secondLoad = NewState(true);
            var hopper = secondLoad.StorageFor(secondLoad.StructureAt(secondLoad.planet, 0));
            Assert.Greater(WarehouseOps.UsedSlots(hopper), 0,
                "the fractional run from the previous visit combines with the next interval");
        }

        // ---- debounce ----------------------------------------------------------------------------------------------------

        [Test]
        public void Changes_AreNotWrittenUntilTheStateHasBeenQuietForASecond()
        {
            var state = NewState();
            Assert.IsTrue(state.Save(), "baseline write");
            int writes = state.saves.WriteCount;
            state.AddXp(5);
            Assert.IsTrue(state.saves.IsDirty);
            Assert.IsFalse(state.Tick(), "no write in the same frame");
            clock.Advance(0.9);
            Assert.IsFalse(state.Tick(), "0.9 s of quiet is not enough");
            Assert.AreEqual(writes, state.saves.WriteCount);
            clock.Advance(0.2);
            Assert.IsTrue(state.Tick(), "1.1 s of quiet writes the save");
            Assert.AreEqual(writes + 1, state.saves.WriteCount);
            Assert.IsFalse(state.saves.IsDirty);
            Assert.AreEqual(5, ReadSave(PrimaryPath).player.xp);
            Assert.IsFalse(state.Tick(), "nothing left to write");
        }

        [Test]
        public void EveryChange_RestartsTheQuietPeriod()
        {
            var state = NewState();
            state.Save();
            state.AddXp(1);
            clock.Advance(0.8);
            state.AddXp(1);
            clock.Advance(0.4);
            Assert.IsFalse(state.Tick(), "only 0.4 s since the last change");
            clock.Advance(0.7);
            Assert.IsTrue(state.Tick());
            Assert.AreEqual(2, ReadSave(PrimaryPath).player.xp);
        }

        [Test]
        public void ContinuousChanges_AreStillWrittenAfterTheMaximumDelay()
        {
            var state = NewState();
            state.Save();
            int writes = state.saves.WriteCount;
            double firstChange = -1.0, written = -1.0;
            for (int i = 1; i <= 40 && written < 0.0; i++)
            {
                clock.Advance(0.5);
                state.AddXp(1);
                if (firstChange < 0.0) firstChange = clock.MonotonicSeconds;
                if (state.Tick()) written = clock.MonotonicSeconds;
            }
            Assert.Greater(written, 0.0, "a player who taps constantly must still be saved");
            Assert.AreEqual(writes + 1, state.saves.WriteCount);
            Assert.GreaterOrEqual(written - firstChange, state.saves.MaxDelaySeconds - 1e-9);
            Assert.LessOrEqual(written - firstChange, state.saves.MaxDelaySeconds + 0.51);
        }

        [Test]
        public void Flush_WritesImmediately_ButOnlyWhenSomethingChanged()
        {
            var state = NewState();
            state.Save();
            int writes = state.saves.WriteCount;
            Assert.IsFalse(state.Flush(), "clean state: nothing to write");
            state.AddXp(3);
            Assert.IsTrue(state.Flush(), "app pause / focus loss / quit");
            Assert.AreEqual(writes + 1, state.saves.WriteCount);
            Assert.AreEqual(3, ReadSave(PrimaryPath).player.xp);
            Assert.IsFalse(state.saves.IsDirty);
        }

        [Test]
        public void Save_AlwaysWrites()
        {
            var state = NewState();
            Assert.IsTrue(state.Save());
            Assert.IsTrue(state.Save());
            Assert.AreEqual(2, state.saves.WriteCount);
        }

        [Test]
        public void SilentChanges_StillMarkTheStateDirty()
        {
            var state = NewState();
            state.Save();
            state.Add(db.FindResource("wood"), 4, false);
            Assert.IsTrue(state.saves.IsDirty, "Add(..., notify: false) must not lose data");
            Assert.IsTrue(state.Flush());
            Assert.AreEqual(4, ReadSave(PrimaryPath).player.inventory.Find(s => s.resourceId == "wood").count - StartCount("wood"));
        }

        int StartCount(string id)
        {
            foreach (var s in db.startingItems) if (s.resource != null && s.resource.id == id) return s.amount;
            return 0;
        }

        [Test]
        public void Notify_FiresChanged_WithoutWriting()
        {
            var state = NewState();
            state.Save();
            int fired = 0;
            state.Changed += () => fired++;
            state.Notify();
            state.Notify();
            Assert.AreEqual(2, fired);
            Assert.AreEqual(1, state.saves.WriteCount, "Notify no longer writes synchronously");
            Assert.IsTrue(state.saves.IsDirty);
        }

        [Test]
        public void PlayTime_CountsNormalFramesButNotLongGaps()
        {
            var state = NewState();
            for (int i = 0; i < 100; i++) { clock.Advance(0.016); state.Tick(); }
            Assert.AreEqual(1.6, state.stats.playSeconds, 0.001);
            clock.Advance(30.0);
            state.Tick();
            Assert.AreEqual(1.6, state.stats.playSeconds, 0.001, "a 30 s gap means the app was suspended");
            for (int i = 0; i < 10; i++) { clock.Advance(0.016); state.Tick(); }
            Assert.AreEqual(1.76, state.stats.playSeconds, 0.001);
        }

        [Test]
        public void PlayTime_MakesTheSaveDirtyOncePerMinute()
        {
            var state = NewState();
            for (int i = 0; i < 125; i++) { clock.Advance(1.0); state.Tick(); }
            // the new game itself (written after its first quiet second) plus one write per minute of play
            Assert.GreaterOrEqual(state.saves.WriteCount, 3);
            Assert.AreEqual(125.0, state.stats.playSeconds, 1.0);
            Assert.GreaterOrEqual(ReadSave(PrimaryPath).stats.playSeconds, 119.0);
        }

        // ---- travel and visits ---------------------------------------------------------------------------------------------

        [Test]
        public void NewGame_CountsTheHomeWorldAsVisited()
        {
            var state = NewState();
            Assert.AreEqual(1, state.world.Visits(state.planet.Id));
            Assert.IsTrue(state.world.IsDiscovered(state.planet.Id));
            Assert.AreEqual(1, state.stats.worldsLanded);
            Assert.AreEqual(0.0, state.stats.maxDistanceFromHomeLy);
            Assert.AreEqual(SaveSource.NewGame, state.LoadReport.source);
        }

        [Test]
        public void Travel_SpendsTheFuelOfTheTravelModel_AndRecordsTheLanding()
        {
            var state = NewState();
            GiveFuel(state, 100);
            var glacia = state.universe.Get("glacia");
            int hopFuel = state.FuelCost(glacia);
            Assert.AreEqual(state.Rules.Model.IntraSystemFuelCost(Math.Abs(state.planet.Descriptor.OrbitRadius - glacia.Descriptor.OrbitRadius), 0, 0, false), hopFuel);
            Assert.AreEqual(0, state.FuelCost(state.planet), "no cost to stay");
            Assert.IsTrue(state.Travel(glacia));
            Assert.AreEqual(100 - hopFuel, state.Count(db.fuel));
            Assert.AreEqual(glacia.Id, state.planet.Id);
            Assert.AreEqual(1, state.world.Visits(glacia.Id));
            Assert.AreEqual(2, state.stats.worldsLanded);
            Assert.AreEqual(0.0, state.stats.maxDistanceFromHomeLy, "same system as home");

            var cinder = state.universe.Get("cinder");
            int cost = state.FuelCost(cinder);
            Assert.Greater(cost, 5, "another system costs more than a hop inside one");
            Assert.IsTrue(state.Travel(cinder));
            Assert.AreEqual(100 - hopFuel - cost, state.Count(db.fuel));
            Assert.AreEqual(3, state.stats.worldsLanded);
            Assert.AreEqual(state.universe.DistanceFromHomeLy(cinder), state.stats.maxDistanceFromHomeLy, 1e-9);
            Assert.AreEqual(213.3, state.stats.maxDistanceFromHomeLy, 0.1, "Emberfall lies at tier 3 distance");
        }

        [Test]
        public void ARepeatedVisit_CountsTheVisitButNotANewWorld()
        {
            var state = NewState();
            GiveFuel(state, 100);
            var glacia = state.universe.Get("glacia");
            state.Travel(glacia);
            state.Travel(state.universe.Home);
            state.Travel(glacia);
            Assert.AreEqual(2, state.world.Visits(glacia.Id));
            Assert.AreEqual(2, state.world.Visits(state.universe.Home.Id));
            Assert.AreEqual(2, state.stats.worldsLanded);
            Assert.IsFalse(string.IsNullOrEmpty(state.world.Get(glacia.Id).firstVisitUtc));
        }

        [Test]
        public void Travel_IsRefused_WithoutEnoughFuel_OrToTheSameWorld()
        {
            var state = NewState();
            state.backpack.Set(db.fuel.id, 0);
            Assert.IsFalse(state.Travel(state.universe.Get("glacia")), "no fuel owned");
            Assert.AreEqual(0, state.Count(db.fuel));
            Assert.AreEqual(state.universe.Home.Id, state.planet.Id);
            Assert.IsFalse(state.Travel(state.planet));
            Assert.IsFalse(state.Travel(null));
        }

        [Test]
        public void Travel_WritesTheSaveImmediately()
        {
            var state = NewState();
            GiveFuel(state, 50);
            state.Save();
            int writes = state.saves.WriteCount;
            Assert.IsTrue(state.Travel(state.universe.Get("glacia")));
            Assert.AreEqual(writes + 1, state.saves.WriteCount, "travel flushes without waiting for the quiet period");
            Assert.IsFalse(state.saves.IsDirty);
            Assert.AreEqual(state.planet.Id, ReadSave(PrimaryPath).location.currentPlanetId);
        }

        [Test]
        public void Travel_RejectsWrongDestination_ModifiedPrice_AndStaleOrigin()
        {
            var state = NewState();
            GiveFuel(state, 100);
            var glacia = state.universe.Get("glacia");
            var route = state.PlanRoute(glacia);
            Assert.IsFalse(state.Travel(route, state.universe.Get("cinder")));
            int price = route.totalFuel;
            route.totalFuel = 0;
            Assert.IsFalse(state.Travel(route, glacia));
            Assert.AreEqual(100, state.Count(db.fuel));
            route.totalFuel = price;
            Assert.IsTrue(state.Travel(state.universe.Get("ferrum")));
            Assert.IsFalse(state.Travel(route, glacia));
            Assert.AreEqual(0, state.world.Visits(glacia.Id));
        }

        [Test]
        public void Travel_DenseFuelPaysTheSameRoute_WithNoOrdinaryCells()
        {
            var state = NewState();
            Assert.IsNotNull(db.FindResource(GameState.PlasmaId));
            state.backpack.Set(db.fuel.id, 0);
            state.backpack.Set(GameState.PlasmaId, 20);
            var target = state.universe.Get("cinder");
            var plan = state.PlanRoute(target);
            Assert.IsTrue(plan.reachable);
            var payment = state.Rules.Pay(plan.totalFuel, state.FuelOnBoard);
            Assert.Greater(payment.plasmaCells, 0);
            Assert.IsTrue(state.Travel(plan, target));
            Assert.AreEqual(0, state.Count(db.fuel));
            Assert.AreEqual(20 - payment.plasmaCells, state.backpack.Count(GameState.PlasmaId));
        }

        [Test]
        public void FuelEfficiencyUpgrades_ReduceTheTravelCost()
        {
            var state = NewState();
            var cinder = state.universe.Get("cinder");
            int full = state.FuelCost(cinder);
            foreach (var u in db.upgrades) if (u != null && u.effect == UpgradeEffect.FuelEfficiency) state.DebugSetUpgrade(u, u.maxLevel);
            Assert.Less(state.FuelMultiplier, 1f);
            Assert.Less(state.FuelCost(cinder), full);
            Assert.GreaterOrEqual(state.FuelCost(cinder), 1);
        }

        [Test]
        public void HangarDiscount_ReducesFuelOnlyWhenDepartingItsPlanet()
        {
            var state = NewState();
            var home = state.universe.Home;
            var destination = state.universe.Get("cinder");
            var hangar = db.StructureOfKind(StructureKind.Hangar);
            Assert.IsNotNull(hangar);
            Assert.IsTrue(hangar.Implemented);

            int withoutHangar = state.PlanRoute(home, destination, false).totalFuel;
            var placed = state.DebugPlace(hangar, 0);
            Assert.IsNotNull(placed);
            Assert.AreEqual(hangar.EffectAt(1), state.TravelFuelDiscount(home), 0.0001f);
            int fromHome = state.PlanRoute(home, destination, false).totalFuel;
            Assert.Less(fromHome, withoutHangar);

            state.DebugTeleport(state.universe.Get("glacia"));
            Assert.AreEqual(0f, state.TravelFuelDiscount(state.planet), 0.0001f);
            Assert.AreEqual(0f, state.Ship.discount, 0.0001);
        }

        [Test]
        public void ShieldPylonAndRepairBay_OnlyAffectTheirOperationalPlanet()
        {
            var state = NewState();
            var pylon = db.StructureOfKind(StructureKind.ShieldPylon);
            var repairBay = db.StructureOfKind(StructureKind.RepairBay);
            Assert.IsNotNull(pylon);
            Assert.IsNotNull(repairBay);
            Assert.IsTrue(pylon.Implemented);
            Assert.IsTrue(repairBay.Implemented);
            float baseHealth = state.ReactorMax;

            var placedPylon = state.DebugPlace(pylon, 0);
            var placedBay = state.DebugPlace(repairBay, 1);
            Assert.IsNotNull(placedPylon);
            Assert.IsNotNull(placedBay);
            Assert.AreEqual(baseHealth * (1f + pylon.EffectAt(1)), state.ReactorMax, 0.001f);
            Assert.AreEqual(repairBay.EffectAt(1), state.ReactorRepairPerSecond(state.planet), 0.001f);

            var otherPlanet = state.universe.Get("glacia");
            Assert.AreEqual(0f, state.ReactorRepairPerSecond(otherPlanet), 0.001f);
            state.DebugTeleport(otherPlanet);
            Assert.AreEqual(baseHealth, state.ReactorMax, 0.001f);
        }

        [Test]
        public void MissileSiloAndTeslaCoil_AreWaveDefenseTurrets()
        {
            var state = NewState();
            foreach (var kind in new[] { StructureKind.MissileSilo, StructureKind.TeslaCoil })
            {
                var definition = db.StructureOfKind(kind);
                Assert.IsNotNull(definition);
                Assert.IsTrue(definition.Implemented);
                Assert.IsTrue(kind.IsTurret());
                Assert.Greater(definition.damage, 0f);
                Assert.Greater(definition.fireInterval, 0f);
                Assert.Greater(definition.range, 0f);
                var placed = state.DebugPlace(definition, kind == StructureKind.MissileSilo ? 0 : 1);
                Assert.IsNotNull(placed);
                Assert.AreEqual(definition.damage, state.TurretPower(placed), 0.001f);
            }
        }

        // ---- structures ------------------------------------------------------------------------------------------------------

        [Test]
        public void StructureObjects_StayTheSame_AndTrackTheirRecord()
        {
            var state = NewState();
            var def = db.StructureOfKind(StructureKind.Warehouse);
            var placed = state.DebugPlace(def, 2);
            Assert.IsNotNull(placed);
            Assert.AreSame(placed, state.structures[0]);
            state.DebugPlace(db.StructureOfKind(StructureKind.Refinery), 3);
            state.Notify();
            Assert.AreEqual(2, state.structures.Count);
            Assert.AreSame(placed, state.StructureAt(state.planet, 2), "the same object after the list was rebuilt");
            GiveAllResources(state);
            int upgradeSeconds = state.StructureJobSeconds(placed.def, placed.level + 1);
            Assert.IsTrue(state.UpgradeStructure(placed));
            if (upgradeSeconds > 0)
            {
                clock.Advance(upgradeSeconds);
                state.Tick();
            }
            Assert.AreEqual(2, placed.level);
            Assert.AreEqual(2, state.world.StructureAt(state.planet.Id, 2).level, "the wrapper reads the saved record");
            Assert.AreSame(placed, state.StructureAt(state.planet, 2));
        }

        void GiveAllResources(GameState state)
        {
            var pack = db.FindUpgrade("pack");
            if (pack != null) state.DebugSetUpgrade(pack, pack.maxLevel);
            foreach (var r in db.resources) state.backpack.Set(r.id, 400);
        }

        [Test]
        public void Place_SpendsTheCostAndRespectsLimitsAndPads()
        {
            var state = NewState();
            GiveAllResources(state);
            var refinery = db.StructureOfKind(StructureKind.Refinery);
            var before = new Dictionary<ResourceSO, int>();
            foreach (var c in refinery.buildCost) before[c.resource] = state.Count(c.resource);
            Assert.IsTrue(state.Place(refinery, 4));
            foreach (var c in refinery.buildCost) Assert.AreEqual(before[c.resource] - c.amount, state.Count(c.resource), "the build cost was taken: " + c.resource.id);
            Assert.AreEqual(1, refinery.maxPerPlanet, "the refinery is limited to one per planet");
            Assert.IsTrue(state.AtLimit(state.planet, refinery));
            Assert.IsFalse(state.Place(refinery, 5), "limit per planet");
            Assert.IsFalse(state.Place(db.StructureOfKind(StructureKind.Alarm), 4), "the pad is taken");
            Assert.IsFalse(state.Place(db.StructureOfKind(StructureKind.Alarm), -1));
            Assert.IsFalse(state.Place(db.StructureOfKind(StructureKind.Alarm), state.planet.BuildingSlots), "out of range");
            Assert.AreEqual(1, state.CountOnPlanet(state.planet, refinery));
            Assert.AreEqual(state.planet.BuildingSlots - 1 - state.ObstaclesOn(state.planet).Count, state.FreePads(state.planet),
                "generated obstructions are not advertised as available build pads");
        }

        [Test]
        public void Waves_AreCountedPerWorld_AndInTheStats()
        {
            var state = NewState();
            var home = state.planet;
            Assert.AreEqual(1, state.NextWave(home));
            state.WaveCleared(home);
            state.WaveCleared(home);
            Assert.AreEqual(3, state.NextWave(home));
            Assert.AreEqual(1, state.NextWave(state.universe.Get("glacia")));
            Assert.AreEqual(2, state.totalWavesCleared);
        }

        // ---- the real v1 save ------------------------------------------------------------------------------------------------

        [Test]
        public void ARealV1SaveFile_IsMigratedWhenTheGameStarts()
        {
            string legacy = SaveFormatTests.Fixture("save_v1_midgame.json");
            File.WriteAllText(PrimaryPath, legacy);
            var state = NewState(true);

            Assert.AreEqual(1, state.LoadReport.migratedFrom);
            Assert.AreEqual(SaveSource.Primary, state.LoadReport.source);
            Assert.AreEqual("glacia", state.planet.AuthoredId);
            Assert.AreEqual(212, state.xp);
            Assert.AreEqual(6, state.totalWavesCleared);
            Assert.AreEqual(2, state.Level(db.FindUpgrade("pack")));
            Assert.AreEqual(30, state.Count(db.fuel));
            Assert.AreEqual(89, state.Count(db.scrap));
            Assert.AreEqual(9, state.structures.Count);
            Assert.AreEqual(3, state.world.WavesCleared(state.universe.Get("sylva").Id));
            Assert.AreEqual(1, state.world.WavesCleared(state.universe.Get("glacia").Id));
            Assert.AreEqual(2, state.world.WavesCleared(state.universe.Get("veridian").Id));
            PlacedStructure sylvaWarehouse = null;
            foreach (var s in state.structures) if (s.def.kind == StructureKind.Warehouse && s.planetId == state.universe.Get("sylva").Id) sylvaWarehouse = s;
            Assert.IsNotNull(sylvaWarehouse);
            Assert.AreEqual(50, state.StorageFor(sylvaWarehouse).slots[1].count);
            Assert.AreEqual("scrap", state.StorageFor(sylvaWarehouse).slots[1].resourceId);

            Assert.AreEqual(legacy, File.ReadAllText(Path.Combine(sandbox.Directory, SaveSchema.LegacyBackupName)), "an exact copy of the v1 file is kept");
            Assert.AreEqual(SaveSchema.Current, SaveSerializer.Instance.ReadSchemaVersion(File.ReadAllText(PrimaryPath)), "the primary file is v2 right after migrating");

            var again = NewState(true);
            Assert.AreEqual(0, again.LoadReport.migratedFrom, "a v2 file is never migrated again");
            Assert.AreEqual(state.xp, again.xp);
            Assert.AreEqual(state.planet.Id, again.planet.Id);
            Assert.AreEqual(state.structures.Count, again.structures.Count);
            Assert.AreEqual(state.stats.worldsLanded, again.stats.worldsLanded);
        }

        // ---- damaged and unusual files ---------------------------------------------------------------------------------------

        [Test]
        public void ACorruptPrimary_IsRecoveredFromTheBackup_AndRepaired()
        {
            var a = NewState();
            a.AddXp(10);
            a.Save();
            a.AddXp(5);
            a.Save();
            Assert.AreEqual(15, ReadSave(PrimaryPath).player.xp);
            Assert.AreEqual(10, ReadSave(BackupPath).player.xp);
            File.WriteAllText(PrimaryPath, "{\"schemaVersion\":2,\"player\":{\"xp\":99");

            var b = NewState(true);
            Assert.AreEqual(SaveSource.Backup, b.LoadReport.source);
            Assert.IsTrue(b.LoadReport.recovered);
            StringAssert.Contains("frontier_save.json", b.LoadReport.error);
            Assert.AreEqual(10, b.xp, "the previous good save");
            Assert.AreEqual(10, ReadSave(PrimaryPath).player.xp, "the primary file was repaired right away");
        }

        [Test]
        public void ALeftoverTempFile_IsRecovered_WhenNothingElseIsReadable()
        {
            var a = NewState();
            a.AddXp(7);
            a.Save();
            File.Copy(PrimaryPath, TempPath);
            File.Delete(PrimaryPath);
            if (File.Exists(BackupPath)) File.Delete(BackupPath);

            var b = NewState(true);
            Assert.AreEqual(SaveSource.TempRecovery, b.LoadReport.source);
            Assert.IsTrue(b.LoadReport.recovered);
            Assert.AreEqual(7, b.xp);
        }

        [Test]
        public void NothingReadable_StartsANewGame_AndReportsWhy()
        {
            File.WriteAllText(PrimaryPath, "this is not json");
            var state = NewState(true);
            Assert.AreEqual(SaveSource.NewGame, state.LoadReport.source);
            Assert.IsFalse(string.IsNullOrEmpty(state.LoadReport.error));
            Assert.AreEqual(0, state.xp);
        }

        [Test]
        public void ASaveFromANewerSchema_IsPlayedReadOnly_AndNeverOverwritten()
        {
            var a = NewState();
            a.AddXp(21);
            a.Save();
            string future = File.ReadAllText(PrimaryPath).Replace("\"schemaVersion\":2", "\"schemaVersion\":99");
            StringAssert.Contains("\"schemaVersion\":99", future);
            File.WriteAllText(PrimaryPath, future);

            var b = NewState(true);
            Assert.IsTrue(b.SaveIsReadOnly);
            Assert.IsTrue(b.LoadReport.readOnly);
            Assert.AreEqual(21, b.xp, "the data is still readable");
            b.AddXp(1000);
            Assert.IsFalse(b.Flush());
            Assert.IsFalse(b.Save());
            clock.Advance(30.0);
            Assert.IsFalse(b.Tick());
            StringAssert.Contains("read-only", b.saves.LastError);
            Assert.AreEqual(future, File.ReadAllText(PrimaryPath), "the newer file is untouched");
        }

        [Test]
        public void ASaveFromANewerGenerator_IsReadOnlyToo()
        {
            var a = NewState();
            a.AddXp(4);
            a.Save();
            string future = File.ReadAllText(PrimaryPath).Replace("\"generatorVersion\":1", "\"generatorVersion\":99");
            StringAssert.Contains("\"generatorVersion\":99", future);
            File.WriteAllText(PrimaryPath, future);

            var b = NewState(true);
            Assert.IsTrue(b.SaveIsReadOnly, "its worlds would be generated differently");
            Assert.AreEqual(4, b.xp);
            Assert.IsFalse(b.Save());
            Assert.AreEqual(future, File.ReadAllText(PrimaryPath));
        }

        [Test]
        public void UnknownContent_IsDroppedAndReported_NotFatal()
        {
            var save = new SaveV2 { saveId = "unknown-content", createdWith = new CreatedWithInfo { generatorVersion = 1, universeSeed = SeedText.Format(1UL) } };
            save.player.xp = 5;
            save.player.upgrades.Add(new IdLevel { id = "no-such-upgrade", level = 3 });
            save.player.upgrades.Add(new IdLevel { id = "pack", level = 1 });
            save.player.inventory.Add(new StackRecord { resourceId = "unobtainium", count = 5 });
            save.player.inventory.Add(new StackRecord { resourceId = "wood", count = 7 });
            save.location.currentPlanetId = "p:7:7:7:7";
            var delta = new PlanetDelta { planetId = "p:0:0:1000:0" };
            delta.structures.Add(new StructureRecord { defId = "lumber", slot = 0 });
            delta.structures.Add(new StructureRecord { defId = "ghost-structure", slot = 1 });
            save.worlds.Add(delta);
            Assert.IsTrue(new SaveStore(sandbox.Directory, SaveSerializer.Instance).Write(save));

            var state = NewState(true);
            var warnings = string.Join("\n", state.LoadReport.warnings);
            StringAssert.Contains("no-such-upgrade", warnings);
            StringAssert.Contains("unobtainium", warnings);
            StringAssert.Contains("ghost-structure", warnings);
            StringAssert.Contains("p:7:7:7:7", warnings);
            StringAssert.Contains("another universe", warnings);
            Assert.AreEqual(5, state.xp);
            Assert.AreEqual(1, state.Level(db.FindUpgrade("pack")));
            Assert.AreEqual(7, state.Count(db.FindResource("wood")));
            Assert.AreEqual(state.universe.Home.Id, state.planet.Id, "an unknown world returns the player home");
            Assert.AreEqual(1, state.structures.Count);
            state.Save();
            Assert.AreEqual("unknown-content", ReadSave(PrimaryPath).saveId, "the save id survives");
        }

        [Test]
        public void ReservedSections_AreWrittenBackUnchanged()
        {
            var save = new SaveV2 { saveId = "reserved", createdWith = new CreatedWithInfo { generatorVersion = 1, universeSeed = SeedText.Format(UniverseService.DefaultSeed) } };
            save.wallet.Add(new WalletEntry { currency = "gems", amount = 25 });
            save.builders.Add(new BuilderSlot { slotId = "b1", target = "p:0:0:1000:0#3", busyUntilUtc = "2026-10-14T00:00:00.0000000Z" });
            save.atlas.Add("p:3:0:1001:0");
            save.settings.Add(new SettingEntry { key = "quality", value = "high" });
            Assert.IsTrue(new SaveStore(sandbox.Directory, SaveSerializer.Instance).Write(save));

            var state = NewState(true);
            state.AddXp(1);
            state.Save();
            var back = ReadSave(PrimaryPath);
            Assert.AreEqual(1, back.wallet.Count);
            Assert.AreEqual(25, back.wallet[0].amount);
            Assert.AreEqual("b1", back.builders[0].slotId);
            Assert.AreEqual("p:3:0:1001:0", back.atlas[0]);
            Assert.AreEqual("high", back.settings[0].value);
            Assert.AreEqual("reserved", back.saveId);
        }

        // ---- generated worlds in the session ----------------------------------------------------------------------------------

        [Test]
        public void AGeneratedWorld_CanBeBuiltOn_SavedAndReloaded()
        {
            var a = NewState();
            var generated = a.universe.FindGenerated(Frontier.Universe.Biome.Lush, null);
            Assert.IsNotNull(generated);
            a.DebugTeleport(generated);
            var lumber = db.StructureOfKind(StructureKind.Lumber);
            Assert.IsNotNull(a.DebugPlace(lumber, 3, 2));
            Assert.IsNotNull(a.DebugPlace(db.StructureOfKind(StructureKind.Warehouse), 7));
            a.WaveCleared(generated);
            a.Save();

            var delta = ReadSave(PrimaryPath).worlds.Find(w => w.planetId == generated.Id);
            Assert.IsNotNull(delta, "only the planets the player changed are saved");
            Assert.AreEqual(2, delta.structures.Count);

            var b = NewState(true);
            Assert.AreEqual(generated.Id, b.planet.Id);
            Assert.IsFalse(b.planet.IsAuthored);
            Assert.AreEqual(generated.Name, b.planet.Name);
            Assert.AreEqual(generated.Seed, b.planet.Seed);
            Assert.AreEqual(2, b.StructuresOn(b.planet).Count);
            Assert.AreEqual(2, b.StructureAt(b.planet, 3).level);
            Assert.AreEqual(2, b.NextWave(b.planet));
            Assert.AreEqual(1, b.world.Visits(generated.Id));
        }

        [Test]
        public void OnlyChangedWorlds_AreSaved()
        {
            var state = NewState();
            state.Save();
            Assert.AreEqual(1, ReadSave(PrimaryPath).worlds.Count, "just the home world the player landed on");
            GiveFuel(state, 100);
            state.Travel(state.universe.Get("glacia"));
            state.Save();
            Assert.AreEqual(2, ReadSave(PrimaryPath).worlds.Count);
        }

        [Test]
        public void ResetProgress_DeletesEveryFile_AndStartsFresh()
        {
            File.WriteAllText(Path.Combine(sandbox.Directory, SaveSchema.LegacyBackupName), "{}");
            var state = NewState();
            state.AddXp(5);
            state.Save();
            state.AddXp(5);
            state.Save();
            Assert.IsTrue(File.Exists(BackupPath));
            state.ResetProgress();
            Assert.AreEqual(0, state.xp);
            Assert.IsFalse(File.Exists(BackupPath), "the old backup is gone");
            Assert.IsFalse(File.Exists(Path.Combine(sandbox.Directory, SaveSchema.LegacyBackupName)));
            Assert.AreEqual(0, ReadSave(PrimaryPath).player.xp, "a fresh save is written right away");
        }
    }
}
