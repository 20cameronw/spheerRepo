using System;
using System.Collections.Generic;
using System.Globalization;
using Frontier.Economy;

namespace Frontier
{
    public enum BuilderFinishResult { Rejected, Saved, PendingSave }
    public enum BuilderPurchaseResult { Rejected, Saved, PendingSave }

    public partial class GameState
    {
        const string ThirdCrewPurchasedKey = "builder_third_purchased";
        const string FourthCrewPurchasedKey = "builder_fourth_purchased";

        public int PurchasedBuilderCount
        {
            get { return (HasSetting(ThirdCrewPurchasedKey) ? 1 : 0) + (HasSetting(FourthCrewPurchasedKey) ? 1 : 0); }
        }

        public int BuilderCount
        {
            get
            {
                if (db.shardRules == null || db.shardRules.data == null)
                    return Math.Max(1, db.builders);

                int hubBonus = 0;
                foreach (var structure in structures)
                {
                    if (!IsOperational(structure) || structure.def.effectKind != "extraBuilders") continue;
                    hubBonus += Math.Max(0, (int)Math.Round(structure.def.EffectAt(structure.level)));
                }
                return BuilderPlan.Count(db.shardRules.data, IsResearched("field_engineering"), hubBonus, PurchasedBuilderCount);
            }
        }

        public int BusyBuilderCount
        {
            get
            {
                int busy = 0;
                foreach (var slot in builders)
                {
                    if (IsActiveBuilderSlot(slot)) busy++;
                }
                return busy;
            }
        }

        public int AvailableBuilderCount { get { return Math.Max(0, BuilderCount - BusyBuilderCount); } }

        public List<DateTimeOffset> ActiveBuilderCompletionTimes()
        {
            var times = new List<DateTimeOffset>();
            foreach (var slot in builders)
                if (IsActiveBuilderSlot(slot) && UtcText.TryParse(slot.busyUntilUtc, out var end)) times.Add(end);
            times.Sort();
            return times;
        }
        public int NextBuilderPurchaseCost
        {
            get
            {
                if (db.shardRules == null || db.shardRules.data == null || PurchasedBuilderCount >= 2) return -1;
                return BuilderPlan.NextBuilderPrice(db.shardRules.data, BuilderCount);
            }
        }

        public BuilderPurchaseResult PurchaseBuilder(int expectedOwned, int maximumCost, out string message)
        {
            if (SaveIsReadOnly)
            {
                message = UiText.Get("crew.purchase.read-only");
                return BuilderPurchaseResult.Rejected;
            }
            int owned = BuilderCount;
            int price = NextBuilderPurchaseCost;
            if (price < 0)
            {
                message = UiText.Get("crew.purchase.unavailable");
                return BuilderPurchaseResult.Rejected;
            }
            if (owned != expectedOwned || maximumCost < price)
            {
                message = UiText.Get("crew.purchase.changed");
                return BuilderPurchaseResult.Rejected;
            }
            if (ShardBalance < price)
            {
                message = UiText.Get("crew.purchase.shards-missing");
                return BuilderPurchaseResult.Rejected;
            }
            SpendShards(price);
            SetSetting(HasSetting(ThirdCrewPurchasedKey) ? FourthCrewPurchasedKey : ThirdCrewPurchasedKey, "1");
            Notify();
            if (!saves.Save())
            {
                message = UiText.Get("crew.purchase.pending-save", saves.LastError);
                return BuilderPurchaseResult.PendingSave;
            }
            message = UiText.Get("crew.purchase.done");
            return BuilderPurchaseResult.Saved;
        }

        public bool IsUnderConstruction(PlacedStructure structure)
        {
            return IsPlaced(structure) && !string.IsNullOrEmpty(structure.record.buildEndUtc) &&
                UtcText.TryParse(structure.record.buildEndUtc, out var end) && end > clock.UtcNow;
        }

        public bool IsUpgradeInProgress(PlacedStructure structure)
        {
            return IsPlaced(structure) && !string.IsNullOrEmpty(structure.record.upgradeEndUtc) &&
                UtcText.TryParse(structure.record.upgradeEndUtc, out var end) && end > clock.UtcNow;
        }

        public bool IsOperational(PlacedStructure structure)
        {
            return IsPlaced(structure) && !IsUnderConstruction(structure);
        }

        public bool IsBuilderBusy(PlacedStructure structure)
        {
            return IsUnderConstruction(structure) || IsUpgradeInProgress(structure);
        }

        public int BuilderJobSecondsRemaining(PlacedStructure structure)
        {
            if (!IsPlaced(structure)) return 0;
            string timestamp = IsUnderConstruction(structure) ? structure.record.buildEndUtc :
                IsUpgradeInProgress(structure) ? structure.record.upgradeEndUtc : "";
            if (!UtcText.TryParse(timestamp, out var end)) return 0;
            double remaining = (end - clock.UtcNow).TotalSeconds;
            return remaining <= 0 ? 0 : (int)Math.Ceiling(remaining);
        }

        public int BuilderSkipCost(PlacedStructure structure)
        {
            if (db.shardRules == null || db.shardRules.data == null || !IsBuilderBusy(structure)) return 0;
            return ShardCurve.SkipCost(db.shardRules.data, BuilderJobSecondsRemaining(structure));
        }

        public BuilderFinishResult FinishBuilderJob(PlacedStructure structure, int maximumCost, out string message)
        {
            if (SaveIsReadOnly)
            {
                message = UiText.Get("crew.finish.read-only");
                return BuilderFinishResult.Rejected;
            }
            if (db.shardRules == null || db.shardRules.data == null || !IsBuilderBusy(structure))
            {
                message = UiText.Get("crew.finish.unavailable");
                return BuilderFinishResult.Rejected;
            }
            int cost = BuilderSkipCost(structure);
            if (cost > maximumCost || maximumCost < 0)
            {
                message = UiText.Get("crew.finish.price-changed");
                return BuilderFinishResult.Rejected;
            }
            if (ShardBalance < cost)
            {
                message = UiText.Get("crew.finish.shards-missing");
                return BuilderFinishResult.Rejected;
            }
            BuilderSlot assignment = null;
            string target = BuilderTarget(structure.planetId, structure.slot);
            foreach (var slot in builders)
                if (slot != null && slot.target == target && IsMatchingBuilderJob(slot, structure.record))
                {
                    assignment = slot;
                    break;
                }
            if (assignment == null)
            {
                message = UiText.Get("crew.finish.assignment-missing");
                return BuilderFinishResult.Rejected;
            }

            SpendShards(cost);
            string completedAt = UtcText.Format(clock.UtcNow);
            if (IsUnderConstruction(structure)) structure.record.buildEndUtc = completedAt;
            else structure.record.upgradeEndUtc = completedAt;
            assignment.busyUntilUtc = completedAt;
            CompleteBuilderJobs();
            Notify();
            if (!saves.Save())
            {
                message = UiText.Get("crew.finish.pending-save", saves.LastError);
                return BuilderFinishResult.PendingSave;
            }
            message = UiText.Get("crew.finish.done");
            return BuilderFinishResult.Saved;
        }

        public string BuilderJobStatus(PlacedStructure structure)
        {
            if (IsUnderConstruction(structure)) return UiText.Get("crew.status.construction", FormatDuration(BuilderJobSecondsRemaining(structure)));
            if (IsUpgradeInProgress(structure))
                return UiText.Get("crew.status.upgrade", structure.record.pendingUpgradeLevel, FormatDuration(BuilderJobSecondsRemaining(structure)));
            return "";
        }

        public string BuilderAvailabilityReason()
        {
            return AvailableBuilderCount > 0 ? null : UiText.Get("crew.busy");
        }

        static string FormatDuration(int seconds)
        {
            if (seconds >= 3600) return UiText.Get("duration.hours-minutes", seconds / 3600, seconds % 3600 / 60);
            if (seconds >= 60) return UiText.Get("duration.minutes-seconds", seconds / 60, seconds % 60);
            return UiText.Get("duration.seconds", seconds);
        }

        public int StructureJobSeconds(StructureSO definition, int targetLevel)
        {
            if (definition == null || !db.UsesImportedEconomy) return 0;
            var row = definition.Row(targetLevel);
            return row == null ? 0 : Math.Max(0, row.buildSeconds);
        }

        void StartBuilderJob(PlacedStructure structure, int seconds, int upgradeLevel)
        {
            if (seconds <= 0) return;
            string endUtc = UtcText.Format(clock.UtcNow.AddSeconds(seconds));
            if (upgradeLevel > 0)
            {
                structure.record.upgradeEndUtc = endUtc;
                structure.record.pendingUpgradeLevel = upgradeLevel;
            }
            else structure.record.buildEndUtc = endUtc;
            string target = BuilderTarget(structure.planetId, structure.slot);
            builders.Add(new BuilderSlot
            {
                slotId = NextBuilderId(),
                target = target,
                busyUntilUtc = endUtc
            });
            world.Touch();
        }

        string NextBuilderId()
        {
            for (int i = 1; i <= BuilderCount; i++)
            {
                string id = "builder-" + i.ToString(CultureInfo.InvariantCulture);
                bool inUse = false;
                foreach (var slot in builders)
                {
                    if (slot != null && slot.slotId == id && IsActiveBuilderSlot(slot)) { inUse = true; break; }
                }
                if (!inUse) return id;
            }
            return "builder-" + (builders.Count + 1).ToString(CultureInfo.InvariantCulture);
        }

        bool CompleteBuilderJobs()
        {
            bool changed = false;
            for (int i = builders == null ? -1 : builders.Count - 1; i >= 0; i--)
            {
                var slot = builders[i];
                if (slot == null) continue;
                if (IsObstacleBuilderTarget(slot.target)) continue;
                StructureRecord record = FindBuilderTarget(slot.target);
                if (record == null) continue;
                bool isBuild = record.buildEndUtc == slot.busyUntilUtc && !string.IsNullOrEmpty(record.buildEndUtc);
                bool isUpgrade = record.upgradeEndUtc == slot.busyUntilUtc && !string.IsNullOrEmpty(record.upgradeEndUtc);
                if (!isBuild && !isUpgrade) continue;
                if (!UtcText.TryParse(slot.busyUntilUtc, out var end))
                {
                    DebugBuilderWarning("discarded a builder assignment with an invalid end time");
                    record.buildEndUtc = "";
                    record.upgradeEndUtc = "";
                    record.pendingUpgradeLevel = 0;
                    world.Touch();
                    builders.RemoveAt(i);
                    changed = true;
                    continue;
                }
                if (end > clock.UtcNow) continue;

                if (isBuild)
                {
                    record.buildEndUtc = "";
                    world.Touch();
                    var definition = db.FindStructure(record.defId);
                    RecordAtlasStructureBuilt(definition);
                    RecordMegaprojectStage(definition);
                }
                else
                {
                    int level = record.pendingUpgradeLevel > record.level
                        ? record.pendingUpgradeLevel : record.level + 1;
                    var definition = db.FindStructure(record.defId);
                    if (definition != null) level = Math.Min(level, definition.maxLevel);
                    record.upgradeEndUtc = "";
                    record.pendingUpgradeLevel = 0;
                    world.SetLevel(record, level);
                    RecordMegaprojectStage(definition);
                }

                builders.RemoveAt(i);
                changed = true;
            }
            if (CompleteObstacleJobs()) changed = true;
            return changed;
        }

        bool RecoverBuilderJobs(List<string> warnings)
        {
            var savedSlots = builders ?? new List<BuilderSlot>();
            var recovered = new List<BuilderSlot>();
            var matched = new HashSet<string>(StringComparer.Ordinal);
            DateTime now = clock.UtcNow;
            bool changed = false;

            foreach (var delta in world.All)
            {
                foreach (var record in delta.structures)
                {
                    if (!string.IsNullOrEmpty(record.buildEndUtc) && !string.IsNullOrEmpty(record.upgradeEndUtc))
                    {
                        warnings.Add("cancelled the conflicting upgrade timer for '" + record.defId + "' on " + delta.planetId);
                        record.upgradeEndUtc = "";
                        record.pendingUpgradeLevel = 0;
                        changed = true;
                    }

                    bool isBuild = !string.IsNullOrEmpty(record.buildEndUtc);
                    string timestamp = isBuild ? record.buildEndUtc : record.upgradeEndUtc;
                    if (string.IsNullOrEmpty(timestamp)) continue;
                    string target = BuilderTarget(delta.planetId, record.slot);
                    matched.Add(target);
                    if (!UtcText.TryParse(timestamp, out var end))
                    {
                        warnings.Add("cleared an invalid builder timer for '" + record.defId + "' on " + delta.planetId);
                        record.buildEndUtc = "";
                        record.upgradeEndUtc = "";
                        record.pendingUpgradeLevel = 0;
                        changed = true;
                        continue;
                    }
                    if (!isBuild && (record.pendingUpgradeLevel <= record.level ||
                        record.pendingUpgradeLevel > (db.FindStructure(record.defId) != null ? db.FindStructure(record.defId).maxLevel : record.level + 1)))
                        record.pendingUpgradeLevel = record.level + 1;

                    if (end <= now)
                    {
                        if (isBuild)
                        {
                            record.buildEndUtc = "";
                            var definition = db.FindStructure(record.defId);
                            RecordAtlasStructureBuilt(definition);
                            RecordMegaprojectStage(definition);
                        }
                        else
                        {
                            var definition = db.FindStructure(record.defId);
                            int level = record.pendingUpgradeLevel;
                            if (definition != null) level = Math.Min(level, definition.maxLevel);
                            record.upgradeEndUtc = "";
                            record.pendingUpgradeLevel = 0;
                            world.SetLevel(record, level);
                            RecordMegaprojectStage(definition);
                        }
                        changed = true;
                        continue;
                    }

                    recovered.Add(new BuilderSlot
                    {
                        slotId = "builder-" + (recovered.Count + 1).ToString(CultureInfo.InvariantCulture),
                        target = target,
                        busyUntilUtc = timestamp
                    });
                }
                for (int i = delta.obstacleJobs.Count - 1; i >= 0; i--)
                {
                    ObstacleJobRecord job = delta.obstacleJobs[i];
                    string target = ObstacleBuilderTarget(delta.planetId, job.pad);
                    matched.Add(target);
                    if (!UtcText.TryParse(job.endUtc, out var end))
                    {
                        warnings.Add("cleared an invalid obstacle timer for pad " + job.pad + " on " + delta.planetId);
                        delta.obstacleJobs.RemoveAt(i);
                        changed = true;
                        continue;
                    }
                    if (end <= now) continue;
                    recovered.Add(new BuilderSlot
                    {
                        slotId = "builder-" + (recovered.Count + 1).ToString(CultureInfo.InvariantCulture),
                        target = target,
                        busyUntilUtc = job.endUtc
                    });
                }
            }

            foreach (var saved in savedSlots)
                if (saved != null && !string.IsNullOrEmpty(saved.target) && !matched.Contains(saved.target))
                    recovered.Add(saved);
            builders = recovered;
            if (changed) world.Touch();
            return changed;
        }

        static string BuilderTarget(string planetId, int slot)
        {
            return planetId + "#" + slot.ToString(CultureInfo.InvariantCulture);
        }

        static string ObstacleBuilderTarget(string planetId, int pad)
        {
            return planetId + "#obstacle#" + pad.ToString(CultureInfo.InvariantCulture);
        }

        static bool IsObstacleBuilderTarget(string target)
        {
            return !string.IsNullOrEmpty(target) && target.LastIndexOf("#obstacle#", StringComparison.Ordinal) > 0;
        }

        bool IsActiveBuilderSlot(BuilderSlot slot)
        {
            if (slot == null || !UtcText.TryParse(slot.busyUntilUtc, out var end) || end <= clock.UtcNow) return false;
            if (IsObstacleBuilderTarget(slot.target))
            {
                var job = FindObstacleBuilderTarget(slot.target);
                return job != null && job.endUtc == slot.busyUntilUtc;
            }
            var record = FindBuilderTarget(slot.target);
            return record != null && IsMatchingBuilderJob(slot, record);
        }

        StructureRecord FindBuilderTarget(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;
            int separator = target.LastIndexOf('#');
            if (separator <= 0 || !int.TryParse(target.Substring(separator + 1), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int slot)) return null;
            string planetId = target.Substring(0, separator);
            return world.StructureAt(planetId, slot);
        }

        ObstacleJobRecord FindObstacleBuilderTarget(string target)
        {
            if (!IsObstacleBuilderTarget(target)) return null;
            int separator = target.LastIndexOf("#obstacle#", StringComparison.Ordinal);
            if (!int.TryParse(target.Substring(separator + 10), NumberStyles.None, CultureInfo.InvariantCulture, out int pad)) return null;
            string planetId = target.Substring(0, separator);
            if (!world.TryGet(planetId, out var delta)) return null;
            foreach (var job in delta.obstacleJobs) if (job.pad == pad) return job;
            return null;
        }

        static bool IsMatchingBuilderJob(BuilderSlot slot, StructureRecord record)
        {
            return slot != null && record != null &&
                (record.buildEndUtc == slot.busyUntilUtc || record.upgradeEndUtc == slot.busyUntilUtc);
        }

        static void DebugBuilderWarning(string message)
        {
            UnityEngine.Debug.LogWarning("Frontier: " + message);
        }
    }
}
