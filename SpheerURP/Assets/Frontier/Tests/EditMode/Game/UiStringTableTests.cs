using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Tests
{
    public class UiStringTableTests
    {
        [Test]
        public void EnglishLocalizationMatchesCatalogAndIsPreloaded()
        {
            Assert.DoesNotThrow(EditorTools.FrontierLocalization.ValidateEnglishCatalog);
        }

        [Test]
        public void ServiceMessageAndQualitySourcesAreCoveredAndGuarded()
        {
            var table = Resources.Load<UiStringTableSO>("Frontier/UiStrings");
            int count = 0;
            foreach (var entry in Services.ServiceMessage.EnglishEntries)
            {
                Assert.AreEqual(entry.Value, table.Template(entry.Key), entry.Key);
                count++;
            }
            Assert.Greater(count, 80);
            foreach (QualityPreset preset in Enum.GetValues(typeof(QualityPreset)))
                Assert.IsNotEmpty(table.Template("settings.quality-preset." + preset.ToString().ToLowerInvariant()));
            foreach (PerformanceTier tier in Enum.GetValues(typeof(PerformanceTier)))
                Assert.IsNotEmpty(table.Template("settings.quality-tier." + tier.ToString().ToLowerInvariant()));
            int index = Array.FindIndex(table.entries, entry => entry.key == "service.deletion.partial");
            var original = table.entries[index];
            try
            {
                table.entries[index].text = "Outdated warning {0}";
                table.Validate();
                Assert.Throws<UnityEditor.Build.BuildFailedException>(EditorTools.FrontierLocalization.ValidateEnglishCatalog);
            }
            finally
            {
                table.entries[index] = original;
                table.Validate();
            }
        }

        [Test]
        public void AuthoredContentKeysAreUniqueCoveredAndKeepEnglishAuthoringLive()
        {
            var db = GameDatabase.Load();
            var objectives = Resources.Load<ObjectiveCatalogSO>("Frontier/ObjectiveCatalog");
            var table = Resources.Load<UiStringTableSO>("Frontier/UiStrings");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in ContentText.EnglishEntries(db, objectives))
            {
                Assert.IsTrue(keys.Add(entry.Key), "Duplicate authored-content key: " + entry.Key);
                if (!string.IsNullOrWhiteSpace(entry.Value))
                    Assert.DoesNotThrow(() => table.Template(entry.Key), entry.Key);
            }
            Assert.Greater(keys.Count, 600);
            var resource = ScriptableObject.CreateInstance<ResourceSO>();
            try
            {
                resource.id = "authoring-test";
                resource.displayName = "Original";
                resource.description = "Original description";
                Assert.AreEqual("Original", resource.LocalizedName);
                resource.displayName = "Edited in inspector";
                resource.description = "Edited description";
                Assert.AreEqual(resource.displayName, resource.LocalizedName);
                Assert.AreEqual(resource.description, resource.LocalizedDescription);
                Assert.AreEqual("authoring-test", resource.id);
            }
            finally { Object.DestroyImmediate(resource); }
        }

        [Test]
        public void LocalizationBuildGuardRejectsStaleEnglish()
        {
            var source = Resources.Load<UiStringTableSO>("Frontier/UiStrings");
            var original = source.entries[0];
            try
            {
                source.entries[0].text = original.text + " changed";
                Assert.Throws<UnityEditor.Build.BuildFailedException>(EditorTools.FrontierLocalization.ValidateEnglishCatalog);
            }
            finally
            {
                source.entries[0] = original;
                source.Validate();
            }
            Assert.DoesNotThrow(EditorTools.FrontierLocalization.ValidateEnglishCatalog);
        }

        [Test]
        public void AuthoredTablePreservesShellCopyAndFormatting()
        {
            var table = Resources.Load<UiStringTableSO>("Frontier/UiStrings");
            Assert.IsNotNull(table);
            table.Validate();
            Assert.AreEqual("en", table.locale);
            Assert.AreEqual("LANGUAGE", UiText.Get("settings.language"));
            StringAssert.Contains("only language", UiText.Get("settings.language.only-english"));
            StringAssert.Contains("no room", UiText.Get("harvest.bonus.no-room"));
            Assert.AreEqual("COMMAND", UiText.Get("nav.command"));
            Assert.AreEqual("COLLECT RESOURCES", UiText.Get("harvest.collect"));
            Assert.AreEqual("Run abandoned. No resources collected.", UiText.Get("harvest.abandoned"));
            Assert.AreEqual("25 XP", UiText.Get("common.xp", 25));
            Assert.AreEqual("JOB NOTIFICATIONS: OFF", UiText.Get("notifications.off"));
            Assert.AreEqual("Crew job complete", UiText.Get("notifications.job.title"));
            Assert.AreEqual("FELL THE TREE", UiText.Get("harvest.node.title.tree"));
            Assert.AreEqual("Axe", UiText.Get("harvest.tool.axe"));
            Assert.AreEqual("HOLD", UiText.Get("harvest.drill.hold-control"));
            Assert.AreEqual("PRESSURE LOCK", UiText.Get("harvest.drill.locked"));
            Assert.AreEqual("PULSES 2/4   ·   Clean pressure lock",
                UiText.Get("harvest.siphon.status", 2, 4, UiText.Get("harvest.siphon.clean")));
            Assert.AreEqual("SEAM 2/3  ·  14 SCANS", UiText.Get("harvest.excavate.progress", 2, 3, 14));
            Assert.AreEqual("STRONG ECHO  ·  SEAM 1/3  ·  16 SCANS",
                UiText.Get("harvest.excavate.echo-status", UiText.Get("harvest.excavate.strong-echo"), 1, 3, 16));
            Assert.AreEqual("CASTS 2/8  ·  FRAGMENT LOCKED  ·  1/6  ·  6 CASTS",
                UiText.Get("harvest.salvage.status", 2, 8, UiText.Get("harvest.salvage.locked", 1, 6, 6)));
            Assert.AreEqual("ASTEROID FIELD", UiText.Get("harvest.travel.title"));
            Assert.AreEqual("Orbs 3     Hits 2", UiText.Get("harvest.travel.status", 3, 2));
            Assert.AreEqual("LV 3", UiText.Get("hud.player-level", 3));
            Assert.AreEqual("TRAVEL GAME", UiText.Get("travel.game.title"));
            Assert.AreEqual("PLAYING: LEGACY TAP GAME", UiText.Get("travel.debug.legacy"));
            Assert.AreEqual("PLAYING: HYPERSPACE RUN", UiText.Get("travel.debug.hyperspace"));
            Assert.AreEqual("PERFORMANCE OVERLAY: ON", UiText.Get("travel.debug.overlay.on"));
            Assert.AreEqual("PERFORMANCE OVERLAY: OFF", UiText.Get("travel.debug.overlay.off"));
            Assert.AreEqual("DEBUG TOOLS - FOR TESTING ONLY", UiText.Get("debug.header"));
            Assert.AreEqual("GIVE ONE OF EACH x100", UiText.Get("debug.give-each", 100));
            Assert.AreEqual("Terra - Lush, small, tier 12", UiText.Get("debug.world-found", "Terra", "Lush", "small", 12));
            Assert.AreEqual("12+", UiText.Get("debug.tier-choice", 12));
            Assert.AreEqual("GAS GIANT", UiText.Get("debug.biome.gas-giant"));
            Assert.AreEqual("Now on: Terra  home-1", UiText.Get("debug.now-on", "Terra", "home-1"));
            Assert.AreEqual("QUALITY: LOW", UiText.Get("settings.quality", "LOW"));
            Assert.AreEqual("Refinery  LV3", UiText.Get("world.label.level", "Refinery", 3));
            Assert.AreEqual("Version 1.0", UiText.Get("about.version", "1.0"));
            Assert.AreEqual("Arrived at Sylva", UiText.Get("travel.arrived", "Sylva"));
            Assert.AreEqual("Need 3 more energy · craft fuel", UiText.Get("route.fuel-short", 3));
            Assert.AreEqual("Build on Sylva  -  2 / 12 free slots  ·  CREWS 1/2 busy. Tap a structure on the planet to use it.",
                UiText.Get("shop.summary", "Sylva", 2, 12, 1, 2));
            Assert.AreEqual("HULL 85   SHIELD 1/2", UiText.Get("hyperspace.status", 85, 1, 2));
            Assert.AreEqual("9 kills  ·  best combo x3\n2 hits  ·  1 revives",
                UiText.Get("hyperspace.result.summary", 9, 3, 2, 1));
            Assert.AreEqual("+5 fuel refunded  ·  +12 XP", UiText.Get("hyperspace.result.rewards", 5, 12));
            Assert.AreEqual("1 / 6", UiText.Get("guide.progress", 1, 6));
            StringAssert.Contains("planets cannot be attacked", UiText.Get("guide.defend.help"));
            Assert.AreEqual("2 / 9 slots used  -  drag to rearrange", UiText.Get("pack.capacity.rearrange", 2, 9));
            Assert.AreEqual("In this slot: 4 / 20", UiText.Get("pack.slot-count", 4, 20));
            Assert.AreEqual("8 waiting on Sylva", UiText.Get("pack.salvage.waiting", 8, "Sylva"));
            Assert.AreEqual("Sylva  -  LV 2  -  3 / 12 storage slots", UiText.Get("storage.summary", "Sylva", 2, 3, 12));
            Assert.AreEqual("PULL 8 · 2 ENERGY", UiText.Get("relay.pull", 8, 2));
            Assert.AreEqual("Stored 8 items.", UiText.Get("storage.quick-stored", 8));
            Assert.AreEqual("Withdrew 8 items.", UiText.Get("storage.quick-withdrew", 8));
            Assert.AreEqual("2 / 26 collected  |  1 ready", UiText.Get("goals.summary", 2, 26, 1));
            Assert.AreEqual("Complete  |  5 Nova Shards", UiText.Get("goals.reward-summary", UiText.Get("goals.complete"), 5));
            Assert.AreEqual("TIER 2/7", UiText.Get("tools.tier", 2, 7));
            Assert.AreEqual($"NEXT  ·  Steel Axe  ·  YIELD x{1.5f:0.0}", UiText.Get("tools.next", "Steel Axe", 1.5f));
            Assert.AreEqual($"{1.3f:0.0}x", UiText.Get("settings.drag-value", 1.3f));
            Assert.AreEqual("10 fuel slots · +20% fuel stack", UiText.Get("effect.fuel-storage-stack", 10, 20));
            Assert.AreEqual("system beam · 80% energy", UiText.Get("effect.beam.system", 0, 80));
            Assert.AreEqual("-10% fuel · +10% jump range", UiText.Get("effect.warp-gate", 10));
            Assert.AreEqual("Refinery  LV\u00a02", UiText.Get("structure.header", "Refinery", 2));
            Assert.AreEqual("BATCH CAPACITY  ·  1 CRAFT PER TAP", UiText.Get("craft.capacity.one", 1));
            Assert.AreEqual("BATCH CAPACITY  ·  3 CRAFTS PER TAP", UiText.Get("craft.capacity.many", 3));
            Assert.AreEqual("WAVE 3   4 / 8", UiText.Get("wave.progress", 3, 4, 8));
            Assert.AreEqual($"Tool: Steel Axe  (LV 2, damage x{1.5f:0.0})", UiText.Get("gather.tool", "Steel Axe", 2, 1.5f));
            Assert.AreEqual("1h 02m", UiText.Get("duration.hours-minutes", 1, 2));
            Assert.AreEqual("2m 03s", UiText.Get("duration.minutes-seconds", 2, 3));
            Assert.AreEqual("Jump range from this world: 80 ly  ->  96 ly", UiText.Get("structure.warp.range", 80d, 96d));
            Assert.AreEqual("BOSS Titan x1", UiText.Get("wave.ranked-alien", "Titan", 1, UiText.Get("wave.rank.boss")));
            Assert.AreEqual("+2 crews · -45% clearance cost", UiText.Get("effect.builders-discount", 2, 45));
            Assert.AreEqual("1 / 2 crews busy  ·  50 Nova Shards", UiText.Get("builders.summary", 1, 2, 50));
            Assert.AreEqual("CONFIRM  ·  3 SHARDS", UiText.Get("builders.confirm-paid", 3));
            Assert.AreEqual("50 Nova Shards available", UiText.Get("hyperspace.revive.balance", 50));
            Assert.AreEqual("REVIVE  ·  10 SHARDS", UiText.Get("hyperspace.revive.shards", 10));
            Assert.AreEqual("CONFIRM REVIVE  ·  10 SHARDS", UiText.Get("hyperspace.revive.shards-confirm", 10));
            Assert.AreEqual("4 / 20 energy aboard · 12s estimate", UiText.Get("map.price.manual", 4, 20, 12));
            Assert.AreEqual("4 / 20 energy aboard · 12s", UiText.Get("map.price.autopilot", 4, 20, 12));
            Assert.AreEqual($"12 building slots · danger {1.5f:0.0} · BASE", UiText.Get("map.world.summary-base", 12, 1.5f));
            Assert.AreEqual($"Mine · richness x{1.25f:0.00} · yield 3", UiText.Get("map.deposit", "Mine", 1.25f, 3));
            Assert.AreEqual("Set Glacia as the next stop toward Nyx", UiText.Get("map.next-stop.tooltip", "Glacia", "Nyx"));
            Assert.AreEqual("RESOURCES  2/34  ·  RECIPES  1/12", UiText.Get("atlas.summary.resources-recipes", 2, 34, 1, 12));
            Assert.AreEqual("Craft  ·  TIER 3  ·  Refinery LV 2  ·  RESEARCH: Alloys",
                UiText.Get("atlas.recipe.gate-research", "Craft", 3, "Refinery", 2, "Alloys"));
            Assert.AreEqual($"Sylva  ·  {12.5:0.#} LY FROM HOME",
                UiText.Get("atlas.resource.sample", "Sylva", 12.5));
            Assert.AreEqual($"FIRST WAVE 10  ·  THREAT {3.5f:0.0}", UiText.Get("atlas.enemy.summary", 10, 3.5f));
            Assert.AreEqual("20% ARMOR", UiText.Get("atlas.enemy.armor", 20));
            Assert.AreEqual("Provider: Apple", UiText.Get("recovery.provider", "Apple"));
            Assert.AreEqual("XP 50  ·  2 worlds  ·  revision 3  ·  current world sylva",
                UiText.Get("cloud.snapshot", 50, 2, 3, "sylva"));
            Assert.AreEqual("The cloud save uses schema 3; this build can compare only schema 2.",
                UiText.Get("cloud.schema", 3, 2));
            Assert.AreEqual("Cloud restore failed: Offline", UiText.Get("cloud.restore-failed", "Offline"));
            StringAssert.Contains("Tap again to confirm.", UiText.Get("cloud.restore.warning"));
            StringAssert.Contains("retained for undo", UiText.Get("cloud.restore.warning"));
            Assert.AreEqual("YOUR FRONTIER: LANDFALL", UiText.Get("ranks.era", "LANDFALL"));
            Assert.AreEqual("NEXT: INTERSTELLAR", UiText.Get("ranks.era.next", "INTERSTELLAR"));
            Assert.AreEqual("#3  Explorer", UiText.Get("ranks.entry", 3, "Explorer"));
            Assert.AreEqual("Online rankings are not configured in this build.", UiText.Get("ranks.error.not-configured"));
            Assert.AreEqual("Enable Online Services in Privacy Settings to load rankings.", UiText.Get("ranks.error.consent"));
            Assert.AreEqual("Leaderboard request rejected: Invalid board", UiText.Get("ranks.error.request", "Invalid board"));
            Assert.AreEqual("Leaderboard connection failed: Offline", UiText.Get("ranks.error.failed", "Offline"));
            StringAssert.Contains("Local progress", UiText.Get("deletion.help"));
            StringAssert.Contains("cannot be undone", UiText.Get("deletion.warning"));
            StringAssert.Contains("cloud save remains deleted", UiText.Get("deletion.warning"));
            Assert.AreEqual("UPGRADING TO LV 3  ·  1m 02s", UiText.Get("crew.status.upgrade", 3,
                UiText.Get("duration.minutes-seconds", 1, 2)));
            Assert.AreEqual("Crew recruited for this session, but saving failed. Keep the game open while saving retries. Disk full",
                UiText.Get("crew.purchase.pending-save", "Disk full"));
            Assert.AreEqual("Revived for this session, but saving failed. Keep the game open while saving retries. Disk full",
                UiText.Get("revive.pending-save", "Disk full"));
            Assert.AreEqual("Requires Research Lab level 3 on this world", UiText.Get("gate.lab-world", 3));
            Assert.AreEqual("Requires research: field engineering", UiText.Get("gate.research", "field engineering"));
            Assert.AreEqual("+5 Nova Shards", UiText.Get("claim.shards", 5));
            StringAssert.Contains("Disk full", UiText.Get("claim.pending-save", "Disk full"));
            Assert.AreEqual("The previous local save was restored. The save it replaced is retained on this device for undo.",
                UiText.Get("restore.done", UiText.Get("restore.source.local")));
            Assert.AreEqual("This save uses schema 3; this build can restore only schema 2.", UiText.Get("restore.schema", 3, 2));
            StringAssert.Contains("The previous save is still retained.", UiText.Get("restore.pending-save", "cloud", "Disk full"));
            Assert.AreEqual("ASCENSION 2", UiText.Get("ascension.title", 2));
            Assert.AreEqual("This save is read-only.", UiText.Get("ascension.lock.read-only"));
            Assert.AreEqual("Complete stage 3 of a megaproject in this universe.", UiText.Get("ascension.lock.project", 3));
            Assert.AreEqual("Next Legacy harvest yield: x1.05. The authored bonus caps at +50%; one-time rewards cannot be farmed again.",
                UiText.Get("ascension.next-yield", 1.05f, 50));
            Assert.AreEqual("Ascension completed, but the updated local save needs a retry. Keep the game open. Disk full",
                UiText.Get("ascension.save-retry", "Disk full"));
            Assert.AreEqual("Ascension 2 complete. Your previous universe is retained in the local recovery copy.",
                UiText.Get("ascension.completed", 2));
            foreach (AlienRank rank in Enum.GetValues(typeof(AlienRank)))
                if (rank != AlienRank.Standard)
                    Assert.DoesNotThrow(() => UiText.Get("wave.rank." + rank.ToString().ToLowerInvariant()));
            foreach (var category in Enum.GetValues(typeof(Frontier.Economy.ResourceCategory)))
                Assert.DoesNotThrow(() => UiText.Get("resource.category." + category.ToString().ToLowerInvariant()));
            Assert.AreEqual($"{1234:N0} PTS   9 KILLS   x3   {0.5f:P0}",
                UiText.Get("hyperspace.score", 1234, 9, 3, 0.5f));
            foreach (var entry in table.entries)
                Assert.DoesNotThrow(() => table.Get(entry.key, "one", "two", "three", "four", "five"), entry.key);
        }

        [Test]
        public void InvalidCatalogKeysAndFormatsFailExplicitly()
        {
            var table = ScriptableObject.CreateInstance<UiStringTableSO>();
            try
            {
                table.entries = new[] { new UiStringTableSO.Entry { key = "a", text = "Value {0}" } };
                Assert.Throws<FormatException>(() => table.Get("a"));
                Assert.Throws<KeyNotFoundException>(() => table.Get("missing"));
                table.entries = new[]
                {
                    new UiStringTableSO.Entry { key = "a", text = "First" },
                    new UiStringTableSO.Entry { key = "a", text = "Duplicate" }
                };
                Assert.Throws<InvalidOperationException>(() => table.Validate());
                table.entries = new[] { new UiStringTableSO.Entry { key = "", text = "No key" } };
                Assert.Throws<InvalidOperationException>(() => table.Validate());
                table.entries = null;
                Assert.Throws<InvalidOperationException>(() => table.Validate());
            }
            finally { Object.DestroyImmediate(table); }
        }
    }
}
