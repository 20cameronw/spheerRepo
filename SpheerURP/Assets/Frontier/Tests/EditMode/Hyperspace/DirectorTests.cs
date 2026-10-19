using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Hyperspace.Tests
{
    [TestFixture]
    public class DirectorTests
    {
        static readonly EncounterLibrary Lib = TestEnv.Lib;

        [Test]
        public void LibraryHasThirtyPlusWellFormedTemplates()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            var ids = new HashSet<string>();
            int playable = 0;
            for (int i = 0; i < Lib.Count; i++)
            {
                EncounterTemplate tp = Lib[i];
                Assert.IsTrue(ids.Add(tp.id), "duplicate id " + tp.id);
                Assert.AreEqual(i, tp.index, tp.id);
                Assert.IsTrue(tp.minTier >= 0 && tp.maxTier <= 12 && tp.minTier <= tp.maxTier, tp.id + " tier window");
                Assert.IsTrue(tp.cost > 0f && tp.durationZ >= 0f && tp.entries.Length > 0, tp.id + " cost/entries");
                Assert.IsTrue(tp.declaredSpeed > 0f && tp.declaredSpeed <= 1.0001f, tp.id + " declared speed " + tp.declaredSpeed);
                Assert.IsFalse(string.IsNullOrEmpty(tp.notes), tp.id + " needs a designer note");
                if (tp.role != EncounterRole.Boss) playable++;
                foreach (SpawnEntry e in tp.entries)
                {
                    if (e.type == EntityType.LaserGate || e.type == EntityType.Guardian) continue;
                    double ex = e.path == PathType.Curve ? e.p2 : e.x, ey = e.path == PathType.Curve ? e.p3 : e.y;
                    Assert.IsTrue(Math.Abs(ex) <= t.areaHalfWidth + 0.8 && Math.Abs(ey) <= t.areaHalfHeight + 0.8, tp.id + " entry ends outside the play area: " + ex + "," + ey);
                    Assert.IsTrue(e.zOffset >= -0.001f && e.zOffset <= tp.durationZ + 0.001f, tp.id + " z offset " + e.zOffset + " outside durationZ " + tp.durationZ);
                }
            }
            Assert.GreaterOrEqual(playable, 30, "the library needs at least 30 hand-designed templates");
            Assert.GreaterOrEqual(Lib.Count, 30);
            string[] mustHave = { "drone_line", "drone_v", "drone_wedge", "gate_slit_wide", "drone_helix", "pincer", "turret_crossfire", "asteroid_field", "gate_sequence", "seeker_swarm", "elite_escort", "boss_guardian" };
            foreach (string id in mustHave) Assert.IsNotNull(Lib.ById(id), "missing template " + id);
            for (int tier = 0; tier <= 12; tier++) Assert.GreaterOrEqual(Lib.CountAt(tier), 5, "too few templates at tier " + tier);
        }

        [Test]
        public void SpawnedBudgetNeverExceedsTheRouteBudget()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            float[] dists = { 1f, 5f, 50f, 200f, 800f, 3000f };
            int routes = 0;
            for (int tier = 0; tier <= 12; tier++)
            {
                foreach (float d in dists)
                {
                    for (ulong seed = 1; seed <= 4; seed++)
                    {
                        foreach (RunMode mode in new[] { RunMode.Normal, RunMode.Assist, RunMode.Challenge })
                        {
                            ShipStats ship = ShipStats.AtProgress(tier / 12f);
                            RouteSpec route = RouteSpec.Create(TestEnv.Route(seed * 7919UL + (ulong)tier, d, tier, t.ShipPower(ship), mode), ship, t);
                            Schedule s = SpawnDirector.Build(route, Lib, t);
                            Assert.LessOrEqual(s.spent, route.budgetTotal + 1e-3f, "total spend, tier " + tier + " " + d + " ly");
                            double cumulative = 0.0;
                            foreach (PlannedEncounter pe in s.encounters)
                            {
                                if (pe.kind == PacingKind.Boss) continue;
                                cumulative += pe.cost;
                                float endTime = (float)(pe.endStep / 60.0);
                                Assert.LessOrEqual(cumulative, route.BudgetBy(endTime) + 1e-3, "cumulative spend at " + endTime + " s, tier " + tier);
                            }
                            routes++;
                        }
                    }
                }
            }
            Assert.Greater(routes, 800);
        }

        [Test]
        public void NoThreeIdenticalTemplatesOrFamiliesInARow()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            var used = new HashSet<int>();
            for (int tier = 0; tier <= 12; tier += 2)
            {
                for (ulong seed = 1; seed <= 25; seed++)
                {
                    ShipStats ship = ShipStats.AtProgress(tier / 12f);
                    RouteSpec route = RouteSpec.Create(TestEnv.Route(seed * 31UL + (ulong)tier, 400f, tier, t.ShipPower(ship)), ship, t);
                    Schedule s = SpawnDirector.Build(route, Lib, t);
                    PlannedEncounter[] e = s.encounters;
                    for (int i = 0; i < e.Length; i++)
                    {
                        used.Add(e[i].template);
                        if (i < 2) continue;
                        Assert.IsFalse(e[i].template == e[i - 1].template && e[i].template == e[i - 2].template, "three identical templates (" + Lib[e[i].template].id + ") tier " + tier + " seed " + seed);
                        EncounterFamily f = Lib[e[i].template].family;
                        bool boss = e[i].kind == PacingKind.Boss;
                        Assert.IsFalse(!boss && f == Lib[e[i - 1].template].family && f == Lib[e[i - 2].template].family, "three " + f + " templates in a row, tier " + tier + " seed " + seed);
                    }
                }
            }
            Assert.GreaterOrEqual(used.Count, 40, "the director should use a wide variety of templates across routes (used " + used.Count + ")");
        }

        [Test]
        public void PacingFollowsCalmBuildPeakRelaxWaves()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = ShipStats.AtProgress(0.5f);
            RouteSpec route = RouteSpec.Create(TestEnv.Route(5UL, 800f, 6, t.ShipPower(ship)), ship, t);
            PacingSegment[] seg = route.segments;
            Assert.AreEqual(PacingKind.Intro, seg[0].kind);
            Assert.AreEqual(PacingKind.Outro, seg[seg.Length - 1].kind);
            Assert.AreEqual(PacingKind.Boss, seg[seg.Length - 2].kind);
            PacingKind[] order = { PacingKind.Calm, PacingKind.Build, PacingKind.Peak, PacingKind.Relax };
            int waves = (seg.Length - 3) / 4;
            Assert.GreaterOrEqual(waves, 2, "a long route has several waves");
            for (int i = 1; i < seg.Length; i++) Assert.AreEqual(seg[i - 1].end, seg[i].start, 1e-3f, "segments must be contiguous");
            for (int w = 0; w < waves; w++)
            {
                for (int k = 0; k < 4; k++) Assert.AreEqual(order[k], seg[1 + w * 4 + k].kind);
                PacingSegment calm = seg[1 + w * 4], build = seg[2 + w * 4], peak = seg[3 + w * 4], relax = seg[4 + w * 4];
                Assert.Less(calm.rate, build.rate, "calm -> build");
                Assert.Less(build.rate, peak.rate, "build -> peak");
                Assert.Greater(peak.rate, relax.rate, "peak -> relax");
                Assert.Less(relax.rate, build.rate, "relax is calmer than build");
            }
            double[] cost = new double[7];
            double[] secs = new double[7];
            for (ulong sd = 1; sd <= 60; sd++)
            {
                RouteSpec r = RouteSpec.Create(TestEnv.Route(sd, 800f, 6, t.ShipPower(ship)), ship, t);
                Schedule s = SpawnDirector.Build(r, Lib, t);
                foreach (PacingSegment g in r.segments) secs[(int)g.kind] += g.end - g.start;
                foreach (PlannedEncounter pe in s.encounters) cost[(int)pe.kind] += pe.cost;
                foreach (PlannedEncounter pe in s.encounters) if (pe.kind != PacingKind.Boss) Assert.GreaterOrEqual(pe.anchorStep, (int)(t.introSeconds * 60) - 1, "nothing arrives during the intro");
            }
            double calmD = cost[(int)PacingKind.Calm] / secs[(int)PacingKind.Calm], buildD = cost[(int)PacingKind.Build] / secs[(int)PacingKind.Build];
            double peakD = cost[(int)PacingKind.Peak] / secs[(int)PacingKind.Peak], relaxD = cost[(int)PacingKind.Relax] / secs[(int)PacingKind.Relax];
            Assert.Less(calmD, buildD, "threat density calm " + calmD + " build " + buildD);
            Assert.Less(buildD, peakD, "threat density build " + buildD + " peak " + peakD);
            Assert.Less(relaxD, buildD, "threat density relax " + relaxD + " build " + buildD);
        }

        [Test]
        public void GuardianAppearsOnlyOnLongOrDeepRoutesAndNearTheEnd()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            foreach (int tier in new[] { 0, 3, 5, 6, 9, 12 })
            {
                foreach (float d in new[] { 5f, 100f, 249f, 250f, 800f, 3000f })
                {
                    ShipStats ship = ShipStats.AtProgress(tier / 12f);
                    RouteSpec route = RouteSpec.Create(TestEnv.Route(3UL, d, tier, t.ShipPower(ship)), ship, t);
                    bool expected = d >= t.bossMinDistanceLy || tier >= t.bossMinTier;
                    Assert.AreEqual(expected, route.hasBoss, "tier " + tier + " " + d + " ly");
                    Schedule s = SpawnDirector.Build(route, Lib, t);
                    int bosses = 0;
                    foreach (SpawnRecord r in s.records) if (r.type == EntityType.Guardian) bosses++;
                    Assert.AreEqual(expected ? 1 : 0, bosses);
                    if (!expected) continue;
                    Assert.GreaterOrEqual(route.bossStart, route.waveEnd, "the boss slot comes after the last ordinary wave");
                    Assert.LessOrEqual(route.duration - route.bossStart, t.bossArriveSeconds + t.bossMaxSeconds + t.outroSeconds + 0.1f, "nothing follows the boss slot but the outro");
                    foreach (SpawnRecord r in s.records) if (r.type != EntityType.Guardian) Assert.Less(r.step, s.bossSpawnStep, "the Guardian is the last thing to spawn");
                    foreach (PlannedEncounter pe in s.encounters)
                        if (pe.kind != PacingKind.Boss) Assert.Less(pe.endStep, (int)(route.bossStart * 60) + 5, "all ordinary encounters finish before the boss");
                }
            }
        }

        [Test]
        public void RouteDurationGrowsFromTwentySecondsToNinety()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            float previous = 0f;
            foreach (float d in new[] { 1f, 5f, 15f, 50f, 200f, 800f, 3000f })
            {
                RouteSpec r = RouteSpec.Create(TestEnv.Route(1UL, d, 0, 1f), ShipStats.Starter(), t);
                Assert.GreaterOrEqual(r.duration, previous - 1e-3f, "duration must not shrink with distance");
                previous = r.duration;
                if (d <= 5f) Assert.IsTrue(r.duration >= 20f && r.duration <= 35f, "short hop " + d + " ly -> " + r.duration + " s");
                if (d >= 3000f) Assert.IsTrue(r.duration >= 60f && r.duration <= 92.5f, "longest route -> " + r.duration + " s");
                Assert.AreEqual((int)Math.Round(r.duration * 60.0), r.totalSteps);
            }
            RouteSpec deep = RouteSpec.Create(TestEnv.Route(1UL, 5f, 12, 1f), ShipStats.Starter(), t);
            Assert.LessOrEqual(deep.duration, t.durationMax + t.outroSeconds + 40f, "even a deep short hop stays bounded");
        }

        [Test]
        public void PlannedDurationControlsRunLength_AndBossKeepsItsRequiredSlot()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = ShipStats.Starter();
            RouteSpec ordinary = RouteSpec.Create(new RunParams(1UL, 5f, 0.6f, 0, 1f, RunMode.Normal, 34f), ship, t);
            Assert.AreEqual(34f, ordinary.duration, 0.01f);

            RouteSpec boss = RouteSpec.Create(new RunParams(1UL, 250f, 3f, 0, 1f, RunMode.Normal, 70f), ship, t);
            Assert.IsTrue(boss.hasBoss);
            Assert.AreEqual(70f, boss.duration, 0.01f);
            Assert.AreEqual(boss.waveEnd + t.bossIntroSeconds, boss.bossStart, 1e-4f);

            RouteSpec shortBoss = RouteSpec.Create(new RunParams(1UL, 250f, 3f, 0, 1f, RunMode.Normal, 30f), ship, t);
            float minimumBossRun = 16f + t.bossIntroSeconds + t.bossArriveSeconds + t.bossMaxSeconds + t.outroSeconds;
            Assert.AreEqual(minimumBossRun, shortBoss.duration, 0.01f, "short requests retain enough time for the minimum wave phase and full boss slot");
        }

        [Test]
        public void DifficultyIsMonotonicInDistanceDangerTierAndPower()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats s = ShipStats.Starter();
            RouteSpec Make(float ly, float danger, int tier, float power, RunMode mode = RunMode.Normal) { return RouteSpec.Create(new RunParams(9UL, ly, danger, tier, power, mode), s, t); }
            float prev = -1f, prevBudget = -1f;
            foreach (float d in new[] { 0f, 1f, 5f, 20f, 50f, 200f, 800f, 3000f, 9000f }) { RouteSpec r = Make(d, 3f, 3, 1f); Assert.GreaterOrEqual(r.difficulty, prev, "distance"); Assert.GreaterOrEqual(r.budgetTotal, prevBudget - 1e-3f, "distance budget"); prev = r.difficulty; prevBudget = r.budgetTotal; }
            prev = -1f;
            foreach (float danger in new[] { 0f, 1f, 3f, 7f, 12f, 20f }) { RouteSpec r = Make(50f, danger, 3, 1f); Assert.GreaterOrEqual(r.difficulty, prev, "danger"); prev = r.difficulty; }
            prev = -1f;
            prevBudget = -1f;
            for (int tier = 0; tier <= 12; tier++) { RouteSpec r = Make(50f, 3f, tier, 1f); Assert.Greater(r.difficulty, prev, "tier " + tier); Assert.GreaterOrEqual(r.enemyHpScale, 1f); prev = r.difficulty; }
            prev = -1f;
            foreach (float p in new[] { 1f, 1.5f, 2f, 4f, 8f, 12f }) { RouteSpec r = Make(50f, 3f, 3, p); Assert.GreaterOrEqual(r.difficulty, prev, "power"); prev = r.difficulty; }
            Assert.Less(Make(50f, 3f, 3, 1f, RunMode.Assist).difficulty, Make(50f, 3f, 3, 1f).difficulty);
            Assert.Less(Make(50f, 3f, 3, 1f).difficulty, Make(50f, 3f, 3, 1f, RunMode.Challenge).difficulty);
        }

        [Test]
        public void TelegraphsRespectTheHumanReactionBudget()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            for (int tier = 0; tier <= 12; tier++)
            {
                Assert.GreaterOrEqual(t.TurretTelegraphFor(tier), t.reactionBudget + 0.25f, "turret telegraph at tier " + tier + " must leave time to react and move");
                Assert.GreaterOrEqual(t.SeekerCommitFor(tier), t.reactionBudget + 0.25f, "seeker commit time at tier " + tier);
                Assert.GreaterOrEqual(t.GapScaleFor(tier), 0.5f);
            }
            Assert.GreaterOrEqual(t.gateTelegraphSeconds, 1.0f, "gates are visible long before they arrive");
            Assert.GreaterOrEqual(t.entryLead, t.reactionBudget + 0.5f, "the director leaves time to react between templates");
        }
    }
}
