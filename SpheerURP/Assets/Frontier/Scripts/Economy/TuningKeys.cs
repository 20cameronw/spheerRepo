using System;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>Registers every tuning key of the economy in one place (the order here is the order of the exported list).</summary>
    public static class TuningKeys
    {
        public static void RegisterAll(Tuning t)
        {
            ValueModel.RegisterKeys(t);
            JobTimes.RegisterKeys(t);
            CatalogBuilder.RegisterKeys(t);
            TravelModel.RegisterKeys(t);
            WaveEconomy.RegisterKeys(t);
            ScoreFormula.RegisterKeys(t);
            ObstacleRules.RegisterKeys(t);
            ShardRulesDef.RegisterKeys(t);
            SimEngine.RegisterKeys(t);
        }
    }
}
