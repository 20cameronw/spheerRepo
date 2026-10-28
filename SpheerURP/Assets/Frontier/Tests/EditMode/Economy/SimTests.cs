using System;
using System.Collections.Generic;
using Frontier.Economy;
using Frontier.Universe;
using NUnit.Framework;

namespace Frontier.Economy.Tests
{
    /// <summary>The balance simulator: deterministic, and the three player archetypes land in the era bands without dead zones.</summary>
    [TestFixture, Category("Slow")]
    public class SimulatorTests
    {
        const double Horizon = 730.0;
        static readonly string[] Names = { "casual", "regular", "hardcore" };

        [Test]
        public void SameSeed_GivesTheSameTimeline()
        {
            Archetype a = SimFx.Archetype("regular");
            var s1 = new SimSettings { seed = 7UL, horizonDays = 120 };
            SimResult x = SimEngine.Run(SimFx.Catalog, SimFx.Tuning, SimFx.World, a, s1);
            SimResult y = SimEngine.Run(SimFx.Catalog, SimFx.Tuning, SimFx.World, a, new SimSettings { seed = 7UL, horizonDays = 120 });
            Assert.AreEqual(x.Digest(), y.Digest(), "same seed, same digest");
            Assert.AreEqual(x.milestoneOrder.Count, y.milestoneOrder.Count);
            for (int i = 0; i < x.milestoneOrder.Count; i++)
            {
                Assert.AreEqual(x.milestoneOrder[i], y.milestoneOrder[i]);
                Assert.AreEqual(x.milestones[x.milestoneOrder[i]], y.milestones[y.milestoneOrder[i]], 0.0, x.milestoneOrder[i]);
            }
            Assert.AreEqual(x.finalScore, y.finalScore);
            SimResult z = SimEngine.Run(SimFx.Catalog, SimFx.Tuning, SimFx.World, a, new SimSettings { seed = 8UL, horizonDays = 120 });
            Assert.AreNotEqual(x.Digest(), z.Digest(), "another seed plays another game");
            TestLog.Info("regular, 120 days: digest " + x.Digest().ToString("x") + " (seed 7) vs " + z.Digest().ToString("x") + " (seed 8)");
        }

        [Test]
        public void EraBands_HoldForAllThreeArchetypes()
        {
            var all = new List<string>();
            foreach (string name in Names)
            {
                SimResult r = SimFx.Run(name, 1UL, Horizon);
                all.AddRange(SimBands.Check(r));
                TestLog.Info(name + ": settlement " + r.Days("era_settlement").ToString("0.0") + " d, industry " + r.Days("era_industry").ToString("0.0") + " d, interstellar " + r.Days("era_interstellar").ToString("0.0") + " d, ascension " + r.Days("era_ascension").ToString("0.0") + " d");
            }
            Assert.IsEmpty(all, string.Join("\n", all));
        }

        [Test]
        public void EraBands_AreRobustAgainstOtherPlayerSeeds()
        {
            var all = new List<string>();
            foreach (ulong seed in new ulong[] { 2UL, 3UL })
                foreach (string name in Names)
                    all.AddRange(SimBands.Check(SimFx.Run(name, seed, Horizon)));
            Assert.IsEmpty(all, string.Join("\n", all));
        }

        [Test]
        public void EraBands_AndDeadZones_HoldInOtherUniverses()
        {
            // The same economy in three other seeded universes (the Ascension reset makes a new one): bands, dead zones and stuck goals.
            var all = new List<string>();
            foreach (ulong worldSeed in new ulong[] { 3UL, 4UL, 5UL })
            {
                SimWorld world = SimWorld.Build(SimFx.Catalog, StarterSystem.CreateUniverse(SimFx.Catalog, worldSeed), 3600.0);
                foreach (string name in Names)
                {
                    SimResult r = SimEngine.Run(SimFx.Catalog, SimFx.Tuning, world, SimFx.Archetype(name), new SimSettings { seed = 1UL, horizonDays = Horizon });
                    foreach (string s in SimBands.Check(r)) all.Add("universe " + worldSeed + ": " + s);
                    foreach (string s in SimBands.CheckGoalGaps(r)) all.Add("universe " + worldSeed + ": " + s);
                    if (r.stuck != 0) all.Add("universe " + worldSeed + " " + name + ": " + r.stuck + " stuck goals");
                    TestLog.Info("universe " + worldSeed + " " + name + ": settlement " + r.Days("era_settlement").ToString("0.0") + " d, industry " + r.Days("era_industry").ToString("0.0") + " d, interstellar " + r.Days("era_interstellar").ToString("0.0") + " d, ascension " + r.Days("era_ascension").ToString("0.0") + " d");
                }
            }
            Assert.IsEmpty(all, string.Join("\n", all));
        }

        [Test]
        public void FirstSession_ReachesFirstWarehouseFirstFuelAndFirstHop()
        {
            SimResult r = SimFx.Run("regular", 1UL, Horizon);
            Assert.Less(r.Days("first_warehouse") * 1440.0, 15.0, "warehouse within a 15 minute session");
            Assert.Less(r.Days("first_fuel") * 1440.0, 15.0, "first fuel within a 15 minute session");
            Assert.Less(r.Days("first_hop") * 1440.0, 15.0, "first hop within a 15 minute session");
            Assert.Less(r.Days("era_foothold"), 1.0, "day one is Foothold");
            // The pack is small on purpose: the warehouse comes before anything else that needs storing.
            Assert.Less(r.Days("first_warehouse"), r.Days("first_hop"));
        }

        [Test]
        public void NoDeadZones_AndNoGoalGetsStuck()
        {
            var all = new List<string>();
            foreach (string name in Names)
            {
                SimResult r = SimFx.Run(name, 1UL, Horizon);
                all.AddRange(SimBands.CheckGoalGaps(r));
                Assert.AreEqual(0, r.stuck, name + ": a headline goal had no way forward");
                double sessionMinutes = SimFx.Archetype(name).DailyMinutes * Horizon;
                TestLog.Info(name + ": played " + (r.playSeconds / 3600.0).ToString("0") + " h of " + (sessionMinutes / 60.0).ToString("0") + " h offered, " + r.goals.Count + " headline goals done");
            }
            Assert.IsEmpty(all, string.Join("\n", all));
        }

        [Test]
        public void NothingIsWastedNorBlocked_TheBagsNeverStarveAHarvest()
        {
            foreach (string name in Names)
            {
                SimResult r = SimFx.Run(name, 1UL, Horizon);
                Archetype a = SimFx.Archetype(name);
                // Through the designed eras (before the megaprojects) a player who opens the app always finds something to do. Later the
                // multi-day megaproject timers legitimately leave gaps (Clash of Clans style), and the hardcore player is timer limited.
                for (int era = 0; era <= 4; era++)
                {
                    double start = era == 0 ? 0.0 : r.Days(SimBands.EraMilestones[era]);
                    double end = era == 4 ? r.Days("era_ascension") : r.Days(SimBands.EraMilestones[era + 1]);
                    if (double.IsNaN(start) || double.IsNaN(end)) continue;
                    double offeredSeconds = (end - start) * a.DailyMinutes * 60.0;
                    if (offeredSeconds < 3600.0) continue;
                    double idleShare = r.idleByEra[era] / offeredSeconds;
                    TestLog.Info(name + " " + SimBands.EraNames[era] + ": " + (100.0 * idleShare).ToString("0") + " % of the offered play time left unused");
                    if (name != "hardcore") Assert.Less(idleShare, 0.25, name + " leaves too many sessions early in " + SimBands.EraNames[era]);
                }
                Assert.Greater(r.harvestRuns, 3000, name + " should be harvesting");
            }
        }

        [Test]
        public void MoreTimeAndSkill_MeanFasterProgress_InEveryEra()
        {
            SimResult c = SimFx.Run("casual", 1UL, Horizon), g = SimFx.Run("regular", 1UL, Horizon), h = SimFx.Run("hardcore", 1UL, Horizon);
            foreach (string era in new[] { "era_settlement", "era_industry", "era_interstellar" })
            {
                Assert.Less(h.Days(era), g.Days(era), era + ": hardcore before regular");
                Assert.Less(g.Days(era), c.Days(era), era + ": regular before casual");
            }
            // The spread stays sane: the heaviest player is a few times faster, never a hundred times.
            double ratio = c.Days("era_interstellar") / h.Days("era_interstellar");
            Assert.Greater(ratio, 3.0);
            Assert.Less(ratio, 25.0);
            TestLog.Info("casual / hardcore time to Interstellar: " + ratio.ToString("0.0") + "x");
        }

        [Test]
        public void TheGameIsBiggerThanTheCasualAndRegularPlayerFinishInTwoYears_AndTheHardcorePlayerNeedsAboutAYear()
        {
            foreach (string name in Names)
            {
                SimResult r = SimFx.Run(name, 1UL, Horizon);
                double sec;
                bool finished = r.milestones.TryGetValue("ladder_complete", out sec);
                if (name == "hardcore")
                {
                    // 2.5 hours a day: every headline want ticked after about a year; Ascension and the endless goals must be ready by then
                    Assert.IsTrue(finished, "the hardcore player does run out of headline goals");
                    Assert.That(r.Days("ladder_complete"), Is.InRange(250.0, 500.0), "hardcore finishes the headline ladder after about a year");
                    TestLog.Info("hardcore: headline ladder complete on day " + r.Days("ladder_complete").ToString("0") + "; the rest of the 2-year horizon is endless goals and idle time (" + (r.idleSeconds / 3600.0).ToString("0") + " h)");
                }
                else Assert.IsFalse(finished, name + " ran out of headline goals inside two years");
                int total = SimEngine.DefaultLadderLines.Length;
                Assert.Greater(total, 120, "the headline ladder is a long list of wants");
            }
        }

        [Test]
        public void EveryEra_HasAClearListOfWants()
        {
            SimResult r = SimFx.Run("regular", 1UL, Horizon);
            var perEra = new int[6];
            foreach (SimGoalTime g in r.goals) perEra[SimBands.EraAt(r, g.seconds)]++;
            string line = "";
            for (int i = 0; i < 6; i++) line += SimBands.EraNames[i] + " " + perEra[i] + "  ";
            TestLog.Info("headline goals completed while in each era: " + line);
            Assert.GreaterOrEqual(perEra[0], 4, "Landfall has wants");
            Assert.GreaterOrEqual(perEra[1], 12, "Foothold has wants");
            Assert.GreaterOrEqual(perEra[2], 15, "Settlement has wants");
            Assert.GreaterOrEqual(perEra[3], 25, "Industry has wants");
            Assert.GreaterOrEqual(perEra[4], 8, "Interstellar has wants");
            Assert.GreaterOrEqual(perEra[5], 8, "the Ascension era has wants");
        }
    }

    /// <summary>The loop the owner asked for: a late project needs thousands of tier-0 harvests, and only the end-game tool makes that a reasonable evening.</summary>
    [TestFixture]
    public class ReturnToStarterWorldsTests
    {
        static double WoodEquivalents(EconomyCatalog c, ItemAmount[] cost)
        {
            return c.Prices.T0Volume(cost);
        }

        /// <summary>Expected wood per harvest run on a tier-0 forest: base yield x richness x tool x (1 + camp) x (0.4 + 1.1 x quality).</summary>
        static double WoodPerRun(EconomyCatalog c, int toolTier, int campLevel, double terraformStages, double quality)
        {
            double tool = c.Tool("axe").tiers[toolTier - 1].yieldMult;
            double camp = c.Structure("lumber").levels[campLevel - 1].effect;
            double baseYield = c.Resource("wood").baseYield;
            return baseYield * 1.0 * tool * (1.0 + camp + terraformStages) * (0.4 + 1.1 * quality);
        }

        [Test]
        public void LateMegaprojectStage_NeedsTierZeroVolume_ThatOnlyTheEndgameToolMakesReasonable()
        {
            EconomyCatalog c = SimFx.Catalog;
            var lines = new List<string>();
            foreach (string id in new[] { "terraformer", "warp_gate", "dyson_collector" })
            {
                StructureDef s = c.Structure(id);
                for (int stage = 1; stage <= 5; stage += 2)
                {
                    double volume = WoodEquivalents(c, s.levels[stage - 1].cost);
                    double runsT1 = volume / WoodPerRun(c, 1, 1, 0.0, 0.74);
                    double runsT3 = volume / WoodPerRun(c, 3, 5, 0.0, 0.74);
                    double runsT5 = volume / WoodPerRun(c, 5, 8, 0.0, 0.74);
                    double runsT7 = volume / WoodPerRun(c, 7, 10, 0.0, 0.74);
                    lines.Add(id + " stage " + stage + ": " + volume.ToString("0") + " wood-eq -> runs: tool 1 " + runsT1.ToString("0") + ", tool 3 + camp 5 " + runsT3.ToString("0") + ", tool 5 + camp 8 " + runsT5.ToString("0") + ", tool 7 + camp 10 " + runsT7.ToString("0") + " (" + (runsT7 * 33.0 / 3600.0).ToString("0.0") + " h)");
                    Assert.Greater(runsT1, 4000.0, id + " stage " + stage + " must be unreasonable with starter tools");
                    Assert.Greater(runsT1 / runsT7, 30.0, "the end-game tool and camp do the job at least 30 times faster");
                    Assert.Less(runsT7 * 33.0 / 3600.0, 400.0, id + " stage " + stage + " stays a finite chunk of play with the end-game tool");
                }
            }
            foreach (string l in lines) TestLog.Info(l);
        }

        [Test]
        public void SimulatedPlayers_KeepReturningToStarterWorlds()
        {
            // The tier-0 harvest is a large share of everything harvested: the starter forest is farmed for the whole game.
            SimResult r = SimFx.Run("regular", 1UL, 730.0);
            double axe = r.stats["harvest_v:axe"], pick = r.stats["harvest_v:pick"], claw = r.stats["harvest_v:claw"];
            double total = 0.0;
            foreach (string k in r.statOrder) if (k.StartsWith("harvest_v:")) total += r.stats[k];
            double t0 = axe + claw + 0.5 * pick;
            TestLog.Info("regular, 2 years: " + (100.0 * t0 / total).ToString("0") + " % of harvested value comes from the wood, stone and scrap of the starter-style harvests (axe " + axe.ToString("0") + ", pick " + pick.ToString("0") + ", claw " + claw.ToString("0") + ")");
            Assert.Greater(t0 / total, 0.30, "the tier-0 loop keeps feeding the economy");
        }
    }
}
