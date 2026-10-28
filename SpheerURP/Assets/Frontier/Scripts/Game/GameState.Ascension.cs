using System;
using System.Collections.Generic;
using Frontier.Economy;
using UnityEngine;

namespace Frontier
{
    public partial class GameState
    {
        AscensionProgress ascension = new AscensionProgress();
        AscensionRulesSO ascensionRules;
        public int AscensionRank { get { return ascension.rank; } }
        public int LegacyPoints { get { return ascension.legacyPoints; } }
        public bool AscensionAvailable { get { return ascensionRules != null && (ascensionRules.enableAscension || NetworkComplete || AscensionRank > 0); } }
        public bool AscensionNeedsResearch
        {
            get { return ascensionRules != null && !NetworkComplete && !IsResearched(ascensionRules.data.requiredResearch); }
        }
        public float MaximumLegacyHarvestBonus
        {
            get { return ascensionRules != null ? ascensionRules.data.maxHarvestBonus : 0f; }
        }
        public float LegacyHarvestMultiplier
        {
            get { return AscensionAvailable ? ascensionRules.data.HarvestMultiplier(LegacyPoints) : 1f; }
        }
        public float NextLegacyHarvestMultiplier
        {
            get
            {
                if (!AscensionAvailable) return 1f;
                return ascensionRules.data.HarvestMultiplier((int)Math.Min(int.MaxValue,
                    (long)LegacyPoints + ascensionRules.data.legacyPointsPerRank));
            }
        }

        void InitializeAscensionRules()
        {
            if (!db.UsesImportedEconomy) return;
            var asset = Resources.Load<AscensionRulesSO>("Frontier/AscensionRules");
            bool researchExists = false;
            if (asset != null && asset.data != null && asset.data.IsValid && db.research != null)
                foreach (var node in db.research)
                    if (node != null && node.data != null && node.data.id == asset.data.requiredResearch)
                        researchExists = true;
            if (!researchExists)
            {
                Debug.LogError("Frontier: Ascension rules or research gate are invalid; Ascension is unavailable.");
                return;
            }
            ascensionRules = asset;
        }

        public string AscensionLockReason()
        {
            if (SaveIsReadOnly) return UiText.Get("ascension.lock.read-only");
            if (ascensionRules == null) return UiText.Get("ascension.lock.unavailable");
            if (!AscensionAvailable) return UiText.Get("ascension.lock.disabled");
            if (AscensionRank == int.MaxValue || (long)LegacyPoints + ascensionRules.data.legacyPointsPerRank > int.MaxValue)
                return UiText.Get("ascension.lock.maximum");
            if (!NetworkComplete && !IsResearched(ascensionRules.data.requiredResearch)) return UiText.Get("ascension.lock.research");
            if (!NetworkComplete && EraProgress.highestMegaprojectStage < ascensionRules.data.requiredProjectStage)
                return UiText.Get("ascension.lock.project", ascensionRules.data.requiredProjectStage);
            return null;
        }

        public bool TryAscend(out string message)
        {
            message = AscensionLockReason();
            if (message != null) return false;

            // Work on a detached candidate. The live universe is not changed until the replacement and recovery copy are durable.
            var candidate = JsonUtility.FromJson<SaveV2>(JsonUtility.ToJson(BuildSave())).Normalize();
            int nextRank = candidate.ascension.rank + 1;
            ulong nextSeed = AscensionRules.NextSeed(universe.Seed, nextRank);
            var nextUniverse = new UniverseService(db, seed: nextSeed);
            candidate.createdWith = new CreatedWithInfo
            {
                appVersion = Application.version,
                generatorVersion = nextUniverse.Universe.GeneratorVersion,
                universeSeed = SeedText.Format(nextSeed)
            };
            candidate.ascension.rank = nextRank;
            candidate.ascension.legacyPoints += ascensionRules.data.legacyPointsPerRank;
            candidate.player.xp = 0;
            candidate.player.upgrades.Clear();
            candidate.player.toolTiers.Clear();
            candidate.player.inventory.Clear();
            candidate.player.fuelTank.Clear();
            candidate.network = new NetworkProgress();
            var starting = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in db.startingItems)
                if (item.resource != null && item.amount > 0) starting[item.resource.id] = item.amount;
            foreach (var item in starting)
                (IsTankFuel(item.Key) ? candidate.player.fuelTank : candidate.player.inventory)
                    .Add(new StackRecord { resourceId = item.Key, count = item.Value });
            candidate.builders.Clear();
            candidate.worlds.Clear();
            var newWorld = new WorldStore();
            newWorld.RecordVisit(nextUniverse.Home.Id, clock.UtcNow);
            candidate.worlds = newWorld.Export();
            candidate.location.currentPlanetId = nextUniverse.Home.Id;
            candidate.stats = new PlayerStats();
            candidate.stats.RecordLanding(0, true);
            candidate.settings.RemoveAll(entry => entry != null &&
                (entry.key == EraReachedKey || entry.key == FirstSessionGuideDismissedKey));
            candidate.settings.Add(new SettingEntry { key = FirstSessionGuideDismissedKey, value = "1" });
            candidate.clock.lastSavedUtc = UtcText.Format(clock.UtcNow);
            if (!TryRestoreSnapshot(candidate, "Ascension", out message)) return false;
            if (saves.IsDirty)
            {
                message = UiText.Get("ascension.save-retry", saves.LastError);
                return true;
            }
            message = UiText.Get("ascension.completed", AscensionRank);
            return true;
        }
    }
}
