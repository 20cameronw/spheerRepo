using System;
using System.Collections.Generic;

namespace Frontier.Economy
{
    public static class TravelTuningOverrides
    {
        public static bool TryApply(Tuning bundled, IReadOnlyDictionary<string, string> overrides,
            out Tuning effective, out string error)
        {
            if (bundled == null) throw new ArgumentNullException(nameof(bundled));
            if (overrides == null) throw new ArgumentNullException(nameof(overrides));
            effective = bundled.Clone();
            foreach (var pair in overrides)
            {
                if (pair.Key == null || !pair.Key.StartsWith("travel.", StringComparison.Ordinal)
                    || !bundled.Has(pair.Key))
                {
                    error = "Remote runtime tuning does not support key '" + pair.Key + "'.";
                    return false;
                }
            }
            if (!bundled.TryApplyOverrides(overrides, out var candidate, out error)) return false;
            foreach (string key in new[] { "travel.plating_l2_tier", "travel.plating_l3_tier" })
            {
                if (candidate.D(key) == candidate.I(key)) continue;
                error = "Remote travel tuning requires whole-number plating tiers.";
                return false;
            }
            if (candidate.D("travel.run_cap_seconds") < candidate.D("travel.run_base_seconds")
                || candidate.I("travel.plating_l3_tier") < candidate.I("travel.plating_l2_tier"))
            {
                error = "Remote travel tuning has inconsistent duration or plating thresholds.";
                return false;
            }
            effective = candidate;
            return true;
        }
    }
}
