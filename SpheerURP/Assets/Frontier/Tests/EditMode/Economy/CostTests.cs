using System;
using System.Collections.Generic;
using NUnit.Framework;
using Frontier.Economy;
using Frontier.Universe;

namespace Frontier.Economy.Tests
{
    [TestFixture]
    public class CostTests
    {
        Tuning _t;
        EconomyCatalog _c;

        [OneTimeSetUp]
        public void Setup() { _t = Fx.NewTuning(); _c = CatalogBuilder.Build(_t); }

        static int Amount(ItemAmount[] items, string id)
        {
            for (int i = 0; i < items.Length; i++) if (items[i].resourceId == id) return items[i].amount;
            return 0;
        }

        static void AssertGrows(ItemAmount[] prev, ItemAmount[] next, string where)
        {
            foreach (ItemAmount p in prev)
                Assert.GreaterOrEqual(Amount(next, p.resourceId), p.amount, where + ": " + p.resourceId + " shrank");
        }

        [Test]
        public void StructureUpgradeCosts_AreMonotone_PerResourceAndInTotalValue()
        {
            int rows = 0;
            foreach (StructureDef s in _c.structures)
            {
                double prevValue = -1;
                for (int l = 1; l < s.levels.Length; l++)
                {
                    StructureLevelDef a = s.levels[l - 1], b = s.levels[l];
                    AssertGrows(a.cost, b.cost, s.id + " L" + a.level + "->L" + b.level);
                    double va = _c.Prices.Value(a.cost), vb = _c.Prices.Value(b.cost);
                    Assert.GreaterOrEqual(vb, va - 1e-9, s.id + " total value fell at L" + b.level);
                    prevValue = vb;
                    rows++;
                }
                Assert.Greater(_c.Prices.Value(s.levels[s.levels.Length - 1].cost), 0, s.id);
            }
            TestLog.Info(rows + " structure level steps checked");
        }

        [Test]
        public void StructureCosts_AreGeometricUntilTheCapsBind()
        {
            // Early levels follow value x growth^(level-1) within rounding: lumber camp levels 1..5.
            StructureDef lumber = _c.Structure("lumber");
            double g = _t.D("cost.g.lumber");
            double v1 = _c.Prices.Value(lumber.levels[0].cost);
            for (int l = 2; l <= 5; l++)
            {
                double expected = v1 * Math.Pow(g, l - 1);
                double actual = _c.Prices.Value(lumber.levels[l - 1].cost);
                Assert.AreEqual(expected, actual, expected * 0.12 + 1.0, "lumber L" + l);
            }
            // The first structures keep the legacy build costs.
            Assert.AreEqual(6, Amount(lumber.levels[0].cost, "scrap"));
            Assert.AreEqual(0, _c.Structure("mine").levels[0].cost.Length, "the Mining Rig is free to build");
            Assert.AreEqual(15, Amount(_c.Structure("refinery").levels[0].cost, "scrap"));
            Assert.AreEqual(12, Amount(_c.Structure("alarm").levels[0].cost, "scrap"));
            Assert.AreEqual(20, Amount(_c.Structure("warehouse").levels[0].cost, "scrap"));
            Assert.AreEqual(10, Amount(_c.Structure("warehouse").levels[0].cost, "wood"));
            // The Deep Driller keeps its legacy composition; its price follows the era-1 pacing multiplier.
            int drillerScrap = Amount(_c.Structure("driller").levels[0].cost, "scrap");
            Assert.GreaterOrEqual(drillerScrap, 25);
            Assert.LessOrEqual(drillerScrap, (int)Math.Ceiling(25 * _t.D("cost.era.1")) + 2);
            Assert.AreEqual(Math.Round(8.0 / 25.0 * drillerScrap), Amount(_c.Structure("driller").levels[0].cost, "stone"), 1.5);
        }

        [Test]
        public void NoSingleCostLine_ExceedsWhatABaseCanHold_ExceptMegaprojects()
        {
            int worst = 0; string worstWhere = "";
            foreach (StructureDef s in _c.structures)
            {
                if (s.role == StructureRole.Megaproject) continue;
                foreach (StructureLevelDef d in s.levels)
                {
                    double cap = _t.D("cost.cap.base_slots") + _t.D("cost.cap.slots_per_tier") * d.requiresReachTier;
                    foreach (ItemAmount a in d.cost)
                    {
                        int slots = (a.amount + _c.Resource(a.resourceId).stackSize - 1) / _c.Resource(a.resourceId).stackSize;
                        Assert.LessOrEqual(slots, cap + 1, s.id + " L" + d.level + " asks " + a.amount + " " + a.resourceId + " (" + slots + " slots, cap " + cap + ")");
                        if (slots > worst) { worst = slots; worstWhere = s.id + " L" + d.level + " " + a.resourceId; }
                    }
                }
            }
            TestLog.Info("largest single cost line outside megaprojects: " + worst + " slots (" + worstWhere + ")");
        }

        [Test]
        public void ToolLadders_YieldRises_CostsRise_AndTheStartIsFree()
        {
            foreach (ToolLadderDef l in _c.tools)
            {
                Assert.AreEqual(0, l.tiers[0].cost.Length, l.id + " tier 1 is the free starting tool");
                Assert.AreEqual(1.0, l.tiers[0].yieldMult, 1e-9);
                double prevMult = 0, prevValue = -1, prevRun = 2;
                int prevReach = -1, prevLab = -1;
                foreach (ToolTierDef d in l.tiers)
                {
                    Assert.Greater(d.yieldMult, prevMult, l.id + " T" + d.tier);
                    Assert.Less(d.runTimeMult, prevRun + 1e-9);
                    double v = _c.Prices.Value(d.cost);
                    Assert.Greater(v, prevValue, l.id + " T" + d.tier + " cost must rise");
                    Assert.GreaterOrEqual(d.requiresReachTier, prevReach);
                    Assert.GreaterOrEqual(d.requiresLabLevel, prevLab);
                    prevMult = d.yieldMult; prevValue = v; prevRun = d.runTimeMult; prevReach = d.requiresReachTier; prevLab = d.requiresLabLevel;
                }
                Assert.GreaterOrEqual(l.tiers[6].yieldMult, 10.0, "the end-game tool is worth a very different run");
            }
            // The tier 1 -> 7 multiplier is what makes the same mini-game pay for a megaproject-sized volume.
            Assert.AreEqual(12.5, _c.Tool("axe").tiers[6].yieldMult, 1e-6);
        }

        [Test]
        public void UpgradeLevels_ValuesMoveTheRightWay_AndCostsRise()
        {
            foreach (UpgradeDef u in _c.upgrades)
            {
                double prevValue = u.effect == EffectKind.FuelEfficiency ? 1.0 : 0.0, prevCost = -1;
                for (int i = 0; i < u.levels.Length; i++)
                {
                    UpgradeLevelDef d = u.levels[i];
                    if (u.effect == EffectKind.FuelEfficiency) Assert.Less(d.value, prevValue, u.id + " L" + d.level);
                    else Assert.Greater(d.value, prevValue, u.id + " L" + d.level);
                    double c = _c.Prices.Value(d.cost);
                    Assert.Greater(c, prevCost, u.id + " L" + d.level + " cost must rise");
                    if (i > 0) Assert.GreaterOrEqual(d.requiresReachTier, u.levels[i - 1].requiresReachTier);
                    prevValue = d.value; prevCost = c;
                }
            }
            Assert.AreEqual(9.0, _c.Upgrade("pack").levels[0].value - 9.0, 1e-9, "backpack level 1 adds one row of 9 to the base 9");
            Assert.AreEqual(81.0, _c.Upgrade("pack").levels[7].value, 1e-9);
        }

        [Test]
        public void BuildTimes_AreMonotone_AndWithinTheCaps()
        {
            var times = new JobTimes(_t);
            double cap = times.Cap;
            foreach (StructureDef s in _c.structures)
            {
                int prev = -1;
                foreach (StructureLevelDef d in s.levels)
                {
                    Assert.GreaterOrEqual(d.buildSeconds, prev, s.id + " L" + d.level);
                    if (s.role == StructureRole.Megaproject) { Assert.LessOrEqual(d.buildSeconds, times.MegaCap, s.id); Assert.Greater(d.buildSeconds, cap, "megaproject stages are longer than ordinary jobs"); }
                    else Assert.LessOrEqual(d.buildSeconds, cap, s.id + " L" + d.level + " exceeds a day");
                    prev = d.buildSeconds;
                }
            }
            foreach (string id in new[] { "lumber", "mine", "refinery", "warehouse", "alarm", "gunner" })
                Assert.AreEqual(0, _c.Structure(id).levels[0].buildSeconds, id + " first build is instant so the first session flows");
            Assert.LessOrEqual(_c.Structure("research_lab").levels[0].buildSeconds, 10);
            Assert.GreaterOrEqual(_c.Structure("chem_reactor").levels[9].buildSeconds, 20 * 3600, "late levels climb to about a day");
            int ordinaryMax = 0;
            foreach (StructureDef s in _c.structures) if (s.role != StructureRole.Megaproject) foreach (StructureLevelDef d in s.levels) ordinaryMax = Math.Max(ordinaryMax, d.buildSeconds);
            Assert.AreEqual(86400, ordinaryMax, "the longest ordinary job is exactly the one-day cap");
        }

        [Test]
        public void LevelRequirements_RewardGoingOutward()
        {
            foreach (StructureDef s in _c.structures)
            {
                int prev = 0;
                foreach (StructureLevelDef d in s.levels) { Assert.GreaterOrEqual(d.requiresReachTier, prev); prev = d.requiresReachTier; }
                Assert.AreEqual(s.unlockTier, s.levels[0].requiresReachTier);
            }
            Assert.AreEqual(4, _c.Structure("lumber").levels[9].requiresReachTier, "maxing the starter Lumber Camp needs the Industry era");
            Assert.AreEqual(4, _c.Structure("warehouse").levels[9].requiresReachTier);
        }

        [Test]
        public void TierZeroVolumeDemand_GrowsWithTier_SoTheReturnLoopExists()
        {
            var demand = new double[CatalogBuilder.EraCount];
            var value = new double[CatalogBuilder.EraCount];
            var megaDemand = new double[CatalogBuilder.EraCount];
            foreach (StructureDef s in _c.structures)
                foreach (StructureLevelDef d in s.levels)
                {
                    int tier = Math.Min(d.requiresReachTier, demand.Length - 1);
                    double t0 = _c.Prices.T0Volume(d.cost);
                    if (s.role == StructureRole.Megaproject) megaDemand[tier] += t0; else demand[tier] += t0;
                    value[tier] += _c.Prices.Value(d.cost);
                }
            foreach (ToolLadderDef l in _c.tools)
                foreach (ToolTierDef d in l.tiers) { int tier = Math.Min(d.eraTier, demand.Length - 1); demand[tier] += _c.Prices.T0Volume(d.cost); value[tier] += _c.Prices.Value(d.cost); }
            foreach (UpgradeDef u in _c.upgrades)
                foreach (UpgradeLevelDef d in u.levels) { int tier = Math.Min(d.eraTier, demand.Length - 1); demand[tier] += _c.Prices.T0Volume(d.cost); value[tier] += _c.Prices.Value(d.cost); }
            foreach (ResearchNodeDef n in _c.research) { int tier = Math.Min(n.tier, demand.Length - 1); demand[tier] += _c.Prices.T0Volume(n.cost); value[tier] += _c.Prices.Value(n.cost); }
            var line = new List<string>();
            for (int tier = 0; tier < demand.Length; tier++) line.Add("T" + tier + ": " + (demand[tier] + megaDemand[tier]).ToString("0") + " wood-eq (" + value[tier].ToString("0") + " value)");
            TestLog.Info("tier-0 volume demanded by purchases that unlock at each reach tier: " + string.Join("; ", line));
            for (int tier = 1; tier <= 7; tier++)
                Assert.Greater(demand[tier] + megaDemand[tier], (demand[tier - 1] + megaDemand[tier - 1]) * 1.15, "tier-0 demand must grow from tier " + (tier - 1) + " to " + tier);
            Assert.Greater(demand[7] + megaDemand[7], (demand[0] + megaDemand[0]) * 1000.0, "late tiers demand thousands of times the starter volume");
        }

        [Test]
        public void ResearchTree_IsAnAcyclicTree_GatedByTheLab()
        {
            var tier = new Dictionary<string, int>();
            foreach (ResearchNodeDef n in _c.research) tier[n.id] = n.tier;
            foreach (ResearchNodeDef n in _c.research)
            {
                Assert.AreEqual(n.tier, n.labLevel, n.id + " needs a Research Lab of its tier");
                foreach (string p in n.prereqs) Assert.LessOrEqual(tier[p], n.tier, n.id + " has a prerequisite of a later tier (" + p + ")");
                Assert.IsFalse(Array.IndexOf(n.prereqs, n.id) >= 0);
                Assert.Greater(n.cost.Length, 0);
            }
            // Every structure with an unlock research node is actually unlocked by it.
            foreach (StructureDef s in _c.structures)
            {
                if (s.unlockResearch == null) continue;
                bool found = false;
                foreach (string u in _c.Research(s.unlockResearch).unlocks) if (u == "structure:" + s.id) found = true;
                Assert.IsTrue(found, s.id + " is not listed in the unlocks of " + s.unlockResearch);
            }
            foreach (RecipeDef r in _c.recipes)
            {
                if (r.unlockResearch == null) continue;
                bool found = false;
                foreach (string u in _c.Research(r.unlockResearch).unlocks) if (u == "recipe:" + r.id) found = true;
                Assert.IsTrue(found, r.id + " is not listed in the unlocks of " + r.unlockResearch);
            }
            // Depth check: the longest prerequisite chain.
            var depth = new Dictionary<string, int>();
            int longest = 0;
            foreach (ResearchNodeDef n in _c.research) longest = Math.Max(longest, Depth(n.id, depth));
            TestLog.Info(_c.research.Length + " research nodes, longest prerequisite chain " + longest);
            Assert.GreaterOrEqual(longest, 6);
        }

        int Depth(string id, Dictionary<string, int> memo)
        {
            int d;
            if (memo.TryGetValue(id, out d)) return d;
            d = 1;
            foreach (string p in _c.Research(id).prereqs) d = Math.Max(d, 1 + Depth(p, memo));
            memo[id] = d;
            return d;
        }
    }

    [TestFixture]
    public class TuningTests
    {
        [Test]
        public void Keys_AreUnique_InRange_AndDocumented()
        {
            Tuning t = Tuning.CreateDefault();
            var seen = new HashSet<string>();
            var groups = new Dictionary<string, int>();
            for (int i = 0; i < t.Count; i++)
            {
                TuningEntry e = t.EntryAt(i);
                Assert.IsTrue(seen.Add(e.key), "duplicate key " + e.key);
                Assert.GreaterOrEqual(e.defaultValue, e.min, e.key);
                Assert.LessOrEqual(e.defaultValue, e.max, e.key);
                Assert.AreEqual(e.defaultValue, e.value, e.key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.description), e.key + " needs a description");
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.group), e.key + " needs a group");
                int n; groups.TryGetValue(e.group, out n); groups[e.group] = n + 1;
            }
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> kv in groups) parts.Add(kv.Key + " " + kv.Value);
            TestLog.Info(t.Count + " tuning keys: " + string.Join(", ", parts));
            Assert.Greater(t.Count, 300);
        }

        [Test]
        public void Set_ClampsToTheLegalRange_AndCloneIsIndependent()
        {
            Tuning t = Tuning.CreateDefault();
            Tuning copy = t.Clone();
            t.Set("cost.scale.structure", 1000);
            Assert.AreEqual(t.EntryAt(IndexOf(t, "cost.scale.structure")).max, t.Get("cost.scale.structure"));
            t.Set("cost.scale.structure", -5);
            Assert.AreEqual(t.EntryAt(IndexOf(t, "cost.scale.structure")).min, t.Get("cost.scale.structure"), 1e-6);
            Assert.AreEqual(1.0f, copy.Get("cost.scale.structure"), "the clone is not affected");
            Assert.AreNotEqual(t.Digest(), copy.Digest());
            t.ResetAll();
            Assert.AreEqual(copy.Digest(), t.Digest());
            Assert.Throws<KeyNotFoundException>(() => t.Get("no.such.key"));
        }

        [Test]
        public void TryApplyOverrides_AcceptsLegalBounds_AndUsesInvariantCulture()
        {
            Tuning t = Tuning.CreateDefault();
            TuningEntry entry = t.EntryAt(IndexOf(t, "cost.scale.structure"));
            TuningEntry timeScale = t.EntryAt(IndexOf(t, "time.scale"));
            var overrides = new Dictionary<string, string>
            {
                { "cost.scale.structure", entry.min.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "time.scale", timeScale.max.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "score.world_base", "1.25" }
            };

            Tuning effective;
            string error;
            Assert.IsTrue(t.TryApplyOverrides(overrides, out effective, out error), error);
            Assert.AreEqual(entry.min, effective.Get("cost.scale.structure"));
            Assert.AreEqual(timeScale.max, effective.Get("time.scale"));
            Assert.AreEqual(1.25f, effective.Get("score.world_base"));
            Assert.AreEqual(entry.defaultValue, t.Get("cost.scale.structure"), "the source tuning is unchanged");
            Assert.AreNotEqual(t.Digest(), effective.Digest());
        }

        [TestCase("unknown.key", "1")]
        [TestCase("score.world_base", "not-a-number")]
        [TestCase("score.world_base", "NaN")]
        [TestCase("score.world_base", "Infinity")]
        public void TryApplyOverrides_RejectsInvalidSetWithoutPartialChanges(string key, string value)
        {
            Tuning t = Tuning.CreateDefault();
            Tuning original = t.Clone();
            var overrides = new Dictionary<string, string>
            {
                { "cost.scale.structure", "2" },
                { key, value }
            };

            Tuning effective;
            string error;
            Assert.IsFalse(t.TryApplyOverrides(overrides, out effective, out error));
            Assert.IsNotEmpty(error);
            Assert.AreEqual(original.Digest(), effective.Digest(), "failure returns the unmodified local tuning");
            Assert.AreEqual(original.Digest(), t.Digest(), "failure does not mutate the source");
        }

        [Test]
        public void TryApplyOverrides_RejectsValuesOutsideLegalBounds()
        {
            Tuning t = Tuning.CreateDefault();
            TuningEntry entry = t.EntryAt(IndexOf(t, "score.world_base"));
            foreach (float value in new float[] { entry.min - 1f, entry.max + 1f })
            {
                Tuning effective;
                string error;
                Assert.IsFalse(t.TryApplyOverrides(
                    new Dictionary<string, string> { { entry.key, value.ToString(System.Globalization.CultureInfo.InvariantCulture) } },
                    out effective, out error));
                Assert.IsNotEmpty(error);
                Assert.AreEqual(t.Digest(), effective.Digest());
            }
        }

        static int IndexOf(Tuning t, string key)
        {
            for (int i = 0; i < t.Count; i++) if (t.EntryAt(i).key == key) return i;
            return -1;
        }

        [Test]
        public void ChangingAKey_ChangesTheCatalog_Deterministically()
        {
            Tuning a = Tuning.CreateDefault(), b = Tuning.CreateDefault();
            EconomyCatalog ca = CatalogBuilder.Build(a), cb = CatalogBuilder.Build(b);
            Assert.AreEqual(Dump(ca), Dump(cb), "same tuning, same catalog");
            b.Set("cost.scale.structure", 2.0);
            b.Set("time.scale", 2.0);
            b.Set("tool.mult.7", 20.0);
            b.Set("value.raw.wood", 2.0);
            EconomyCatalog c2 = CatalogBuilder.Build(b);
            Assert.AreNotEqual(Dump(ca), Dump(c2));
            Assert.Greater(c2.Prices.Value(c2.Structure("refinery").levels[4].cost), ca.Prices.Value(ca.Structure("refinery").levels[4].cost) * 1.2);
            Assert.AreEqual(20.0, c2.Tool("axe").tiers[6].yieldMult, 1e-6);
            Assert.AreEqual(2.0, c2.Resource("wood").baseValue, 1e-6);
            Assert.Greater(c2.Resource("planks").baseValue, ca.Resource("planks").baseValue, "crafted prices follow their inputs");
        }

        [Test]
        public void EveryItemHasItsOwnCostKnobs()
        {
            Tuning t = Tuning.CreateDefault();
            EconomyCatalog c = CatalogBuilder.Build(t);
            foreach (StructureDef s in c.structures) { Assert.IsTrue(t.Has("cost.v." + s.id), s.id); Assert.IsTrue(t.Has("cost.g." + s.id), s.id); Assert.IsTrue(t.Has("time.start." + s.id), s.id); }
            foreach (ToolLadderDef l in c.tools) Assert.IsTrue(t.Has("cost.v." + l.id));
            foreach (UpgradeDef u in c.upgrades) { Assert.IsTrue(t.Has("cost.v." + u.id)); for (int i = 1; i <= u.maxLevel; i++) Assert.IsTrue(t.Has("upg." + u.id + "." + i)); }
            foreach (ResearchNodeDef n in c.research) Assert.IsTrue(t.Has("cost.v." + n.id));
            foreach (ResourceDef r in c.resources) if (r.category == ResourceCategory.Raw) { Assert.IsTrue(t.Has("value.raw." + r.id)); Assert.IsTrue(t.Has("yield.base." + r.id)); }
            // Halving one structure's budget halves its level 1 cost (scrap-based Refinery).
            t.Set("cost.v.refinery", 30);
            Assert.AreEqual(30, c.Structure("refinery").levels[0].cost[0].amount + 15, "baseline check of the default (15 scrap)");
            EconomyCatalog c2 = CatalogBuilder.Build(t);
            Assert.AreEqual(30, c2.Structure("refinery").levels[0].cost[0].amount);
        }

        static string Dump(EconomyCatalog c)
        {
            var sb = new System.Text.StringBuilder();
            foreach (ResourceDef r in c.resources) sb.Append(r.id).Append(r.baseValue).Append(';');
            foreach (StructureDef s in c.structures) foreach (StructureLevelDef d in s.levels) { sb.Append(s.id).Append(d.level).Append(':').Append(d.buildSeconds); foreach (ItemAmount a in d.cost) sb.Append(a.resourceId).Append(a.amount); sb.Append(';'); }
            foreach (ToolLadderDef l in c.tools) foreach (ToolTierDef d in l.tiers) { sb.Append(l.id).Append(d.tier).Append(d.yieldMult); foreach (ItemAmount a in d.cost) sb.Append(a.resourceId).Append(a.amount); sb.Append(';'); }
            foreach (UpgradeDef u in c.upgrades) foreach (UpgradeLevelDef d in u.levels) { sb.Append(u.id).Append(d.level).Append(d.value); foreach (ItemAmount a in d.cost) sb.Append(a.resourceId).Append(a.amount); sb.Append(';'); }
            foreach (ResearchNodeDef n in c.research) { sb.Append(n.id); foreach (ItemAmount a in n.cost) sb.Append(a.resourceId).Append(a.amount); sb.Append(';'); }
            return sb.ToString();
        }
    }
}
