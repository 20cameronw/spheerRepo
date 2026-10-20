using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Frontier.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Frontier.Tests
{
    /// <summary>
    /// Smoke tests that run the real game code in Play mode: the authored database loads, a game state can be created and saved
    /// in an isolated folder, and <see cref="FrontierBootstrap"/> builds the whole session (camera, planet, backdrop, defense, UI
    /// document, world surface) without errors.
    /// </summary>
    public class SmokeTests
    {
        string saveDir;
        string previousOverride;
        GameState previousState;
        HashSet<GameObject> rootsBefore;
        AscensionRulesSO ascensionTestRules;
        bool previousAscensionEnabled;
        IPrefsStore previousTravelPrefs;

        [SetUp]
        public void SetUp()
        {
            previousOverride = GameState.SaveDirectoryOverride;
            previousState = GameState.I;
            previousTravelPrefs = TravelPrefs.Store;
            TravelPrefs.Store = new MemoryPrefsStore();
            saveDir = Path.Combine(Application.temporaryCachePath, "frontier-playmode-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(saveDir);
            GameState.SaveDirectoryOverride = saveDir;
            FrontierRuntime.Clear();
            rootsBefore = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
        }

        [TearDown]
        public void TearDown()
        {
            // Write anything pending while the folder still exists (the app component flushes again when it is destroyed at the end of the frame).
            if (FrontierRuntime.Current != null && FrontierRuntime.Current.State != null) FrontierRuntime.Current.State.Flush();
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (!rootsBefore.Contains(root)) Object.Destroy(root);
            FrontierRuntime.Clear();
            GameState.SaveDirectoryOverride = previousOverride;
            GameState.I = previousState;
            TravelPrefs.Store = previousTravelPrefs;
            if (ascensionTestRules != null)
            {
                ascensionTestRules.enableAscension = previousAscensionEnabled;
                ascensionTestRules = null;
            }
            try { Directory.Delete(saveDir, true); } catch (IOException) { }
        }

        static int ActiveRippleCount()
        {
            int count = 0;
            foreach (var ripple in Resources.FindObjectsOfTypeAll<RippleAnim>())
                if (ripple != null && ripple.gameObject.activeInHierarchy) count++;
            return count;
        }

        [Test]
        public void AuthoredDatabase_LoadsFromResources()
        {
            var db = GameDatabase.Load();
            Assert.IsNotNull(db);
            Assert.IsTrue(db.UsesImportedEconomy, "validated economy should be the live default");
            Assert.AreSame(Resources.Load<GameDatabase>("Frontier/EconomyDatabase"), db.ContentSource);
            Assert.IsNotNull(db.startPlanet);
            Assert.Greater(db.planets.Length, 0);
            Assert.Greater(db.resources.Length, 0);
            Assert.IsNotNull(ModelLibrary.Instance, "the model library asset is missing from Resources/Frontier");
        }

        [UnityTest]
        public IEnumerator RankedAliensUseArmorHealthArcsAndOnlyDieOnce()
        {
            var state = new GameState(GameDatabase.Load(), false);
            var host = new GameObject("RankedAlienProbe");
            var defense = host.AddComponent<DefenseController>();
            defense.enabled = false;
            defense.Init(null, null, state);
            Assert.IsTrue(defense.StartWave(1));
            try
            {
                foreach (var rank in new[] { AlienRank.Elite, AlienRank.Boss })
                {
                    var data = Array.Find(state.db.enemies, e => e.rank == rank);
                    Assert.IsNotNull(data);
                    var alien = AlienActor.Spawn(data, defense.Current.healthMult, Vector3.right * 20f, defense);
                    float initial = alien.health;
                    var ring = alien.transform.Find(rank + "Health").GetComponent<LineRenderer>();
                    Assert.AreEqual(33, ring.positionCount);
                    alien.Damage(10f);
                    Assert.AreEqual(initial - data.DamageAfterArmor(10f), alien.health, 0.001f);
                    Assert.Less(ring.GetPosition(ring.positionCount - 1).y, ring.GetPosition(0).y);
                    int killed = defense.Killed;
                    alien.Damage(initial * 20f);
                    alien.Damage(initial * 20f);
                    Assert.AreEqual(killed + 1, defense.Killed);
                    Assert.IsFalse(AlienActor.All.Contains(alien));
                }
            }
            finally
            {
                defense.DebugEnd(false);
                Object.Destroy(host);
            }
            yield return null;
        }

        [Test]
        public void WavePreviewAppliesModifiersAndKeepsBossRewardsDeterministic()
        {
            var state = new GameState(GameDatabase.Load(), false);
            var host = new GameObject("WavePreviewProbe");
            try
            {
                var defense = host.AddComponent<DefenseController>();
                defense.Init(null, null, state);
                for (int wave = 1; wave < 10; wave++) state.WaveCleared(state.planet);
                var info = defense.Preview(1);
                var repeat = defense.Preview(1);
                Assert.AreEqual(10, info.waveNumber);
                Assert.IsNotNull(info.modifier);
                Assert.AreEqual(state.db.formula.HealthMult(10) * info.modifier.health, info.healthMult, 0.001f);
                Assert.AreEqual(info.modifier.damage, info.damageMult, 0.001f);
                Assert.AreEqual(info.modifier.speed, info.speedMult, 0.001f);
                Assert.AreEqual(1, info.aliens.FindAll(e => e.rank == AlienRank.Boss).Count);
                var expected = new Dictionary<ResourceSO, float>();
                foreach (var alien in info.aliens)
                    foreach (var item in alien.bounty)
                    {
                        expected.TryGetValue(item.resource, out float value);
                        expected[item.resource] = value + item.amount * info.rewardMult;
                    }
                Assert.AreEqual(expected.Count, info.reward.Count);
                for (int i = 0; i < info.reward.Count; i++)
                {
                    var item = info.reward[i];
                    Assert.AreEqual(Mathf.Max(1, Mathf.RoundToInt(expected[item.resource])), item.amount);
                    Assert.AreSame(item.resource, repeat.reward[i].resource);
                    Assert.AreEqual(item.amount, repeat.reward[i].amount);
                }
            }
            finally { Object.Destroy(host); }
        }

        [Test]
        public void GameState_StartsAndSavesInAnIsolatedFolder()
        {
            var state = new GameState(GameDatabase.Load(), false);
            state.AddXp(10);
            Assert.AreEqual(10, state.xp);
            string file = Path.Combine(saveDir, "frontier_save.json");
            Assert.IsFalse(File.Exists(file), "changes only mark the state dirty; the write is debounced");
            Assert.IsTrue(state.Flush(), "Flush writes the pending changes");
            Assert.IsTrue(File.Exists(file), "the save must go to the override folder");
        }

        [UnityTest]
        public IEnumerator SurfaceLabelsUseSharedWorldToolkitAndRespectVisibility()
        {
            new GameObject("WorldLabelBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            rt.Ui.enabled = false;
            rt.Planet.Show(rt.State.planet, false, rt.State.TerraformerStageOn(rt.State.planet));
            var first = rt.State.DebugPlace(rt.State.db.FindStructure("warehouse"), 0);
            rt.State.DebugPlace(rt.State.db.FindStructure("gunner"), 1);
            rt.Planet.Frozen = true;
            yield return null;
            var anchor = rt.Planet.Spin.Find("Struct_warehouse");
            var label = anchor.Find("Label").GetComponent<UIDocument>();
            var other = rt.Planet.Spin.Find("Struct_gunner/Label").GetComponent<UIDocument>();
            Assert.IsNotNull(label);
            Assert.IsNull(anchor.GetComponentInChildren<TextMesh>());
            Assert.AreEqual(PanelRenderMode.WorldSpace, label.panelSettings.renderMode);
            Assert.AreSame(label.panelSettings, other.panelSettings, "World labels must not create a panel per structure.");
            Assert.AreNotSame(rt.Document.panelSettings, label.panelSettings);
            var previousPreset = PresentationQuality.Preset;
            try
            {
                PresentationQuality.Preset = QualityPreset.Low;
                Assert.AreEqual(1024, label.panelSettings.dynamicAtlasSettings.maxAtlasSize);
                Assert.AreEqual(1024, rt.Document.panelSettings.dynamicAtlasSettings.maxAtlasSize);
            }
            finally { PresentationQuality.Preset = previousPreset; }
            Assert.AreEqual(PickingMode.Ignore, label.rootVisualElement.pickingMode);
            StringAssert.Contains("LV" + first.level, label.rootVisualElement.Q<Label>().text);
            Assert.AreEqual(340f, label.rootVisualElement.resolvedStyle.width, 0.1f);
            rt.Surface.ShowStructureLabels = false;
            yield return null;
            Assert.AreEqual(Visibility.Hidden, label.rootVisualElement.resolvedStyle.visibility);
            rt.Surface.Selected = anchor;
            rt.Camera.GetComponent<CameraFit>().enabled = false;
            rt.Camera.transform.position = anchor.position + (anchor.position - rt.Planet.Spin.position).normalized * 10f;
            rt.Camera.transform.LookAt(rt.Planet.Spin.position);
            yield return null;
            yield return null;
            Assert.IsTrue(rt.Surface.Visible(anchor.TransformPoint(Vector3.up * 0.3f)));
            Assert.AreEqual(Visibility.Visible, label.rootVisualElement.resolvedStyle.visibility);
            rt.Surface.Locked = true;
            yield return null;
            yield return null;
            Assert.AreEqual(Visibility.Hidden, label.rootVisualElement.resolvedStyle.visibility);
            rt.Surface.Locked = false;
            rt.Surface.BuildMode = true;
            yield return null;
            var pads = rt.Planet.Spin.GetComponentsInChildren<UIDocument>();
            Assert.Greater(pads.Length, 2);
            foreach (var document in pads) Assert.AreSame(label.panelSettings, document.panelSettings);
            rt.Surface.BuildMode = false;
            yield return null;
            Assert.AreEqual(2, rt.Planet.Spin.GetComponentsInChildren<UIDocument>().Length);
        }

        [UnityTest]
        public IEnumerator StructureMomentsPreserveUnchangedModelsAndDoNotReplayOnArrival()
        {
            new GameObject("StructureMomentsBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            var state = rt.State;
            var first = state.DebugPlace(state.db.FindStructure("warehouse"), 0);
            Assert.IsNotNull(first);
            var anchor = rt.Planet.Spin.Find("Struct_warehouse");
            Assert.IsNotNull(anchor);
            yield return null;
            Assert.Less(anchor.Find("StructureVisualRoot").localScale.x, 1f);
            state.DebugPlace(state.db.FindStructure("gunner"), 1);
            Assert.AreSame(anchor, rt.Planet.Spin.Find("Struct_warehouse"), "Unrelated builds must not recreate or reanimate existing models.");
            first.record.buildEndUtc = UtcText.Format(DateTime.UtcNow.AddHours(1));
            state.Notify();
            yield return null;
            anchor = rt.Planet.Spin.Find("Struct_warehouse");
            Assert.IsNotNull(anchor.Find("ConstructionScan"));
            rt.Ui.DebugResearch(0);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == state.BuilderJobStatus(first)));
            Assert.IsFalse(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("command.maxed")),
                "A busy low-level structure must not be described as maxed.");
            rt.Ui.DebugReset();
            Assert.IsTrue(rt.Surface.SimulateTap(first));
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("structure.builder")));
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("structure.offline-time")));
            rt.Ui.DebugReset();
            first.record.buildEndUtc = "";
            state.Notify();
            yield return null;
            anchor = rt.Planet.Spin.Find("Struct_warehouse");
            Assert.IsNull(anchor.Find("ConstructionScan"));
            Assert.Greater(anchor.Find("StructureVisualRoot").localScale.x, 1f, "Completion should pulse, not replay the small build-in.");
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(Vector3.one, anchor.Find("StructureVisualRoot").localScale);
            var home = state.planet;
            rt.Planet.Show(home, false, state.TerraformerStageOn(home));
            yield return null;
            Assert.AreEqual(Vector3.one, rt.Planet.Spin.Find("Struct_warehouse/StructureVisualRoot").localScale,
                "Loading or revisiting a world must not replay construction effects.");
        }

        [UnityTest]
        public IEnumerator BackDismissesOnlyTheTopSurfaceAndPreservesHarvestProgress()
        {
            new GameObject("BackNavigationBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            yield return null;
            rt.Ui.DebugMenu("settings");
            var language = rt.Ui.DebugRoot.Q<DropdownField>("settings-language");
            Assert.IsNotNull(language);
            Assert.AreEqual(UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.Locales.Count,
                language.choices.Count);
            Assert.IsFalse(language.enabledSelf, "Only authored English is shipped; do not advertise unshipped translations.");
            Assert.IsNotNull(rt.Ui.DebugRoot.Q<Button>("settings-haptics"));
            var notifications = rt.Ui.DebugRoot.Q<Button>("settings-notifications");
            Assert.IsNotNull(notifications);
            Assert.IsFalse(notifications.enabledSelf, "Native notifications stay unavailable in the editor.");
            Assert.IsTrue(rt.Ui.HandleBack());
            Assert.IsNull(rt.Ui.DebugRoot.Q<Button>("settings-haptics"));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q(className: "fr-modal"));
            Assert.IsTrue(rt.Ui.HandleBack());
            Assert.IsNull(rt.Ui.DebugRoot.Q(className: "fr-modal"));
            Assert.IsFalse(rt.Ui.HandleBack(), "Back at the world must not silently quit.");
            rt.Ui.DebugPack(-1);
            Assert.IsTrue(rt.Ui.HandleBack());
            Assert.IsNull(rt.Ui.DebugRoot.Q(className: "fr-modal"));
            rt.Ui.DebugMap(rt.State.planet, 2f);
            Assert.IsTrue(rt.Ui.HandleBack());
            Assert.IsFalse(rt.Ui.HandleBack());
            Assert.IsTrue(rt.Ui.DebugPausedGather(MinigameKind.Chop, true));
            Assert.IsTrue(rt.Ui.HandleBack());
            Assert.IsNull(rt.Ui.DebugRoot.Q<Button>("minigame-confirm-abandon"));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q<Button>("minigame-resume"));
            Assert.IsTrue(rt.Ui.HandleBack());
            Assert.IsNull(rt.Ui.DebugRoot.Q("minigame-pause-layer"));
            Assert.IsTrue(rt.Ui.DebugBusy);
            Assert.IsTrue(rt.Ui.HandleBack());
            Assert.IsNotNull(rt.Ui.DebugRoot.Q<Button>("minigame-resume"));
            rt.Ui.DebugReset();
        }

        sealed class LanguageTableProvider : UnityEngine.Localization.Settings.ITableProvider
        {
            public UnityEngine.Localization.Tables.StringTable complete, incomplete;

            public UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<TTable> ProvideTableAsync<TTable>(
                string tableCollectionName, UnityEngine.Localization.Locale locale)
                where TTable : UnityEngine.Localization.Tables.LocalizationTable
            {
                if (tableCollectionName != UiText.CollectionName) return default;
                var selected = locale.Identifier.Code == "fr" ? complete
                    : locale.Identifier.Code == "de" ? incomplete : null;
                return selected == null ? default
                    : UnityEngine.AddressableAssets.Addressables.ResourceManager.CreateCompletedOperation(selected as TTable, null);
            }
        }

        [UnityTest]
        public IEnumerator LanguageSelectionRefreshesLiveUiAndPersistsWithoutChangingProgress()
        {
            new GameObject("LanguageBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var runtime = FrontierRuntime.Current;
            Assert.IsNotNull(runtime);
            runtime.State.DebugPlace(runtime.State.db.FindStructure("warehouse"), 0);
            yield return null;
            var database = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase;
            var locales = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales;
            var previousProvider = database.TableProvider;
            var previousLocale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
            bool hadPreference = PlayerPrefs.HasKey(UiText.LocalePreferenceKey);
            string preference = PlayerPrefs.GetString(UiText.LocalePreferenceKey, "");
            var french = UnityEngine.Localization.Locale.CreateLocale("fr");
            var german = UnityEngine.Localization.Locale.CreateLocale("de");
            var complete = ScriptableObject.CreateInstance<UnityEngine.Localization.Tables.StringTable>();
            var incomplete = ScriptableObject.CreateInstance<UnityEngine.Localization.Tables.StringTable>();
            var source = Resources.Load<UiStringTableSO>("Frontier/UiStrings");
            complete.SharedData = ScriptableObject.CreateInstance<UnityEngine.Localization.Tables.SharedTableData>();
            incomplete.SharedData = ScriptableObject.CreateInstance<UnityEngine.Localization.Tables.SharedTableData>();
            complete.SharedData.TableCollectionName = incomplete.SharedData.TableCollectionName = UiText.CollectionName;
            complete.LocaleIdentifier = french.Identifier;
            incomplete.LocaleIdentifier = german.Identifier;
            foreach (var entry in source.entries) complete.AddEntry(entry.key, entry.text);
            foreach (var entry in source.entries)
                if (entry.key.StartsWith("content.", StringComparison.Ordinal))
                    complete.GetEntry(entry.key).Value = "LOCALIZED " + entry.key;
            foreach (var entry in source.entries)
                if (entry.key.StartsWith("planet.blurb.opener.", StringComparison.Ordinal)
                    || entry.key.StartsWith("planet.blurb.belt.", StringComparison.Ordinal))
                    complete.GetEntry(entry.key).Value = "LOCALIZED TERRAIN";
            complete.GetEntry("planet.enum.biome.lush").Value = "LOCALIZED BIOME";
            complete.GetEntry("nav.command").Value = "LOCALIZED COMMAND";
            complete.GetEntry("nav.menu").Value = "LOCALIZED MENU";
            complete.GetEntry("settings.language").Value = "LOCALIZED LANGUAGE";
            complete.GetEntry("guide.skip").Value = "LOCALIZED SKIP";
            var provider = new LanguageTableProvider { complete = complete, incomplete = incomplete };
            locales.AddLocale(french);
            locales.AddLocale(german);
            database.TableProvider = provider;
            int structures = runtime.State.structures.Count, shards = runtime.State.ShardBalance;
            string planet = runtime.State.planet.Id;
            var contentDb = runtime.State.db;
            string catalogEnglish = JsonUtility.ToJson(contentDb.ToCatalog());
            var existingStructure = runtime.State.structures[0].def;
            string structureEnglish = existingStructure.displayName;
            var research = contentDb.research[0];
            var ladder = contentDb.toolLadders[0];
            var objective = runtime.State.Objectives[0];
            var generated = runtime.State.universe.FindGenerated(Frontier.Universe.Biome.Lush, null);
            string generatedEnglish = generated.Description;
            ulong generatedSeed = generated.Seed;
            try
            {
                runtime.Ui.DebugMenu("settings");
                var dropdown = runtime.Ui.DebugRoot.Q<DropdownField>("settings-language");
                Assert.IsTrue(dropdown.enabledSelf);
                dropdown.value = french.LocaleName;
                string overlapping = null;
                yield return UiText.SelectLocale("en", message => overlapping = message);
                Assert.AreEqual(UiText.Get("settings.language.busy"), overlapping);
                deadline = Time.realtimeSinceStartup + 10f;
                while (UiText.IsChangingLocale && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null;
                Assert.AreEqual("fr", UiText.LocaleCode);
                Assert.AreEqual("fr", PlayerPrefs.GetString(UiText.LocalePreferenceKey));
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == "LOCALIZED COMMAND"));
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == "LOCALIZED MENU"));
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == "LOCALIZED LANGUAGE"));
                Assert.AreEqual("LOCALIZED SKIP", runtime.Ui.DebugRoot.Q("first-session-guide-skip").tooltip);
                StringAssert.Contains("LOCALIZED TERRAIN", generated.Description);
                Assert.AreEqual("LOCALIZED BIOME", generated.BiomeName);
                Assert.AreEqual(generatedSeed, generated.Seed, "Language must not regenerate the planet.");
                Assert.IsTrue(runtime.Ui.DebugRoot.enabledSelf);
                foreach (var resource in contentDb.resources)
                    Assert.AreEqual("LOCALIZED content.resource." + resource.id + ".name", resource.LocalizedName);
                Assert.AreEqual("LOCALIZED content.structure." + existingStructure.id + ".name", existingStructure.LocalizedName);
                Assert.AreEqual("LOCALIZED content.research." + research.data.id + ".name", research.LocalizedName);
                Assert.AreEqual("LOCALIZED content.tool." + ladder.data.id + ".tier-" + ladder.data.tiers[0].tier,
                    ladder.TierName(ladder.data.tiers[0]));
                Assert.AreEqual("LOCALIZED content.objective." + objective.id + ".title", objective.LocalizedTitle);
                var modifier = contentDb.formula.modifiers[0];
                Assert.AreEqual("LOCALIZED content.wave-modifier.0.name", contentDb.formula.ModifierName(modifier));
                Assert.AreEqual(structureEnglish, existingStructure.displayName, "Translations must not rewrite authored fields.");
                Assert.AreEqual(catalogEnglish, JsonUtility.ToJson(contentDb.ToCatalog()), "Localization must not change gameplay catalog data.");
                var captions = runtime.Planet.Spin.GetComponentsInChildren<UIDocument>();
                Assert.IsTrue(Array.Exists(captions, caption => caption.rootVisualElement.Query<Label>().ToList()
                    .Exists(label => label.text != null && label.text.Contains(existingStructure.LocalizedName.ToUpper()))));
                runtime.Ui.DebugResearch(2);
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList()
                    .Exists(label => label.text == research.LocalizedName.ToUpper()));
                runtime.Ui.DebugResearch(1);
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList()
                    .Exists(label => label.text == ladder.LocalizedName.ToUpper()));
                runtime.Ui.DebugMenu("goals");
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == objective.LocalizedTitle));

                string failure = null;
                LogAssert.Expect(LogType.Warning, "Frontier UI localization is missing a plain-text entry: " + source.entries[0].key);
                yield return UiText.SelectLocale("de", message => failure = message);
                Assert.AreEqual(UiText.Get("settings.language.failed"), failure);
                Assert.AreEqual("fr", UiText.LocaleCode);
                Assert.AreEqual(french, UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale);
                Assert.AreEqual("fr", PlayerPrefs.GetString(UiText.LocalePreferenceKey));
                yield return UiText.SelectLocale("not-installed", message => failure = message);
                Assert.AreEqual(UiText.Get("settings.language.unavailable"), failure);
                Assert.AreEqual("fr", UiText.LocaleCode);

                UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = previousLocale;
                yield return UiText.InitializeLocalization();
                Assert.AreEqual("fr", UiText.LocaleCode, "Startup must honor the saved language, not the device locale.");
                Assert.AreEqual(structures, runtime.State.structures.Count);
                Assert.AreEqual(shards, runtime.State.ShardBalance);
                Assert.AreEqual(planet, runtime.State.planet.Id);
                yield return UiText.SelectLocale("en", message => Assert.IsNull(message));
                Assert.AreEqual(generatedEnglish, generated.Description);
                Assert.AreEqual(structureEnglish, existingStructure.LocalizedName);
                Assert.AreEqual(catalogEnglish, JsonUtility.ToJson(contentDb.ToCatalog()));
            }
            finally
            {
                database.TableProvider = previousProvider;
                UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = previousLocale;
                database.ReleaseTable(UiText.CollectionName, french);
                database.ReleaseTable(UiText.CollectionName, german);
                locales.RemoveLocale(french);
                locales.RemoveLocale(german);
                if (hadPreference) PlayerPrefs.SetString(UiText.LocalePreferenceKey, preference);
                else PlayerPrefs.DeleteKey(UiText.LocalePreferenceKey);
                PlayerPrefs.Save();
                Object.Destroy(complete.SharedData);
                Object.Destroy(incomplete.SharedData);
                Object.Destroy(complete);
                Object.Destroy(incomplete);
                Object.Destroy(french);
                Object.Destroy(german);
            }
        }

        [UnityTest]
        public IEnumerator SettingsResetProtectsPurchaseHistoryAndExplainsWhyItWasRefused()
        {
            new GameObject("PurchaseResetBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            var grants = (IPurchaseGrantStore)rt.State;
            Assert.IsTrue(grants.CommitVerifiedPurchase(grants.ContextId,
                new VerifiedPurchase("test-only-store", "shards_1", "test-only-transaction")).succeeded);
            string context = grants.ContextId;
            int balance = rt.State.ShardBalance;
            string saved = File.ReadAllText(rt.State.saves.Store.PrimaryPath);
            rt.Ui.DebugMenu("settings");
            for (int press = 0; press < 2; press++)
            {
                var reset = rt.Ui.DebugRoot.Q<Button>("settings-reset");
                Assert.IsNotNull(reset);
                using (var submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = reset;
                    reset.SendEvent(submit);
                }
                yield return null;
            }
            Assert.IsNotNull(rt.Ui.DebugRoot.Q<Button>("settings-reset"), "Settings must stay open after refusal.");
            Assert.AreEqual(context, grants.ContextId);
            Assert.AreEqual(balance, rt.State.ShardBalance);
            Assert.AreEqual(saved, File.ReadAllText(rt.State.saves.Store.PrimaryPath));
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("purchase.recovery-required")));
        }

        [UnityTest]
        public IEnumerator SoundscapeTracksLiveDefenseAndMuteWithoutExtraSources()
        {
            bool wasMuted = Sfx.Muted;
            try
            {
                Sfx.Muted = false;
                new GameObject("SoundscapeBootstrap").AddComponent<FrontierBootstrap>();
                float deadline = Time.realtimeSinceStartup + 30;
                while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
                var rt = FrontierRuntime.Current;
                Assert.IsNotNull(rt);
                Assert.IsNotNull(Object.FindFirstObjectByType<FrontierSoundscape>());
                var library = Resources.Load<SoundscapeLibrary>("Frontier/SoundscapeLibrary");
                var sources = rt.Camera.GetComponents<AudioSource>();
                Assert.AreEqual(AudioService.VoiceCount + 2, sources.Length);
                var music = sources[AudioService.VoiceCount + 1];
                Assert.AreSame(library.exploration, music.clip);
                if (rt.State.backpack.Count(rt.Db.fuel.id) < 1) rt.State.Add(rt.Db.fuel, 1, false);
                Assert.IsTrue(rt.Defense.StartWave(1));
                deadline = Time.realtimeSinceStartup + 3;
                while (music.clip != library.combat && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreSame(library.combat, music.clip);
                Sfx.Muted = true;
                yield return null;
                Assert.IsFalse(music.isPlaying);
                Sfx.Muted = false;
                yield return null;
                Assert.AreSame(library.combat, music.clip);
                Assert.IsTrue(music.isPlaying);
                rt.Defense.DebugEnd(false);
                deadline = Time.realtimeSinceStartup + 3;
                while (music.clip != library.exploration && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreSame(library.exploration, music.clip);
                Assert.AreSame(library.Ambience(rt.State.planet.Weather), sources[AudioService.VoiceCount].clip);
            }
            finally { Sfx.Muted = wasMuted; }
        }

        [UnityTest]
        public IEnumerator Bootstrap_BuildsTheWholeSession()
        {
            var host = new GameObject("TestBootstrap");
            host.AddComponent<FrontierBootstrap>();

            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;

            var runtime = FrontierRuntime.Current;
            Assert.IsNotNull(runtime, "FrontierBootstrap did not publish a FrontierRuntime within 30 s");
            Assert.IsNotNull(runtime.Db);
            Assert.IsTrue(UiText.UsesUnityLocalization, "Bootstrap must await the packaged Unity string table before building UI.");
            Assert.AreEqual("COMMAND", UiText.Get("nav.command"));
            Assert.IsTrue(runtime.Db.UsesImportedEconomy);
            Assert.IsNotNull(runtime.State);
            Assert.IsInstanceOf<SessionClock>(runtime.State.clock,
                "The live session must anchor timer UTC to monotonic elapsed time.");
            Assert.IsNotNull(runtime.State.planet);
            Assert.IsNotNull(runtime.Camera);
            Assert.IsNotNull(runtime.Backdrop);
            Assert.IsNotNull(runtime.Planet);
            Assert.IsNotNull(runtime.Defense);
            Assert.IsNotNull(runtime.Surface);
            Assert.IsNotNull(runtime.Ui);
            Assert.IsNotNull(runtime.Document);
            Assert.AreSame(runtime.State.planet, runtime.Planet.Current, "the planet view must show the player's planet");
            Assert.IsNotNull(runtime.Camera.GetComponent<HarvestHaptics>(), "Harvest haptics must share the audio host lifecycle.");

            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsNotNull(runtime.Document.rootVisualElement, "the UI document has no visual tree");
            Assert.Greater(runtime.Document.rootVisualElement.childCount, 0, "the UI was not built");
            runtime.Ui.DebugMenu("settings");
            var haptics = runtime.Ui.DebugRoot.Q<Button>("settings-haptics");
            Assert.IsNotNull(haptics);
            Assert.IsFalse(haptics.enabledSelf, "The editor must not claim native vibration support.");
            runtime.Ui.DebugReset();
            runtime.Ui.DebugMenu("ranks");
            Assert.IsNotNull(runtime.Ui.DebugRoot.Q("era-progress"));
            Assert.AreEqual(Frontier.Economy.FrontierEra.Landfall, runtime.State.Era);
            foreach (var key in new[] { "ranks.score", "ranks.record", "ranks.era.help", "ranks.unavailable" })
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get(key)), key);
            var score = runtime.State.FrontierScore;
            foreach (var pair in new Dictionary<string, string>
            {
                { "ranks.total", score.total.ToString("N0") }, { "ranks.reach", score.reach.ToString("N0") },
                { "ranks.worlds", score.worlds.ToString("N0") }, { "ranks.industry", score.industry.ToString("N0") },
                { "ranks.technology", score.tech.ToString("N0") }, { "ranks.defense", score.defense.ToString("N0") },
                { "ranks.atlas", score.atlas.ToString("N0") }
            })
            {
                var row = runtime.Ui.DebugRoot.Query<FrontierStatRow>().ToList()
                    .Find(candidate => candidate.Caption == UiText.Get(pair.Key));
                Assert.IsNotNull(row, pair.Key);
                Assert.AreEqual(pair.Value, row.Value, pair.Key);
            }
            var eraAction = runtime.Ui.DebugRoot.Q<Button>("era-next-action");
            Assert.IsNotNull(eraAction);
            Assert.IsTrue(eraAction.Query<Label>().ToList().Exists(label => label.text == UiText.Get("guide.action.map")));
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = eraAction; eraAction.SendEvent(submit); }
            Assert.IsNotNull(runtime.Ui.DebugRoot.Q(className: "fr-map"), "The next-era action should open the live map.");
            float discoveryDeadline = Time.realtimeSinceStartup + 5f;
            while (!runtime.State.IsMapDiscovered(runtime.State.planet.Star.Id) && Time.realtimeSinceStartup < discoveryDeadline)
                yield return null;
            Assert.IsTrue(runtime.State.IsMapDiscovered(runtime.State.planet.Star.Id),
                "Opening the map should persist the surveyed home system.");
            var pinToggle = runtime.Ui.DebugRoot.Q<Button>("map-pin-toggle");
            Assert.IsNotNull(pinToggle, "The selected destination should offer a map pin action.");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = pinToggle; pinToggle.SendEvent(submit); }
            Assert.IsTrue(runtime.State.IsMapPinned(runtime.State.planet.Id));
            Assert.IsNotNull(runtime.Ui.DebugRoot.Q<Button>("map-pin-" + runtime.State.planet.Id),
                "Pinned destinations should be listed in the map card.");
            if (runtime.State.backpack.Count(runtime.Db.fuel.id) < 1)
                runtime.State.Add(runtime.Db.fuel, 1, false);
            int activeRipples = ActiveRippleCount();
            Vector3 planetCenter = runtime.Defense.Center;
            Assert.IsTrue(runtime.Defense.StartWave(1), "The bootstrap session should be able to start a test wave.");
            runtime.Defense.DebugEnd(false);
            Assert.Greater(ActiveRippleCount(), activeRipples,
                "Ending a called wave should emit a pooled result ripple on the current planet.");
            bool rippleAtPlanet = false;
            foreach (var ripple in Resources.FindObjectsOfTypeAll<RippleAnim>())
                if (ripple != null && ripple.gameObject.activeInHierarchy &&
                    (ripple.transform.position - planetCenter).sqrMagnitude < 0.0001f)
                    rippleAtPlanet = true;
            Assert.IsTrue(rippleAtPlanet, "The wave result effect must stay centered on the current planet.");
            runtime.Ui.DebugReset();
        }

        // Any Debug.LogError or exception during the frames below fails the test (Unity's default), so this also proves the UI
        // refresh handlers survive a state change.
        [UnityTest]
        public IEnumerator Ascension_RequiresConfirmation_ThenUpdatesTheLiveUniverseViewsAndRankEffects()
        {
            ascensionTestRules = Resources.Load<AscensionRulesSO>("Frontier/AscensionRules");
            previousAscensionEnabled = ascensionTestRules.enableAscension;
            ascensionTestRules.enableAscension = true;
            var seedState = new GameState(GameDatabase.Load(), false);
            seedState.DebugPlace(seedState.db.StructureOfKind(StructureKind.Terraformer), 0, 3);
            seedState.Save();
            string file = Path.Combine(saveDir, SaveSchema.FileName);
            var save = JsonUtility.FromJson<SaveV2>(File.ReadAllText(file));
            save.player.researched.Add("ascension_protocol");
            File.WriteAllText(file, JsonUtility.ToJson(save));
            new GameObject("AscensionTestBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var runtime = FrontierRuntime.Current;
            Assert.IsNotNull(runtime);
            for (int i = 0; i < 10; i++) yield return null;
            ulong originalSeed = runtime.State.universe.Seed;
            runtime.Ui.DebugMenu("ascension");
            Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("ascension.title", 0)));
            Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("ascension.summary")));
            Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("ascension.next-yield",
                    runtime.State.NextLegacyHarvestMultiplier, runtime.State.MaximumLegacyHarvestBonus * 100)));
            Assert.AreEqual(WhiteSpace.Normal, runtime.Ui.DebugRoot.Q<Label>("ascension-keeps").style.whiteSpace.value,
                "The retained/reset disclosure must wrap instead of clipping.");
            var review = runtime.Ui.DebugRoot.Q<Button>("ascension-review");
            Assert.IsNotNull(review);
            Assert.IsTrue(review.Query<Label>().ToList().Exists(label => label.text == UiText.Get("ascension.review")));
            Assert.IsNull(runtime.Ui.DebugRoot.Q<Button>("ascension-confirm"));
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = review; review.SendEvent(submit); }
            var cancel = runtime.Ui.DebugRoot.Q<Button>("ascension-cancel");
            Assert.IsNotNull(cancel);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = cancel; cancel.SendEvent(submit); }
            Assert.AreEqual(0, runtime.State.AscensionRank);
            review = runtime.Ui.DebugRoot.Q<Button>("ascension-review");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = review; review.SendEvent(submit); }
            var confirm = runtime.Ui.DebugRoot.Q<Button>("ascension-confirm");
            Assert.IsNotNull(confirm);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = confirm; confirm.SendEvent(submit); }
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(1, runtime.State.AscensionRank);
            Assert.AreNotEqual(originalSeed, runtime.State.universe.Seed);
            Assert.AreSame(runtime.State.planet, runtime.Planet.Current);
            Assert.AreEqual(0, runtime.State.structures.Count);
            Assert.AreEqual(1, runtime.Planet.GetComponent<AscensionAura>().Rank);
            Assert.IsNotNull(runtime.Planet.Root.GetComponentInChildren<LineRenderer>());
            using (var travel = new Hyperspace.View.HyperspaceScene(runtime.Camera.transform, runtime.State.AscensionRank))
                Assert.AreEqual(AscensionStyle.Accent(1), travel.TrailColor);
            runtime.Ui.DebugReset();
            runtime.Ui.DebugMap(null, 2);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsNotNull(runtime.Ui.DebugRoot.Q(className: "fr-map"));
            runtime.Ui.DebugReset();
        }

        [UnityTest]
        public IEnumerator MenuTutorialAndShopUseAuthoredStringsWithoutChangingState()
        {
            new GameObject("StringScreenBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            int structures = rt.State.structures.Count, shards = rt.State.ShardBalance;
            rt.Ui.DebugMenu("");
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(l => l.text == UiText.Get("menu.tutorial")));
            rt.Ui.DebugMenu("tutorial");
            foreach (var step in new[] { "build", "storage", "gather", "refine", "defend", "travel", "upgrade" })
            {
                Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(l => l.text == UiText.Get("tutorial." + step + ".title")));
                Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(l => l.text == UiText.Get("tutorial." + step + ".body")));
            }
            rt.Ui.DebugMenu("about");
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(l => l.text == UiText.Get("about.body")));
            rt.Ui.DebugMenu("settings");
            foreach (var key in new[] { "privacy.title", "privacy.help", "recovery.title", "recovery.help" })
                Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get(key)), key);
            var consentChoices = rt.Ui.DebugRoot.Query<Button>(className: "fr-consent-choice").ToList();
            Assert.AreEqual(3, consentChoices.Count);
            foreach (var key in new[] { "privacy.analytics", "privacy.ads", "privacy.online" })
                Assert.IsTrue(consentChoices.Exists(choice => choice.Query<Label>().ToList()
                    .Exists(label => label.text == UiText.Get(key))), key);
            foreach (var choice in consentChoices)
            {
                var state = choice.Q<Label>(className: "fr-consent-choice-state");
                Assert.IsNotNull(state);
                Assert.IsTrue(state.text == UiText.Get("privacy.on") || state.text == UiText.Get("privacy.off"));
            }
            var assist = rt.Ui.DebugRoot.Q<Button>("assist-toggle");
            Assert.IsNotNull(assist);
            Assert.IsTrue(assist.Query<Label>().ToList().Exists(label => label.text == UiText.Get("settings.assist.off")));
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = assist; assist.SendEvent(submit); }
            Assert.IsTrue(TravelPrefs.AssistMode);
            Assert.IsTrue(rt.Ui.DebugRoot.Q<Button>("assist-toggle").Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("settings.assist.on")));
            rt.Ui.DebugResearch(2);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get("research.branch")));
            rt.Ui.DebugResearch(1);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get("tools.title")));
            rt.Ui.DebugReset();
            rt.Ui.DebugShop(0);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(l => l.text == UiText.Get("shop.summary",
                rt.State.planet.Name, rt.State.FreePads(rt.State.planet), rt.State.planet.BuildingSlots,
                rt.State.BusyBuilderCount, rt.State.BuilderCount)));
            rt.Ui.DebugMenu("debug");
            var debugLabels = rt.Ui.DebugRoot.Query<Label>().ToList();
            foreach (var key in new[]
            {
                "debug.header", "debug.items", "debug.progress", "debug.generated-world", "debug.generated-help",
                "debug.biome.gas-giant", "debug.size.colossal", "debug.minimum-tier", "debug.dump-descriptor"
            })
                Assert.IsTrue(debugLabels.Exists(label => label.text == UiText.Get(key)), key);
            Assert.IsTrue(debugLabels.Exists(label => label.text == UiText.Get("debug.now-on", rt.State.planet.Name, rt.State.planet.Id)));
            Assert.AreEqual(structures, rt.State.structures.Count);
            Assert.AreEqual(shards, rt.State.ShardBalance);
            rt.Ui.DebugReset();
        }

        sealed class DeletionFixture : IAuthService, ICloudSaveService
        {
            public string playerId = "deletion-test-player";
            public string CurrentPlayerId { get { return playerId; } }
            public bool IsConfigured { get { return true; } }
            public bool CanRecoverAccount { get { return false; } }
            public string RecoveryProviderName { get { return ""; } }
            public int cloudDeletes, accountDeletes;
            public TaskCompletionSource<ServiceResult> cloudReply;
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<AuthIdentity>> SignInAnonymouslyAsync() { throw new InvalidOperationException("Deletion must not sign in."); }
            public Task<ServiceResult<AuthIdentity>> LinkRecoveryAccountAsync() { throw new InvalidOperationException("Deletion must not link."); }
            public Task<ServiceResult<AuthIdentity>> SignInWithRecoveryAccountAsync() { throw new InvalidOperationException("Deletion must not sign in."); }
            public Task<ServiceResult> DeleteAccountAsync(string expectedPlayerId)
            {
                Assert.AreEqual(playerId, expectedPlayerId);
                accountDeletes++;
                playerId = "";
                return Task.FromResult(ServiceResult.Success());
            }
            public Task<ServiceResult> DeleteSaveAsync(string expectedPlayerId)
            {
                Assert.AreEqual(playerId, expectedPlayerId);
                cloudDeletes++;
                return cloudReply.Task;
            }
            public Task<ServiceResult<CloudSaveRecord>> ReadAsync() { throw new InvalidOperationException("Deletion must not compare or restore."); }
            public Task<ServiceResult<CloudSaveRecord>> WriteAsync(string payload, string writeLock) { throw new InvalidOperationException("Deletion must not upload."); }
        }

        sealed class DeletionConsentStore : IConsentStore
        {
            ConsentPreferences value;
            public ConsentPreferences Load() { return value; }
            public void Save(ConsentPreferences preferences) { value = preferences; }
        }

        [UnityTest]
        public IEnumerator OnlineDeletionRequiresConfirmationAllowsCancelAndPreservesLocalProgress()
        {
            new GameObject("AccountDeletionBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            for (int i = 0; i < 10; i++) yield return null;
            var provider = new DeletionFixture();
            var unavailable = new UnavailableFrontierProvider();
            var services = new FrontierServices(new ConsentService(new DeletionConsentStore()), unavailable, unavailable,
                provider, unavailable, provider, unavailable, unavailable, new LocalTimeService(), unavailable);
            rt.Ui.DebugSetServices(services);
            Assert.IsTrue(rt.State.Save());
            string savePath = Path.Combine(saveDir, SaveSchema.FileName);
            string localSave = File.ReadAllText(savePath);
            int shards = rt.State.ShardBalance;
            string planet = rt.State.planet.Id;
            void Press(string name)
            {
                var button = rt.Ui.DebugRoot.Q<Button>(name);
                Assert.IsNotNull(button, name);
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
            }
            rt.Ui.DebugMenu("settings");
            Press("settings-delete-account");
            Assert.AreEqual(0, provider.cloudDeletes + provider.accountDeletes);
            Assert.IsNotNull(rt.Ui.DebugRoot.Q<Button>("settings-delete-account-cancel"));
            Press("settings-delete-account-cancel");
            Assert.IsNull(rt.Ui.DebugRoot.Q<Button>("settings-delete-account-cancel"));
            Press("settings-delete-account");
            rt.Ui.DebugReset();
            rt.Ui.DebugMenu("settings");
            Press("settings-delete-account");
            Assert.AreEqual(0, provider.cloudDeletes, "Closing Settings must discard the destructive confirmation.");
            provider.playerId = "different-test-player";
            Press("settings-delete-account");
            Assert.AreEqual(0, provider.cloudDeletes, "Changing accounts must require a new confirmation.");
            provider.cloudReply = new TaskCompletionSource<ServiceResult>();
            Press("settings-delete-account");
            Assert.AreEqual(1, provider.cloudDeletes);
            Assert.AreEqual(0, provider.accountDeletes);
            Assert.IsFalse(rt.Ui.DebugRoot.Q<Button>("settings-delete-account").enabledSelf);
            provider.cloudReply.SetResult(ServiceResult.Failed("Test cloud conflict"));
            yield return null;
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == "Test cloud conflict"));
            Assert.AreEqual(0, provider.accountDeletes);
            provider.cloudReply = new TaskCompletionSource<ServiceResult>();
            Press("settings-delete-account");
            Press("settings-delete-account");
            provider.cloudReply.SetResult(ServiceResult.Success());
            yield return null;
            Assert.AreEqual(2, provider.cloudDeletes);
            Assert.AreEqual(1, provider.accountDeletes);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get("deletion.done")));
            Assert.IsNull(rt.Ui.DebugRoot.Q<Button>("settings-delete-account"));
            Assert.AreEqual(shards, rt.State.ShardBalance);
            Assert.AreEqual(planet, rt.State.planet.Id);
            Assert.AreEqual(localSave, File.ReadAllText(savePath), "Online deletion cannot overwrite or erase local progress.");
            Assert.IsFalse(services.Consent.Current.onlineServicesAllowed, "Erasure cannot silently opt the player into online features.");
            rt.Ui.DebugReset();
        }

        [UnityTest]
        public IEnumerator AudioSettingsSlidersApplyImmediatelyAndPersistAcrossPanelReopening()
        {
            bool hadEffects = PlayerPrefs.HasKey(Sfx.EffectsVolumeKey), hadMusic = PlayerPrefs.HasKey(Sfx.MusicVolumeKey);
            float effects = PlayerPrefs.GetFloat(Sfx.EffectsVolumeKey), music = PlayerPrefs.GetFloat(Sfx.MusicVolumeKey);
            try
            {
                new GameObject("AudioSettingsBootstrap").AddComponent<FrontierBootstrap>();
                float deadline = Time.realtimeSinceStartup + 30f;
                while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
                var rt = FrontierRuntime.Current;
                Assert.IsNotNull(rt);
                for (int i = 0; i < 10; i++) yield return null;
                int shards = rt.State.ShardBalance;
                string planet = rt.State.planet.Id;
                rt.Ui.DebugMenu("settings");
                var effectsSlider = rt.Ui.DebugRoot.Q<Slider>("settings-effects-volume");
                var musicSlider = rt.Ui.DebugRoot.Q<Slider>("settings-music-volume");
                Assert.IsNotNull(effectsSlider);
                Assert.IsNotNull(musicSlider);
                effectsSlider.value = 0.25f;
                musicSlider.value = 0.75f;
                Assert.AreEqual(0.25f, Sfx.EffectsVolume);
                Assert.AreEqual(0.75f, Sfx.MusicVolume);
                Assert.AreSame(effectsSlider, rt.Ui.DebugRoot.Q<Slider>("settings-effects-volume"),
                    "Dragging must not rebuild the active slider.");
                foreach (var percent in new[] { 25, 75 })
                    Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                        .Exists(label => label.text == UiText.Get("settings.audio.percent", percent)));
                rt.Ui.DebugReset();
                rt.Ui.DebugMenu("settings");
                Assert.AreEqual(0.25f, rt.Ui.DebugRoot.Q<Slider>("settings-effects-volume").value);
                Assert.AreEqual(0.75f, rt.Ui.DebugRoot.Q<Slider>("settings-music-volume").value);
                rt.Ui.DebugRoot.Q<Slider>("settings-effects-volume").value = 0;
                Assert.AreEqual(0f, Sfx.EffectsVolume);
                Assert.AreEqual(0.75f, Sfx.MusicVolume);
                Assert.AreEqual(shards, rt.State.ShardBalance);
                Assert.AreEqual(planet, rt.State.planet.Id);
                rt.Ui.DebugReset();
            }
            finally
            {
                Sfx.EffectsVolume = effects;
                Sfx.MusicVolume = music;
                if (!hadEffects) PlayerPrefs.DeleteKey(Sfx.EffectsVolumeKey);
                if (!hadMusic) PlayerPrefs.DeleteKey(Sfx.MusicVolumeKey);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator AtlasTabsUseAuthoredStringsAndNavigationCannotGrantDiscoveriesOrResources()
        {
            new GameObject("AtlasStringBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            for (int i = 0; i < 10; i++) yield return null;
            var before = new Dictionary<ResourceSO, int>();
            foreach (var resource in rt.Db.resources) before[resource] = rt.State.Count(resource);
            int shards = rt.State.ShardBalance;
            int discoveries = rt.State.AtlasDiscoveredCount("resource") + rt.State.AtlasDiscoveredCount("recipe")
                + rt.State.AtlasDiscoveredCount("structure") + rt.State.AtlasDiscoveredCount("enemy");
            string planet = rt.State.planet.Id;
            rt.Ui.DebugAtlas(0);
            bool HasText(string key) => rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get(key));
            void SelectTab(string key)
            {
                var tab = rt.Ui.DebugRoot.Query<FrontierTab>().ToList().Find(candidate => candidate.Caption == UiText.Get(key));
                Assert.IsNotNull(tab, key);
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = tab; tab.SendEvent(submit); }
            }
            Assert.IsTrue(HasText("atlas.resource.where"));
            foreach (Frontier.Economy.ResourceCategory category in Enum.GetValues(typeof(Frontier.Economy.ResourceCategory)))
            {
                var picker = rt.Ui.DebugRoot.Q<DropdownField>();
                picker.index = (int)category;
                Assert.AreEqual(UiText.Get("resource.category." + category.ToString().ToLowerInvariant()),
                    rt.Ui.DebugRoot.Q<DropdownField>().value);
                Assert.IsTrue(HasText("atlas.resource.where"));
            }
            SelectTab("atlas.recipes");
            Assert.IsTrue(HasText("atlas.recipes.help"));
            Assert.IsTrue(HasText("atlas.recipe.input"));
            Assert.IsTrue(HasText("atlas.recipe.output"));
            SelectTab("atlas.worlds");
            Assert.IsTrue(HasText("atlas.worlds.help"));
            Assert.IsTrue(HasText("atlas.world.resources"));
            SelectTab("atlas.bestiary");
            Assert.IsTrue(HasText("atlas.enemy.unknown"));
            Assert.IsTrue(HasText("atlas.enemy.reveal"));
            SelectTab("atlas.structures");
            Assert.IsTrue(HasText("atlas.structure.unknown"));
            Assert.AreEqual(discoveries, rt.State.AtlasDiscoveredCount("resource") + rt.State.AtlasDiscoveredCount("recipe")
                + rt.State.AtlasDiscoveredCount("structure") + rt.State.AtlasDiscoveredCount("enemy"));
            foreach (var enemy in rt.Db.enemies) rt.State.RecordAtlasEnemySeen(enemy);
            rt.Ui.DebugAtlas(3);
            foreach (var enemy in rt.Db.enemies)
            {
                Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                    .Exists(label => label.text == UiText.Get("atlas.enemy.summary", enemy.minWave, enemy.threat)));
                if (enemy.rank == AlienRank.Boss)
                    Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                        .Exists(label => label.text == UiText.Get("atlas.enemy.boss-return", enemy.bossInterval)));
            }
            Assert.AreEqual(shards, rt.State.ShardBalance);
            foreach (var pair in before) Assert.AreEqual(pair.Value, rt.State.Count(pair.Key), pair.Key.id);
            Assert.AreEqual(planet, rt.State.planet.Id);
            rt.Ui.DebugReset();
        }

        [UnityTest]
        public IEnumerator Goals_CollectsRealReward_AndRoutesToTheBuildShop()
        {
            new GameObject("GoalsTestBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var runtime = FrontierRuntime.Current;
            Assert.IsNotNull(runtime);
            for (int i = 0; i < 10; i++) yield return null;
            runtime.State.RecordHarvestRunCompleted();
            int shards = runtime.State.ShardBalance;
            runtime.Ui.DebugMenu("goals");
            Assert.IsNotNull(runtime.Ui.DebugRoot.Q("frontier-goals"));
            Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get("goals.title")));
            var claim = runtime.Ui.DebugRoot.Q<Button>("objective-claim-first_harvest");
            Assert.IsNotNull(claim);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = claim; claim.SendEvent(submit); }
            Assert.IsTrue(runtime.State.ObjectiveClaimed("first_harvest"));
            Assert.AreEqual(shards + 5, runtime.State.ShardBalance);
            Assert.IsNull(runtime.Ui.DebugRoot.Q<Button>("objective-claim-first_harvest"));
            var action = runtime.Ui.DebugRoot.Q<Button>("objective-action-first_structure");
            Assert.IsNotNull(action);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = action; action.SendEvent(submit); }
            Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == "BUILD"),
                "The next objective action should open the live Build shop.");
            runtime.Ui.DebugReset();
        }

        [UnityTest]
        public IEnumerator StartupIntroSkipsWithoutProgressionAndRepeatArrivalOmitsTheLogo()
        {
            Assert.IsFalse(TravelPrefs.IntroSeen);
            new GameObject("IntroTestBootstrap").AddComponent<FrontierBootstrap>();
            UIRoot ui = null;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                ui = Object.FindFirstObjectByType<UIRoot>();
                if (ui != null && ui.DebugRoot != null && ui.DebugRoot.Q("intro-logo") != null) break;
                yield return null;
            }
            Assert.IsNotNull(ui);
            Assert.IsNotNull(ui.DebugRoot.Q("intro-logo"));
            Assert.IsNull(FrontierRuntime.Current, "Runtime readiness must wait for the intro to release input.");
            Assert.IsFalse(TravelPrefs.IntroSeen);
            var state = GameState.I;
            string planet = state.planet.Id;
            int fuel = state.Count(state.db.fuel), level = state.PlayerLevel, slots = state.SlotsUsed;
            Assert.IsTrue(ui.HandleBack(), "Back skips the first-launch logo.");
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var runtime = FrontierRuntime.Current;
            Assert.IsNotNull(runtime);
            Assert.IsTrue(TravelPrefs.IntroSeen);
            Assert.IsTrue(runtime.Planet.Root.gameObject.activeSelf);
            Assert.IsTrue(runtime.Surface.gameObject.activeSelf);
            Assert.IsNull(ui.DebugRoot.Q("arrival-intro"));
            Vector3 cameraOrigin = runtime.Camera.transform.position;

            bool repeatComplete = false;
            IEnumerator RepeatArrival()
            {
                yield return ui.PlayArrivalIntro(false);
                repeatComplete = true;
            }
            ui.StartCoroutine(RepeatArrival());
            yield return null;
            Assert.IsNull(ui.DebugRoot.Q("intro-logo"), "Repeat launches use the short arrival, not the logo sequence.");
            var skip = ui.DebugRoot.Q<Button>("travel-skip-cinematic");
            Assert.IsNotNull(skip);
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = skip;
                skip.SendEvent(submit);
            }
            while (ui.DebugRoot.Q("travel-skip-cinematic") != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            float revealDeadline = Time.realtimeSinceStartup + 2f;
            do
            {
                yield return null;
            }
            while ((!repeatComplete ||
                Mathf.Abs(ui.DebugRoot.Q<VisualElement>(className: "fr-topbar").resolvedStyle.translate.y) > 0.01f ||
                Mathf.Abs(ui.DebugRoot.Q<VisualElement>(className: "fr-dock").resolvedStyle.translate.y) > 0.01f) &&
                Time.realtimeSinceStartup < revealDeadline);
            Assert.IsTrue(repeatComplete, "The short arrival must finish and release input.");
            Assert.AreEqual(planet, state.planet.Id);
            Assert.AreEqual(fuel, state.Count(state.db.fuel));
            Assert.AreEqual(level, state.PlayerLevel);
            Assert.AreEqual(slots, state.SlotsUsed);
            Assert.AreEqual(cameraOrigin, runtime.Camera.transform.position,
                "Skipping an arrival cinematic must restore the camera after any touchdown settle.");
            Assert.IsNull(ui.DebugRoot.Q("arrival-intro"));
            Assert.IsNull(ui.DebugRoot.Q("travel-skip-cinematic"));
            Assert.IsNull(Object.FindFirstObjectByType<ShipRig>());
            Assert.AreEqual(0f, ui.DebugRoot.Q<VisualElement>(className: "fr-topbar").resolvedStyle.translate.y, 0.01f);
            Assert.AreEqual(0f, ui.DebugRoot.Q<VisualElement>(className: "fr-dock").resolvedStyle.translate.y, 0.01f);
            Vector3 naturalCameraOrigin = runtime.Camera.transform.position;
            bool naturalArrivalComplete = false, observedTouchdownImpulse = false;
            IEnumerator NaturalArrival()
            {
                yield return ui.PlayArrivalIntro(false);
                naturalArrivalComplete = true;
            }
            ui.StartCoroutine(NaturalArrival());
            float naturalDeadline = Time.realtimeSinceStartup + 3f;
            while (!naturalArrivalComplete && Time.realtimeSinceStartup < naturalDeadline)
            {
                if (Vector3.Distance(naturalCameraOrigin, runtime.Camera.transform.position) > 0.0001f)
                    observedTouchdownImpulse = true;
                yield return null;
            }
            Assert.IsTrue(naturalArrivalComplete, "The natural arrival must finish and release input.");
            Assert.IsTrue(observedTouchdownImpulse, "Touchdown should produce a visible, bounded camera settle.");
            Assert.AreEqual(naturalCameraOrigin, runtime.Camera.transform.position,
                "The camera must settle back to its exact origin.");
            var menu = ui.DebugRoot.Q<Button>("menu-button");
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = menu;
                menu.SendEvent(submit);
            }
            Assert.IsNotNull(ui.DebugRoot.Q<VisualElement>(className: "fr-menu-grid"),
                "Input must be released after either intro variant.");
        }

        [UnityTest]
        public IEnumerator Bootstrap_SurvivesAStateChangeWithTheUiAttached()
        {
            var host = new GameObject("TestBootstrap");
            host.AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var runtime = FrontierRuntime.Current;
            Assert.IsNotNull(runtime, "FrontierBootstrap did not publish a FrontierRuntime within 30 s");
            Assert.AreEqual(0, runtime.Camera.cullingMask & (1 << 31),
                "Offscreen Hyperspace rig geometry must not render a second time through the world camera.");

            var destination = runtime.State.universe.Get(runtime.Db.planets[1].id);
            Assert.AreNotSame(runtime.State.planet, destination);
            runtime.State.DebugTeleport(destination);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreSame(destination, runtime.State.planet);
        }

        [UnityTest]
        public IEnumerator QualitySettings_UpdateLiveCamera_AndBoundHarvestAndTravelSurfaces()
        {
            bool hadQuality = PlayerPrefs.HasKey(PresentationQuality.QualityKey);
            bool hadFps = PlayerPrefs.HasKey(PresentationQuality.FrameRateKey);
            int quality = PlayerPrefs.GetInt(PresentationQuality.QualityKey), fps = PlayerPrefs.GetInt(PresentationQuality.FrameRateKey);
            int originalTarget = Application.targetFrameRate, originalVsync = QualitySettings.vSyncCount;
            MinigameStage stage = null;
            Hyperspace.View.HyperspaceScene travel = null;
            try
            {
                PresentationQuality.Preset = QualityPreset.Automatic;
                PresentationQuality.FrameRate = 0;
                new GameObject("QualityTestBootstrap").AddComponent<FrontierBootstrap>();
                float deadline = Time.realtimeSinceStartup + 30f;
                while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
                var runtime = FrontierRuntime.Current;
                Assert.IsNotNull(runtime);
                for (int i = 0; i < 10; i++) yield return null;
                runtime.Ui.DebugMenu("settings");
                var button = runtime.Ui.DebugRoot.Q<Button>("settings-quality");
                Assert.IsNotNull(button);
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
                Assert.AreEqual(QualityPreset.Low, PresentationQuality.Preset, "The real settings action should select Low after Automatic.");
                Assert.IsFalse(runtime.Camera.allowHDR, "Low must disable HDR/post cost on the live camera.");
                Assert.AreEqual(30, Application.targetFrameRate);

                var frameButton = runtime.Ui.DebugRoot.Q<Button>("settings-frame-rate");
                Assert.IsNotNull(frameButton);
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = frameButton; frameButton.SendEvent(submit); }
                Assert.AreEqual(30, PresentationQuality.FrameRate);
                frameButton = runtime.Ui.DebugRoot.Q<Button>("settings-frame-rate");
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = frameButton; frameButton.SendEvent(submit); }
                Assert.AreEqual(60, Application.targetFrameRate);

                stage = MinigameStage.Create(Color.black);
                Assert.IsTrue(stage.Resize(2340, 1080));
                Assert.AreEqual(768, stage.rt.width);
                Assert.LessOrEqual(stage.rt.height, 768);
                Assert.IsFalse(stage.cam.allowHDR);
                travel = new Hyperspace.View.HyperspaceScene(runtime.Camera.transform);
                Assert.LessOrEqual(travel.Texture.width, 768);
                Assert.LessOrEqual(travel.Texture.height, 768);
                Assert.IsTrue(travel.Texture.IsCreated());
                PresentationQuality.Preset = QualityPreset.High;
                Assert.IsTrue(runtime.Camera.allowHDR, "Changing back to High must re-enable the existing camera.");
                Assert.IsTrue(stage.Resize(2340, 1080), "An existing stage must recalculate its surface cap.");
                Assert.AreEqual(1280, stage.rt.width);
                runtime.Ui.DebugReset();
            }
            finally
            {
                travel?.Dispose();
                if (stage != null) Object.Destroy(stage.gameObject);
                if (hadQuality) PlayerPrefs.SetInt(PresentationQuality.QualityKey, quality);
                else PlayerPrefs.DeleteKey(PresentationQuality.QualityKey);
                if (hadFps) PlayerPrefs.SetInt(PresentationQuality.FrameRateKey, fps);
                else PlayerPrefs.DeleteKey(PresentationQuality.FrameRateKey);
                PlayerPrefs.Save();
                PresentationQuality.Initialize(FrontierSettings.I.targetFrameRate);
                Application.targetFrameRate = originalTarget;
                QualitySettings.vSyncCount = originalVsync;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstSessionCues_PointToLiveActions_AndRespectSkipAndAffordability()
        {
            new GameObject("GuideCueTestBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            for (int i = 0; i < 10; i++) yield return null;
            var lumber = rt.Db.StructureOfKind(StructureKind.Lumber);
            foreach (var cost in lumber.buildCost) rt.State.backpack.Set(cost.resource.id, cost.amount);
            rt.Ui.DebugFirstSessionGuideShop(0);
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("first-session-cue-0"));
            Assert.IsTrue(rt.Ui.DebugRoot.Q("first-session-cue-0").Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("guide.build.title")));
            Assert.AreEqual(1, rt.Ui.DebugRoot.Query(className: "fr-guide-cue").ToList().Count);

            rt.Ui.DebugReset();
            rt.State.DebugPlace(lumber, 0);
            yield return null;
            rt.Ui.DebugFirstSessionGuideAction(1);
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("first-session-cue-1"));

            rt.Ui.DebugReset();
            rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Refinery), 1);
            foreach (var resource in rt.Db.resources) rt.State.backpack.Set(resource.id, 100);
            yield return null;
            rt.Ui.DebugFirstSessionGuideAction(3);
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("first-session-cue-3"));
            Assert.AreEqual(1, rt.Ui.DebugRoot.Query(className: "fr-guide-cue").ToList().Count);

            rt.Ui.DebugReset();
            rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Alarm), 2);
            yield return null;
            rt.Ui.DebugFirstSessionGuideAction(4);
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("first-session-cue-4"));
            rt.State.backpack.Set(rt.Db.fuel.id, 0);
            rt.State.Notify();
            Assert.IsNull(rt.Ui.DebugRoot.Q("first-session-cue-4"), "Do not point at the disabled wave button.");

            rt.Ui.DebugReset();
            rt.State.DismissFirstSessionGuide();
            Assert.IsTrue(rt.Surface.SimulateTap(rt.State.StructureOfKind(rt.State.planet, StructureKind.Lumber)));
            Assert.IsNull(rt.Ui.DebugRoot.Q("first-session-cue-1"), "Skipping must remove contextual pointers too.");
            rt.Ui.DebugReset();
        }

        [UnityTest]
        public IEnumerator WorldActionsUseCatalogCopyAndPreserveCraftAndWaveCosts()
        {
            new GameObject("WorldActionCopyBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            rt.Ui.DebugEmptyPack();
            rt.Ui.DebugReset();
            var lumber = rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Lumber), 0);
            var refinery = rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Refinery), 1);
            var alarm = rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Alarm), 2);
            yield return null;
            Assert.IsTrue(rt.Surface.SimulateTap(lumber));
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("gather.title")));
            Assert.AreEqual(0, rt.State.SlotsUsed, "Opening a gatherer must not grant a harvest.");
            rt.Ui.DebugReset();

            RecipeSO recipe = null;
            foreach (var candidate in rt.Db.recipes)
                if (rt.State.RecipeAt(candidate, refinery) && rt.State.RecipeLockReason(candidate, refinery) == null)
                {
                    recipe = candidate;
                    break;
                }
            Assert.IsNotNull(recipe);
            foreach (var input in recipe.inputs) rt.State.backpack.Set(input.resource.id, input.amount);
            var expected = new Dictionary<ResourceSO, int>();
            foreach (var resource in rt.Db.resources) expected[resource] = rt.State.Count(resource);
            foreach (var input in recipe.inputs) expected[input.resource] -= input.amount;
            foreach (var output in recipe.outputs)
                expected[output.resource] += rt.State.OutputAmount(output, rt.State.StructureBonus(refinery));
            Assert.IsTrue(rt.Surface.SimulateTap(refinery));
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("craft.recipes")));
            var craftButton = rt.Ui.DebugRoot.Query<Button>().ToList()
                .Find(button => button.text == UiText.Get("craft.make-batch", 1));
            Assert.IsNotNull(craftButton);
            Assert.IsTrue(craftButton.enabledInHierarchy);
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = craftButton;
                craftButton.SendEvent(submit);
            }
            foreach (var pair in expected) Assert.AreEqual(pair.Value, rt.State.Count(pair.Key), pair.Key.id);
            rt.Ui.DebugReset();

            rt.State.backpack.Set(rt.Db.fuel.id, 3);
            Assert.IsTrue(rt.Surface.SimulateTap(alarm));
            var callLabel = rt.Ui.DebugRoot.Query<Label>().ToList()
                .Find(label => label.text == UiText.Get("wave.call"));
            Assert.IsNotNull(callLabel);
            var callButton = callLabel.GetFirstAncestorOfType<Button>();
            Assert.IsNotNull(callButton);
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = callButton;
                callButton.SendEvent(submit);
            }
            Assert.IsTrue(rt.Defense.Active);
            Assert.AreEqual(0, rt.State.Count(rt.Db.fuel), "Calling the previewed wave must spend its three fuel once.");
            yield return null;
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("wave.tap-help")));
        }

        [UnityTest]
        public IEnumerator SpecialtyStructurePopupsUseCatalogWithoutGrantingResources()
        {
            new GameObject("SpecialtyStructureCopyBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            var cases = new[]
            {
                (StructureKind.Terraformer, "structure.terraformer.subtitle"),
                (StructureKind.WarpGate, "structure.warp.subtitle"),
                (StructureKind.DysonCollector, "structure.dyson.subtitle"),
                (StructureKind.ResearchLab, "structure.lab.subtitle"),
                (StructureKind.ColonyHub, "structure.hub.subtitle"),
                (StructureKind.RadarTower, "structure.radar.subtitle"),
                (StructureKind.Hangar, "structure.hangar.subtitle"),
                (StructureKind.ShieldPylon, "structure.shield.subtitle"),
                (StructureKind.RepairBay, "structure.repair.subtitle"),
                (StructureKind.Gunner, "structure.turret.automatic"),
                (StructureKind.MissileSilo, "structure.turret.missile"),
                (StructureKind.TeslaCoil, "structure.turret.tesla")
            };
            for (int slot = 0; slot < cases.Length; slot++)
            {
                var structure = rt.State.DebugPlace(rt.Db.StructureOfKind(cases[slot].Item1), slot);
                Assert.IsNotNull(structure, cases[slot].Item1.ToString());
                yield return null;
                var before = new Dictionary<ResourceSO, int>();
                foreach (var resource in rt.Db.resources) before[resource] = rt.State.Count(resource);
                Assert.IsTrue(rt.Surface.SimulateTap(structure));
                Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                    .Exists(label => label.text == UiText.Get(cases[slot].Item2)), cases[slot].Item2);
                if (cases[slot].Item1 == StructureKind.ColonyHub)
                {
                    rt.State.DebugSetStructureLevel(structure, 10);
                    Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                        .Exists(label => label.text == UiText.Get("structure.hub.help", 45)));
                }
                foreach (var pair in before) Assert.AreEqual(pair.Value, rt.State.Count(pair.Key), pair.Key.id);
                rt.Ui.DebugReset();
            }
        }

        [UnityTest]
        public IEnumerator CrewPanelConfirmsFreeFinishAndKeepsTimerControlsStable()
        {
            new GameObject("CrewPanelBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            var definition = rt.Db.StructureOfKind(StructureKind.Refinery);
            int originalDuration = definition.Row(1).buildSeconds;
            try
            {
                definition.Row(1).buildSeconds = 20;
                foreach (var item in definition.buildCost) rt.State.backpack.Set(item.resource.id, item.amount);
                Assert.IsTrue(rt.State.Place(definition, 0));
                var structure = rt.State.StructureAt(rt.State.planet, 0);
                Assert.IsTrue(rt.State.IsBuilderBusy(structure));
                rt.Ui.DebugResearch(3);
                string buttonName = "builder-finish-" + structure.planetId + "-" + structure.slot;
                var finish = rt.Ui.DebugRoot.Q<Button>(buttonName);
                Assert.IsNotNull(finish);
                Assert.IsTrue(finish.enabledInHierarchy);
                Assert.IsTrue(finish.Query<Label>().ToList().Exists(label => label.text == UiText.Get("builders.finish-free")));
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.AreSame(finish, rt.Ui.DebugRoot.Q<Button>(buttonName),
                    "Timer refresh must not replace a control while a finger is on it.");
                using (var submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = finish;
                    finish.SendEvent(submit);
                }
                Assert.IsTrue(rt.State.IsBuilderBusy(structure), "The first press only confirms the price.");
                finish = rt.Ui.DebugRoot.Q<Button>(buttonName);
                Assert.IsTrue(finish.Query<Label>().ToList().Exists(label => label.text == UiText.Get("builders.confirm-free")));
                using (var submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = finish;
                    finish.SendEvent(submit);
                }
                Assert.IsFalse(rt.State.IsBuilderBusy(structure));
                Assert.AreEqual(0, rt.State.BusyBuilderCount);
                Assert.IsNull(rt.Ui.DebugRoot.Q<Button>(buttonName));
                Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get("builders.empty")));
                Assert.IsTrue(rt.Ui.HandleBack());
                Assert.IsNull(rt.Ui.DebugRoot.Q("builder-jobs"));
            }
            finally { definition.Row(1).buildSeconds = originalDuration; }
        }

        [UnityTest]
        public IEnumerator CrewRecruitmentRequiresConfirmationAndRefreshesToTheSharedCap()
        {
            new GameObject("CrewRecruitmentBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            Assert.IsTrue(rt.State.TryCreateCloudSaveSnapshot(out var snapshot, out var error), error);
            snapshot.player.researched.Add("field_engineering");
            snapshot.wallet.Add(new WalletEntry { currency = "shards", amount = 10000 });
            Assert.IsTrue(rt.State.TryRestoreCloudSave(snapshot, out error), error);
            Assert.AreEqual(2, rt.State.BuilderCount);
            rt.Ui.DebugResearch(3);
            int balance = rt.State.ShardBalance;
            for (int expected = 3; expected <= 4; expected++)
            {
                int price = rt.State.NextBuilderPurchaseCost;
                var buy = rt.Ui.DebugRoot.Q<Button>("builder-recruit");
                Assert.IsNotNull(buy);
                Assert.IsTrue(buy.enabledInHierarchy);
                using (var submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = buy;
                    buy.SendEvent(submit);
                }
                Assert.AreEqual(expected - 1, rt.State.BuilderCount);
                Assert.AreEqual(balance, rt.State.ShardBalance);
                buy = rt.Ui.DebugRoot.Q<Button>("builder-recruit");
                Assert.IsTrue(buy.Query<Label>().ToList()
                    .Exists(label => label.text == UiText.Get("builders.recruit-confirm", price)));
                using (var submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = buy;
                    buy.SendEvent(submit);
                }
                balance -= price;
                Assert.AreEqual(expected, rt.State.BuilderCount);
                Assert.AreEqual(balance, rt.State.ShardBalance);
                yield return null;
            }
            Assert.IsNull(rt.Ui.DebugRoot.Q<Button>("builder-recruit"));
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("builders.cap-reached")));
        }

        [UnityTest]
        public IEnumerator EmptyBackpack_OffersBuildOrLiveHarvest_WithoutGrantingResources()
        {
            new GameObject("EmptyPackTestBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            for (int i = 0; i < 10; i++) yield return null;
            rt.Ui.DebugEmptyPack();
            var empty = rt.Ui.DebugRoot.Q<Button>("empty-pack-action");
            Assert.IsNotNull(empty);
            Assert.IsTrue(empty.enabledSelf);
            Assert.AreEqual(0, rt.State.SlotsUsed);
            rt.Ui.DebugEmptyPackAction();
            Assert.IsNotNull(rt.Ui.DebugRoot.Q(className: "fr-tabs"), "No gatherer should lead to the build shop.");
            Assert.AreEqual(0, rt.State.SlotsUsed);

            rt.Ui.DebugReset();
            rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Lumber), 0);
            yield return null;
            rt.Ui.DebugEmptyPack();
            rt.Ui.DebugEmptyPackAction();
            Assert.IsNotNull(rt.Ui.DebugRoot.Q(className: "fr-popup"), "An operational local gatherer should open directly.");
            Assert.AreEqual(0, rt.State.SlotsUsed, "Navigation must not grant an unearned harvest.");
            rt.Ui.DebugReset();
        }

        [UnityTest]
        public IEnumerator EmptyBaseStorageAndRelay_ExplainTheNextLocalAction()
        {
            new GameObject("EmptyLogisticsTestBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            for (int i = 0; i < 10; i++) yield return null;
            rt.Ui.DebugResearch(0);
            Assert.IsNotNull(rt.Ui.DebugRoot.Q<Button>("empty-base-action"));

            rt.Ui.DebugEmptyPack();
            rt.Ui.DebugReset();
            var warehouse = rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Warehouse), 0);
            yield return null;
            Assert.IsTrue(rt.Surface.SimulateTap(warehouse));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("empty-storage-hint"));
            Assert.AreEqual(UiText.Get("storage.empty.both"), rt.Ui.DebugRoot.Q<Label>("empty-storage-hint").text);
            Assert.IsNotNull(rt.Ui.DebugRoot.Q<Button>("empty-storage-action"));
            var filter = rt.Ui.DebugRoot.Q<DropdownField>();
            Assert.AreEqual(UiText.Get("storage.filter.all"), filter.choices[0]);
            Assert.AreEqual(6, filter.choices.Count);
            rt.Ui.DebugReset();
            var relay = rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Satellite), 1);
            yield return null;
            Assert.IsTrue(rt.Surface.SimulateTap(relay));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q<Button>("empty-relay-action"));
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get("relay.empty")));
            Assert.AreEqual(0, rt.State.SlotsUsed);
            rt.Ui.DebugReset();
        }

        [UnityTest]
        public IEnumerator StorageQuickActionsPreserveResourcesAndUseCatalogMessages()
        {
            new GameObject("StorageCopyTestBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            rt.Ui.DebugEmptyPack();
            rt.Ui.DebugReset();
            var wood = rt.Db.FindResource("wood");
            rt.State.backpack.Set(wood.id, 20);
            var warehouse = rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Warehouse), 0);
            yield return null;
            Assert.IsTrue(rt.Surface.SimulateTap(warehouse));

            void SubmitAction(string key)
            {
                var label = rt.Ui.DebugRoot.Query<Label>().ToList().Find(item => item.text == UiText.Get(key));
                Assert.IsNotNull(label, key);
                var button = label.GetFirstAncestorOfType<Button>();
                Assert.IsNotNull(button, key);
                using (var submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = button;
                    button.SendEvent(submit);
                }
            }

            SubmitAction("storage.quick-store");
            Assert.AreEqual(0, rt.State.Count(wood));
            Assert.AreEqual(20, rt.State.StorageFor(warehouse).slots[0].count);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("storage.quick-stored", 20)));
            SubmitAction("storage.quick-withdraw");
            Assert.AreEqual(20, rt.State.Count(wood));
            Assert.AreEqual(0, rt.State.StorageFor(warehouse).slots[0].count);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("storage.quick-withdrew", 20)));
            SubmitAction("storage.quick-withdraw");
            Assert.AreEqual(20, rt.State.Count(wood));
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("storage.quick-withdraw-empty")));
            rt.Ui.DebugReset();
        }

        [UnityTest]
        public IEnumerator Map_UsesLatestSelection_RevalidatesFuel_AndSearchesFarCoordinates()
        {
            new GameObject("MapTestBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            for (int i = 0; i < 10; i++) yield return null;
            long originalFuel = rt.State.FuelOnBoard.Energy;
            int originalShards = rt.State.ShardBalance;
            string originalPlanet = rt.State.planet.Id;
            rt.Ui.DebugMap(rt.State.universe.Get("nyx"), 1);
            yield return null;
            var target = rt.State.universe.Get("glacia");
            rt.Ui.DebugMap(target, 12);
            deadline = Time.realtimeSinceStartup + 15;
            while ((rt.Ui.DebugMapPreview == null || rt.Ui.DebugMapSystemCount == 0) && Time.realtimeSinceStartup < deadline) yield return null;
            var preview = rt.Ui.DebugMapPreview;
            Assert.IsNotNull(preview, "the worker never delivered the latest route");
            Assert.AreEqual(target.Id, preview.toPlanetId, "a stale Nyx result must not overwrite Glacia");
            Assert.IsTrue(preview.intraSystem);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("map.price.manual", preview.totalFuel, originalFuel, preview.totalSeconds)));
            Assert.AreEqual(UiText.Get("map.pin"), rt.Ui.DebugRoot.Q<Button>("map-pin-toggle").text);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Toggle>().ToList()
                .Exists(toggle => toggle.label == UiText.Get("map.autopilot.help")));
            int manualPrice = preview.totalFuel;
            rt.Ui.DebugMapAutopilot();
            deadline = Time.realtimeSinceStartup + 10;
            while (rt.Ui.DebugMapPreview == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(rt.Ui.DebugMapPreview.totalFuel, manualPrice);
            Assert.AreEqual(12, rt.Ui.DebugMapPreview.totalSeconds);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("map.price.autopilot",
                    rt.Ui.DebugMapPreview.totalFuel, originalFuel, rt.Ui.DebugMapPreview.totalSeconds)));
            Assert.AreEqual(originalFuel, rt.State.FuelOnBoard.Energy);
            Assert.AreEqual(originalShards, rt.State.ShardBalance);
            Assert.AreEqual(originalPlanet, rt.State.planet.Id, "Previewing a destination cannot travel there.");
            rt.State.backpack.Set(rt.Db.fuel.id, 0);
            rt.State.Notify();
            deadline = Time.realtimeSinceStartup + 10;
            while (rt.Ui.DebugMapPreview == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(rt.Ui.DebugMapPreview.Has(RouteReasons.NotEnoughFuel));
            rt.Ui.DebugMapSearch("");
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("map.search.empty")));
            rt.Ui.DebugMapSearch("12000,-3000");
            Assert.AreEqual(12000, rt.Ui.DebugMapCenterX, 0.001);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("map.search.coordinates")));
            rt.Ui.DebugMapSearch("not-a-known-system-or-world");
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("map.search.not-found")));
            Assert.AreEqual(12000, rt.Ui.DebugMapCenterX, 0.001, "Failed searches cannot move the map.");
            rt.Ui.DebugReset();
            yield return null;
        }

        sealed class ScriptedFlight : ITravelMinigame
        {
            public readonly List<TravelRunRequest> requests = new List<TravelRunRequest>();
            public void Run(UnityEngine.UIElements.VisualElement host, TravelRunRequest request, Action<TravelRunResult> done)
            {
                requests.Add(request);
                done(new TravelRunResult { completed = request.legIndex == 0, quality = 1 });
            }
        }

        static void GiveFullHazardPlating(GameState state)
        {
            foreach (string upgrade in new[] { "hull_heat", "hull_cold", "hull_toxic", "hull_rad", "hull_void" })
                state.DebugSetShipLevel(upgrade, 3);
        }

        [UnityTest]
        public IEnumerator ColdStartupResumesTheExactHyperspaceRunPausedAndSettlesOnce()
        {
            var db = GameDatabase.Load();
            var state = new GameState(db, false);
            foreach (var upgrade in db.upgrades) state.DebugSetUpgrade(upgrade, upgrade.MaxLevel);
            state.backpack.Set(db.fuel.id, 100);
            var target = state.universe.Get("glacia");
            var plan = state.PlanRoute(target);
            Assert.IsTrue(plan.reachable);
            Assert.AreEqual(1, plan.LegCount);
            Assert.IsTrue(state.BeginTravelCheckpoint(plan, out string error), error);
            var checkpoint = state.PendingTravel.legs[0];
            Assert.IsTrue(SeedText.TryParse(checkpoint.seed, out ulong seed));
            var request = new TravelRunRequest { seed = seed, distanceLy = plan.legs[0].lengthLy,
                seconds = plan.legs[0].seconds, danger = target.Danger, tier = target.Tier };
            var tuning = new Frontier.Hyperspace.HyperspaceTuning();
            var ship = Frontier.Hyperspace.View.ShipStatsMapper.Map(Frontier.Hyperspace.View.ShipFamiliesFromGame.Read(state));
            var run = Frontier.Hyperspace.View.HyperspaceSetup.CreateRun(request, ship, tuning, false);
            checkpoint.replay = Frontier.Hyperspace.RunReplay.Attach(run, JsonUtility.ToJson(tuning));
            checkpoint.hasReplay = true;
            checkpoint.freeRevives = Mathf.Max(0, FrontierSettings.I.freeRevivesPerRun);
            for (int i = 0; i < 300; i++) run.Step(new Frontier.Hyperspace.PlayerInput(1.25f, -0.5f, false));
            Assert.AreEqual(Frontier.Hyperspace.RunState.Running, run.State);
            ulong digest = run.StateDigest();
            int fuel = state.Count(db.fuel), xp = state.xp, shards = state.ShardBalance;
            string origin = state.planet.Id;
            Assert.IsTrue(state.Save());
            new GameObject("ColdTravelBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            Frontier.Hyperspace.View.HyperspaceSession session = null;
            while (Time.realtimeSinceStartup < deadline && session == null)
            {
                session = Object.FindFirstObjectByType<Frontier.Hyperspace.View.HyperspaceSession>();
                yield return null;
            }
            var runtime = FrontierRuntime.Current;
            Assert.IsNotNull(runtime);
            Assert.IsNotNull(session, "Startup must reopen the saved leg through the real UI travel flow.");
            session.DebugFrozen = true;
            Assert.IsTrue(session.Paused, "Restoration must not restart combat before the player resumes.");
            Assert.AreEqual(digest, session.Run.StateDigest());
            Assert.AreEqual(origin, runtime.State.planet.Id);
            Assert.AreEqual(fuel, runtime.State.Count(db.fuel));
            Assert.AreEqual(xp, runtime.State.xp);
            Assert.AreEqual(shards, runtime.State.ShardBalance);
            Assert.IsTrue(runtime.Ui.IsTravelling);
            session.Resume();
            session.FinishTrip();
            yield return null;
            session.Close();
            deadline = Time.realtimeSinceStartup + 15;
            while (runtime.Ui.DebugBusy && Time.realtimeSinceStartup < deadline)
            {
                runtime.Ui.HandleBack();
                yield return null;
            }
            Assert.IsFalse(runtime.Ui.DebugBusy);
            Assert.AreEqual(target.Id, runtime.State.planet.Id);
            Assert.IsNull(runtime.State.PendingTravel);
            Assert.AreEqual(fuel - plan.totalFuel, runtime.State.Count(db.fuel));
            var restarted = new GameState(db, true);
            Assert.IsNull(restarted.PendingTravel);
            Assert.AreEqual(target.Id, restarted.planet.Id);
            Assert.AreEqual(runtime.State.Count(db.fuel), restarted.Count(db.fuel));
            Assert.AreEqual(xp, restarted.xp, "Finishing an unfinished leg must not grant run XP.");
        }

        [UnityTest]
        public IEnumerator InvalidInterruptedJourneyBlocksPlayUntilExplicitDiscard()
        {
            var db = GameDatabase.Load();
            var seedState = new GameState(db, false);
            foreach (var upgrade in db.upgrades) seedState.DebugSetUpgrade(upgrade, upgrade.MaxLevel);
            seedState.backpack.Set(db.fuel.id, 100);
            Assert.IsTrue(seedState.BeginTravelCheckpoint(seedState.PlanRoute(seedState.universe.Get("glacia")), out _));
            seedState.PendingTravel.version++;
            string origin = seedState.planet.Id;
            int fuel = seedState.Count(db.fuel), xp = seedState.xp;
            Assert.IsTrue(seedState.Save());
            new GameObject("InvalidTravelBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var runtime = FrontierRuntime.Current;
            Assert.IsNotNull(runtime);
            Assert.IsNotNull(runtime.State.PendingTravel);
            Assert.IsTrue(runtime.Ui.DebugBusy);
            var discard = runtime.Ui.DebugRoot.Q<Button>("travel-recovery-discard");
            Assert.IsNotNull(discard);
            Assert.IsNotNull(runtime.Ui.DebugRoot.Q("travel-recovery-error"));
            Assert.IsNull(Object.FindFirstObjectByType<Frontier.Hyperspace.View.HyperspaceSession>());
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = discard; discard.SendEvent(submit); }
            Assert.IsNull(runtime.Ui.DebugRoot.Q("travel-recovery-error"));
            Assert.IsFalse(runtime.Ui.DebugBusy);
            Assert.IsNull(runtime.State.PendingTravel);
            var restarted = new GameState(db, true);
            Assert.IsNull(restarted.PendingTravel);
            Assert.AreEqual(origin, restarted.planet.Id);
            Assert.AreEqual(fuel, restarted.Count(db.fuel));
            Assert.AreEqual(xp, restarted.xp);
        }

        [UnityTest]
        public IEnumerator TravelFlow_FliesEachLeg_PaysOnce_AndRefundsOnlyCompletedManualLegs()
        {
            new GameObject("TravelTestBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            for (int i = 0; i < 10; i++) yield return null;
            GiveFullHazardPlating(rt.State);
            rt.State.DebugSetUpgrade(rt.Db.FindUpgrade("pack"), 2);
            rt.State.backpack.Set(rt.Db.fuel.id, 200);
            var target = rt.State.universe.Get("cinder");
            var plan = rt.State.PlanRoute(target);
            Assert.IsTrue(plan.reachable);
            Assert.Greater(plan.LegCount, 1);
            var flight = new ScriptedFlight();
            var oldFactory = TravelMinigames.Create;
            try
            {
                TravelMinigames.Create = () => flight;
                rt.Ui.DebugStartTravel(target);
                Assert.IsTrue(rt.Ui.IsTravelling);
                var skipDeparture = rt.Ui.DebugRoot.Q<Button>("travel-skip-cinematic");
                Assert.IsNotNull(skipDeparture);
                Assert.AreEqual(200, rt.State.Count(rt.Db.fuel), "Skipping a visual must not pay for or grant a trip.");
                using (var submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = skipDeparture;
                    skipDeparture.SendEvent(submit);
                }
                deadline = Time.realtimeSinceStartup + 25;
                int arrivalSkips = 0;
                while (rt.Ui.DebugBusy && Time.realtimeSinceStartup < deadline)
                {
                    var skipArrival = rt.Ui.DebugRoot.Q<Button>("travel-skip-cinematic");
                    if (skipArrival != null && skipArrival != skipDeparture)
                    {
                        arrivalSkips++;
                        Assert.IsTrue(rt.Ui.HandleBack(), "Back skips only the arrival presentation.");
                    }
                    yield return null;
                }
                Assert.IsFalse(rt.Ui.DebugBusy, "travel did not finish");
                Assert.IsFalse(rt.Ui.IsTravelling);
                Assert.AreEqual(1, arrivalSkips);
                Assert.IsNull(rt.Ui.DebugRoot.Q<Button>("travel-skip-cinematic"));
                yield return null;
                Assert.IsNull(rt.Ui.GetComponentInChildren<ShipRig>());
                Assert.AreEqual(plan.LegCount, flight.requests.Count);
                Assert.AreEqual(target.Id, rt.State.planet.Id);
                for (int i = 0; i < plan.LegCount; i++)
                {
                    Assert.AreEqual(i, flight.requests[i].legIndex);
                    Assert.AreEqual(plan.LegCount, flight.requests[i].legCount);
                    Assert.AreEqual(plan.legs[i].toSystemId, flight.requests[i].toSystemId);
                    Assert.AreEqual(plan.legs[i].lengthLy, flight.requests[i].distanceLy);
                }
                int refund = rt.State.Rules.Refund(plan.legs[0].fuel, 1, false);
                Assert.AreEqual(200 - plan.totalFuel + refund, rt.State.Count(rt.Db.fuel));
                Assert.AreEqual(1, rt.State.world.Visits(target.Id));
                Assert.IsFalse(rt.State.saves.IsDirty, "arrival and its refund must persist together");
            }
            finally { TravelMinigames.Create = oldFactory; }
        }

        [UnityTest]
        public IEnumerator TravelFlow_AutopilotSkipsTheMinigame_AndPaysWithoutRefund()
        {
            new GameObject("AutoTestBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            for (int i = 0; i < 10; i++) yield return null;
            GiveFullHazardPlating(rt.State);
            rt.State.backpack.Set(rt.Db.fuel.id, 100);
            var target = rt.State.universe.Get("glacia");
            var plan = rt.State.PlanRoute(target, true);
            var flight = new ScriptedFlight();
            var oldFactory = TravelMinigames.Create;
            float start = Time.realtimeSinceStartup;
            try
            {
                TravelMinigames.Create = () => flight;
                rt.Ui.DebugStartTravel(target, true);
                Assert.IsTrue(rt.Ui.IsTravelling, "Autopilot also owns the travel soundscape.");
                deadline = start + 30;
                while (rt.Ui.DebugBusy && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsFalse(rt.Ui.DebugBusy);
                Assert.IsFalse(rt.Ui.IsTravelling);
                Assert.AreEqual(target.Id, rt.State.planet.Id);
                Assert.AreEqual(0, flight.requests.Count);
                Assert.AreEqual(100 - plan.totalFuel, rt.State.Count(rt.Db.fuel));
                Assert.GreaterOrEqual(Time.realtimeSinceStartup - start, plan.totalSeconds);
            }
            finally { TravelMinigames.Create = oldFactory; }
        }

        [UnityTest]
        public IEnumerator TheAppComponent_WritesTheDebouncedSave_AfterAQuietSecond()
        {
            var host = new GameObject("TestBootstrap");
            host.AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var runtime = FrontierRuntime.Current;
            Assert.IsNotNull(runtime, "FrontierBootstrap did not publish a FrontierRuntime within 30 s");

            string file = Path.Combine(saveDir, "frontier_save.json");
            runtime.State.Flush();
            File.Delete(file);
            runtime.State.AddXp(3);
            yield return null;
            Assert.IsFalse(File.Exists(file), "nothing is written in the frame of the change");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.IsTrue(File.Exists(file), "the app component flushes after about a second of quiet");
            Assert.IsFalse(runtime.State.saves.IsDirty);
        }

        // Every biome, size class, ring and moon flag and every model key the generator can produce must build a planet and its surface
        // without errors (a Debug.LogError fails the test), and a generated world has the same on-screen size as a hand made one.
        [UnityTest]
        public IEnumerator GeneratedWorlds_OfEveryKind_Render()
        {
            var host = new GameObject("TestBootstrap");
            host.AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var runtime = FrontierRuntime.Current;
            Assert.IsNotNull(runtime, "FrontierBootstrap did not publish a FrontierRuntime within 30 s");

            var universe = runtime.State.universe;
            var worlds = new List<PlanetData>();
            var ids = new HashSet<string>();
            Action<PlanetData> add = p => { if (p != null && ids.Add(p.Id)) worlds.Add(p); };
            foreach (Frontier.Universe.Biome biome in Enum.GetValues(typeof(Frontier.Universe.Biome)))
                for (int skip = 0; skip < 2; skip++) add(universe.FindGenerated(biome, null, 0, skip, 3000));
            foreach (Frontier.Universe.SizeClass size in Enum.GetValues(typeof(Frontier.Universe.SizeClass))) add(universe.FindGenerated(null, size, 0, 0, 3000));
            add(universe.FindGenerated(d => d.HasRings, 0, 0, 3000));
            add(universe.FindGenerated(d => d.HasRings && d.Biome == Frontier.Universe.Biome.GasGiant, 0, 0, 3000));
            add(universe.FindGenerated(d => d.MoonCount >= 5, 0, 0, 3000));
            add(universe.FindGenerated(d => d.Weather == Frontier.Universe.Weather.Storm, 0, 0, 3000));
            foreach (string key in universe.Universe.Config.ModelKeys) add(universe.FindGenerated(d => d.ModelKey == key, 0, 0, 3000));
            foreach (var authored in universe.AuthoredPlanets) add(authored);
            Assert.GreaterOrEqual(worlds.Count, 40, "the sample must cover many kinds of world");

            int withRings = 0, withMoons = 0;
            foreach (var world in worlds)
            {
                runtime.State.DebugTeleport(world);
                runtime.Planet.Show(world, false);
                yield return null;
                Assert.AreEqual(world.Id, runtime.Planet.Current.Id);
                Assert.AreEqual(world.SurfaceRadius, runtime.Planet.SurfaceRadius(), 1e-4f, world.Id + ": the ground collider follows the world");
                if (!world.IsAuthored)
                    Assert.AreEqual(PlanetSO.StandardSurfaceRadius * ModelLibrary.GroundScale(world.ModelKey), runtime.Planet.SurfaceRadius(), 1e-4f, world.Id + ": standard radius scaled by the model's ground scale");
                Assert.AreEqual(world.BuildingSlots, runtime.Surface.SlotCount, world.Id);
                Assert.AreEqual(1f / world.WorldSize, world.StructureScale, 1e-5f);
                Assert.IsNotNull(runtime.Planet.Spin.Find("Core"), world.Id + ": the planet body was not built");
                int moons = 0;
                foreach (Transform child in runtime.Planet.Root) if (child.name == "MoonPivot") moons++;
                Assert.AreEqual(world.MoonCount, moons, world.Id + ": moons");
                if (world.HasRings) withRings++;
                if (moons > 0) withMoons++;
            }
            Assert.Greater(withRings, 2);
            Assert.Greater(withMoons, 10);

            // the star map and the debug page must cope with a generated current world, too
            var generated = universe.FindGenerated(Frontier.Universe.Biome.Oceanic, null, 0, 0, 3000);
            runtime.State.DebugTeleport(generated);
            runtime.Planet.Show(generated, false);
            yield return null;
            runtime.Ui.DebugMap(null, 0.5f);
            yield return null;
            runtime.Ui.DebugMenu("debug", true);
            yield return null;
            runtime.Ui.DebugReset();
            yield return null;
        }

        // The state moves to the destination before the planet view shows it. A destination with buildings on pads beyond the previous
        // world's pad count used to throw an IndexOutOfRangeException from the state's change event in between (a trip from a small world
        // to a big one the player had built on); any exception here fails the test.
        [UnityTest]
        public IEnumerator Travelling_ToABigWorldWithBuildingsOnHighPads_KeepsTheSurfaceConsistent()
        {
            var host = new GameObject("TestBootstrap");
            host.AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var runtime = FrontierRuntime.Current;
            Assert.IsNotNull(runtime, "FrontierBootstrap did not publish a FrontierRuntime within 30 s");

            var gs = runtime.State;
            var home = gs.planet;
            var big = gs.universe.FindGenerated(null, Frontier.Universe.SizeClass.Colossal, 0, 0, 3000);
            Assert.IsNotNull(big, "the universe must offer a colossal world near home");
            Assert.Greater(big.BuildingSlots, home.BuildingSlots);
            var gunner = runtime.Db.StructureOfKind(StructureKind.Gunner);

            gs.DebugTeleport(big);
            runtime.Planet.Show(big, false);
            yield return null;
            int highPad = big.BuildingSlots - 1;
            Assert.IsNotNull(gs.DebugPlace(gunner, highPad));
            yield return null;
            Assert.AreEqual(1, runtime.Surface.TurretAnchors().Count);

            gs.DebugTeleport(home);
            runtime.Planet.Show(home, false);
            yield return null;
            Assert.AreEqual(0, runtime.Surface.TurretAnchors().Count);

            gs.DebugTeleport(big);
            yield return null;
            Assert.AreEqual(0, runtime.Surface.TurretAnchors().Count, "the surface keeps showing the world the planet view shows until it is told otherwise");
            runtime.Planet.Show(big, false);
            yield return null;
            var anchors = runtime.Surface.TurretAnchors();
            Assert.AreEqual(1, anchors.Count);
            Assert.AreEqual(highPad, anchors[0].Key.slot);
        }
    }
}
