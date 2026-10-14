using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Tests
{
    /// <summary>
    /// Content integrity of the authored GameDatabase: unique ids, no dangling references, every structure kind defined and every
    /// cost payable with known resources. The first line of defence for the data-driven content pipeline.
    /// </summary>
    [TestFixture]
    public class GameDatabaseTests
    {
        GameDatabase db;
        HashSet<ResourceSO> known;

        [OneTimeSetUp]
        public void LoadDatabase()
        {
            db = GameDatabase.Load();
            known = new HashSet<ResourceSO>(db.resources);
        }

        static void AssertUniqueIds<T>(IEnumerable<T> items, Func<T, string> id, string what) where T : UnityEngine.Object
        {
            var seen = new HashSet<string>();
            foreach (var item in items)
            {
                Assert.IsNotNull(item, what + " list contains an empty entry");
                string key = id(item);
                Assert.IsFalse(string.IsNullOrEmpty(key), what + " '" + item.name + "' has no id");
                Assert.IsTrue(seen.Add(key), "duplicate " + what + " id '" + key + "'");
            }
            Assert.Greater(seen.Count, 0, "no " + what);
        }

        void AssertKnown(IEnumerable<ItemAmount> items, string owner)
        {
            foreach (var item in items)
            {
                Assert.IsNotNull(item.resource, owner + " references a missing resource");
                Assert.IsTrue(known.Contains(item.resource), owner + " uses '" + item.resource.id + "', which is not in the database");
                Assert.Greater(item.amount, 0, owner + " has a non-positive amount of " + item.resource.id);
            }
        }

        [Test]
        public void Ids_AreUniqueInEveryCollection()
        {
            AssertUniqueIds(db.resources, r => r.id, "resource");
            AssertUniqueIds(db.planets, p => p.id, "planet");
            AssertUniqueIds(db.upgrades, u => u.id, "upgrade");
            AssertUniqueIds(db.structures, s => s.id, "structure");
            AssertUniqueIds(db.enemies, e => e.id, "enemy");
        }

        [Test]
        public void StartPlanetFuelAndScrap_AreAssigned()
        {
            Assert.IsNotNull(db.startPlanet);
            Assert.Contains(db.startPlanet, db.planets);
            Assert.IsNotNull(db.fuel);
            Assert.IsNotNull(db.scrap);
            Assert.IsTrue(known.Contains(db.fuel) && known.Contains(db.scrap), "fuel and scrap must be listed resources");
            Assert.IsNotNull(db.formula);
            AssertKnown(db.startingItems, "starting items");
        }

        [Test]
        public void PlanetResources_AreKnown()
        {
            foreach (var planet in db.planets)
            {
                Assert.Greater(planet.spots.Count, 0, planet.id + " offers nothing to harvest");
                foreach (var spot in planet.spots)
                {
                    Assert.IsNotNull(spot.resource, planet.id + " has a harvest spot without a resource");
                    Assert.IsTrue(known.Contains(spot.resource), planet.id + " offers '" + spot.resource.id + "', which is not in the database");
                    Assert.Greater(spot.baseYield, 0, planet.id + " spot yield");
                }
            }
        }

        [Test]
        public void RecipesStructuresUpgradesAndBounties_UseKnownResources()
        {
            foreach (var recipe in db.recipes)
            {
                AssertKnown(recipe.inputs, "recipe '" + recipe.displayName + "' inputs");
                AssertKnown(recipe.outputs, "recipe '" + recipe.displayName + "' outputs");
                Assert.Greater(recipe.inputs.Count, 0, "recipe '" + recipe.displayName + "' has no inputs");
                Assert.Greater(recipe.outputs.Count, 0, "recipe '" + recipe.displayName + "' has no outputs");
            }
            foreach (var structure in db.structures)
            {
                AssertKnown(structure.buildCost, "structure '" + structure.id + "' build cost");
                Assert.GreaterOrEqual(structure.maxLevel, 1, structure.id);
            }
            foreach (var upgrade in db.upgrades)
            {
                AssertKnown(upgrade.baseCost, "upgrade '" + upgrade.id + "' base cost");
                for (int level = 0; level < upgrade.maxLevel; level++) AssertKnown(upgrade.CostAt(level), "upgrade '" + upgrade.id + "' level " + level);
            }
            foreach (var enemy in db.enemies) AssertKnown(enemy.bounty, "enemy '" + enemy.id + "' bounty");
        }

        [Test]
        public void EveryImplementedStructureKind_HasALiveDefinition()
        {
            foreach (StructureKind kind in Enum.GetValues(typeof(StructureKind)))
            {
                if (kind.IsImplemented())
                    Assert.IsNotNull(db.StructureOfKind(kind), "no live structure is defined for " + kind);
                else
                    Assert.IsNull(db.StructureOfKind(kind), "imported-only structure leaked into the live database: " + kind);
            }
        }

        [Test]
        public void Resources_HaveUsableStacks()
        {
            foreach (var resource in db.resources)
            {
                Assert.GreaterOrEqual(resource.stackSize, 1, resource.id + " stack size");
                Assert.IsFalse(string.IsNullOrEmpty(resource.displayName), resource.id + " has no display name");
            }
        }

        [Test]
        public void FindPlanet_ReturnsEachPlanetById()
        {
            foreach (var planet in db.planets) Assert.AreSame(planet, db.FindPlanet(planet.id));
            Assert.IsNull(db.FindPlanet("no-such-planet"));
        }
    }
}
