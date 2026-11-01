using System;
using NUnit.Framework;

namespace Frontier.Tests
{
    public class HarvestRedesignTests
    {
        static void Cut(DirectionalCutRun run, bool correct)
        {
            var direction = run.Direction;
            float x = direction == CutDirection.Left ? -1f : direction == CutDirection.Right ? 1f : 0f;
            float y = direction == CutDirection.Up ? -1f : direction == CutDirection.Down ? 1f : 0f;
            float precision;
            Assert.IsTrue(run.Cut(correct ? x : -x, correct ? y : -y, out precision));
        }

        [Test]
        public void Lumber_DirectionAndAlignmentDetermineYield_NotElapsedTiming()
        {
            var clean = new DirectionalCutRun(42);
            var wrong = new DirectionalCutRun(42);
            float precision;
            Assert.IsFalse(clean.Cut(0.02f, 0f, out precision), "A tap must not count as a directional cut.");
            while (!clean.Completed) Cut(clean, true);
            while (!wrong.Completed) Cut(wrong, false);
            Assert.AreEqual(1f, clean.Quality, 0.0001f);
            Assert.AreEqual(8, clean.Perfects);
            Assert.AreEqual(0f, wrong.Quality);
            Assert.AreEqual(16, wrong.Attempts);
        }

        [Test]
        public void Lumber_DiagonalCutsWorkButScoreBelowStraightCuts()
        {
            var run = new DirectionalCutRun(42);
            float x = run.Direction == CutDirection.Left ? -1f : run.Direction == CutDirection.Right ? 1f : 0.5f;
            float y = run.Direction == CutDirection.Up ? -1f : run.Direction == CutDirection.Down ? 1f : 0.5f;
            float precision;
            Assert.IsTrue(run.Cut(x, y, out precision));
            Assert.AreEqual(1, run.Cuts);
            Assert.Greater(run.Quality, 0f);
            Assert.Less(run.Quality, 1f / DirectionalCutRun.CutCount);
        }

        [Test]
        public void Mining_AimingAtConnectedOreBreaksOnlyThatClusterAndNeverCollectsTwice()
        {
            var run = new ConnectedOreRun(42, 3f);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 5, 6 }, run.Connected(0));
            Assert.IsTrue(run.Break(0));
            Assert.AreEqual(4, run.Ore);
            Assert.AreEqual(1, run.Chains);
            Assert.AreEqual(-2, run.Cell(6));
            Assert.AreEqual(2, run.Cell(2), "Other material must not chain across a border.");
            Assert.IsFalse(run.Break(0));
            Assert.AreEqual(1, run.Strikes);
            Assert.Greater(run.Quality, 0f);
        }

        [Test]
        public void Mining_FaultsWasteBudgetWhileUpgradeStrengthReducesRequiredStrikes()
        {
            var weak = new ConnectedOreRun(42, 1f);
            var strong = new ConnectedOreRun(42, 3f);
            weak.Break(0); strong.Break(0);
            Assert.AreEqual(0, weak.Ore);
            Assert.Greater(strong.Ore, weak.Ore);
            var fault = new ConnectedOreRun(42, 3f);
            fault.Break(4);
            Assert.AreEqual(1, fault.Faults);
            Assert.AreEqual(3, fault.Strikes);
            Assert.AreEqual(0f, fault.Quality);
        }

        static void Drive(SteeredDrillRun run, bool rich)
        {
            for (int step = 0; step < 1000 && !run.Completed; step++)
            {
                for (int col = 0; col < SteeredDrillRun.Columns; col++)
                    if (run.Cell(run.Depth, col) == (rich ? 3 : -1)) { run.Aim(col); break; }
                run.Advance(0.1f, 0f, run.Heat < 0.75f, false);
            }
        }

        [Test]
        public void Drill_SteeringForGoldVersusRockMateriallyChangesResult()
        {
            var gold = new SteeredDrillRun(42, 0, 0.5f);
            var rock = new SteeredDrillRun(42, 0, 0.5f);
            Drive(gold, true); Drive(rock, false);
            Assert.IsTrue(gold.Completed);
            Assert.IsTrue(rock.Completed);
            Assert.AreEqual(SteeredDrillRun.Layers, gold.RichLayers);
            Assert.Greater(gold.Quality, rock.Quality + 0.5f);
            Assert.Greater(rock.Collisions, 0);
        }

        [Test]
        public void Drill_BoostBuildsHeatAndOverheatBlocksThrustUntilCooled()
        {
            var run = new SteeredDrillRun(42, 0, 1f);
            run.Advance(0.5f, 0f, true, true);
            Assert.Greater(run.Heat, 0.2f);
            for (int i = 0; i < 60 && !run.Overheated; i++) run.Advance(0.1f, 0f, true, true);
            Assert.IsTrue(run.Overheated);
            int depth = run.Depth;
            float progress = run.Progress;
            run.Advance(0.1f, 0f, true, false);
            Assert.AreEqual(depth, run.Depth);
            Assert.AreEqual(progress, run.Progress);
            run.Advance(3f, 0f, false, false);
            Assert.IsFalse(run.Overheated);
            Assert.LessOrEqual(run.Heat, 0.3f);
        }

        [Test]
        public void Drill_EquipmentChangesCoolingAndResistance_NotMiningBreakDamageOrDescentSpeed()
        {
            var basic = new SteeredDrillRun(42, 0, 0.5f);
            var upgraded = new SteeredDrillRun(42, 5, 0.5f);
            basic.Advance(0.5f, 0f, true, false);
            upgraded.Advance(0.5f, 0f, true, false);
            Assert.AreEqual(basic.Progress, upgraded.Progress, 0.0001f);
            Assert.Less(upgraded.Heat, basic.Heat);
            Assert.Greater(upgraded.Cooling, basic.Cooling);
            float basicBefore = basic.Heat, upgradedBefore = upgraded.Heat;
            basic.Advance(0.05f, 0f, false, false); upgraded.Advance(0.05f, 0f, false, false);
            Assert.Greater(upgradedBefore - upgraded.Heat, basicBefore - basic.Heat);
        }

        [Test]
        public void Drill_ChangingLanesLosesPartialFractureSoLastMomentGoldSwitchCannotCheat()
        {
            var run = new SteeredDrillRun(42, 0, 0f);
            run.Advance(0.3f, 0f, true, false);
            Assert.Greater(run.Progress, 0f);
            run.Aim(0);
            Assert.AreEqual(0f, run.Progress);
            run.CancelInput();
            Assert.IsFalse(run.Thrusting);
        }

        static void Connect(FlowBalanceRun run)
        {
            int[] route = { 6, 10, 12, 9, 0, 3, 3, 10, 9 };
            for (int cell = 0; cell < route.Length; cell++)
                for (int turn = 0; turn < 4 && run.Pipe(cell) != route[cell]; turn++) run.Rotate(cell);
        }

        [Test]
        public void Siphon_SealedRouteAndBalancedValveProduceFlow_UnconnectedOrWrongValveDoesNot()
        {
            var balanced = new FlowBalanceRun(42);
            var wrongValve = new FlowBalanceRun(42);
            var unconnected = new FlowBalanceRun(42);
            Connect(balanced); Connect(wrongValve);
            Assert.IsTrue(balanced.Connected);
            Assert.AreEqual(0, balanced.Leaks);
            while ((unconnected.Pipe(3) & 8) != 0) unconnected.Rotate(3);
            wrongValve.SetValve(1f);
            for (int i = 0; i < 501; i++)
            {
                balanced.SetValve(balanced.Demand);
                balanced.Advance(0.1f); wrongValve.Advance(0.1f); unconnected.Advance(0.1f);
            }
            Assert.Greater(balanced.Quality, 0.9f);
            Assert.Greater(balanced.Delivered, wrongValve.Delivered + 3f);
            Assert.AreEqual(0f, unconnected.Delivered);
            Assert.IsTrue(balanced.Completed);
        }

        [Test]
        public void Excavation_ScanRevealsDistanceAndBrushProtectsFindWhereDigDamagesIt()
        {
            var careful = new BuriedFindRun(42);
            var careless = new BuriedFindRun(42);
            int find = careful.Scan(0);
            Assert.That(find, Is.InRange(0, 2), "The nearest buried top-row footprint starts within two cells.");
            while (careful.Soil(find) > 1) careful.Clear(find, false);
            while (careless.Soil(find) > 1) careless.Clear(find, false);
            careful.Clear(find, false); careless.Clear(find, true);
            Assert.AreEqual(1, careful.Found);
            Assert.AreEqual(1, careless.Damaged);
            Assert.Greater(careful.Quality, careless.Quality);
            Assert.IsTrue(careful.Exposed(find));
            Assert.IsFalse(careful.Clear(find, true), "An exposed find cannot be collected twice.");
            careful.Scan(0); careful.Scan(0);
            Assert.AreEqual(-1, careful.Scan(0), "Scanning is a limited decision, not an infinite hint.");
        }

        [Test]
        public void Excavation_TwoLayerDigIsSafeButMustNotOverdigFinalSoil()
        {
            var run = new BuriedFindRun(42);
            int find = run.Scan(0);
            int soil = run.Soil(find);
            run.Clear(find, true);
            Assert.AreEqual(Math.Max(0, soil - 2), run.Soil(find));
            Assert.AreEqual(0, run.Damaged);
            if (run.Soil(find) == 1) run.Clear(find, false);
            Assert.AreEqual(1, run.Found);
        }

        [Test]
        public void Salvage_CorrectBinsSaveUsefulPartsAndRejectHazards_TimeoutAndWrongBinsLoseYield()
        {
            var correct = new SortingSalvageRun(42);
            var wrong = new SortingSalvageRun(42);
            var ignored = new SortingSalvageRun(42);
            while (!correct.Completed)
            {
                var bin = correct.Current == SalvagePart.Metal ? SalvageBin.Metal
                    : correct.Current == SalvagePart.Circuit ? SalvageBin.Circuit : SalvageBin.Reject;
                Assert.IsTrue(correct.Sort(bin));
                Assert.IsFalse(wrong.Sort(bin == SalvageBin.Metal ? SalvageBin.Circuit : SalvageBin.Metal));
            }
            ignored.Advance(60f);
            Assert.AreEqual(1f, correct.Quality, 0.0001f);
            Assert.AreEqual(18, correct.BestChain);
            Assert.AreEqual(0f, wrong.Quality);
            Assert.AreEqual(0f, ignored.Quality);
            Assert.IsTrue(ignored.Completed);
            Assert.AreEqual(18, ignored.Mistakes);
        }

        [Test]
        public void Redesign_RejectsInvalidInputsAndEveryIdleRunTerminates()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectedOreRun(1, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SteeredDrillRun(1, -1, 0f));
            var lumber = new DirectionalCutRun(1); lumber.Advance(60); Assert.IsTrue(lumber.Completed);
            var mining = new ConnectedOreRun(1, 1); mining.Advance(60); Assert.IsTrue(mining.Completed);
            var drill = new SteeredDrillRun(1, 0, 0); drill.Advance(70, 0, false, false); Assert.IsTrue(drill.Completed);
            var siphon = new FlowBalanceRun(1); siphon.Advance(60); Assert.IsTrue(siphon.Completed);
            var excavation = new BuriedFindRun(1); excavation.Advance(70); Assert.IsTrue(excavation.Completed);
            Assert.Throws<ArgumentOutOfRangeException>(() => excavation.Clear(-1, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SortingSalvageRun(1).Advance(float.PositiveInfinity));
        }
    }
}
