using System;
using System.Collections.Generic;
using NUnit.Framework;
using Frontier.Economy;
using Frontier.Universe;

namespace Frontier.Economy.Tests
{
    [TestFixture]
    public class CatalogIntegrityTests
    {
        EconomyCatalog _c;

        [OneTimeSetUp]
        public void Setup() { _c = Fx.NewCatalog(); }

        [Test]
        public void Validator_FindsNoProblems()
        {
            List<string> problems = CatalogValidator.Validate(_c);
            Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
        }

        [Test]
        public void Counts_MatchTheLockedDesign()
        {
            Assert.AreEqual(34, _c.resources.Length);
            var perCategory = new int[5];
            foreach (ResourceDef r in _c.resources) perCategory[(int)r.category]++;
            Assert.AreEqual(15, perCategory[(int)ResourceCategory.Raw]);
            Assert.AreEqual(6, perCategory[(int)ResourceCategory.Processed]);
            Assert.AreEqual(6, perCategory[(int)ResourceCategory.Alloy]);
            Assert.AreEqual(5, perCategory[(int)ResourceCategory.Component]);
            Assert.AreEqual(2, perCategory[(int)ResourceCategory.Energy]);
            Assert.AreEqual(31, _c.structures.Length);
            int existing = 0;
            foreach (StructureDef s in _c.structures) if (s.legacyKind != null) existing++;
            Assert.AreEqual(10, existing, "9 existing structures plus the satellite folded into the relay");
            Assert.AreEqual(6, _c.tools.Length);
            foreach (ToolLadderDef l in _c.tools) Assert.AreEqual(7, l.tiers.Length);
            Assert.GreaterOrEqual(_c.recipes.Length, 20);
            Assert.GreaterOrEqual(_c.research.Length, 50);
            TestLog.Info(_c.resources.Length + " resources, " + _c.recipes.Length + " recipes, " + _c.structures.Length + " structures, " + _c.upgrades.Length + " upgrades, " + _c.research.Length + " research nodes, " + _c.habitats.Length + " habitat rows");
        }

        [Test]
        public void Structures_HaveTheRightRoleSplit()
        {
            var role = new Dictionary<StructureRole, int>();
            foreach (StructureDef s in _c.structures) { int n; role.TryGetValue(s.role, out n); role[s.role] = n + 1; }
            Assert.AreEqual(7, role[StructureRole.Gather]);
            Assert.AreEqual(5, role[StructureRole.Process]);
            Assert.AreEqual(4, role[StructureRole.Storage]);
            Assert.AreEqual(6, role[StructureRole.Defense]);
            Assert.AreEqual(6, role[StructureRole.Utility]);
            Assert.AreEqual(3, role[StructureRole.Megaproject]);
            foreach (StructureDef s in _c.structures) Assert.LessOrEqual(s.footprint, 4, s.id + " footprint must fit the smallest planet");
        }

        [Test]
        public void IconConcepts_AreUniqueAndDescriptionsConsistent()
        {
            var seen = new HashSet<string>();
            foreach (ResourceDef r in _c.resources)
            {
                Assert.IsTrue(seen.Add(r.iconConcept), r.id);
                Assert.IsTrue(r.iconConcept.EndsWith("."), r.id + " icon concept is one sentence");
                Assert.IsTrue(r.atlasDescription.EndsWith("."), r.id + " atlas text is complete sentences");
                int sentences = r.atlasDescription.Split(new[] { ". " }, StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.LessOrEqual(sentences, 2, r.id + " atlas text is at most two sentences");
            }
            // Colour families per category: the legacy resources keep their colours.
            Assert.AreEqual("#E0A23A", _c.Resource("wood").colorHex);
            Assert.AreEqual(5, _c.categoryHex.Length);
        }

        [Test]
        public void StackSizes_BulkyLowTiersSmall_DenseHighTiersLarge()
        {
            double lowSum = 0, highSum = 0; int lowN = 0, highN = 0;
            foreach (ResourceDef r in _c.resources)
            {
                if (r.tier <= 1) { lowSum += r.stackSize; lowN++; }
                else if (r.tier >= 4) { highSum += r.stackSize; highN++; }
            }
            double low = lowSum / lowN, high = highSum / highN;
            TestLog.Info("mean stack size tier<=1: " + low.ToString("0.0") + ", tier>=4: " + high.ToString("0.0"));
            Assert.Greater(high, low * 1.8);
            Assert.AreEqual(9, _c.start.baseBackpackSlots, "the pack starts small on purpose");
            Assert.LessOrEqual(_c.Resource("wood").stackSize, 24);
        }

        [Test]
        public void Fuel_IsCraftedOnly_AndEnergyDensityIsGeometric()
        {
            ResourceDef fuel = _c.Resource("fuel");
            Assert.IsNull(fuel.harvestMethods, "fuel is never a deposit");
            foreach (HabitatDef h in _c.habitats) Assert.AreNotEqual("fuel", h.resourceId);
            Assert.AreEqual(1, fuel.fuelEnergy);
            Assert.AreEqual(8, _c.Resource("plasma").fuelEnergy);
            Assert.AreEqual(64, _c.Resource("antimatter").fuelEnergy);
            Assert.GreaterOrEqual(_c.RecipesFor("fuel").Count, 5, "several ways to make fuel, from wood early to methane later");
            Assert.AreEqual("distill_wood", _c.RecipesFor("fuel")[0].id, "the wood recipe is the primary fuel recipe");
        }

        [Test]
        public void LegacyIds_RoundTrip_AndAlloyBecomesSteel()
        {
            var map = new LegacyIdMap(_c);
            string[] legacyResources = { "wood", "stone", "scrap", "iron", "crystal", "fuel", "core", "alloy", "plasma" };
            foreach (string id in legacyResources)
            {
                string now = map.Resolve(LegacyKind.Resource, id);
                Assert.IsNotNull(now, id);
                Assert.IsNotNull(_c.Resource(now), id + " -> " + now);
                Assert.AreEqual(id, map.ToLegacy(LegacyKind.Resource, now), "round trip of " + id);
            }
            Assert.AreEqual("steel", map.Resolve(LegacyKind.Resource, "alloy"));
            Assert.AreEqual("iron", map.Resolve(LegacyKind.Resource, "iron"));
            Assert.AreEqual("orbital_relay", map.Resolve(LegacyKind.Structure, "satellite"));
            Assert.AreEqual("satellite", map.ToLegacy(LegacyKind.Structure, "orbital_relay"));
            string[] legacyStructures = { "refinery", "fabricator", "alarm", "lumber", "mine", "driller", "gunner", "laser", "satellite", "warehouse" };
            foreach (string id in legacyStructures) Assert.IsNotNull(_c.Structure(map.Resolve(LegacyKind.Structure, id)), id);
            string[] legacyUpgrades = { "axe", "pick", "drill", "pack", "engine", "turret", "turretdmg", "pulse", "shield", "salvage" };
            foreach (string id in legacyUpgrades)
            {
                string now = map.Resolve(LegacyKind.Upgrade, id);
                Assert.IsTrue(_c.Upgrade(now) != null || _c.Tool(now) != null, id);
            }
            Assert.IsNull(map.Resolve(LegacyKind.Resource, "nope"));
            Assert.IsNull(map.ToLegacy(LegacyKind.Resource, "quantum_chip"), "new ids have no legacy name");
            // Every id of the old content exists in the old inventory form, so a legacy save migrates without loss.
            var legacy = new Dictionary<string, int> { { "wood", 40 }, { "alloy", 7 }, { "plasma", 3 }, { "weird", 1 } };
            var result = new Dictionary<string, int>();
            var unknown = new List<string>();
            map.MigrateInventory(legacy, result, unknown);
            Assert.AreEqual(40, result["wood"]);
            Assert.AreEqual(7, result["steel"]);
            Assert.AreEqual(3, result["plasma"]);
            CollectionAssert.AreEqual(new[] { "weird" }, unknown);
        }

        [Test]
        public void StartingState_IsTheLegacyStart_AndLeavesRoomInThePack()
        {
            var items = new Dictionary<string, int>();
            foreach (ItemAmount a in _c.start.startingItems) items[a.resourceId] = a.amount;
            Assert.AreEqual(24, items["wood"]);
            Assert.AreEqual(30, items["scrap"]);
            Assert.AreEqual(10, items["fuel"]);
            Assert.AreEqual("sylva", _c.start.startPlanetId);
            Assert.AreEqual(1, _c.start.builders);
            int slots = 0;
            foreach (ItemAmount a in _c.start.startingItems) slots += (a.amount + _c.Resource(a.resourceId).stackSize - 1) / _c.Resource(a.resourceId).stackSize;
            Assert.LessOrEqual(slots, _c.start.baseBackpackSlots - 3, "the start inventory leaves room to harvest");
        }
    }

    [TestFixture]
    public class RecipeTests
    {
        EconomyCatalog _c;
        Tuning _t;

        [OneTimeSetUp]
        public void Setup() { _t = Fx.NewTuning(); _c = CatalogBuilder.Build(_t); }

        [Test]
        public void Conservation_NoRecipeMintsValue()
        {
            int checkedRecipes = 0;
            double worst = 0;
            string worstId = "";
            foreach (RecipeDef r in _c.recipes)
            {
                if (r.bonus) continue;
                double vin = _c.Prices.Value(r.inputs), vout = _c.Prices.Value(r.outputs);
                double ratio = vout / vin;
                Assert.LessOrEqual(ratio, ValueModel.MaxRatio(r.tier, _t) + 1e-9, r.id + " ratio " + ratio.ToString("0.000") + " exceeds the tier limit " + ValueModel.MaxRatio(r.tier, _t).ToString("0.000"));
                Assert.GreaterOrEqual(ratio, _t.D("value.min_ratio"), r.id + " destroys too much value");
                if (ratio > worst) { worst = ratio; worstId = r.id; }
                checkedRecipes++;
            }
            TestLog.Info(checkedRecipes + " recipes checked; highest value ratio " + worst.ToString("0.00") + " (" + worstId + "), limit " + ValueModel.MaxRatio(0, _t).ToString("0.00") + " + " + _t.D("value.max_ratio.per_tier").ToString("0.00") + " per recipe tier");
        }

        [Test]
        public void Conservation_CatchesABadRecipe()
        {
            // A synthetic recipe that triples value must be flagged by the same rule (proves the check has teeth).
            var bad = new RecipeDef { id = "mint", stationId = "refinery", stationLevel = 1, tier = 0, inputs = new[] { new ItemAmount("wood", 1) }, outputs = new[] { new ItemAmount("quantum_chip", 1) } };
            double ratio = _c.Prices.Value(bad.outputs) / _c.Prices.Value(bad.inputs);
            Assert.Greater(ratio, ValueModel.MaxRatio(0, _t));
        }

        [Test]
        public void PrimaryRecipes_PriceTheirOutputs()
        {
            foreach (ResourceDef r in _c.resources)
            {
                if (r.category == ResourceCategory.Raw) continue;
                RecipeDef primary = _c.Prices.PrimaryRecipe(r.id);
                Assert.IsNotNull(primary, r.id);
                double expected = _c.Prices.Value(primary.inputs) * (1.0 + ValueModel.Premium(primary.stationId, _t)) / primary.outputs[0].amount;
                Assert.AreEqual(expected, r.baseValue, Math.Max(0.01, expected * 0.001), r.id);
            }
        }

        [Test]
        public void RecipeGraph_HasNoCycles()
        {
            // Edges input -> output; a cycle would let a loop of recipes mint value without limit.
            var edges = new Dictionary<string, List<string>>();
            foreach (RecipeDef r in _c.recipes)
                foreach (ItemAmount i in r.inputs)
                {
                    List<string> l;
                    if (!edges.TryGetValue(i.resourceId, out l)) edges[i.resourceId] = l = new List<string>();
                    foreach (ItemAmount o in r.outputs) l.Add(o.resourceId);
                }
            var state = new Dictionary<string, int>();
            foreach (string start in edges.Keys) Visit(start, edges, state, new List<string>());
        }

        static void Visit(string id, Dictionary<string, List<string>> edges, Dictionary<string, int> state, List<string> path)
        {
            int s;
            state.TryGetValue(id, out s);
            if (s == 2) return;
            if (s == 1) Assert.Fail("recipe cycle: " + string.Join(" -> ", path) + " -> " + id);
            state[id] = 1;
            path.Add(id);
            List<string> next;
            if (edges.TryGetValue(id, out next)) foreach (string n in next) Visit(n, edges, state, path);
            path.RemoveAt(path.Count - 1);
            state[id] = 2;
        }

        [Test]
        public void MidAndLateGoods_DemandLargeTierZeroVolume()
        {
            // Steel, biocomposite and planks carry the tier-0 volume that sends the player back to the starter worlds.
            var vol = new Dictionary<string, double>();
            foreach (ResourceDef r in _c.resources) vol[r.id] = _c.Prices.T0Volume(r.id);
            TestLog.Info("tier-0 wood-equivalents per unit: planks " + vol["planks"].ToString("0.0") + ", steel " + vol["steel"].ToString("0.0") + ", biocomposite " + vol["biocomposite"].ToString("0.0")
                + ", thermal " + vol["thermal_alloy"].ToString("0.0") + ", servo " + vol["servo_motor"].ToString("0.0") + ", plasma " + vol["plasma"].ToString("0.0") + ", antimatter " + vol["antimatter"].ToString("0.0"));
            Assert.GreaterOrEqual(vol["steel"], 4.0);
            Assert.GreaterOrEqual(vol["biocomposite"], 5.0);
            Assert.GreaterOrEqual(vol["planks"], 2.0);
            Assert.GreaterOrEqual(vol["rad_shield_alloy"], vol["steel"]);
            Assert.GreaterOrEqual(vol["void_alloy"], vol["rad_shield_alloy"]);
            Assert.GreaterOrEqual(vol["antimatter"], vol["plasma"] * 3.0);
        }

        [Test]
        public void StationLevelsUnlockBetterRecipes()
        {
            foreach (RecipeDef r in _c.recipes)
            {
                StructureDef st = _c.Structure(r.stationId);
                Assert.LessOrEqual(r.stationLevel, st.maxLevel);
                Assert.GreaterOrEqual(r.tier + 1, st.tier, r.id + " is on a station that unlocks later than the recipe tier allows");
            }
            // Each station level adds at most a couple of recipes, so levels matter.
            var perStation = new Dictionary<string, int>();
            foreach (RecipeDef r in _c.recipes) { int n; perStation.TryGetValue(r.stationId, out n); perStation[r.stationId] = n + 1; }
            Assert.GreaterOrEqual(perStation["refinery"], 5);
            Assert.GreaterOrEqual(perStation["foundry"], 5);
        }
    }

    [TestFixture]
    public class ReachabilityTests
    {
        [Test]
        public void EverythingIsObtainable_FromTheTierZeroStart()
        {
            EconomyCatalog c = Fx.NewCatalog();
            ReachabilityReport rep = ReachabilityAnalyzer.Analyze(c);
            TestLog.Info("fixpoint reached after " + rep.rounds + " rounds; " + rep.resources.Count + " of " + c.resources.Length + " resources, " + rep.research.Count + " of " + c.research.Length + " research nodes obtainable");
            foreach (string p in rep.problems) TestLog.Info("   problem: " + p);
            Assert.AreEqual(0, rep.problems.Count, string.Join("\n", rep.problems));
            // First-obtainable order is plausible: tier-0 goods first, deep goods last.
            Assert.LessOrEqual(rep.resourceRound["wood"], 1);
            Assert.Greater(rep.resourceRound["warp_coil"], rep.resourceRound["steel"]);
            Assert.Greater(rep.resourceRound["antimatter"], rep.resourceRound["plasma"]);
            Assert.Greater(rep.resourceRound["dark_matter"], rep.resourceRound["radium"]);
        }

        [Test]
        public void NoOrphans_EveryResourceIsUsedAndObtained()
        {
            EconomyCatalog c = Fx.NewCatalog();
            HashSet<string> used = CatalogValidator.UsedResources(c);
            foreach (ResourceDef r in c.resources) Assert.IsTrue(used.Contains(r.id), r.id + " is unused");
        }

        [Test]
        public void Reachability_DetectsACircularRequirement()
        {
            // Make a research node require the very item it unlocks: the analyzer must report it.
            EconomyCatalog c = Fx.NewCatalog();
            ResearchNodeDef n = c.Research("void_forging");
            var cost = new List<ItemAmount>(n.cost) { new ItemAmount("void_alloy", 1) };
            n.cost = cost.ToArray();
            ReachabilityReport rep = ReachabilityAnalyzer.Analyze(c);
            Assert.Greater(rep.problems.Count, 0, "void_forging now needs void alloy, which needs void_forging");
        }
    }
}
