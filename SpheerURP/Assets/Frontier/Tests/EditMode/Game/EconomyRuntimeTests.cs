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
        public void UnimplementedAndResearchContent_CannotBePurchasedWithFreeMaterials()
        {
            var state = new GameState(db, false);
            foreach (var resource in db.resources) state.backpack.Set(resource.id, 100000);
            foreach (var structure in db.structures)
                if (!structure.Implemented || !string.IsNullOrEmpty(structure.unlockResearch))
                {
                    Assert.IsNotNull(state.BuildLockReason(structure), structure.id);
                    Assert.IsFalse(state.Place(structure, 0), structure.id);
                }
            foreach (var upgrade in imported.upgrades)
            {
                var row = upgrade.NextRow(0);
                if (string.IsNullOrEmpty(row.researchPrereq) && row.requiresReachTier <= state.ReachTier &&
                    upgrade.effect != UpgradeEffect.StackMultiplier && upgrade.effect != UpgradeEffect.FuelTankSlots) continue;
                Assert.IsNotNull(state.UpgradeLockReason(upgrade), upgrade.id);
                Assert.IsFalse(state.Buy(upgrade), upgrade.id);
                Assert.AreEqual(0, state.Level(upgrade));
            }
            Assert.IsFalse(state.Craft(imported.recipes[0]), "no crafting without a station");
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
            Assert.IsFalse(state.CanCraft(next), "upgrading a station does not grant reach tier");
            var home = state.planet;
            state.DebugTeleport(state.universe.Get("glacia"));
            state.DebugTeleport(home);
            Assert.IsTrue(state.CanCraft(next));
            Assert.IsTrue(state.Craft(next));
            Assert.AreEqual(before + 8, state.Count(db.fuel));
        }
    }
}
