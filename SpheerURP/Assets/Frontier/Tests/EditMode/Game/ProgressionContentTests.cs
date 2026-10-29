using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    [TestFixture]
    public class ProgressionContentTests
    {
        SaveSandbox sandbox;
        GameDatabase db;
        FakeClock clock;

        [SetUp]
        public void SetUp()
        {
            sandbox = new SaveSandbox();
            db = GameDatabase.Load();
            clock = new FakeClock();
        }

        [TearDown]
        public void TearDown()
        {
            sandbox.Dispose();
        }

        [Test]
        public void CloudRestoreCannotReplayAnUnsettledReward()
        {
            var state = new GameState(db, false, clock);
            Assert.IsTrue(state.TryCreateCloudSaveSnapshot(out var clean, out var error), error);
            state.backpack.Clear();
            Assert.IsTrue(state.PrepareGameplayReward("double-yield", db.FindResource("wood"), 3, null, out _, out error), error);
            Assert.IsTrue(state.TryCreateCloudSaveSnapshot(out var pending, out error), error);
            Assert.IsFalse(state.TryRestoreCloudSave(clean, out error));
            Assert.AreEqual(UiText.Get("reward.restore-busy"), error);
            Assert.IsTrue(state.CancelUnverifiedGameplayReward(out error), error);
            Assert.IsFalse(state.TryRestoreCloudSave(pending, out error));
            Assert.AreEqual(UiText.Get("reward.restore-busy"), error);
            Assert.IsTrue(state.TryRestoreCloudSave(clean, out error), error);
        }

        [Test]
        public void GameplayFlagsShipDisabledAndWithoutUnapprovedTimerTuning()
        {
            var rules = Resources.Load<GameplayRewardRules>("Frontier/GameplayRewardRules");
            Assert.IsNotNull(rules);
            Assert.IsFalse(rules.harvestEnabled);
            Assert.IsFalse(rules.constructionEnabled);
            Assert.IsTrue(rules.shardsEnabled);
            Assert.AreEqual(0, rules.constructionSeconds);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OptionalShardAdRewardUsesTheSaveJournalAndCannotBeReplayed(bool earned)
        {
            var state = new GameState(db, false, clock);
            int amount = db.shardRules.data.adReward;
            int startingBalance = state.ShardBalance;
            Assert.IsFalse(state.PrepareGameplayReward("free-shards", null, amount + 1, null, out _, out _),
                "The grant must match the authored shard reward.");
            Assert.IsFalse(state.SaveIsReadOnly);
            Assert.IsNull(state.PendingTravel);
            Assert.AreEqual(amount, state.db.shardRules.data.adReward);
            Assert.IsTrue(state.PrepareGameplayReward("free-shards", null, amount, null, out var ticket, out var error),
                error);
            Assert.AreEqual(startingBalance, state.ShardBalance, "No shards are granted before the ad is verified.");
            Assert.IsTrue(state.ResolveGameplayReward(state.RewardRequestContext, ticket.id, earned, out error), error);
            Assert.AreEqual(startingBalance + (earned ? amount : 0), state.ShardBalance);
            Assert.IsTrue(state.ResolveGameplayReward(state.RewardRequestContext, ticket.id, earned, out error), error);
            if (!earned)
                Assert.IsFalse(state.ResolveGameplayReward(state.RewardRequestContext, ticket.id, true, out error),
                    "A late earned callback cannot revive a cancelled reward.");
            Assert.AreEqual(startingBalance + (earned ? amount : 0), state.ShardBalance,
                "Repeating the callback cannot grant a second reward.");
            Assert.AreEqual(startingBalance + (earned ? amount : 0), new GameState(db, true, clock).ShardBalance);
        }

        [Test]
        public void OptionalShardAdRewardCannotOverflowTheWallet()
        {
            var state = new GameState(db, false, clock);
            Assert.IsTrue(state.TryCreateCloudSaveSnapshot(out var save, out var error), error);
            save.wallet.Clear();
            save.wallet.Add(new WalletEntry { currency = "shards", amount = int.MaxValue });
            Assert.IsTrue(state.TryRestoreCloudSave(save, out error), error);
            Assert.IsFalse(state.PrepareGameplayReward("free-shards", null, db.shardRules.data.adReward,
                null, out _, out _));
            Assert.AreEqual(int.MaxValue, state.ShardBalance);
        }

        [Test]
        public void FailedGameplayRewardWritesDoNotPublishProgressAndCanRetry()
        {
            var state = new GameState(db, false, clock);
            var wood = db.FindResource("wood");
            state.backpack.Clear();
            Directory.CreateDirectory(sandbox.SaveFile);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("Frontier save failed:"));
            Assert.IsFalse(state.PrepareGameplayReward("double-yield", wood, 3, null, out _, out var error));
            Assert.IsNotEmpty(error);
            Assert.AreEqual(0, state.Count(wood));
            Assert.IsNull(state.GameplayReward);
            Directory.Delete(sandbox.SaveFile);
            Assert.IsTrue(state.PrepareGameplayReward("double-yield", wood, 3, null, out var ticket, out error), error);
            string backup = sandbox.SaveFile + ".blocked";
            File.Move(sandbox.SaveFile, backup);
            Directory.CreateDirectory(sandbox.SaveFile);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("Frontier save failed:"));
            Assert.IsFalse(state.ResolveGameplayReward(state.RewardRequestContext, ticket.id, true, out error));
            Assert.IsNotEmpty(error);
            Assert.AreEqual(3, state.Count(wood));
            Assert.AreEqual("pending", state.GameplayReward.state);
            Directory.Delete(sandbox.SaveFile);
            File.Move(backup, sandbox.SaveFile);
            Assert.IsTrue(state.ResolveGameplayReward(state.RewardRequestContext, ticket.id, true, out error), error);
            Assert.AreEqual(6, state.Count(wood));
            Assert.AreEqual(6, new GameState(db, true, clock).Count(wood));
        }

        [Test]
        public void ResumeCompletesJobsAndDoesNotRepeatTheReturnSummary()
        {
            var state = new GameState(db, false, clock);
            var job = state.DebugPlace(db.StructureOfKind(StructureKind.Warehouse), 0);
            state.DebugSetBuilderJob(job, 30);
            Assert.IsTrue(state.Save());
            clock.Advance(60);
            state.ResumeOfflineProgress();
            Assert.IsTrue(state.ReturnItems.Any(i => i.slot == job.slot && i.resourceId == null));
            Assert.IsFalse(state.IsUnderConstruction(job));
            int version = state.ReturnSummaryVersion;
            state.ResumeOfflineProgress();
            Assert.IsEmpty(state.ReturnItems);
            Assert.AreEqual(version, state.ReturnSummaryVersion);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void GameplayHarvestJournalPreservesBaseAndSettlesAtMostOnce(bool earned)
        {
            var state = new GameState(db, false, clock);
            var wood = db.FindResource("wood");
            state.backpack.Clear();
            Assert.IsTrue(state.PrepareGameplayReward("double-yield", wood, 3, null, out var ticket, out var error), error);
            Assert.AreEqual(3, state.Count(wood), "Base harvest is secured before the ad.");
            Assert.IsFalse(state.PrepareGameplayReward("double-yield", wood, 3, null, out _, out _), "No overlapping requests.");
            var loaded = new GameState(db, true, clock);
            Assert.AreEqual(3, loaded.Count(wood));
            Assert.AreEqual("pending", loaded.GameplayReward.state);
            Assert.IsFalse(loaded.ResolveGameplayReward(state.RewardRequestContext, ticket.id, true, out _),
                "A callback from a replaced session is invalid.");
            Assert.IsTrue(state.ResolveGameplayReward(state.RewardRequestContext, ticket.id, earned, out error), error);
            Assert.AreEqual(earned ? 6 : 3, state.Count(wood));
            state.ResolveGameplayReward(state.RewardRequestContext, ticket.id, earned, out _);
            Assert.AreEqual(earned ? 6 : 3, state.Count(wood));
            Assert.AreEqual(earned ? 6 : 3, new GameState(db, true, clock).Count(wood));
        }

        [Test]
        public void EarnedHarvestBonusWaitsForCapacityAndSurvivesRestart()
        {
            var state = new GameState(db, false, clock);
            var wood = db.FindResource("wood");
            state.backpack.Clear();
            Assert.IsTrue(state.PrepareGameplayReward("double-yield", wood, 3, null, out var ticket, out var error), error);
            state.backpack.Add(wood.id, int.MaxValue);
            Assert.IsFalse(state.ResolveGameplayReward(state.RewardRequestContext, ticket.id, true, out error));
            Assert.IsNotEmpty(error);
            Assert.AreEqual("earned", state.GameplayReward.state);
            var loaded = new GameState(db, true, clock);
            Assert.AreEqual("earned", loaded.GameplayReward.state);
            loaded.backpack.Clear();
            Assert.IsTrue(loaded.SettleGameplayReward(out error), error);
            Assert.AreEqual(3, loaded.Count(wood));
            Assert.IsFalse(loaded.SettleGameplayReward(out _));
            Assert.AreEqual(3, new GameState(db, true, clock).Count(wood));
        }

        [Test]
        public void ConstructionRewardChangesOnlyTheQuotedJobAndRetainsNormalCompletionRules()
        {
            var state = new GameState(db, false, clock);
            var job = state.DebugPlace(db.StructureOfKind(StructureKind.Warehouse), 0);
            state.DebugSetBuilderJob(job, 120);
            Assert.IsTrue(state.PrepareGameplayReward("timer-skip", null, 60, job, out var ticket, out var error), error);
            Assert.IsTrue(state.ResolveGameplayReward(state.RewardRequestContext, ticket.id, true, out error), error);
            Assert.AreEqual(60, state.BuilderJobSecondsRemaining(job));
            var loaded = new GameState(db, true, clock);
            var loadedJob = loaded.structures.First(s => s.slot == 0);
            Assert.AreEqual(60, loaded.BuilderJobSecondsRemaining(loadedJob));
            clock.Advance(61);
            loaded.Tick();
            Assert.IsFalse(loaded.IsUnderConstruction(loadedJob));
            Assert.AreEqual(loaded.BuilderCount, loaded.AvailableBuilderCount);
        }

        [Test]
        public void ConstructionRewardCannotBoostAReplacementJob()
        {
            var state = new GameState(db, false, clock);
            var job = state.DebugPlace(db.StructureOfKind(StructureKind.Warehouse), 0);
            state.DebugSetBuilderJob(job, 120);
            Assert.IsTrue(state.PrepareGameplayReward("timer-skip", null, 60, job, out var ticket, out _));
            state.DebugSetBuilderJob(job, 240);
            Assert.IsFalse(state.ResolveGameplayReward(state.RewardRequestContext, ticket.id, true, out var error));
            Assert.IsNotEmpty(error);
            Assert.AreEqual(240, state.BuilderJobSecondsRemaining(job));
            Assert.IsTrue(state.CancelUnverifiedGameplayReward(out error), error);
        }

        [Test]
        public void TrackedProjectSurvivesRestartAndRevalidatesWithoutSpending()
        {
            var state = new GameState(db, false, clock);
            var upgrade = db.FindUpgrade("pack");
            var project = new PlayerProject { kind = "upgrade", id = upgrade.id, target = 1, planetId = state.planet.Id };
            int before = state.Count(db.FindResource("wood"));
            Assert.IsTrue(state.TrackProject(project, out var error), error);
            var loaded = new GameState(db, true, clock);
            Assert.AreEqual(project.id, loaded.TrackedProject.id);
            Assert.IsFalse(loaded.ProjectState(loaded.TrackedProject).complete);
            CollectionAssert.AreEquivalent(upgrade.CostAt(0).Select(c => c.amount),
                loaded.ProjectState(loaded.TrackedProject).costs.Select(c => c.amount));
            Assert.AreEqual(before, loaded.Count(db.FindResource("wood")));
            loaded.DebugSetUpgrade(upgrade, 1);
            Assert.IsTrue(loaded.ProjectState(loaded.TrackedProject).complete);
            Assert.IsTrue(loaded.TrackProject(null, out error), error);
            Assert.IsNull(new GameState(db, true, clock).TrackedProject);
        }

        [Test]
        public void CarriedOnlyProjectsDoNotMistakeConnectedStockForSpendableCargo()
        {
            var state = new GameState(db, false, clock);
            var wood = db.FindResource("wood");
            state.backpack.Clear();
            var warehouse = state.DebugPlace(db.StructureOfKind(StructureKind.Warehouse), 0);
            var storage = state.StorageFor(warehouse);
            storage.slots[0].resourceId = wood.id; storage.slots[0].count = 12;
            state.DebugPlace(db.StructureOfKind(StructureKind.CargoDepot), 1);
            var obstacle = new PlayerProject { kind = "obstacle", id = "clear", slot = 2, planetId = state.planet.Id };
            Assert.AreEqual(0, state.ProjectResourceCount(obstacle, wood));
            Assert.AreEqual(12, state.ProjectResourceCount(new PlayerProject { kind = "upgrade" }, wood));
            var options = state.ResourceOptions(wood, 5, carriedOnly: true);
            Assert.IsFalse(options.Any(s => s.kind == ResourceSourceKind.Usable));
            Assert.IsTrue(options.Any(s => s.kind == ResourceSourceKind.LocalStock && s.amount == 12));
            Assert.AreEqual(12, WarehouseOps.Held(storage, wood.id));
        }

        [Test]
        public void TrackedPlanetLinkKeepsItsDestinationAndLiveCosts()
        {
            var state = new GameState(db, false, clock);
            var destination = state.universe.Get(db.planets[1].id);
            var project = new PlayerProject { kind = "link", id = destination.Id, planetId = state.planet.Id };
            Assert.IsTrue(state.TrackProject(project, out var error), error);
            var loaded = new GameState(db, true, clock);
            Assert.AreEqual(destination.Id, loaded.TrackedProject.id);
            Assert.AreEqual(UiText.Get("network.link", destination.Name), loaded.ProjectState(loaded.TrackedProject).name);
            CollectionAssert.AreEquivalent(state.NetworkCosts(false).Select(c => c.amount),
                loaded.ProjectState(loaded.TrackedProject).costs.Select(c => c.amount));
            Assert.IsFalse(loaded.ProjectState(loaded.TrackedProject).complete);
        }

        [Test]
        public void ResourceOptionsPreferUsableAndLocalStockAndKeepRemoteRetrievalExplicit()
        {
            var state = new GameState(db, false, clock);
            var wood = db.FindResource("wood");
            state.backpack.Clear();
            var warehouse = state.DebugPlace(db.StructureOfKind(StructureKind.Warehouse), 0);
            var storage = state.StorageFor(warehouse);
            storage.slots[0].resourceId = wood.id; storage.slots[0].count = 12;
            var options = state.ResourceOptions(wood, 5);
            Assert.AreEqual(ResourceSourceKind.LocalStock, options[0].kind);
            Assert.AreEqual(12, options[0].amount);
            state.DebugPlace(db.StructureOfKind(StructureKind.CargoDepot), 1);
            Assert.AreEqual(ResourceSourceKind.Usable, state.ResourceOptions(wood, 5)[0].kind);
            state.DebugTeleport(state.universe.Get(db.planets[1].id));
            options = state.ResourceOptions(wood, 5);
            Assert.IsTrue(options.Any(s => s.kind == ResourceSourceKind.RemoteStock && s.amount == 12));
            Assert.IsFalse(options.Any(s => s.kind == ResourceSourceKind.Usable));
            Assert.AreEqual(0, state.Count(wood));
            Assert.AreEqual(12, WarehouseOps.Held(storage, wood.id));
        }

        [Test]
        public void ReturnSummaryReportsCompletedJobsOnlyOnceWithoutChangingCargo()
        {
            var state = new GameState(db, false, clock);
            var structure = state.DebugPlace(db.StructureOfKind(StructureKind.Warehouse), 0);
            state.DebugSetBuilderJob(structure, 30);
            Assert.IsTrue(state.Save());
            clock.Advance(60);
            var loaded = new GameState(db, true, clock);
            Assert.IsTrue(loaded.ReturnItems.Any(i => i.slot == 0 && i.resourceId == null));
            Assert.AreEqual(state.Count(db.FindResource("wood")), loaded.Count(db.FindResource("wood")));
            Assert.AreEqual(0, new GameState(db, true, clock).ReturnItems.Count);
        }

        [Test]
        [TestCase("building")]
        [TestCase("structure-upgrade")]
        [TestCase("ship-upgrade")]
        [TestCase("research")]
        [TestCase("tool")]
        [TestCase("recipe")]
        public void AllProgressionGatesRequireXpNotPlanetVisits(string surface)
        {
            var isolated = GameDatabase.Select(db.ContentSource, GameDatabase.LoadLegacy(), out string error);
            Assert.IsNull(error);
            var copies = new List<UnityEngine.Object>();
            try
            {
                isolated.structures = (StructureSO[])isolated.structures.Clone();
                isolated.upgrades = (UpgradeSO[])isolated.upgrades.Clone();
                isolated.research = (ResearchNodeSO[])isolated.research.Clone();
                isolated.toolLadders = (ToolLadderSO[])isolated.toolLadders.Clone();
                isolated.recipes = (RecipeSO[])isolated.recipes.Clone();
                var state = new GameState(isolated, false, clock);
                GiveMaterials(state);
                Func<string> reason;
                const int required = 4;
                if (surface == "building" || surface == "structure-upgrade")
                {
                    var def = CloneForGate(isolated.StructureOfKind(StructureKind.Warehouse), isolated.structures, copies);
                    def.unlockResearch = "";
                    def.unlockTier = surface == "building" ? required : 0;
                    var row = def.Row(surface == "building" ? 1 : 2);
                    row.requiresReachTier = required;
                    row.requiresLabLevel = 0;
                    var placed = surface == "building" ? null : state.DebugPlace(def, 0);
                    reason = surface == "building" ? () => state.BuildLockReason(def)
                        : () => state.StructureUpgradeLockReason(placed);
                }
                else if (surface == "ship-upgrade")
                {
                    var def = CloneForGate(isolated.upgrades.First(u => u.HasLevelRows &&
                        u.effect != UpgradeEffect.StackMultiplier), isolated.upgrades, copies);
                    def.NextRow(0).requiresReachTier = required;
                    def.NextRow(0).researchPrereq = "";
                    reason = () => state.UpgradeLockReason(def);
                }
                else if (surface == "research")
                {
                    var node = CloneForGate(isolated.research[0], isolated.research, copies);
                    node.data.tier = required;
                    node.data.labLevel = 0;
                    node.data.prereqs = new string[0];
                    reason = () => state.ResearchLockReason(node);
                }
                else if (surface == "tool")
                {
                    var ladder = CloneForGate(isolated.toolLadders.First(t => t.data.kind == Economy.ToolKind.Axe),
                        isolated.toolLadders, copies);
                    var row = ladder.data.tiers.First(t => t.tier == 2);
                    row.requiresReachTier = required;
                    row.researchPrereq = "";
                    row.requiresLabLevel = 0;
                    reason = () => state.ToolTierLockReason(ladder);
                }
                else
                {
                    var recipe = CloneForGate(isolated.recipes[0], isolated.recipes, copies);
                    recipe.tier = required;
                    recipe.unlockResearch = "";
                    var station = state.DebugPlace(isolated.FindStructure(recipe.stationId), 0);
                    state.DebugSetStructureLevel(station, recipe.stationLevel);
                    reason = () => state.RecipeLockReason(recipe, station);
                }

                var home = state.planet;
                state.DebugSetLevel(1);
                Assert.AreEqual(UiText.Get("gate.player-level", required, 1), reason());
                state.DebugSetLevel(required);
                Assert.AreEqual(0, state.ReachTier);
                Assert.IsNull(reason(), "High XP must unlock the purchase without ever leaving the starter planet.");
                state.DebugSetLevel(1);
                var high = state.universe.FindGenerated(p => p.Tier >= required, 1, 0, 10000);
                Assert.IsNotNull(high);
                state.DebugTeleport(high);
                state.DebugTeleport(home);
                Assert.GreaterOrEqual(state.ReachTier, required);
                Assert.AreEqual(UiText.Get("gate.player-level", required, 1), reason(),
                    "Visiting a high-tier world must not bypass the XP requirement.");
                int threshold = new Progression(() => FrontierSettings.I.xpPerLevelBase).XpForLevel(required);
                state.AddXp(threshold - 1);
                Assert.AreEqual(required - 1, state.PlayerLevel);
                Assert.IsNotNull(reason(), "One XP below the required level must remain locked.");
                Assert.AreEqual(1, state.XpToNextLevel);
                state.AddXp(1);
                Assert.AreEqual(required, state.PlayerLevel);
                Assert.IsNull(reason(), "The exact XP threshold should unlock the purchase on the starter planet.");
                Assert.IsTrue(state.Save());
                var loaded = new GameState(isolated, true, clock);
                Assert.AreEqual(required, loaded.PlayerLevel);
                Assert.AreEqual(threshold, loaded.xp);
            }
            finally
            {
                foreach (var copy in copies) UnityEngine.Object.DestroyImmediate(copy);
                UnityEngine.Object.DestroyImmediate(isolated);
            }
        }

        static T CloneForGate<T>(T source, T[] entries, List<UnityEngine.Object> copies) where T : UnityEngine.Object
        {
            var copy = UnityEngine.Object.Instantiate(source);
            copies.Add(copy);
            entries[Array.IndexOf(entries, source)] = copy;
            return copy;
        }

        [Test]
        public void ProgressionPurchasesUseOnlyTheConnectedOperationalLocalPool(
            [Values("research", "tool", "ship", "defense")] string purchase,
            [Values("connected", "disconnected", "remote", "building-storage", "building-depot", "short")] string scope)
        {
            var isolated = GameDatabase.Select(db.ContentSource, GameDatabase.LoadLegacy(), out string error);
            Assert.IsNull(error);
            var copies = new List<UnityEngine.Object>();
            try
            {
                isolated.research = (ResearchNodeSO[])isolated.research.Clone();
                isolated.toolLadders = (ToolLadderSO[])isolated.toolLadders.Clone();
                isolated.upgrades = (UpgradeSO[])isolated.upgrades.Clone();
                var state = new GameState(isolated, false, clock);
                var wood = isolated.FindResource("wood");
                var stone = isolated.FindResource("stone");
                var cost = new[] { new Economy.ItemAmount("wood", 12), new Economy.ItemAmount("stone", 5) };
                Func<bool> eligible, buy;
                Func<GameState, bool> purchased;
                if (purchase == "research")
                {
                    var node = CloneForGate(isolated.research[0], isolated.research, copies);
                    node.data.cost = cost;
                    node.data.tier = 1;
                    node.data.labLevel = 0;
                    node.data.prereqs = new string[0];
                    eligible = () => state.CanResearch(node);
                    buy = () => state.Research(node);
                    purchased = loaded => loaded.IsResearched(node.data.id);
                }
                else if (purchase == "tool")
                {
                    var ladder = CloneForGate(isolated.toolLadders.First(t => t.data.kind == Economy.ToolKind.Axe),
                        isolated.toolLadders, copies);
                    var next = ladder.data.tiers.First(t => t.tier == 2);
                    next.cost = cost;
                    next.requiresReachTier = 1;
                    next.requiresLabLevel = 0;
                    next.researchPrereq = "";
                    eligible = () => state.ToolTierLockReason(ladder) == null;
                    buy = () => state.BuyToolTier(ladder);
                    purchased = loaded => loaded.ToolTier(ladder) == 2;
                }
                else
                {
                    var category = purchase == "ship" ? UpgradeCategory.Ship : UpgradeCategory.Defense;
                    var upgrade = CloneForGate(isolated.upgrades.First(u => u.category == category && u.HasLevelRows),
                        isolated.upgrades, copies);
                    var next = upgrade.NextRow(0);
                    next.cost = new List<ItemAmount> { new ItemAmount(wood, 12), new ItemAmount(stone, 5) };
                    next.requiresReachTier = 1;
                    next.researchPrereq = "";
                    eligible = () => state.CanBuy(upgrade);
                    buy = () => state.Buy(upgrade);
                    purchased = loaded => loaded.Level(upgrade) == 1;
                }

                var warehouse = state.DebugPlace(isolated.StructureOfKind(StructureKind.Warehouse), 0);
                var depot = scope == "disconnected" ? null :
                    state.DebugPlace(isolated.StructureOfKind(StructureKind.CargoDepot), 1);
                var storage = state.StorageFor(warehouse);
                storage.slots[0].resourceId = wood.id;
                storage.slots[0].count = 8;
                storage.slots[1].resourceId = stone.id;
                storage.slots[1].count = scope == "short" ? 4 : 5;
                state.backpack.Clear();
                state.backpack.Set(wood.id, 4);
                if (scope == "building-storage") state.DebugSetBuilderJob(warehouse, 60);
                if (scope == "building-depot") state.DebugSetBuilderJob(depot, 60);
                if (scope == "remote") state.DebugTeleport(state.universe.Get(isolated.planets[1].id));
                bool connected = scope == "connected";
                Assert.AreEqual(connected, eligible(), "Eligibility must use the same resource scope as spending.");
                Assert.AreEqual(connected, buy());
                Assert.AreEqual(connected ? 0 : 4, state.Count(wood), "Rejected purchases must not partially spend cargo.");
                Assert.AreEqual(connected ? 0 : 8, WarehouseOps.Held(storage, wood.id));
                Assert.AreEqual(connected ? 0 : scope == "short" ? 4 : 5, WarehouseOps.Held(storage, stone.id));
                Assert.AreEqual(connected, purchased(state));
                Assert.IsTrue(state.Save());
                var loaded = new GameState(isolated, true, clock);
                Assert.AreEqual(connected, purchased(loaded), "Stable progression IDs survive save/load.");
                var loadedWarehouse = loaded.structures.First(s => s.planetId == warehouse.planetId && s.slot == warehouse.slot);
                Assert.AreEqual(connected ? 0 : 8, WarehouseOps.Held(loaded.StorageFor(loadedWarehouse), wood.id));
            }
            finally
            {
                foreach (var copy in copies) UnityEngine.Object.DestroyImmediate(copy);
                UnityEngine.Object.DestroyImmediate(isolated);
            }
        }

        [Test]
        public void ResearchPurchase_SpendsCostAndSurvivesSaveLoad()
        {
            var state = new GameState(db, false);
            if (!db.UsesImportedEconomy || db.research == null || db.research.Length == 0)
                Assert.Ignore("Imported research catalog is unavailable");
            ReachHighestAuthoredTier(state);
            PlaceResearchLab(state);
            GiveMaterials(state);

            var node = db.research.FirstOrDefault(candidate => candidate != null && candidate.data != null &&
                candidate.data.tier <= state.PlayerLevel && candidate.data.labLevel <= 20 &&
                (candidate.data.prereqs == null || candidate.data.prereqs.Length == 0));
            Assert.IsNotNull(node, "catalog should contain a reachable root research node");
            Assert.IsTrue(state.CanResearch(node), state.ResearchLockReason(node));
            var firstCost = node.data.cost.FirstOrDefault(item => item.amount > 0);
            int before = firstCost.amount > 0 ? state.Count(db.ResolveResource(firstCost.resourceId)) : 0;

            Assert.IsTrue(state.Research(node));
            Assert.IsTrue(state.IsResearched(node.data.id));
            if (firstCost.amount > 0)
                Assert.AreEqual(before - firstCost.amount, state.Count(db.ResolveResource(firstCost.resourceId)));

            Assert.IsTrue(state.Save());
            var loaded = new GameState(db, true);
            Assert.IsTrue(loaded.IsResearched(node.data.id));
            Assert.IsTrue(loaded.LoadReport.warnings.Count == 0, string.Join("; ", loaded.LoadReport.warnings));
            loaded.ResetProgress();
            Assert.IsFalse(loaded.IsResearched(node.data.id), "reset must clear purchased research");
        }

        [Test]
        public void ResearchLab_IsBuildableAtStartingXpLevelAndEnablesLabGatedResearch()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported structure catalog is unavailable");
            var state = new GameState(db, false, clock);
            var labDef = db.StructureOfKind(StructureKind.ResearchLab);
            Assert.IsNotNull(labDef);
            Assert.IsTrue(labDef.Implemented, "research lab is now a supported structure behavior");
            Assert.AreEqual(1, state.PlayerLevel);
            Assert.AreEqual(0, state.ReachTier, "no higher-tier planet visit should be needed");
            GiveMaterials(state);
            Assert.IsNull(state.BuildLockReason(labDef));

            var research = db.research.FirstOrDefault(node => node != null && node.data != null &&
                node.data.tier <= state.PlayerLevel && node.data.labLevel == 1 &&
                (node.data.prereqs == null || node.data.prereqs.Length == 0));
            Assert.IsNotNull(research, "catalog contains tier-one research gated by a level-one lab");
            StringAssert.StartsWith("Requires Research Lab level 1", state.ResearchLockReason(research));

            Assert.IsTrue(state.Place(labDef, 0));
            var placed = state.StructureAt(state.planet, 0);
            int seconds = state.StructureJobSeconds(labDef, 1);
            if (seconds > 0) clock.Advance(seconds);
            state.Tick();
            Assert.IsTrue(state.IsOperational(placed));
            Assert.AreSame(placed, state.StructureOfKind(state.planet, StructureKind.ResearchLab));
            Assert.IsNull(state.ResearchLockReason(research), state.ResearchLockReason(research));
        }

        [Test]
        public void RadarTower_ExtendsSurveyRangeAfterConstructionCompletes()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported structure catalog is unavailable");
            var state = new GameState(db, false, clock);
            var towerDef = db.StructureOfKind(StructureKind.RadarTower);
            Assert.IsNotNull(towerDef);
            Assert.IsTrue(towerDef.Implemented, "radar tower is a supported structure behavior");

            var tierOneWorld = state.universe.FindGenerated(planet => planet.Tier >= 1, 1, 0, 10000);
            Assert.IsNotNull(tierOneWorld);
            state.DebugTeleport(tierOneWorld);
            GiveMaterials(state);
            PlaceResearchLab(state);

            var surveyBasics = db.research.FirstOrDefault(node => node != null && node.data != null && node.data.id == "survey_basics");
            Assert.IsNotNull(surveyBasics);
            Assert.IsTrue(state.Research(surveyBasics), state.ResearchLockReason(surveyBasics));
            Assert.IsNull(state.BuildLockReason(towerDef), state.BuildLockReason(towerDef));
            Assert.IsTrue(state.Place(towerDef, 1));
            var tower = state.StructureAt(state.planet, 1);
            Assert.AreEqual(0, state.RadarRangeBonus(state.planet), "construction should not grant scan range early");

            clock.Advance(state.StructureJobSeconds(towerDef, 1));
            state.Tick();
            Assert.IsTrue(state.IsOperational(tower));
            Assert.AreEqual(towerDef.EffectAt(1), state.RadarRangeBonus(state.planet), 0.001f);
        }

        [Test]
        public void DedicatedHarvestGamesUseTheirMatchingToolsAndStructures()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported progression catalog is unavailable");
            var state = new GameState(db, false, clock);
            var ladder = db.toolLadders.FirstOrDefault(candidate => candidate != null && candidate.data != null && candidate.data.id == "siphon");
            var rig = db.StructureOfKind(StructureKind.SiphonRig);
            Assert.IsNotNull(ladder);
            Assert.IsNotNull(rig);
            Assert.AreEqual(MinigameKind.Siphon, HarvestMapping.ToMinigame(Frontier.Universe.HarvestMethod.Siphon));
            Assert.AreEqual(Frontier.Universe.HarvestMethod.Siphon, HarvestMapping.FromMinigame(MinigameKind.Siphon));
            Assert.AreEqual(MinigameKind.Siphon, StructureKind.SiphonRig.Game());
            Assert.IsTrue(StructureKind.SiphonRig.IsGather());
            Assert.IsTrue(rig.Implemented);
            Assert.AreEqual("Hand Pump", state.ToolName(MinigameKind.Siphon));
            Assert.AreNotEqual("This tool's harvest game is not implemented", state.ToolTierLockReason(ladder));

            AssertHarvestTool(state, Frontier.Universe.HarvestMethod.Excavate, MinigameKind.Excavate, "scanner", StructureKind.Excavator);
            AssertHarvestTool(state, Frontier.Universe.HarvestMethod.Salvage, MinigameKind.Salvage, "claw", StructureKind.SalvageYard);
        }

        void AssertHarvestTool(GameState state, Frontier.Universe.HarvestMethod method, MinigameKind game, string ladderId, StructureKind structureKind)
        {
            var ladder = db.toolLadders.FirstOrDefault(candidate => candidate != null && candidate.data != null && candidate.data.id == ladderId);
            var structure = db.StructureOfKind(structureKind);
            Assert.IsNotNull(ladder, ladderId + " tool ladder");
            Assert.IsNotNull(structure, structureKind + " structure");
            Assert.AreEqual(game, HarvestMapping.ToMinigame(method));
            Assert.AreEqual(method, HarvestMapping.FromMinigame(game));
            Assert.AreEqual(game, structureKind.Game());
            Assert.IsTrue(structureKind.IsGather());
            Assert.IsTrue(structure.Implemented);
            Assert.AreNotEqual("This tool's harvest game is not implemented", state.ToolTierLockReason(ladder), ladderId);
            Assert.AreEqual(ladder.data.tiers.First(row => row.tier == 1).displayName, state.ToolName(game));
        }

        [Test]
        public void SupportedToolTierPurchase_ChangesHarvestYieldAndSurvivesSaveLoad()
        {
            var state = new GameState(db, false);
            if (!db.UsesImportedEconomy || db.toolLadders == null || db.toolLadders.Length == 0)
                Assert.Ignore("Imported tool ladders are unavailable");
            ReachHighestAuthoredTier(state);
            PlaceResearchLab(state);
            GiveMaterials(state);

            ToolLadderSO selected = null;
            foreach (var ladder in db.toolLadders)
            {
                if (ladder == null || ladder.data == null ||
                    (ladder.data.kind != Economy.ToolKind.Axe && ladder.data.kind != Economy.ToolKind.Pick &&
                     ladder.data.kind != Economy.ToolKind.Drill && ladder.data.kind != Economy.ToolKind.Siphon &&
                     ladder.data.kind != Economy.ToolKind.Scanner && ladder.data.kind != Economy.ToolKind.Claw) ||
                    ladder.data.games == null || ladder.data.games.Length == 0) continue;
                var next = ladder.data.tiers.FirstOrDefault(row => row != null && row.tier == state.ToolTier(ladder) + 1);
                if (next == null || next.requiresReachTier > state.PlayerLevel ||
                    next.requiresLabLevel > 20 || !string.IsNullOrEmpty(next.researchPrereq)) continue;
                if (state.ToolTierLockReason(ladder) == null) { selected = ladder; break; }
            }
            Assert.IsNotNull(selected, "catalog should expose an affordable first supported tool tier");
            var purchased = selected.data.tiers.First(row => row.tier == 2);
            Assert.IsTrue(state.BuyToolTier(selected));
            Assert.AreEqual(2, state.ToolTier(selected));

            MinigameKind game = HarvestMapping.ToMinigame(selected.data.games[0]);
            Assert.AreEqual(purchased.yieldMult, state.CatalogToolYieldMultiplier(game), 0.001f);
            Assert.AreEqual(1, state.ToolLevel(game), "the purchased tier should be reflected in the active tool level");

            Assert.IsTrue(state.Save());
            var loaded = new GameState(db, true);
            var loadedLadder = db.toolLadders.First(ladder => ladder.data.id == selected.data.id);
            Assert.AreEqual(2, loaded.ToolTier(loadedLadder));
            Assert.AreEqual(purchased.yieldMult, loaded.CatalogToolYieldMultiplier(game), 0.001f);
            loaded.ResetProgress();
            Assert.AreEqual(1, loaded.ToolTier(loadedLadder), "reset must restore starting tool tiers");
        }

        [Test]
        public void MiningPurchaseUpgradesBothGamesAndKeepsTheBestLegacyTier()
        {
            var state = new GameState(db, false);
            var pick = db.toolLadders.First(l => l.data.kind == Economy.ToolKind.Pick);
            var drill = db.toolLadders.First(l => l.data.kind == Economy.ToolKind.Drill);
            var legacyDrill = db.upgrades.First(u => u.effect == UpgradeEffect.ToolPower && u.tool == MinigameKind.Drill);
            state.DebugSetUpgrade(legacyDrill, 2);
            Assert.AreEqual(state.ToolMultiplier(MinigameKind.Mine), state.ToolMultiplier(MinigameKind.Drill));
            Assert.AreEqual(2, state.ToolLevel(MinigameKind.Mine));
            ReachHighestAuthoredTier(state);
            PlaceResearchLab(state);
            GiveMaterials(state);
            Assert.AreSame(pick, state.ToolUpgradeLadder(drill));
            var unknown = UnityEngine.Object.Instantiate(drill);
            try
            {
                Assert.AreSame(unknown, state.ToolUpgradeLadder(unknown), "an unregistered tool must not alias the real Mining purchase");
                Assert.IsFalse(state.BuyToolTier(unknown));
            }
            finally { UnityEngine.Object.DestroyImmediate(unknown); }
            Assert.AreEqual(state.ToolTierLockReason(pick), state.ToolTierLockReason(drill));
            var costs = pick.data.tiers.First(t => t.tier == 2).cost;
            int before = state.Count(db.ResolveResource(costs[0].resourceId));
            Assert.IsTrue(state.BuyToolTier(drill), state.ToolTierLockReason(drill));
            Assert.AreEqual(before - costs[0].amount, state.Count(db.ResolveResource(costs[0].resourceId)));
            Assert.AreEqual(2, state.ToolTier(pick));
            Assert.AreEqual(2, state.ToolTier(drill));
            Assert.AreEqual(state.CatalogToolYieldMultiplier(MinigameKind.Mine), state.CatalogToolYieldMultiplier(MinigameKind.Drill));
            Assert.IsTrue(state.Save());
            var save = SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile)).Normalize();
            save.player.toolTiers.RemoveAll(t => t.id == drill.data.id);
            save.player.toolTiers.Add(new IdLevel { id = drill.data.id, level = 4 });
            File.WriteAllText(sandbox.SaveFile, JsonUtility.ToJson(save));
            var loaded = new GameState(db, true);
            Assert.AreEqual(4, loaded.ToolTier(pick));
            Assert.AreEqual(4, loaded.ToolTier(drill));
            Assert.AreEqual(3, loaded.ToolLevel(MinigameKind.Mine));
            Assert.AreEqual(3, loaded.ToolLevel(MinigameKind.Drill));
            Assert.IsTrue(loaded.Save());
            Assert.AreEqual(4, new GameState(db, true).ToolTier(pick));
        }

        [Test]
        public void LoadingProgression_DropsUnknownEntriesAndClampsOutOfRangeTiers()
        {
            if (!db.UsesImportedEconomy || db.toolLadders == null || db.toolLadders.Length == 0)
                Assert.Ignore("Imported progression catalog is unavailable");
            var state = new GameState(db, false);
            Assert.IsTrue(state.Save());
            var save = SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile)).Normalize();
            var ladder = db.toolLadders.First(candidate => candidate != null && candidate.data != null);
            int maxTier = ladder.data.tiers.Max(row => row.tier);
            save.player.researched.Add("missing_research");
            save.player.toolTiers.Add(new IdLevel { id = "missing_ladder", level = 2 });
            save.player.toolTiers.Add(new IdLevel { id = ladder.data.id, level = maxTier + 100 });
            File.WriteAllText(sandbox.SaveFile, JsonUtility.ToJson(save));

            var loaded = new GameState(db, true);
            Assert.IsFalse(loaded.IsResearched("missing_research"));
            Assert.AreEqual(maxTier, loaded.ToolTier(ladder));
            CollectionAssert.Contains(loaded.LoadReport.warnings, "dropped the unknown research node 'missing_research'");
            CollectionAssert.Contains(loaded.LoadReport.warnings, "dropped the unknown tool ladder 'missing_ladder'");
            CollectionAssert.Contains(loaded.LoadReport.warnings, "clamped the saved tier for tool '" + ladder.data.id + "'");
        }

        [Test]
        public void ProgressionSurvivesLegacyContentFallbackAndCanBeRestored()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported progression catalog is unavailable");
            var state = new GameState(db, false);
            ReachHighestAuthoredTier(state);
            PlaceResearchLab(state);
            GiveMaterials(state);
            var root = db.research.FirstOrDefault(node => node != null && node.data != null &&
                node.data.tier <= state.PlayerLevel && node.data.labLevel <= 20 &&
                (node.data.prereqs == null || node.data.prereqs.Length == 0));
            Assert.IsNotNull(root);
            Assert.IsTrue(state.Research(root));
            ToolLadderSO ladder = null;
            foreach (var candidate in db.toolLadders)
            {
                if (candidate == null || candidate.data == null ||
                    (candidate.data.kind != Economy.ToolKind.Axe && candidate.data.kind != Economy.ToolKind.Pick &&
                     candidate.data.kind != Economy.ToolKind.Drill)) continue;
                var tier = candidate.data.tiers.FirstOrDefault(row => row != null && row.tier == 2);
                if (tier != null && tier.requiresReachTier <= state.PlayerLevel &&
                    tier.requiresLabLevel <= 20 && string.IsNullOrEmpty(tier.researchPrereq))
                {
                    ladder = candidate;
                    break;
                }
            }
            Assert.IsNotNull(ladder, "catalog should contain a supported tier-two tool without additional research");
            Assert.IsTrue(state.BuyToolTier(ladder));
            var legacyDb = GameDatabase.LoadLegacy();
            foreach (var resource in db.resources)
                if (state.IsTankFuel(resource.id) && legacyDb.FindResource(resource.id) == null)
                    state.backpack.Set(resource.id, 0);
            Assert.IsTrue(state.Save());

            var legacy = new GameState(legacyDb, true);
            Assert.IsTrue(legacy.IsResearched(root.data.id));
            Assert.AreEqual(2, legacy.ToolTier(ladder));
            Assert.IsTrue(legacy.Save());

            var restored = new GameState(db, true);
            Assert.IsTrue(restored.IsResearched(root.data.id));
            Assert.AreEqual(2, restored.ToolTier(ladder));
        }

        static void ReachHighestAuthoredTier(GameState state)
        {
            PlanetSO highest = null;
            foreach (var planet in state.db.planets)
                if (planet != null && (highest == null || planet.tier > highest.tier)) highest = planet;
            Assert.IsNotNull(highest, "database should contain authored planets");
            var destination = state.universe.Get(highest.id);
            Assert.IsNotNull(destination);
            if (destination.Tier < 1)
                destination = state.universe.FindGenerated(planet => planet.Tier >= 1, 1, 0, 10000);
            Assert.IsNotNull(destination, "universe should generate at least one tier-one world");
            state.DebugTeleport(destination);
            state.DebugSetLevel(40);
        }

        static void GiveMaterials(GameState state)
        {
            foreach (var resource in state.db.resources)
                if (resource != null) state.backpack.Set(resource.id, 100000000);
        }

        static void PlaceResearchLab(GameState state)
        {
            var labDef = state.db.StructureOfKind(StructureKind.ResearchLab);
            if (labDef == null) return;
            var lab = state.DebugPlace(labDef, 0);
            if (lab != null) state.DebugSetStructureLevel(lab, 20);
        }
    }
}
