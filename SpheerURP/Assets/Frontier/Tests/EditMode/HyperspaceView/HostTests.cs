using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Frontier.Hyperspace;
using Frontier.Hyperspace.View;

namespace Frontier.Tests.HyperspaceView
{
    public class ShipStatsMapperTests
    {
        static void AssertSame(ShipStats a, ShipStats b)
        {
            Assert.AreEqual(a.hull, b.hull, 1e-4f, "hull");
            Assert.AreEqual(a.shield, b.shield, "shield");
            Assert.AreEqual(a.shieldRecharge, b.shieldRecharge, 1e-4f, "shieldRecharge");
            Assert.AreEqual(a.damage, b.damage, 1e-4f, "damage");
            Assert.AreEqual(a.fireRate, b.fireRate, 1e-4f, "fireRate");
            Assert.AreEqual(a.pierce, b.pierce, "pierce");
            Assert.AreEqual(a.magnet, b.magnet, 1e-4f, "magnet");
            Assert.AreEqual(a.handling, b.handling, 1e-4f, "handling");
            Assert.AreEqual(a.bombCapacity, b.bombCapacity, "bombCapacity");
        }

        [Test]
        public void NothingBought_IsTheStarterShip()
        {
            AssertSame(ShipStats.Starter(), ShipStatsMapper.Map(new ShipFamilies { playerLevel = 1 }));
        }

        [Test]
        public void EverythingMaxed_IsTheEndgameShip()
        {
            var f = new ShipFamilies { reactor = 1, turretDamage = 1, turretRate = 1, pulse = 1, reward = 1, engine = 1, playerLevel = 30 };
            AssertSame(ShipStats.Endgame(), ShipStatsMapper.Map(f));
        }

        [Test]
        public void EqualFamilies_MatchTheSimulationsProgressCurve()
        {
            var f = new ShipFamilies { reactor = 0.5f, turretDamage = 0.5f, turretRate = 0.5f, pulse = 0.5f, reward = 0.5f, engine = 0.5f, playerLevel = 16 };
            var mid = ShipStats.AtProgress(0.5f);
            var m = ShipStatsMapper.Map(f);
            Assert.AreEqual(mid.hull, m.hull, 1e-3f);
            Assert.AreEqual(mid.damage, m.damage, 1e-3f);
            Assert.AreEqual(mid.fireRate, m.fireRate, 1e-3f);
            Assert.AreEqual(mid.handling, m.handling, 1e-3f);
            Assert.AreEqual(mid.shield, m.shield);
        }

        [Test]
        public void EachFamilyOnlyMovesItsOwnStats_AndNeverDecreasesThem()
        {
            var zero = ShipStatsMapper.Map(new ShipFamilies { playerLevel = 1 });
            for (int fam = 0; fam < 6; fam++)
            {
                float prevTotal = -1f;
                for (int step = 0; step <= 10; step++)
                {
                    var f = new ShipFamilies { playerLevel = 1 };
                    float v = step / 10f;
                    switch (fam)
                    {
                        case 0: f.reactor = v; break;
                        case 1: f.turretDamage = v; break;
                        case 2: f.turretRate = v; break;
                        case 3: f.pulse = v; break;
                        case 4: f.reward = v; break;
                        default: f.engine = v; break;
                    }
                    var m = ShipStatsMapper.Map(f);
                    float total = m.hull + m.shield + m.damage + m.fireRate + m.pierce + m.magnet + m.handling - m.shieldRecharge;
                    Assert.GreaterOrEqual(total, prevTotal - 1e-3f, "family " + fam + " must be monotonic");
                    prevTotal = total;
                    if (fam == 1) { Assert.AreEqual(zero.hull, m.hull, 1e-4f, "turret damage must not touch the hull"); Assert.AreEqual(zero.fireRate, m.fireRate, 1e-4f); }
                    if (fam == 2) Assert.AreEqual(zero.damage, m.damage, 1e-4f, "turret rate must not touch the damage");
                }
            }
        }

        [Test]
        public void OutOfRangeAndNaN_AreClamped()
        {
            var f = new ShipFamilies { reactor = 7f, turretDamage = -3f, turretRate = float.NaN, pulse = float.PositiveInfinity, reward = 2f, engine = -1f, playerLevel = -5 };
            var m = ShipStatsMapper.Map(f);
            var end = ShipStats.Endgame();
            var start = ShipStats.Starter();
            Assert.AreEqual(end.hull, m.hull, 1e-3f);
            Assert.AreEqual(start.damage, m.damage, 1e-3f);
            Assert.AreEqual(start.fireRate, m.fireRate, 1e-3f);
            Assert.AreEqual(end.pierce, m.pierce);
            Assert.AreEqual(start.handling, m.handling, 1e-3f);
            Assert.AreEqual(1, m.bombCapacity);
        }

        [Test]
        public void BombBay_GrowsWithPlayerLevel()
        {
            int[] expected = { 1, 1, 2, 2, 3, 3, 4, 4 };
            int[] levels = { 1, 7, 8, 15, 16, 23, 24, 99 };
            for (int i = 0; i < levels.Length; i++)
                Assert.AreEqual(expected[i], ShipStatsMapper.Map(new ShipFamilies { playerLevel = levels[i] }).bombCapacity, "level " + levels[i]);
        }

        [Test]
        public void ShipPower_RisesWithEveryFamily()
        {
            var t = new HyperspaceTuning();
            float p0 = ShipStatsMapper.Map(new ShipFamilies { playerLevel = 1 }).Power(t);
            Assert.AreEqual(1f, p0, 0.05f, "the starter ship is power 1");
            float prev = p0;
            for (int step = 1; step <= 10; step++)
            {
                float v = step / 10f;
                var s = ShipStatsMapper.Map(new ShipFamilies { reactor = v, turretDamage = v, turretRate = v, pulse = v, reward = v, engine = v, playerLevel = 1 + step * 3 });
                float p = s.Power(t);
                Assert.Greater(p, prev - 1e-4f);
                prev = p;
            }
            Assert.Greater(prev, 3f, "a maxed ship is far stronger than the starter");
        }
    }

    public class ResultMappingTests
    {
        static readonly Dictionary<string, ResourceSO> Resources = new Dictionary<string, ResourceSO>();

        [OneTimeSetUp]
        public void MakeResources()
        {
            foreach (var id in new[] { "scrap", "alloy", "core", "fuel" })
            {
                var r = ScriptableObject.CreateInstance<ResourceSO>();
                r.id = id;
                r.displayName = id;
                Resources[id] = r;
            }
        }

        [OneTimeTearDown]
        public void FreeResources()
        {
            foreach (var r in Resources.Values) Object.DestroyImmediate(r);
            Resources.Clear();
        }

        static ResourceSO Find(string id) { ResourceSO r; return id != null && Resources.TryGetValue(id, out r) ? r : null; }

        [Test]
        public void LootTable_MapsEveryKindToAnExistingResource()
        {
            Assert.AreEqual("scrap", HyperspaceLoot.IdFor(LootKind.Salvage));
            Assert.AreEqual("alloy", HyperspaceLoot.IdFor(LootKind.Component));
            Assert.AreEqual("core", HyperspaceLoot.IdFor(LootKind.Core));
            Assert.AreEqual(3, HyperspaceLoot.ResourceIds.Length, "one entry per LootKind, kept in a single table");
            foreach (LootKind k in System.Enum.GetValues(typeof(LootKind))) Assert.IsNotNull(Find(HyperspaceLoot.IdFor(k)));
        }

        [Test]
        public void LootAmounts_ScaleWithYield_AndNeverVanish()
        {
            Assert.AreEqual(0, HyperspaceLoot.Scale(0, 1.5f));
            Assert.AreEqual(1, HyperspaceLoot.Scale(1, 0.4f), "a drop is never rounded away");
            Assert.AreEqual(11, HyperspaceLoot.Scale(10, 1.1f));
            Assert.AreEqual(15, HyperspaceLoot.Scale(10, 1.5f));
            var drops = new[] { new LootDrop { kind = LootKind.Salvage, count = 10 }, new LootDrop { kind = LootKind.Core, count = 2 } };
            var list = HyperspaceLoot.Build(drops, 1.5f, Find);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("scrap", list[0].resource.id); Assert.AreEqual(15, list[0].amount);
            Assert.AreEqual("core", list[1].resource.id); Assert.AreEqual(3, list[1].amount);
            Assert.AreEqual(0, HyperspaceLoot.Build(drops, 1.5f, id => null).Count, "unknown resources are skipped");
        }

        static RunResult Result(bool reached, bool defeated, float refund = 0.3f)
        {
            return new RunResult
            {
                grade = Grade.A, quality = 0.8f, yieldMultiplier = 1.28f, killCount = 17, maxCombo = 9, cleanRun = reached && !defeated,
                fuelRefundFraction = refund, reachedEnd = reached, defeated = defeated, revivesUsed = 1, xp = 42,
                lootList = new[] { new LootDrop { kind = LootKind.Salvage, count = 5 }, new LootDrop { kind = LootKind.Component, count = 1 } }
            };
        }

        [Test]
        public void CompletedRun_CarriesRefundLootAndXp()
        {
            var t = HyperspaceResults.ToTravelResult(Result(true, false), Find);
            Assert.IsTrue(t.completed);
            Assert.AreEqual(0.3f, t.quality, 1e-5f, "quality carries the refund fraction");
            Assert.AreEqual("A", t.grade);
            Assert.AreEqual(17, t.kills);
            Assert.AreEqual(1, t.revivesUsed);
            Assert.AreEqual(42, t.xp);
            Assert.AreEqual(2, t.loot.Count);
            Assert.AreEqual(6, t.loot[0].amount);
            Assert.AreEqual(1, t.loot[1].amount);
        }

        [Test]
        public void ComponentLoot_UsesCanonicalSteel_AndRetainsAlloyForLegacyFallback()
        {
            var selected = GameDatabase.Load();
            var imported = HyperspaceResults.ToTravelResult(Result(true, false), selected.ResolveResource);
            Assert.AreEqual(2, imported.loot.Count);
            Assert.AreSame(selected.FindResource("steel"), imported.loot[1].resource);
            var legacy = GameDatabase.LoadLegacy();
            var old = HyperspaceResults.ToTravelResult(Result(true, false), legacy.ResolveResource);
            Assert.AreSame(legacy.FindResource("alloy"), old.loot[1].resource);
        }

        [Test]
        public void AbandonedOrDefeatedRun_GivesNothing()
        {
            foreach (var r in new[] { Result(false, true), Result(true, true) })
            {
                var t = HyperspaceResults.ToTravelResult(r, Find);
                Assert.IsFalse(t.completed);
                Assert.AreEqual(0f, t.quality);
                Assert.AreEqual(0, t.xp);
                Assert.AreEqual(0, t.loot.Count);
            }
        }

        [Test]
        public void FuelRefund_UsesTheFlowsFormula()
        {
            var t = HyperspaceResults.ToTravelResult(Result(true, false, 0.5f), Find);
            Assert.AreEqual(Mathf.FloorToInt(20 * 0.4f * 0.5f), HyperspaceResults.FuelRefund(20, 0.4f, t));
            Assert.AreEqual(0, HyperspaceResults.FuelRefund(20, 0.4f, HyperspaceResults.ToTravelResult(Result(false, true), Find)));
        }

        [Test]
        public void RealRun_ProducesAConsistentTravelResult()
        {
            var tuning = new HyperspaceTuning();
            var ship = ShipStats.Starter();
            var request = new TravelRunRequest { seed = 99, distanceLy = 5, danger = 0.6f, tier = 0 };
            var run = HyperspaceSetup.CreateRun(request, ship, tuning, false);
            var bot = new Bot(BotSkill.Average(), tuning, request.seed);
            while (run.State != RunState.Ended)
            {
                if (run.State == RunState.Downed) run.Abandon(); else run.Step(bot.Decide(run));
            }
            var result = run.BuildResult();
            var t = HyperspaceResults.ToTravelResult(result, Find);
            Assert.AreEqual(result.reachedEnd && !result.defeated, t.completed);
            Assert.AreEqual(result.killCount, t.kills);
            Assert.AreEqual(result.grade.ToString(), t.grade);
            if (t.completed) Assert.AreEqual(result.fuelRefundFraction, t.quality, 1e-5f);
        }
    }

    public class SetupTests
    {
        [Test]
        public void Params_CarryTheRequest_AndClampWhatTheSimulationCannotTake()
        {
            var tuning = new HyperspaceTuning();
            var ship = ShipStats.AtProgress(0.5f);
            var req = new TravelRunRequest { seed = 1234567890123UL, distanceLy = 0.0, danger = 3.2f, tier = 99 };
            var p = HyperspaceSetup.BuildParams(req, ship, tuning, false);
            Assert.AreEqual(1234567890123UL, p.seed);
            Assert.AreEqual(1f, p.distanceLy, "same-system hops report 0 ly and must become 1");
            Assert.AreEqual(3.2f, p.danger, 1e-5f);
            Assert.AreEqual(12, p.tier, "tiers above 12 are clamped");
            Assert.AreEqual(tuning.ShipPower(ship), p.shipPower, 1e-5f);
            Assert.AreEqual(RunMode.Normal, p.mode);
            Assert.AreEqual(RunMode.Assist, HyperspaceSetup.BuildParams(req, ship, tuning, true).mode);
            req.distanceLy = 812.5; req.tier = -3;
            p = HyperspaceSetup.BuildParams(req, ship, tuning, false);
            Assert.AreEqual(812.5f, p.distanceLy, 1e-3f);
            Assert.AreEqual(0, p.tier);
        }

        [Test]
        public void SameRequest_GivesTheSameRun()
        {
            var tuning = new HyperspaceTuning();
            var ship = ShipStats.Starter();
            var req = new TravelRunRequest { seed = 77, distanceLy = 50, danger = 1.5f, tier = 2 };
            var a = HyperspaceSetup.CreateRun(req, ship, tuning, false);
            var b = HyperspaceSetup.CreateRun(req, ship, tuning, false);
            Assert.AreEqual(a.Plan.Digest(), b.Plan.Digest());
            req.seed = 78;
            Assert.AreNotEqual(a.Plan.Digest(), HyperspaceSetup.CreateRun(req, ship, tuning, false).Plan.Digest(), "the seed changes the route");
        }

        [Test]
        public void AssistMode_ShrinksTheEncounterBudget()
        {
            var tuning = new HyperspaceTuning();
            var ship = ShipStats.Starter();
            var req = new TravelRunRequest { seed = 5, distanceLy = 200, danger = 3f, tier = 4 };
            var normal = HyperspaceSetup.CreateRun(req, ship, tuning, false);
            var assist = HyperspaceSetup.CreateRun(req, ship, tuning, true);
            Assert.Less(assist.Route.budgetTotal, normal.Route.budgetTotal);
        }

        [Test]
        public void FreeRevives_DefaultToOne()
        {
            var s = ScriptableObject.CreateInstance<FrontierSettings>();
            try { Assert.AreEqual(1, s.freeRevivesPerRun); }
            finally { Object.DestroyImmediate(s); }
        }
    }

    public class TravelPrefsTests
    {
        IPrefsStore previous;

        [SetUp] public void Swap() { previous = TravelPrefs.Store; TravelPrefs.Store = new MemoryPrefsStore(); }
        [TearDown] public void Restore() { TravelPrefs.Store = previous; }

        [Test]
        public void TutorialTips_AppearOnTheFirstTwoRunsOnly()
        {
            Assert.IsTrue(TravelPrefs.ShowTutorial);
            TravelPrefs.MarkTutorialRunShown();
            Assert.IsTrue(TravelPrefs.ShowTutorial);
            TravelPrefs.MarkTutorialRunShown();
            Assert.IsFalse(TravelPrefs.ShowTutorial);
            Assert.AreEqual(2, TravelPrefs.TutorialRunsShown);
            TravelPrefs.MarkTutorialRunShown();
            Assert.IsFalse(TravelPrefs.ShowTutorial);
        }

        [Test]
        public void TutorialFlag_SurvivesANewSession()
        {
            var shared = new MemoryPrefsStore();
            TravelPrefs.Store = shared;
            TravelPrefs.MarkTutorialRunShown();
            TravelPrefs.MarkTutorialRunShown();
            TravelPrefs.Store = shared;
            Assert.IsFalse(TravelPrefs.ShowTutorial, "the count lives in the store, not in a static");
        }

        [Test]
        public void Options_RoundTripAndClamp()
        {
            Assert.IsFalse(TravelPrefs.AssistMode);
            TravelPrefs.AssistMode = true;
            Assert.IsTrue(TravelPrefs.AssistMode);
            Assert.AreEqual(TravelPrefs.SensitivityDefault, TravelPrefs.DragSensitivity, 1e-4f);
            TravelPrefs.DragSensitivity = 9f;
            Assert.AreEqual(TravelPrefs.SensitivityMax, TravelPrefs.DragSensitivity, 1e-4f);
            TravelPrefs.DragSensitivity = 0.01f;
            Assert.AreEqual(TravelPrefs.SensitivityMin, TravelPrefs.DragSensitivity, 1e-4f);
            TravelPrefs.DragSensitivity = 1.44f;
            Assert.AreEqual(1.4f, TravelPrefs.DragSensitivity, 1e-4f, "stored in 0.1 steps");
            Assert.IsFalse(TravelPrefs.UseLegacyGame);
            TravelPrefs.UseLegacyGame = true;
            Assert.IsTrue(TravelPrefs.UseLegacyGame);
        }
    }
}
