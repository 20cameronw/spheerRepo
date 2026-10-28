using System;

namespace Frontier.Economy
{
    public enum AchievementSize { Small, Medium, Large }

    /// <summary>One-time milestones already budgeted by ShardModel. Goals, recurring tasks and premium spending cannot mint extra awards.</summary>
    public static class AchievementRewards
    {
        public static bool TrySize(string id, out AchievementSize size)
        {
            size = AchievementSize.Small;
            switch (id)
            {
                case "first_harvest": case "first_structure": case "first_warehouse": case "first_fuel":
                case "first_hop": case "research_lab": case "first_wave": case "jump_range_1":
                case "first_steel": case "first_hazard_plating": case "mega_first_stage": case "mega_stage_3":
                    return true;
            }
            for (int i = 1; i < SimBands.EraMilestones.Length; i++)
                if (id == SimBands.EraMilestones[i]) { size = AchievementSize.Large; return true; }
            for (int i = 1; i <= 9; i++)
                if (id == "reach_tier_" + i) { size = AchievementSize.Medium; return true; }
            return false;
        }

        public static int Amount(string id, ShardRulesDef rules)
        {
            if (!TrySize(id, out var size)) throw new ArgumentException("Unbudgeted achievement id: " + id, nameof(id));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            int amount = size == AchievementSize.Large ? rules.achievementLarge
                : size == AchievementSize.Medium ? rules.achievementMedium : rules.achievementSmall;
            return Math.Max(0, amount);
        }
    }
}
