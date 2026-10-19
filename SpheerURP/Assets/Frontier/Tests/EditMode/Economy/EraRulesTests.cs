using System;
using NUnit.Framework;

namespace Frontier.Economy.Tests
{
    public class EraRulesTests
    {
        [Test]
        public void EachMilestone_AdvancesTheMatchingEra()
        {
            var facts = new EraFacts { worldsLanded = 1 };
            Assert.AreEqual(FrontierEra.Landfall, EraRules.Reached(facts));
            facts.worldsLanded = 2;
            Assert.AreEqual(FrontierEra.Foothold, EraRules.Reached(facts));
            facts.researchLabBuilt = facts.jumpRangePurchased = true;
            Assert.AreEqual(FrontierEra.Settlement, EraRules.Reached(facts));
            facts.hazardPlatingPurchased = true;
            Assert.AreEqual(FrontierEra.Industry, EraRules.Reached(facts));
            facts.radiationPlatingPurchased = facts.antimatterCrafted = true;
            Assert.AreEqual(FrontierEra.Interstellar, EraRules.Reached(facts));
            facts.highestMegaprojectStage = 2;
            Assert.AreEqual(FrontierEra.Interstellar, EraRules.Reached(facts));
            facts.highestMegaprojectStage = 3;
            Assert.AreEqual(FrontierEra.Ascension, EraRules.Reached(facts));
        }

        [Test]
        public void CompoundGates_RequireBothFacts_AndReportConcreteRequirements()
        {
            var facts = new EraFacts { researchLabBuilt = true, radiationPlatingPurchased = true };
            Assert.IsFalse(EraRules.Meets(FrontierEra.Settlement, facts));
            Assert.IsFalse(EraRules.Meets(FrontierEra.Interstellar, facts));
            var settlement = EraRules.NextRequirements(FrontierEra.Foothold, facts);
            Assert.AreEqual(2, settlement.Length);
            Assert.IsTrue(settlement[0].Complete);
            Assert.IsFalse(settlement[1].Complete);
            facts.researchLabBuilt = false;
            facts.jumpRangePurchased = true;
            facts.radiationPlatingPurchased = false;
            facts.antimatterCrafted = true;
            Assert.IsFalse(EraRules.Meets(FrontierEra.Settlement, facts));
            Assert.IsFalse(EraRules.Meets(FrontierEra.Interstellar, facts));
        }

        [Test]
        public void MilestoneIdentifiers_StayAlignedWithSimulatorEras()
        {
            Assert.AreEqual(6, SimBands.EraNames.Length);
            for (var era = FrontierEra.Landfall; era <= FrontierEra.Ascension; era++)
            {
                Assert.AreEqual(era.ToString(), SimBands.EraNames[(int)era]);
                Assert.AreEqual(era == FrontierEra.Landfall ? "" : "era_" + era.ToString().ToLowerInvariant(),
                    SimBands.EraMilestones[(int)era]);
                Assert.AreEqual(era == FrontierEra.Ascension ? 0 : era == FrontierEra.Foothold || era == FrontierEra.Industry ? 2 : 1,
                    EraRules.NextRequirements(era, default).Length);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => EraRules.Meets((FrontierEra)6, default));
            Assert.Throws<ArgumentOutOfRangeException>(() => EraRules.NextRequirements((FrontierEra)(-1), default));
        }
    }
}
