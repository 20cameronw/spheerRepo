using System;
using NUnit.Framework;

namespace Frontier.Tests
{
    [TestFixture]
    public class TravelModelTests
    {
        static TravelNode At(string system, double x, double y, double orbit = 1.0, double phase = 0.0)
        {
            return new TravelNode(system, x, y, orbit, phase);
        }

        readonly TravelModel model = new TravelModel();

        [Test]
        public void SameSystemHop_IsAlwaysCheaperThanAnyInterSystemHop()
        {
            var rng = new Random(7);
            for (int i = 0; i < 2000; i++)
            {
                var a = At("s:0:0:1000", 0, 0, rng.NextDouble() * 80.0, rng.NextDouble() * 6.3);
                var b = At("s:0:0:1000", 0, 0, rng.NextDouble() * 80.0, rng.NextDouble() * 6.3);
                var far = At("s:1:0:3", 8.0 + rng.NextDouble() * 20000.0, rng.NextDouble() * 100.0);
                int same = model.FuelCost(a, b, 1f);
                int inter = model.FuelCost(a, far, 1f);
                Assert.Less(same, inter, "same " + same + " vs inter " + inter);
                Assert.LessOrEqual(same, 8, "a same-system hop is small and bounded");
            }
        }

        [Test]
        public void InterSystemCost_GrowsWithDistance_NeverDecreases()
        {
            var home = At("s:0:0:1000", 0, 0);
            int previous = 0;
            for (double d = 8.0; d <= 30000.0; d *= 1.01)
            {
                int cost = model.FuelCost(home, At("s:x", d, 0), 1f);
                Assert.GreaterOrEqual(cost, previous, "cost fell at " + d + " ly");
                previous = cost;
            }
            Assert.Greater(previous, model.FuelCost(home, At("s:x", 100, 0), 1f));
        }

        [Test]
        public void SameSystemCost_GrowsWithOrbitDistance_NeverDecreases()
        {
            var a = At("s:0:0:1000", 0, 0, 1.0, 0.0);
            int previous = 0;
            for (double r = 1.0; r <= 60.0; r += 0.05)
            {
                int cost = model.FuelCost(a, At("s:0:0:1000", 0, 0, r, 0.0), 1f);
                Assert.GreaterOrEqual(cost, previous);
                previous = cost;
            }
            Assert.AreEqual(8, previous, "the cap");
        }

        [Test]
        public void Cost_IsAtLeastOne_EvenForTinyHopsAndTheBestEngine()
        {
            var a = At("s:0:0:1000", 0, 0, 0.4, 1.0);
            Assert.AreEqual(1, new TravelModel(0.0, 1.0, 8.0, 11.0, 0.0, 1).FuelCost(a, a, 0.25f));
            Assert.GreaterOrEqual(model.FuelCost(a, a, 0.25f), 1);
            Assert.GreaterOrEqual(model.FuelCost(a, At("s:2", 9, 0), 0.25f), 1);
            Assert.GreaterOrEqual(model.FuelCost(a, At("s:2", 9, 0), 0f), 1, "even with a zero multiplier");
            Assert.GreaterOrEqual(new TravelModel(0.0, 0.0, 0.0, 0.0, 0.0, 0).FuelCost(a, a, 1f), 1, "a minimum below 1 is raised to 1");
        }

        [Test]
        public void Efficiency_ScalesTheWholeCost()
        {
            var home = At("s:0:0:1000", 0, 0);
            var far = At("s:1:0:3", 1000, 0);
            int full = model.FuelCost(home, far, 1f);
            int half = model.FuelCost(home, far, 0.5f);
            int best = model.FuelCost(home, far, 0.25f);
            Assert.Greater(full, half);
            Assert.Greater(half, best);
            Assert.AreEqual(14, half, "(11 + 0.017 x 1000) x 0.5");
            Assert.AreEqual(28, full, "11 + 0.017 x 1000");
        }

        [Test]
        public void SylvaToGlacia_StaysFiveFuel_LikeTheOldMapFormula()
        {
            // Sylva and Glacia on their orbits (AU = 0.4 + (oldMapRadius - 0.055) * 60; the phase is the old map angle).
            var sylva = At("s:0:0:1000", 0, 0, 0.4, 88.2 * Math.PI / 180.0);
            var glacia = At("s:0:0:1000", 0, 0, 0.88, 328.2 * Math.PI / 180.0);
            Assert.AreEqual(1.134, TravelModel.OrbitDistanceAu(sylva, glacia), 0.01);
            Assert.AreEqual(5, model.FuelCost(sylva, glacia, 1f));
            Assert.AreEqual(5, model.FuelCost(glacia, sylva, 1f), "symmetric");
        }

        [Test]
        public void Distance_UsesOrbitsInsideASystemAndLightYearsBetweenSystems()
        {
            var a = At("s:0:0:1000", 0, 0, 3.0, 0.0);
            var b = At("s:0:0:1000", 0, 0, 4.0, Math.PI / 2.0);
            Assert.AreEqual(5.0, model.Distance(a, b), 1e-9, "3-4-5 triangle");
            Assert.IsTrue(TravelModel.IsSameSystem(a, b));
            var c = At("s:3:0:1001", 3.0, 4.0);
            Assert.AreEqual(5.0, model.Distance(At("s:0:0:1000", 0, 0), c), 1e-9);
            Assert.IsFalse(TravelModel.IsSameSystem(a, c));
        }

        [Test]
        public void InterSystemHop_ChargesTheBaseEvenForNeighbours()
        {
            var a = At("s:0:0:1000", 0, 0);
            var b = At("s:0:0:7", 8.0, 0.0);
            Assert.GreaterOrEqual(model.FuelCost(a, b, 1f), 11);
        }

        [Test]
        public void InvalidParameters_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => new TravelModel(5.0, 1.0, 4.0, 11.0, 0.017, 1), "max below base");
            Assert.Throws<ArgumentException>(() => new TravelModel(3.0, 1.0, 8.0, 7.0, 0.017, 1), "inter-system base below the same-system cap");
        }

        [Test]
        public void OrbitDistance_IsSymmetricAndZeroForTheSamePlace()
        {
            var a = At("s:0:0:1000", 0, 0, 2.0, 1.0);
            var b = At("s:0:0:1000", 0, 0, 5.0, 4.0);
            Assert.AreEqual(TravelModel.OrbitDistanceAu(a, b), TravelModel.OrbitDistanceAu(b, a), 1e-12);
            Assert.AreEqual(0.0, TravelModel.OrbitDistanceAu(a, a), 1e-9);
        }
    }

    [TestFixture]
    public class HarvestMappingTests
    {
        [Test]
        public void TheFirstThreeMethods_MapOneToOneAndBack()
        {
            Assert.AreEqual(MinigameKind.Chop, HarvestMapping.ToMinigame(Frontier.Universe.HarvestMethod.Chop));
            Assert.AreEqual(MinigameKind.Mine, HarvestMapping.ToMinigame(Frontier.Universe.HarvestMethod.Mine));
            Assert.AreEqual(MinigameKind.Drill, HarvestMapping.ToMinigame(Frontier.Universe.HarvestMethod.Drill));
            foreach (MinigameKind kind in Enum.GetValues(typeof(MinigameKind)))
                Assert.AreEqual(kind, HarvestMapping.ToMinigame(HarvestMapping.FromMinigame(kind)), "round trip of " + kind);
            Assert.AreEqual((int)MinigameKind.Chop, (int)Frontier.Universe.HarvestMethod.Chop);
            Assert.AreEqual((int)MinigameKind.Mine, (int)Frontier.Universe.HarvestMethod.Mine);
            Assert.AreEqual((int)MinigameKind.Drill, (int)Frontier.Universe.HarvestMethod.Drill);
        }

        [Test]
        public void TheNewMethods_BorrowTheClosestExistingMiniGame_ThroughOneFunction()
        {
            Assert.AreEqual(MinigameKind.Drill, HarvestMapping.ToMinigame(Frontier.Universe.HarvestMethod.Siphon));
            Assert.AreEqual(MinigameKind.Mine, HarvestMapping.ToMinigame(Frontier.Universe.HarvestMethod.Excavate));
            Assert.AreEqual(MinigameKind.Mine, HarvestMapping.ToMinigame(Frontier.Universe.HarvestMethod.Salvage));
            Assert.AreEqual(MinigameKind.Chop, HarvestMapping.ToMinigame(Frontier.Universe.HarvestMethod.Harvest));
            foreach (Frontier.Universe.HarvestMethod m in Enum.GetValues(typeof(Frontier.Universe.HarvestMethod)))
                Assert.IsTrue(Enum.IsDefined(typeof(MinigameKind), HarvestMapping.ToMinigame(m)), m + " must map to a real mini-game");
        }
    }
}
