using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Frontier.Economy;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Tests
{
    public sealed class EconomyRuntimeTests
    {
        GameDatabase db, imported, legacy;
        SaveSandbox sandbox;

        [SetUp]
        public void SetUp()
        {
            sandbox = new SaveSandbox();
            imported = Resources.Load<GameDatabase>("Frontier/EconomyDatabase");
            legacy = GameDatabase.LoadLegacy();
            db = GameDatabase.Load();
            Assert.IsTrue(db.UsesImportedEconomy);
        }

        [TearDown]
        public void TearDown() { sandbox.Dispose(); }

        [Test]
        public void ActiveSelection_IsCachedValidatedAndDoesNotMutateAuthoredAssets()
        {
            Assert.AreSame(db, GameDatabase.Load());
            Assert.AreSame(imported, db.ContentSource);
            Assert.AreSame(db, GameDatabase.ForRuntime(imported), "an Inspector-assigned import must not bypass the adapter");
            Assert.AreSame(legacy, GameDatabase.ForRuntime(legacy), "explicit legacy overrides remain supported");
            Assert.IsTrue(GameDatabase.CanActivate(imported, legacy, out var reason), reason);
            Assert.AreEqual(16, db.ToCatalog().upgrades.Length, "legacy tools are not catalog upgrades");
            Assert.AreEqual(19, db.upgrades.Length);
            Assert.AreEqual(30, db.planets.Length);
            foreach (var planet in db.planets)
            {
                var source = imported.FindPlanet(planet.id);
                Assert.AreNotSame(source, planet);
                Assert.AreEqual(source.spots.Count(s => db.FindResource(db.ResolveResourceId(s.resource.id)).IsRaw), planet.spots.Count);
                foreach (var spot in planet.spots)
                {
                    Assert.AreSame(db.FindResource(spot.resource.id), spot.resource);
                    Assert.IsTrue(spot.resource.IsRaw, "crafted fuel must not be harvested");
                }
            }
            foreach (var enemy in db.enemies)
                foreach (var bounty in enemy.bounty) Assert.Contains(bounty.resource, db.resources);
            Assert.AreSame(legacy.startPlanet, imported.startPlanet);
            Assert.AreSame(legacy.formula, db.formula);
            var table = UniverseService.BuildResourceTable(db, false);
            Assert.IsFalse(table.Defs.Any(r => r.Id == "fuel"));
            Assert.IsTrue(table.Defs.Any(r => r.Id == "copper_ore"));
            foreach (var recipe in db.recipes)
            {
                Assert.NotNull(db.FindStructure(recipe.stationId));
                foreach (var amount in recipe.inputs.Concat(recipe.outputs)) Assert.Contains(amount.resource, db.resources);
            }
            foreach (string tool in new[] { "axe", "pick", "drill" })
            {
                var upgrade = db.FindUpgrade(tool);
                Assert.NotNull(upgrade);
                Assert.AreEqual(UpgradeEffect.ToolPower, upgrade.effect);
                for (int i = 0; i < upgrade.MaxLevel; i++)
                    foreach (var cost in upgrade.CostAt(i)) Assert.Contains(cost.resource, db.resources);
            }
        }

        [TestCase("missing")]
        [TestCase("version")]
        [TestCase("resource")]
        [TestCase("world")]
        [TestCase("enemy")]
        [TestCase("formula")]
        [TestCase("rules")]
        [TestCase("start")]
        [TestCase("aliases")]
        public void IncompleteImport_FallsBackWithoutMutatingLegacy(string fault)
        {
            var bad = Object.Instantiate(imported);
            LegacyIdMapSO aliases = null;
            try
            {
                if (fault == "version") bad.schemaVersion = 99;
                if (fault == "resource") bad.resources = bad.resources.Skip(1).ToArray();
                if (fault == "world") bad.planets = bad.planets.Skip(1).ToArray();
                if (fault == "enemy") bad.enemies = new EnemySO[0];
                if (fault == "formula") bad.formula = null;
                if (fault == "rules") bad.habitats = null;
                if (fault == "start") bad.startingItems = new List<ItemAmount> { new ItemAmount(bad.fuel, int.MaxValue) };
                if (fault == "aliases")
                {
                    aliases = Object.Instantiate(bad.legacyIds);
                    aliases.rows = aliases.rows.Where(x => x.legacyId != "alloy").ToArray();
                    bad.legacyIds = aliases;
                }
                var selected = GameDatabase.Select(fault == "missing" ? null : bad, legacy, out var reason);
                Assert.AreSame(legacy, selected);
                Assert.IsNotEmpty(reason);
                Assert.IsFalse(selected.UsesImportedEconomy);
                Assert.NotNull(selected.FindResource("alloy"), "fallback retains original ids");
            }
            finally { Object.DestroyImmediate(bad); if (aliases != null) Object.DestroyImmediate(aliases); }
        }

        [Test]
        public void MissingBothAssets_UsesDefaultContent()
        {
            var fallback = GameDatabase.Select(null, null, out var reason);
            Assert.IsNotEmpty(reason);
            Assert.NotNull(fallback.startPlanet);
            Assert.NotNull(fallback.FindResource("wood"));
            Assert.IsFalse(fallback.UsesImportedEconomy);
            Object.DestroyImmediate(fallback);
        }

        [Test]
        public void FreshSession_UsesImportedStartAndCumulativeEffects()
        {
            var state = new GameState(db, false);
            foreach (var start in imported.startingItems) Assert.AreEqual(start.amount, state.Count(start.resource));
            Assert.LessOrEqual(state.SlotsUsed, state.Slots);
            Assert.AreEqual(imported.baseBackpackSlots, state.Slots);
            var pack = db.FindUpgrade("pack");
            state.DebugSetUpgrade(pack, 1);
            Assert.AreEqual(pack.ValueAt(1), state.Slots, "catalog backpack values are totals, not additive");
            var engine = db.FindUpgrade("engine");
            state.DebugSetUpgrade(engine, 1);
            Assert.AreEqual(engine.ValueAt(1), state.FuelMultiplier, 0.00001);
            var turret = db.FindUpgrade("turretdmg");
            state.DebugSetUpgrade(turret, 2);
            Assert.AreEqual(turret.ValueAt(2), state.EffectSum(UpgradeEffect.TurretDamage), 0.00001);
            var refinery = state.DebugPlace(db.FindStructure("refinery"), 0);
            Assert.AreEqual(0, state.StructureBonus(refinery), "batch size is not an output multiplier");
        }

        [Test]
        public void Terraformer_AppliesGlobalHarvestBonusOnlyWhileOperational()
        {
            var state = new GameState(db, false);
            var terraformer = db.StructureOfKind(StructureKind.Terraformer);
            Assert.IsNotNull(terraformer);
            Assert.IsTrue(terraformer.Implemented);

            var first = state.DebugPlace(terraformer, 0, 3);
            Assert.AreEqual(3, state.TerraformerStageOn(state.planet));
            Assert.AreEqual(0.18f, state.TerraformerHarvestBonus, 0.00001f);

            var remoteDefinition = db.planets.First(p => p != null && p.id != state.planet.AuthoredId);
            PlanetData remote = state.universe.Get(remoteDefinition.id);
            state.DebugTeleport(remote);
            var second = state.DebugPlace(terraformer, 0, 2);
            Assert.AreEqual(2, state.TerraformerStageOn(remote));
            Assert.AreEqual(0.30f, state.TerraformerHarvestBonus, 0.00001f,
                "terraforming effects stack across worlds");

            second.record.buildEndUtc = UtcText.Format(state.clock.UtcNow.AddHours(1));
            Assert.AreEqual(0, state.TerraformerStageOn(remote));
            Assert.AreEqual(0.18f, state.TerraformerHarvestBonus, 0.00001f,
                "a project under construction has no stage effect");
            Assert.NotNull(first);
        }

        [Test]
        public void UnimplementedAndResearchContent_CannotBePurchasedWithFreeMaterials()
        {
            var state = new GameState(db, false);
            foreach (var resource in db.resources) state.backpack.Set(resource.id, 100000);
            var orbitalRelay = db.StructureOfKind(StructureKind.Satellite);
            Assert.IsNotNull(orbitalRelay);
            Assert.IsTrue(orbitalRelay.Implemented);
            Assert.AreEqual("Requires research: orbital logistics", state.BuildLockReason(orbitalRelay));
            foreach (var structure in db.structures)
                if (!structure.Implemented || !string.IsNullOrEmpty(structure.unlockResearch))
                {
                    Assert.IsNotNull(state.BuildLockReason(structure), structure.id);
                    Assert.IsFalse(state.Place(structure, 0), structure.id);
                }
            foreach (var upgrade in imported.upgrades)
            {
                var row = upgrade.NextRow(0);
                if (string.IsNullOrEmpty(row.researchPrereq) && row.requiresReachTier <= state.PlayerLevel &&
                    upgrade.effect != UpgradeEffect.StackMultiplier) continue;
                Assert.IsNotNull(state.UpgradeLockReason(upgrade), upgrade.id);
                Assert.IsFalse(state.Buy(upgrade), upgrade.id);
                Assert.AreEqual(0, state.Level(upgrade));
            }
            Assert.IsFalse(state.Craft(imported.recipes[0]), "no crafting without a station");
        }

        [Test]
        public void CargoDepotPoolsOrdinaryPlanetWarehousesForCraftingAndConstruction()
        {
            var state = new GameState(db, false);
            state.backpack.Clear();
            var warehouseA = state.DebugPlace(db.FindStructure("warehouse"), 1);
            var warehouseB = state.DebugPlace(db.FindStructure("warehouse"), 2);
            var refinery = state.DebugPlace(db.FindStructure("refinery"), 3);
            Assert.NotNull(warehouseA);
            Assert.NotNull(warehouseB);
            Assert.NotNull(refinery);

            var wood = db.FindResource("wood");
            state.backpack.Set(wood.id, 1);
            Assert.AreEqual(1, state.Deposit(warehouseA, 0, wood, 1));
            state.backpack.Set(wood.id, 1);
            Assert.AreEqual(1, state.Deposit(warehouseB, 0, wood, 1));
            var planksRecipe = db.recipes.First(recipe => recipe.id == "saw_planks");
            Assert.IsFalse(state.HasPlanetResources(new[] { new IdAmount(wood.id, 2) }),
                "warehouse contents are not available to costs before a Cargo Depot is operational");
            Assert.IsFalse(state.Craft(planksRecipe), "crafting remains backpack-only without an operational Cargo Depot");

            var cargoDepot = state.DebugPlace(db.FindStructure("cargo_depot"), 0);
            Assert.NotNull(cargoDepot);
            Assert.IsTrue(state.HasPlanetResources(new[] { new IdAmount(wood.id, 2) }));
            Assert.AreEqual(2, state.PlanetCount(wood));
            Assert.AreEqual(0, state.Count(wood), "pooled stock is not carried fuel or backpack cargo");
            Assert.IsFalse(state.Has(new List<ItemAmount> { new ItemAmount(wood, 2) }),
                "research and other backpack-only purchases must not treat warehouse stock as carried resources");
            Assert.IsTrue(state.Craft(planksRecipe), "craft inputs can be taken across two warehouses");
            Assert.AreEqual(1, state.Count(db.FindResource("planks")));
            Assert.AreEqual(0, WarehouseOps.Held(state.StorageFor(warehouseA), wood.id) +
                WarehouseOps.Held(state.StorageFor(warehouseB), wood.id));

            var alarm = db.FindStructure("alarm");
            foreach (var item in alarm.buildCost)
            {
                state.backpack.Set(item.resource.id, item.amount);
                Assert.AreEqual(item.amount, state.Deposit(warehouseA, 1, item.resource, item.amount));
            }
            int openPad = -1;
            for (int pad = 0; pad < state.planet.BuildingSlots; pad++)
                if (state.StructureAt(state.planet, pad) == null && !state.ObstacleAt(state.planet, pad).HasValue)
                {
                    openPad = pad;
                    break;
                }
            Assert.GreaterOrEqual(openPad, 0);
            Assert.IsTrue(state.Place(alarm, openPad), "construction costs can be paid from the pooled warehouses");
            Assert.AreEqual(0, WarehouseOps.Held(state.StorageFor(warehouseA), alarm.buildCost[0].resource.id));
        }

        [Test]
        public void CargoDepotPoolExcludesFuelDepotAndOtherPlanets()
        {
            var state = new GameState(db, false);
            state.backpack.Clear();
            state.DebugPlace(db.FindStructure("cargo_depot"), 0);
            var warehouse = state.DebugPlace(db.FindStructure("warehouse"), 1);
            var fuelDepot = state.DebugPlace(db.FindStructure("fuel_depot"), 2);
            var autoHarvester = state.DebugPlace(db.FindStructure("auto_harvester"), 3);
            var wood = db.FindResource("wood");
            var stone = db.FindResource("stone");
            var fuel = db.fuel;
            state.backpack.Set(wood.id, 1);
            Assert.AreEqual(1, state.Deposit(warehouse, 0, wood, 1));
            state.backpack.Set(stone.id, 1);
            Assert.AreEqual(1, state.Deposit(autoHarvester, 0, stone, 1));
            state.backpack.Set(fuel.id, 1);
            Assert.AreEqual(1, state.Deposit(fuelDepot, 0, fuel, 1));
            Assert.IsTrue(state.HasPlanetResources(new[] { new IdAmount(wood.id, 1) }));
            Assert.IsTrue(state.HasPlanetResources(new[] { new IdAmount(stone.id, 1) }),
                "the Cargo Depot pool includes an Auto-Harvester's output hopper");
            Assert.IsFalse(state.HasPlanetResources(new[] { new IdAmount(fuel.id, 1) }),
                "Fuel Depot reserves remain isolated from the Cargo Depot pool");

            state.DebugTeleport(state.universe.Get("glacia"));
            Assert.IsFalse(state.HasPlanetResources(new[] { new IdAmount(wood.id, 1) }),
                "a Cargo Depot never pools warehouses on a different planet");
            Assert.IsFalse(state.HasPlanetResources(new[] { new IdAmount(stone.id, 1) }),
                "Auto-Harvester output remains local to its planet");
        }

        [Test]
        public void BackpackAndCargoBootstrapCanBeFundedFromCarriedCargoWithoutAds()
        {
            var clock = new FakeClock();
            var state = new GameState(db, false, clock);
            var pack = db.FindUpgrade("pack");
            state.backpack.Clear();
            for (int level = 0; level < 3; level++)
            {
                if (level == 2)
                {
                    BuildCarriedBootstrapStructure(state, clock, "refinery");
                    BuildCarriedBootstrapStructure(state, clock, "research_lab");
                    ResearchCarriedPrerequisites(state, db.research.First(node => node.data.id == "smelting"));
                    ResearchCarriedPrerequisites(state, db.research.First(node => node.data.id == "fabrication"));
                    BuildCarriedBootstrapStructure(state, clock, "foundry");
                    BuildCarriedBootstrapStructure(state, clock, "fabricator");
                }
                FundCarriedCostWithinCapacity(state, pack.CostAt(level));
                Assert.IsTrue(state.CanBuy(pack), state.UpgradeLockReason(pack));
                Assert.IsTrue(state.Buy(pack));
                Assert.AreEqual(level + 1, state.Level(pack));
            }
            Assert.AreEqual(36, state.Slots);

            var cargo = db.research.First(node => node.data.id == "cargo_logistics");
            var lab = state.StructureOfKind(state.planet, StructureKind.ResearchLab);
            FundCarriedCostWithinCapacity(state, lab.def.UpgradeCost(lab.level));
            Assert.IsTrue(state.UpgradeStructure(lab));
            clock.Advance(state.BuilderJobSecondsRemaining(lab));
            state.Tick();
            state.DebugSetLevel(2);
            ResearchCarriedPrerequisites(state, cargo);
            FundCarriedCostWithinCapacity(state, db.FindStructure("cargo_depot").buildCost);
            Assert.IsFalse(state.CargoDepotActive);
            int openPad = Enumerable.Range(0, state.planet.BuildingSlots).First(pad =>
                state.StructureAt(state.planet, pad) == null && !state.ObstacleAt(state.planet, pad).HasValue);
            Assert.IsTrue(state.Place(db.FindStructure("cargo_depot"), openPad),
                "constructing the first Depot must not depend on its own storage pool");
            Assert.IsTrue(state.Save());
            var loaded = new GameState(db, true);
            Assert.AreEqual(3, loaded.Level(pack));
            Assert.IsTrue(loaded.IsResearched(cargo.data.id));
            Assert.AreEqual(0, loaded.SlotsUsed);
            Assert.IsNotNull(lab);
        }

        static void BuildCarriedBootstrapStructure(GameState state, FakeClock clock, string id)
        {
            var definition = state.db.FindStructure(id);
            FundCarriedCostWithinCapacity(state, definition.buildCost);
            int pad = Enumerable.Range(0, state.planet.BuildingSlots).First(index =>
                state.StructureAt(state.planet, index) == null && !state.ObstacleAt(state.planet, index).HasValue);
            Assert.IsTrue(state.Place(definition, pad), state.BuildLockReason(definition));
            clock.Advance(state.StructureJobSeconds(definition, 1));
            state.Tick();
            Assert.IsNotNull(state.StructureOfKind(state.planet, definition.kind));
        }

        static void ResearchCarriedPrerequisites(GameState state, ResearchNodeSO node)
        {
            if (state.IsResearched(node.data.id)) return;
            foreach (string prerequisite in node.data.prereqs)
                ResearchCarriedPrerequisites(state, state.db.research.First(n => n.data.id == prerequisite));
            FundCarriedCostWithinCapacity(state, node.data.cost.Select(item =>
                new ItemAmount(state.db.ResolveResource(item.resourceId), item.amount)).ToList());
            Assert.IsTrue(state.Research(node), node.data.id + ": " + state.ResearchLockReason(node));
        }

        static void FundCarriedCostWithinCapacity(GameState state, List<ItemAmount> cost)
        {
            Assert.AreEqual(0, state.SlotsUsed);
            foreach (var item in cost)
                Assert.AreEqual(item.amount, state.backpack.Add(item.resource.id, item.amount),
                    "the test must not use over-capacity debug inventory");
            Assert.LessOrEqual(state.SlotsUsed, state.Slots);
        }

        [Test]
        public void ImportedLegacyToolsCannotBePurchasedButOwnedEffectsSurviveReload()
        {
            var state = new GameState(db, false);
            foreach (var upgrade in db.upgrades.Where(u => u.effect == UpgradeEffect.ToolPower))
            {
                state.backpack.Clear();
                FundCarriedCostWithinCapacity(state, upgrade.CostAt(0));
                Assert.IsFalse(state.CanBuy(upgrade), upgrade.id);
                Assert.IsFalse(state.Buy(upgrade), upgrade.id);
                Assert.AreEqual(0, state.Level(upgrade));
                state.DebugSetUpgrade(upgrade, 2);
            }
            float axe = state.ToolMultiplier(MinigameKind.Chop);
            float mining = state.ToolMultiplier(MinigameKind.Mine);
            Assert.Greater(axe, 1f);
            Assert.Greater(mining, 1f);
            Assert.IsTrue(state.Save());
            var loaded = new GameState(db, true);
            Assert.AreEqual(axe, loaded.ToolMultiplier(MinigameKind.Chop));
            Assert.AreEqual(mining, loaded.ToolMultiplier(MinigameKind.Mine));
            foreach (var upgrade in db.upgrades.Where(u => u.effect == UpgradeEffect.ToolPower))
                Assert.AreEqual(2, loaded.Level(upgrade));
        }

        [Test]
        public void OrbitalRelay_WithdrawsFromVisitedSameSystemStorageForCarriedEnergy()
        {
            var state = new GameState(db, false);
            var home = state.planet;
            var relay = state.DebugPlace(db.FindStructure("orbital_relay"), 0);
            Assert.IsNotNull(relay);
            Assert.IsTrue(relay.def.Implemented);

            var sameSystemWorlds = state.universe.PlanetsOf(home.Star);
            var remote = sameSystemWorlds.FirstOrDefault(p => p.Id != home.Id);
            Assert.IsNotNull(remote, "the home system has another world for a same-system logistics fixture");
            state.DebugTeleport(remote);
            var warehouse = state.DebugPlace(db.FindStructure("warehouse"), 0);
            Assert.IsNotNull(warehouse);
            var wood = db.FindResource("wood");
            state.backpack.Set(wood.id, 20);
            Assert.AreEqual(20, state.Deposit(warehouse, 0, wood, 20));
            state.DebugTeleport(home);
            state.backpack.Set(db.fuel.id, 10);

            Assert.Contains(warehouse, state.RelayWarehouses(relay));
            Assert.AreEqual(2, state.RelayTransferEnergyCost(relay, warehouse, 20),
                "same-system pulls cost one energy per ten items at relay level one");
            Assert.AreEqual(20, state.WithdrawThroughRelay(relay, warehouse, 0, 20));
            Assert.AreEqual(20, state.Count(wood));
            Assert.AreEqual(8, state.Count(db.fuel), "relay energy is paid from carried fuel");
            Assert.AreEqual(0, WarehouseOps.Held(state.StorageFor(warehouse), wood.id));
        }

        [Test]
        public void OrbitalRelay_UpgradesExtendBeamButFuelDepotReservesStayPrivate()
        {
            var state = new GameState(db, false);
            var relay = state.DebugPlace(db.FindStructure("orbital_relay"), 0);
            var remote = state.universe.FindGenerated(_ => true);
            Assert.IsNotNull(remote);
            Assert.AreNotEqual(state.planet.Star.Id, remote.Star.Id);

            state.DebugTeleport(remote);
            var warehouse = state.DebugPlace(db.FindStructure("warehouse"), 0);
            var fuelDepot = state.DebugPlace(db.FindStructure("fuel_depot"), 1);
            Assert.IsNotNull(warehouse);
            Assert.IsNotNull(fuelDepot);
            state.DebugTeleport(state.universe.Home);
            Assert.IsFalse(state.RelayWarehouses(relay).Contains(warehouse),
                "level one only links storage within the same system");

            state.DebugSetStructureLevel(relay, 10);
            Assert.Contains(warehouse, state.RelayWarehouses(relay),
                "the authored level-ten beam reaches the visited generated system");
            Assert.IsFalse(state.RelayWarehouses(relay).Contains(fuelDepot),
                "Fuel Depot reserves are never exposed to remote pulls");
            Assert.AreEqual(0.37f, relay.def.EffectAt(relay.level));
        }

        [Test]
        public void Recipes_AreOfferedOnlyAtTheirExactStationAndRespectLevelResearchAndLocation()
        {
            var state = new GameState(db, false);
            foreach (var resource in db.resources) state.backpack.Set(resource.id, 100000);
            var refinery = state.DebugPlace(db.FindStructure("refinery"), 0);
            var fabricator = state.DebugPlace(db.FindStructure("fabricator"), 1);
            foreach (var recipe in db.recipes)
            {
                Assert.AreEqual(recipe.stationId == "refinery", state.RecipeAt(recipe, refinery), recipe.id);
                Assert.AreEqual(recipe.stationId == "fabricator", state.RecipeAt(recipe, fabricator), recipe.id);
                if (!string.IsNullOrEmpty(recipe.unlockResearch))
                {
                    Assert.IsFalse(state.CanCraft(recipe), recipe.id);
                    Assert.IsFalse(state.Craft(recipe), recipe.id);
                }
            }
            var first = db.recipes.First(r => r.stationId == "refinery" && r.stationLevel == 1 && string.IsNullOrEmpty(r.unlockResearch));
            Assert.IsNull(state.RecipeLockReason(first, refinery));
            Assert.IsTrue(state.CanCraft(first));
            state.DebugTeleport(state.universe.Get("glacia"));
            Assert.IsFalse(state.CanCraft(first), "a station on another world does not count");
        }

        [Test]
        public void CatalogCraftingStations_AreImplementedAndServeOnlyTheirRecipes()
        {
            var state = new GameState(db, false);
            string[] stationIds = { "refinery", "fabricator", "foundry", "chem_reactor", "quantum_lab" };
            int slot = 0;
            foreach (string id in stationIds)
            {
                var definition = db.FindStructure(id);
                Assert.IsNotNull(definition, id);
                Assert.IsTrue(definition.Implemented, id + " uses the existing recipe-station behavior");
                Assert.IsTrue(definition.kind.IsStation(), id);
                var placed = state.DebugPlace(definition, slot++);
                var matchingRecipes = db.recipes.Where(recipe => recipe.stationId == id).ToList();
                Assert.IsNotEmpty(matchingRecipes, id + " has catalog recipes");
                foreach (var recipe in db.recipes)
                    Assert.AreEqual(recipe.stationId == id, state.RecipeAt(recipe, placed), id + "/" + recipe.id);
            }
        }

        [Test]
        public void CraftBatch_UsesOperationalStationCapacityWithoutIncreasingPerRecipeYield()
        {
            var state = new GameState(db, false);
            var refinery = state.DebugPlace(db.FindStructure("refinery"), 0);
            var recipe = db.recipes.Single(r => r.id == "distill_wood");
            Assert.AreEqual(1, state.StationBatchSize(refinery));

            state.DebugSetStructureLevel(refinery, 2);
            Assert.AreEqual(2, state.StationBatchSize(refinery));
            state.backpack.Set("wood", 16);
            int fuelBefore = state.Count(db.fuel);

            Assert.IsFalse(state.CraftBatch(recipe, refinery, 3), "a tap cannot exceed the station's authored batch capacity");
            Assert.AreEqual(16, state.Count(db.FindResource("wood")), "a rejected batch spends nothing");
            Assert.IsTrue(state.CraftBatch(recipe, refinery, 2));
            Assert.AreEqual(0, state.Count(db.FindResource("wood")));
            Assert.AreEqual(fuelBefore + 6, state.Count(db.fuel), "two recipes yield two times the exact recipe output");

            state.backpack.Set("wood", 8);
            Assert.IsTrue(state.CraftBatch(recipe, refinery, 1), "a partial batch remains craftable when fewer inputs are available");
            Assert.AreEqual(fuelBefore + 9, state.Count(db.fuel));
        }

        [Test]
        public void RealV1Fixture_LoadsWithoutLostItemsToolsOrWorldDeltas()
        {
            string original = SaveFormatTests.Fixture("save_v1_midgame.json");
            File.WriteAllText(sandbox.SaveFile, original);
            var state = new GameState(db, true);
            Assert.AreEqual(1, state.LoadReport.migratedFrom);
            Assert.IsEmpty(state.LoadReport.warnings);
            Assert.AreEqual(original, File.ReadAllText(Path.Combine(sandbox.Directory, SaveSchema.LegacyBackupName)));
            Assert.AreEqual(5, state.Count(db.FindResource("steel")));
            Assert.AreEqual(2, state.Level(db.FindUpgrade("pack")));
            Assert.AreEqual(1, state.Level(db.FindUpgrade("axe")));
            Assert.AreEqual(1, state.Level(db.FindUpgrade("pick")));
            Assert.AreEqual(9, state.structures.Count);
            Assert.AreEqual(6, state.totalWavesCleared);
            Assert.AreEqual("glacia", state.planet.AuthoredId);
            var again = new GameState(db, true);
            Assert.AreEqual(0, again.LoadReport.migratedFrom);
            Assert.IsEmpty(again.LoadReport.warnings);
            Assert.AreEqual(5, again.Count(db.FindResource("steel")));
            Assert.AreEqual(9, again.structures.Count);
        }

        [Test]
        public void V2Aliases_MergeInventoryPreserveWarehouseSlotsAndRoundTripOnlyOnce()
        {
            var state = new GameState(db, false);
            state.Save();
            var save = SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile)).Normalize();
            save.player.inventory = new List<StackRecord>
            {
                new StackRecord { resourceId = "alloy", count = 7 }, new StackRecord { resourceId = "steel", count = 9 }
            };
            save.player.upgrades.Add(new IdLevel { id = "axe", level = 2 });
            var home = save.worlds.Single();
            home.structures.Add(new StructureRecord { defId = "satellite", slot = 0, level = 2 });
            home.structures.Add(new StructureRecord { defId = "warehouse", slot = 1, level = 1 });
            home.storages.Add(new StorageRecord { structureSlot = 1, slots = new List<StackRecord>
            {
                new StackRecord(), new StackRecord { resourceId = "alloy", count = 11 }
            } });
            File.WriteAllText(sandbox.SaveFile, SaveSerializer.Instance.Serialize(save));
            var loaded = new GameState(db, true);
            Assert.IsEmpty(loaded.LoadReport.warnings);
            Assert.AreEqual(16, loaded.Count(db.FindResource("steel")));
            Assert.AreEqual("orbital_relay", loaded.StructureAt(loaded.planet, 0).def.id);
            Assert.AreEqual(2, loaded.Level(db.FindUpgrade("axe")));
            var storage = loaded.StorageFor(loaded.StructureAt(loaded.planet, 1));
            Assert.IsTrue(string.IsNullOrEmpty(storage.slots[0].resourceId));
            Assert.AreEqual("steel", storage.slots[1].resourceId);
            Assert.AreEqual(11, storage.slots[1].count);
            var again = new GameState(db, true);
            Assert.AreEqual(16, again.Count(db.FindResource("steel")));
            Assert.AreEqual(0, again.saves.WriteCount, "already canonical v2 does not trigger a second migration/write");
            Assert.IsEmpty(again.LoadReport.warnings);
        }

        [Test]
        public void StableIds_AreNeverReinterpretedByAnAlias()
        {
            var copy = Object.Instantiate(imported);
            var map = Object.Instantiate(imported.legacyIds);
            try
            {
                map.rows = new[] { new LegacyIdEntry { kind = LegacyKind.Resource, legacyId = "wood", id = "stone" } };
                copy.legacyIds = map;
                Assert.AreEqual("wood", copy.ResolveResourceId("wood"));
                Assert.AreEqual("pack", db.ResolveUpgradeId("pack"));
                Assert.AreEqual("steel", db.ResolveResourceId("steel"));
            }
            finally { Object.DestroyImmediate(copy); Object.DestroyImmediate(map); }
        }

        [Test]
        public void UpgradeAliases_ResolveOnceAndMergeByMaximumLevel()
        {
            var source = Object.Instantiate(imported);
            var map = Object.Instantiate(imported.legacyIds);
            GameDatabase selected = null;
            try
            {
                map.rows = map.rows.Concat(new[] { new LegacyIdEntry { kind = LegacyKind.Upgrade, legacyId = "old_pack", id = "pack" } }).ToArray();
                source.legacyIds = map;
                selected = GameDatabase.Select(source, legacy, out var reason);
                Assert.IsTrue(selected.UsesImportedEconomy, reason);
                var initial = new GameState(selected, false);
                initial.Save();
                var save = SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile)).Normalize();
                save.player.upgrades.Add(new IdLevel { id = "old_pack", level = 2 });
                save.player.upgrades.Add(new IdLevel { id = "pack", level = 1 });
                File.WriteAllText(sandbox.SaveFile, SaveSerializer.Instance.Serialize(save));
                var state = new GameState(selected, true);
                Assert.AreEqual(2, state.Level(selected.FindUpgrade("pack")));
                Assert.IsEmpty(state.LoadReport.warnings);
                var saved = SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile));
                Assert.AreEqual(1, saved.player.upgrades.Count(u => u.id == "pack"));
                Assert.IsFalse(saved.player.upgrades.Any(u => u.id == "old_pack"));
            }
            finally
            {
                if (selected != null && selected != legacy) Object.DestroyImmediate(selected);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(map);
            }
        }

        [Test]
        public void LegacyFallback_RoundTripsOriginalIdsAndTools()
        {
            var fallback = GameDatabase.Select(null, legacy, out _);
            var state = new GameState(fallback, false);
            state.backpack.Set("alloy", 5);
            state.DebugSetUpgrade(fallback.FindUpgrade("axe"), 2);
            state.DebugPlace(fallback.FindStructure("satellite"), 0);
            state.Save();
            var again = new GameState(fallback, true);
            Assert.AreEqual(5, again.Count(fallback.FindResource("alloy")));
            Assert.AreEqual(2, again.Level(fallback.FindUpgrade("axe")));
            Assert.AreEqual("satellite", again.StructureAt(again.planet, 0).def.id);
            Assert.IsEmpty(again.LoadReport.warnings);
        }

        [Test]
        public void ContentAliases_DoNotOverwriteNewerReadOnlySaves()
        {
            var state = new GameState(db, false);
            state.Save();
            var save = SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile));
            save.schemaVersion = SaveSchema.Current + 1;
            save.player.inventory.Add(new StackRecord { resourceId = "alloy", count = 5 });
            string original = SaveSerializer.Instance.Serialize(save);
            File.WriteAllText(sandbox.SaveFile, original);
            var loaded = new GameState(db, true);
            Assert.IsTrue(loaded.SaveIsReadOnly);
            Assert.AreEqual(5, loaded.Count(db.FindResource("steel")));
            Assert.IsFalse(loaded.Save());
            Assert.AreEqual(original, File.ReadAllText(sandbox.SaveFile));
        }

        [Test]
        public void StarterRefinery_CraftsExactImportedOutputs_AndEnforcesItsNextRecipeLevel()
        {
            var state = new GameState(db, false);
            var refineryDef = db.FindStructure("refinery");
            foreach (var cost in refineryDef.buildCost) state.backpack.Set(cost.resource.id, cost.amount);
            Assert.IsTrue(state.Place(refineryDef, 0));
            var refinery = state.StructureAt(state.planet, 0);
            var recipe = db.recipes.Single(r => r.id == "distill_wood");
            state.backpack.Set("wood", 8);
            int before = state.Count(db.fuel);
            Assert.IsTrue(state.Craft(recipe, state.StructureBonus(refinery)));
            Assert.AreEqual(before + 3, state.Count(db.fuel));
            Assert.AreEqual(0, state.Count(db.FindResource("wood")));
            var next = db.recipes.Single(r => r.id == "ferment_biomass");
            state.backpack.Set("biomass", 6);
            Assert.IsFalse(state.CanCraft(next));
            StringAssert.Contains("station level 2", state.RecipeLockReason(next, refinery));
            state.DebugSetStructureLevel(refinery, 2);
            state.DebugSetLevel(Math.Max(1, next.tier - 1));
            Assert.AreEqual(state.PlayerLevel >= next.tier, state.CanCraft(next),
                "Recipe unlocks must use player XP level rather than a planet visit.");
            state.DebugSetLevel(Math.Max(1, next.tier));
            Assert.IsTrue(state.CanCraft(next));
            Assert.IsTrue(state.Craft(next));
            Assert.AreEqual(before + 8, state.Count(db.fuel));
        }

        [Test]
        public void AtlasRecipeDiscovery_AwardsOnlyAfterTheFirstSuccessfulCraft()
        {
            var state = new GameState(db, false);
            var recipe = db.recipes.Single(r => r.id == "distill_wood");
            state.DebugPlace(db.FindStructure("refinery"), 0);
            state.backpack.Set("wood", 0);
            int before = state.ShardBalance;

            Assert.IsFalse(state.Craft(recipe));
            Assert.IsFalse(state.IsAtlasDiscovered("recipe", recipe.id));
            Assert.AreEqual(before, state.ShardBalance, "a failed craft is not a discovery");

            state.backpack.Set("wood", 8);
            Assert.IsTrue(state.Craft(recipe));
            Assert.IsTrue(state.IsAtlasDiscovered("recipe", recipe.id));
            Assert.AreEqual(before + db.shardRules.data.atlasRecipeShards, state.ShardBalance);

            state.backpack.Set("wood", 8);
            Assert.IsTrue(state.Craft(recipe));
            Assert.AreEqual(before + db.shardRules.data.atlasRecipeShards, state.ShardBalance);
            Assert.AreEqual(1, state.AtlasDiscoveredCount("recipe"));
        }

        [Test]
        public void AtlasEnemyDiscovery_RecordsFirstEncounterWithoutCurrencyReward()
        {
            var state = new GameState(db, false);
            var enemy = db.enemies[0];
            int before = state.ShardBalance;

            Assert.IsTrue(state.RecordAtlasEnemySeen(enemy));
            Assert.IsTrue(state.IsAtlasDiscovered("enemy", enemy.id));
            Assert.AreEqual(1, state.AtlasDiscoveredCount("enemy"));
            Assert.AreEqual(db.enemies.Length, state.AtlasCatalogCount("enemy"));
            Assert.AreEqual(before, state.ShardBalance, "alien silhouettes reveal on encounter without adding an unbudgeted reward");
            Assert.IsFalse(state.RecordAtlasEnemySeen(enemy));
            Assert.IsFalse(state.IsAtlasDiscovered("enemy", "unknown"));
            Assert.AreEqual(before, state.ShardBalance);
            Assert.IsTrue(state.Save());
            var loaded = new GameState(db, true);
            Assert.IsTrue(loaded.IsAtlasDiscovered("enemy", enemy.id), "encounter knowledge is stored in the save-backed Atlas ledger");
            Assert.AreEqual(1, loaded.AtlasDiscoveredCount("enemy"));
        }
    }
}
