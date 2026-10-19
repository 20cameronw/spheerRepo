using System;
using System.Globalization;
using Frontier.Economy;

namespace Frontier
{
    public partial class GameState
    {
        const string EraReachedKey = "frontier_era_reached";
        FrontierEra eraReached;

        public FrontierEra Era { get { return (FrontierEra)Math.Max((int)eraReached, (int)EraRules.Reached(EraProgress)); } }

        public EraFacts EraProgress
        {
            get
            {
                var facts = new EraFacts
                {
                    researchLabBuilt = IsAtlasDiscovered(AtlasStructureType, StructureKind.ResearchLab.ToString()),
                    jumpRangePurchased = progression.UpgradeLevel("jump") >= 1,
                    radiationPlatingPurchased = progression.UpgradeLevel("hull_rad") >= 1
                };
                foreach (var delta in world.All)
                    if (delta != null && delta.visits > 0 && universe.Get(delta.planetId) != null) facts.worldsLanded++;
                if (db.upgrades != null)
                    foreach (var upgrade in db.upgrades)
                        if (upgrade != null && upgrade.effect == UpgradeEffect.HullPlating && progression.UpgradeLevel(upgrade.id) > 0)
                            facts.hazardPlatingPurchased = true;
                if (db.recipes != null)
                    foreach (var recipe in db.recipes)
                    {
                        if (recipe == null || !IsAtlasDiscovered(AtlasRecipeType, recipe.id) || recipe.outputs == null) continue;
                        foreach (var output in recipe.outputs)
                            if (output.resource != null && output.resource.id == AntimatterId && output.amount > 0)
                                facts.antimatterCrafted = true;
                    }
                foreach (var structure in structures)
                {
                    if (structure == null || structure.def == null || !IsOperational(structure)) continue;
                    if (structure.def.kind == StructureKind.ResearchLab) facts.researchLabBuilt = true;
                    if (structure.def.role == StructureRole.Megaproject)
                        facts.highestMegaprojectStage = Math.Max(facts.highestMegaprojectStage, structure.level);
                }
                return facts;
            }
        }

        bool ObserveEraProgress()
        {
            var reached = EraRules.Reached(EraProgress);
            if (reached <= eraReached) return false;
            eraReached = reached;
            SetSetting(EraReachedKey, ((int)eraReached).ToString(CultureInfo.InvariantCulture));
            return true;
        }

        void LoadEraProgress(SaveLoadReport report)
        {
            bool invalid = false;
            foreach (var setting in settings)
            {
                if (setting == null || setting.key != EraReachedKey) continue;
                if (int.TryParse(setting.value, NumberStyles.None, CultureInfo.InvariantCulture, out int value) &&
                    value >= 0 && value <= (int)FrontierEra.Ascension)
                    eraReached = (FrontierEra)Math.Max((int)eraReached, value);
                else
                {
                    invalid = true;
                    report.warnings.Add("ignored an invalid reached-era record; inferring progress from this save");
                }
            }
            bool advanced = ObserveEraProgress();
            if (invalid) SetSetting(EraReachedKey, ((int)eraReached).ToString(CultureInfo.InvariantCulture));
            if (invalid || advanced) saves.MarkDirty();
        }
    }
}
