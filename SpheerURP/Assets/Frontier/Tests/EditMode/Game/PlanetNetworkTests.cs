using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Frontier.Universe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class PlanetNetworkTests
    {
        SaveSandbox sandbox;
        GameDatabase db;
        [SetUp] public void SetUp() { sandbox = new SaveSandbox(); db = GameDatabase.Load(); }
        [TearDown] public void TearDown() { sandbox.Dispose(); }
        SaveV2 Read() { return JsonUtility.FromJson<SaveV2>(File.ReadAllText(sandbox.SaveFile)).Normalize(); }
        void Write(SaveV2 save) { File.WriteAllText(sandbox.SaveFile, JsonUtility.ToJson(save)); }

        GameState ReadyToLink()
        {
            var state = new GameState(db, false);
            foreach (var upgrade in db.upgrades) state.DebugSetUpgrade(upgrade, upgrade.MaxLevel);
            var home = state.planet;
            state.DebugPlace(db.FindStructure("warp_gate"), 0);
            state.DebugTeleport(state.universe.Get("glacia"));
            state.DebugPlace(db.FindStructure("warp_gate"), 0);
            state.DebugTeleport(home);
            foreach (var cost in state.NetworkCosts(false)) state.backpack.Set(cost.resource.id, cost.amount);
            state.Save();
            var save = Read();
            save.player.researched.Add(state.NetworkRules.linkResearch);
            Write(save);
            return new GameState(db, true);
        }

        [Test]
        public void FuelHasSeparateCapacityAndSaveSection_AndUpgradesArePurchasable()
        {
            var state = new GameState(db, false);
            state.backpack.Clear();
            state.backpack.Set(db.scrap.id, state.Slots * db.scrap.stackSize);
            Assert.AreEqual(state.Slots, state.SlotsUsed);
            Assert.Greater(state.Add(db.fuel, 1), 0, "A full backpack does not fill the ship tanks.");
            Assert.AreEqual(state.Slots, state.SlotsUsed);
            Assert.AreEqual(1000, state.FuelGallons);
            var tank = db.FindUpgrade("fuel_tank");
            state.backpack.Set(db.scrap.id, 0);
            foreach (var cost in tank.CostAt(0)) state.backpack.Set(cost.resource.id, cost.amount);
            Assert.IsNull(state.UpgradeLockReason(tank));
            Assert.IsTrue(state.Buy(tank));
            Assert.AreEqual(6, state.FuelTankSlots);
            state.Save();
            var save = Read();
            Assert.AreEqual(1, save.player.fuelStorageVersion);
            Assert.IsFalse(save.player.inventory.Any(s => state.IsTankFuel(s.resourceId)));
            Assert.AreEqual(1, save.player.fuelTank.Single(s => s.resourceId == db.fuel.id).count);
            Assert.AreEqual(1000, new GameState(db, true).FuelGallons);
        }

        [Test]
        public void LegacyOverfilledFuelMigratesWithoutLoss_AndDoesNotConsumePackSlots()
        {
            var state = new GameState(db, false);
            state.Save();
            var save = Read();
            save.player.fuelStorageVersion = 0;
            save.player.inventory.Clear();
            save.player.fuelTank.Clear();
            save.player.inventory.Add(new StackRecord { resourceId = db.fuel.id, count = 4000 });
            save.player.inventory.Add(new StackRecord { resourceId = "plasma", count = 3000 });
            save.player.inventory.Add(new StackRecord { resourceId = "antimatter", count = 2000 });
            save.player.inventory.Add(new StackRecord { resourceId = db.scrap.id, count = 7 });
            Write(save);
            state = new GameState(db, true);
            Assert.AreEqual(156000000L, state.FuelGallons);
            Assert.AreEqual(1, state.SlotsUsed);
            Assert.Greater(state.FuelTankUsed, state.FuelTankSlots);
            Assert.AreEqual(0, state.FreeSpace(db.fuel));
            Assert.Greater(state.FreeSpace(db.scrap), 0);
            Assert.AreEqual(0, state.Add(db.fuel, 10));
            state.Save();
            state = new GameState(db, true);
            Assert.AreEqual(4000, state.Count(db.fuel));
            Assert.AreEqual(3000, state.Count(db.FindResource("plasma")));
            Assert.AreEqual(2000, state.Count(db.FindResource("antimatter")));
            Assert.AreEqual(1, state.SlotsUsed);
            Assert.AreEqual(1, Read().player.inventory.Count);
        }

        [Test]
        public void CraftingAcrossPackAndTanksIsAtomic_AndDoesNotOverfillEitherPool()
        {
            var inventory = new Inventory(() => 1, _ => 10, id => id == "fuel", () => 1);
            inventory.Set("wood", 10);
            inventory.Set("fuel", 10);
            var inputs = new[] { new IdAmount("wood", 1) };
            var outputs = new[] { new IdAmount("fuel", 1) };
            Assert.IsFalse(inventory.Craft(inputs, outputs));
            Assert.AreEqual(10, inventory.Count("wood"));
            Assert.AreEqual(10, inventory.Count("fuel"));
            inventory.Remove("fuel", 1);
            Assert.IsTrue(inventory.Craft(inputs, outputs));
            Assert.AreEqual(9, inventory.Count("wood"));
            Assert.AreEqual(10, inventory.Count("fuel"));
            inventory.Set("fuel", 100);
            Assert.IsFalse(inventory.Craft(inputs, outputs), "An overfilled old reserve must not accept new fuel within a partial rack.");
            Assert.IsTrue(inventory.Craft(new[] { new IdAmount("wood", 1) }, new[] { new IdAmount("wood", 1) }));
            Assert.AreEqual(100, inventory.Count("fuel"), "Legacy reserves cannot block unrelated crafting.");
        }

        [Test]
        public void LegacyTravelRetainsItsDepartureCargoSurchargeAcrossFuelMigration()
        {
            var state = ReadyToLink();
            state.backpack.Set(db.fuel.id, 100);
            var target = state.universe.Get("glacia");
            var caps = state.Ship.Clone();
            caps.cargoFill = Math.Min(1, (state.SlotsUsed + state.FuelTankUsed) / (double)state.Slots);
            var oldPlan = state.Planner.Plan(state.universe.EndpointOf(state.planet), state.universe.EndpointOf(target), caps, true, state.FuelOnBoard);
            Assert.IsTrue(oldPlan.reachable);
            state.Save();
            var save = Read();
            save.player.fuelStorageVersion = 0;
            save.player.inventory.AddRange(save.player.fuelTank);
            save.player.fuelTank.Clear();
            save.travelPending = true;
            save.travel = new TravelCheckpoint { plan = oldPlan };
            foreach (var leg in oldPlan.legs)
                save.travel.legs.Add(new TravelLegCheckpoint { seed = "0000000000000001", autopilotRemaining = leg.seconds });
            Write(save);
            state = new GameState(db, true);
            Assert.IsTrue(state.ValidatePendingTravel(out _, out string error), error);
            Assert.AreEqual(caps.cargoFill, state.CargoFill);
            state = new GameState(db, true);
            Assert.IsTrue(state.ValidatePendingTravel(out _, out error), error);
        }

        [Test]
        public void QuickStorageKeepsShipFuelOutOfOrdinaryCargo_AndDepotsRefillTanksWithoutCargoSpace()
        {
            var state = new GameState(db, false);
            state.backpack.Clear();
            var warehouse = state.DebugPlace(db.FindStructure("warehouse"), 0);
            var depot = state.DebugPlace(db.FindStructure("fuel_depot"), 1);
            state.Add(db.fuel, 10);
            Assert.AreEqual(0, state.QuickStore(warehouse));
            Assert.AreEqual(10000, state.FuelGallons);
            Assert.AreEqual(10, state.QuickStore(depot));
            Assert.AreEqual(0, state.FuelGallons);
            state.backpack.Set(db.scrap.id, state.Slots * db.scrap.stackSize);
            Assert.AreEqual(10, state.QuickWithdraw(depot));
            Assert.AreEqual(10000, state.FuelGallons);
            Assert.AreEqual(state.Slots, state.SlotsUsed);
        }

        [Test]
        public void LinkCostsArePaidOnce_ConnectionsAreUndirectedAndTransitive_TravelIsFreeAndDurable()
        {
            var state = ReadyToLink();
            var target = state.universe.Get("glacia");
            var home = state.planet;
            var unlinked = state.universe.AuthoredPlanets.First(p => p.Id != home.Id && p.Id != target.Id);
            Assert.IsFalse(state.PlanRoute(target).instant);
            Assert.IsTrue(state.TryLinkPlanet(target, out string error), error);
            Assert.AreEqual(1, state.PlanetLinks.Count());
            foreach (var cost in state.NetworkCosts(false)) Assert.AreEqual(0, state.Count(cost.resource));
            Assert.IsFalse(state.TryLinkPlanet(target, out _));
            Assert.AreEqual(1, state.PlanetLinks.Count());
            var connectedSave = Read();
            Assert.AreEqual(1, connectedSave.network.links.Count, "A link is durable before it is published.");
            connectedSave.network.links.Add(new PlanetLink { fromPlanetId = target.Id, toPlanetId = unlinked.Id });
            Write(connectedSave);
            state = new GameState(db, true);
            Assert.IsTrue(state.HasPlanetLink(home, unlinked));
            Assert.IsTrue(state.HasPlanetLink(unlinked, home));
            state.backpack.Set(db.fuel.id, 0);
            state.backpack.Set("plasma", 0);
            state.backpack.Set("antimatter", 0);
            var plan = state.PlanRoute(target, true);
            Assert.IsTrue(plan.reachable);
            Assert.IsTrue(plan.instant);
            Assert.AreEqual(0, plan.totalFuel);
            Assert.AreEqual(0, plan.totalSeconds);
            Assert.AreEqual(0, plan.LegCount, "Gate travel has no mini-game legs or timer checkpoint.");
            Assert.IsFalse(state.BeginTravelCheckpoint(plan, out _));
            Assert.IsTrue(state.Travel(plan, target));
            Assert.IsNull(state.PendingTravel);
            state = new GameState(db, true);
            Assert.AreEqual(target.Id, state.planet.Id);
            Assert.AreEqual(0, state.FuelGallons);
            Assert.IsTrue(state.PlanRoute(home).instant);
        }

        [Test]
        public void FailedLinkWriteDoesNotSpendOrPublish_AndRetryDoesNotDoubleSpend()
        {
            var state = ReadyToLink();
            var target = state.universe.Get("glacia");
            string blocked = sandbox.SaveFile + ".tmp";
            Directory.CreateDirectory(blocked);
            LogAssert.Expect(LogType.Warning, new Regex("Frontier save failed:"));
            Assert.IsFalse(state.TryLinkPlanet(target, out string error));
            StringAssert.Contains("Nothing was spent", error);
            Assert.IsEmpty(state.PlanetLinks);
            foreach (var cost in state.NetworkCosts(false)) Assert.AreEqual(cost.amount, state.Count(cost.resource));
            Directory.Delete(blocked);
            Assert.IsTrue(state.TryLinkPlanet(target, out error), error);
            Assert.AreEqual(1, new GameState(db, true).PlanetLinks.Count());
        }

        [Test]
        public void LinksRequireBothCompletedGatesAndCannotBypassLandingProtection()
        {
            var state = ReadyToLink();
            var target = state.universe.Get("glacia");
            var gate = state.StructureOfKind(target, StructureKind.WarpGate);
            gate.record.buildEndUtc = UtcText.Format(DateTime.UtcNow.AddDays(1));
            Assert.IsFalse(state.TryLinkPlanet(target, out _));
            gate.record.buildEndUtc = "";
            Assert.IsTrue(state.TryLinkPlanet(target, out _));
            foreach (var upgrade in db.upgrades)
                if (upgrade.effect == UpgradeEffect.HullPlating) state.DebugSetUpgrade(upgrade, 0);
            bool previous = FrontierSettings.I.enforceHazardPlating;
            FrontierSettings.I.enforceHazardPlating = true;
            try
            {
                Assert.AreNotEqual(Hazards.None, target.Hazards);
                var plan = state.PlanRoute(target);
                Assert.IsTrue(plan.instant);
                Assert.IsFalse(plan.reachable);
                Assert.IsTrue(plan.Has(RouteReasons.Hazard));
                Assert.IsFalse(state.TryInstantTravel(target, out _));
            }
            finally { FrontierSettings.I.enforceHazardPlating = previous; }
        }

        IEnumerable<PlanetData> SearchWorlds(GameState state)
        {
            foreach (var p in state.universe.AuthoredPlanets) yield return p;
            foreach (int distance in new[] { 0, 2, 10, 30, 100, 300 })
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                        foreach (var system in state.universe.Universe.SystemsInSector(new SectorCoord(distance + x, y)))
                            foreach (var p in state.universe.PlanetsOf(system)) yield return p;
        }

        GameState ReadyArchive()
        {
            var state = ReadyToLink();
            var worlds = SearchWorlds(state).ToList();
            var archive = worlds.First(p => p.BuildingSlots >= 48 && p.Hazards == Hazards.None);
            state.DebugTeleport(archive);
            state.DebugPlace(db.FindStructure("warp_gate"), 0);
            state.DebugPlace(db.FindStructure("cargo_depot"), 1);
            for (int i = 0; i < state.NetworkWarehouseRequirement; i++)
                state.DebugPlace(db.FindStructure("warehouse"), i + 2, 10);
            state.Save();
            var save = Read();
            save.player.researched.Add(state.NetworkRules.finaleResearch);
            var used = new HashSet<string> { archive.Id };
            foreach (string id in state.NetworkRules.resourceWorlds)
            {
                var source = worlds.First(p => !used.Contains(p.Id) && p.Descriptor.Deposits.Any(d => d.ResourceId == id));
                used.Add(source.Id);
                save.network.links.Add(new PlanetLink { fromPlanetId = archive.Id, toPlanetId = source.Id });
            }
            Write(save);
            state = new GameState(db, true);
            state.backpack.Clear();
            var warehouses = state.StructuresOn(state.planet).Where(s => s.def.kind == StructureKind.Warehouse).ToList();
            foreach (var cost in state.NetworkCosts(true))
            {
                state.backpack.Set(cost.resource.id, cost.amount);
                int moved = 0;
                foreach (var warehouse in warehouses) moved += state.QuickStore(warehouse, null, false);
                Assert.AreEqual(cost.amount, moved, "The actual final shipment must fit on the authored warehouse world.");
                Assert.AreEqual(0, state.Count(cost.resource));
            }
            state.Notify();
            return state;
        }

        [Test]
        public void FinaleNeedsDistinctSupplyWorldsAndAWarehouseWorld_ConsumesShipmentOnce_AndUnlocksOptionalPrestige()
        {
            var state = ReadyArchive();
            Assert.AreEqual(5, state.NetworkAssignedWorlds().Where(id => id != null).Distinct().Count());
            Assert.IsFalse(state.NetworkComplete);
            Assert.IsFalse(state.AscensionAvailable);
            Assert.IsNull(state.NetworkCompletionLockReason());
            var warehouse = state.StructuresOn(state.planet).First(s => s.def.kind == StructureKind.Warehouse);
            warehouse.record.buildEndUtc = UtcText.Format(DateTime.UtcNow.AddDays(1));
            StringAssert.Contains("warehouses", state.NetworkCompletionLockReason());
            Assert.IsFalse(state.TryCompleteNetwork(out _));
            warehouse.record.buildEndUtc = "";
            // A separate missing-world check must not depend on spending any resources.
            state.Save();
            var save = Read();
            var removed = save.network.links[save.network.links.Count - 1];
            save.network.links.RemoveAt(save.network.links.Count - 1);
            Write(save);
            var missing = new GameState(db, true);
            Assert.IsNotNull(missing.NetworkCompletionLockReason());
            Assert.IsFalse(missing.TryCompleteNetwork(out _));
            save.network.links.Add(removed);
            Write(save);
            state = new GameState(db, true);
            string origin = state.planet.Id;
            string blocked = sandbox.SaveFile + ".tmp";
            Directory.CreateDirectory(blocked);
            LogAssert.Expect(LogType.Warning, new Regex("Frontier save failed:"));
            Assert.IsFalse(state.TryCompleteNetwork(out string writeError));
            StringAssert.Contains("Nothing was spent", writeError);
            Assert.IsFalse(state.NetworkComplete);
            foreach (var cost in state.NetworkCosts(true)) Assert.AreEqual(cost.amount, state.PlanetCount(cost.resource));
            Directory.Delete(blocked);
            Assert.IsTrue(state.TryCompleteNetwork(out string error), error);
            Assert.IsTrue(state.NetworkComplete);
            Assert.AreEqual(origin, state.NetworkArchiveId);
            foreach (var cost in state.NetworkCosts(true)) Assert.AreEqual(0, state.PlanetCount(cost.resource));
            Assert.IsFalse(state.TryCompleteNetwork(out _));
            Assert.IsTrue(state.AscensionAvailable);
            Assert.AreEqual(origin, state.planet.Id, "Completion must never automatically reset the player's world.");
            state = new GameState(db, true);
            Assert.IsTrue(state.NetworkComplete);
            var visited = state.universe.AuthoredPlanets.First(p => p.Id != state.planet.Id && !state.HasPlanetLink(state.planet, p));
            state.world.RecordVisit(visited.Id, DateTime.UtcNow);
            state.Save();
            Assert.IsTrue(state.PlanRoute(visited).instant, "The finale adds lightspeed to visited worlds outside the network.");
            Assert.IsTrue(state.TryAscend(out error), error);
            Assert.IsFalse(state.NetworkComplete);
            Assert.IsEmpty(state.PlanetLinks);
            Assert.AreEqual(1.05f, state.LegacyHarvestMultiplier, 0.0001f);
            Assert.IsTrue(new GameState(db, true).LegacyHarvestMultiplier > 1);
        }
    }
}
