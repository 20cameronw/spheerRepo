using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Frontier.Hyperspace.Tests
{
    /// <summary>
    /// The perfect solver (FairnessSolver) proves that every encounter template has a collision-free path for a player limited to the template's declared fraction of the fair ship speed
    /// (fairnessSpeedFraction x the starter ship's top speed), with the human reaction budget frozen after every aimed lock, starting anywhere in the play area entryLead seconds ahead.
    /// </summary>
    [TestFixture]
    public class FairnessTests
    {
        const float SearchFloor = 0.3f;

        public static float RouteSpeed(HyperspaceTuning t, int tier, float fraction)
        {
            return (float)((t.speedBase + (double)t.speedPerTier * tier + t.speedPerDistanceTerm) * fraction);
        }

        [Test]
        public void EveryTemplateIsSolvableInEveryAllowedTierAtItsDeclaredSpeed()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            EncounterLibrary lib = TestEnv.Lib;
            var failures = new ConcurrentBag<string>();
            int[] checks = new int[1];
            var todo = new List<EncounterTemplate>();
            for (int i = 0; i < lib.Count; i++) if (lib[i].role != EncounterRole.Boss) todo.Add(lib[i]);
            Parallel.ForEach(todo, tp =>
            {
                var solver = new FairnessSolver(t);
                float[] fractions = { t.speedStartFraction, 1f };
                for (int tier = tp.minTier; tier <= tp.maxTier; tier++)
                {
                    foreach (float f in fractions)
                    {
                        for (int m = 0; m < 2; m++)
                        {
                            if (m == 1 && !tp.mirrorable) continue;
                            SolveResult r = solver.SolveTemplate(tp, tier, m == 1, RouteSpeed(t, tier, f), tp.declaredSpeed);
                            System.Threading.Interlocked.Increment(ref checks[0]);
                            if (!r.solvable) failures.Add(tp.id + " tier " + tier + " speed x" + f + (m == 1 ? " mirrored" : "") + " fails at t=" + r.failTime.ToString("0.00") + "s (" + r.failSources + ")" + (r.unsupported ? " [overlapping lock windows]" : ""));
                        }
                    }
                }
            });
            Console.WriteLine("    solver: " + todo.Count + " templates, " + checks[0] + " tier/speed/mirror instances proven collision-free");
            Assert.AreEqual(0, failures.Count, "unsolvable templates:\n  " + string.Join("\n  ", failures));
            Assert.Greater(checks[0], 800);
        }

        [Test]
        public void DeclaredSpeedCoversTheMeasuredRequirement()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            EncounterLibrary lib = TestEnv.Lib;
            var bad = new ConcurrentBag<string>();
            var todo = new List<EncounterTemplate>();
            for (int i = 0; i < lib.Count; i++) if (lib[i].role != EncounterRole.Boss) todo.Add(lib[i]);
            Parallel.ForEach(todo, tp =>
            {
                var solver = new FairnessSolver(t);
                float worst = 0f;
                for (int tier = tp.minTier; tier <= tp.maxTier; tier += (tier < tp.maxTier && tp.maxTier - tier > 3) ? 2 : 1)
                {
                    SolveResult r = solver.MeasureTemplate(tp, tier, false, RouteSpeed(t, tier, 1f));
                    if (!r.solvable) bad.Add(tp.id + " tier " + tier + " is not solvable even at full speed");
                    else if (r.minSpeedFraction > worst) worst = r.minSpeedFraction;
                }
                if (worst > tp.declaredSpeed + 1e-3f && worst > SearchFloor + 1e-3f) bad.Add(tp.id + " declares " + tp.declaredSpeed.ToString("0.00") + " but needs " + worst.ToString("0.00") + " of the fair speed");
            });
            Assert.AreEqual(0, bad.Count, string.Join("\n  ", bad));
        }

        [Test]
        public void EveryGuardianPhaseIsSolvable()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            var solver = new FairnessSolver(t);
            foreach (int tier in new[] { 5, 8, 12 })
            {
                for (int phase = 1; phase <= 3; phase++)
                {
                    SolveResult r = solver.SolveBossPhase(phase, tier, RouteSpeed(t, tier, 1f), 1f);
                    Assert.IsTrue(r.solvable, "boss phase " + phase + " at tier " + tier + " fails at t=" + r.failTime + " (" + r.failSources + ")");
                }
            }
        }

        static EncounterTemplate Build(Action<TemplateBuilder> fill)
        {
            var b = new TemplateBuilder("probe", 0, 12, 5f, EncounterRole.Peak, EncounterFamily.Gates, 1f);
            fill(b);
            return b.Done();
        }

        [Test]
        public void SolverRejectsTemplatesThatCannotBeFlown()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            var solver = new FairnessSolver(t);
            float speed = RouteSpeed(t, 3, 1f);

            EncounterTemplate narrow = Build(b => b.Gate(GateKind.Slit, 0f, 0f, 0f, 0.2f, 0f, 0f, 0f, 0f, 0f, false));
            Assert.IsFalse(solver.SolveTemplate(narrow, 3, false, speed, 1f).solvable, "a slit narrower than the ship is a wall");

            EncounterTemplate zigzag = Build(b => b.Gate(GateKind.Slit, -2.1f, 0f, 0f, 0.8f, 0f, 0f, 0f, 0f, 0f, false).Gate(GateKind.Slit, 2.1f, 0f, 3f, 0.8f, 0f, 0f, 0f, 0f, 0f, false));
            Assert.IsFalse(solver.SolveTemplate(zigzag, 3, false, speed, 1f).solvable, "two slits at opposite edges 0.1 s apart need an impossible lateral speed");

            EncounterTemplate fine = Build(b => b.Gate(GateKind.Slit, -1.0f, 0f, 0f, 1.3f, 0f, 0f, 0f, 0f, 0f, false).Gate(GateKind.Slit, 1.0f, 0f, 40f, 1.3f, 0f, 0f, 0f, 0f, 0f, false));
            Assert.IsTrue(solver.SolveTemplate(fine, 3, false, speed, 1f).solvable, "wide, well spaced slits are flyable");
        }

        [Test]
        public void SolverReportsTheSpeedRequirementOfADemandingTemplate()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            var solver = new FairnessSolver(t);
            EncounterTemplate sweep = Build(b => b.Gate(GateKind.Slit, -1.6f, 0f, 0f, 0.95f, 0f, 0f, 0f, 0f, 0f, false).Gate(GateKind.Slit, 1.6f, 0f, 18f, 0.95f, 0f, 0f, 0f, 0f, 0f, false));
            SolveResult m = solver.MeasureTemplate(sweep, 0, false, RouteSpeed(t, 0, 1f));
            Assert.IsTrue(m.solvable, "failed at t=" + m.failTime + " (" + m.failSources + ")");
            Assert.Greater(m.minSpeedFraction, SearchFloor + 0.05f, "a 3.2 u sweep in about 0.6 s should demand more than the minimum speed (" + m.minSpeedFraction + ")");
            Assert.LessOrEqual(m.minSpeedFraction, 1f);
        }
    }
}
