using System;

namespace Frontier.Economy
{
    public enum FrontierEra { Landfall = 0, Foothold = 1, Settlement = 2, Industry = 3, Interstellar = 4, Ascension = 5 }

    public struct EraFacts
    {
        public int worldsLanded, highestMegaprojectStage;
        public bool researchLabBuilt, jumpRangePurchased, hazardPlatingPurchased, radiationPlatingPurchased, antimatterCrafted;
    }

    public readonly struct EraRequirement
    {
        public readonly string Id;
        public readonly string Label;
        public readonly bool Complete;
        public EraRequirement(string label, bool complete) : this(null, label, complete) { }
        public EraRequirement(string id, string label, bool complete) { Id = id; Label = label; Complete = complete; }
    }

    /// <summary>The same milestone gates for live progression and the economy simulator; never inferred from XP or currency.</summary>
    public static class EraRules
    {
        public static bool Meets(FrontierEra era, EraFacts facts)
        {
            switch (era)
            {
                case FrontierEra.Landfall: return true;
                case FrontierEra.Foothold: return facts.worldsLanded >= 2;
                case FrontierEra.Settlement: return facts.researchLabBuilt && facts.jumpRangePurchased;
                case FrontierEra.Industry: return facts.hazardPlatingPurchased;
                case FrontierEra.Interstellar: return facts.radiationPlatingPurchased && facts.antimatterCrafted;
                case FrontierEra.Ascension: return facts.highestMegaprojectStage >= 3;
                default: throw new ArgumentOutOfRangeException(nameof(era));
            }
        }

        public static FrontierEra Reached(EraFacts facts)
        {
            for (var era = FrontierEra.Ascension; era > FrontierEra.Landfall; era--)
                if (Meets(era, facts)) return era;
            return FrontierEra.Landfall;
        }

        public static EraRequirement[] NextRequirements(FrontierEra era, EraFacts facts)
        {
            switch (era)
            {
                case FrontierEra.Landfall: return new[] { new EraRequirement("second-world", "Land on a second world", facts.worldsLanded >= 2) };
                case FrontierEra.Foothold: return new[]
                {
                    new EraRequirement("research-lab", "Build a Research Lab", facts.researchLabBuilt),
                    new EraRequirement("jump-range", "Purchase Jump Range level 1", facts.jumpRangePurchased)
                };
                case FrontierEra.Settlement: return new[] { new EraRequirement("hazard-plating", "Purchase any hazard plating", facts.hazardPlatingPurchased) };
                case FrontierEra.Industry: return new[]
                {
                    new EraRequirement("radiation-plating", "Purchase Rad-Shield Plating", facts.radiationPlatingPurchased),
                    new EraRequirement("antimatter", "Craft an Antimatter Pod", facts.antimatterCrafted)
                };
                case FrontierEra.Interstellar: return new[] { new EraRequirement("megaproject", "Complete stage 3 of any megaproject", facts.highestMegaprojectStage >= 3) };
                case FrontierEra.Ascension: return Array.Empty<EraRequirement>();
                default: throw new ArgumentOutOfRangeException(nameof(era));
            }
        }
    }
}
