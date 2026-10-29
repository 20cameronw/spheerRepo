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

        [UnityTest]
        public IEnumerator MissingResourceChipsRouteToBuildProducerAndPlanetAndExplainServiceAvailability()
        {
            new GameObject("ResourceSourceBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            yield return null;
            var skip = rt.Ui.DebugRoot.Q<Button>("intro-skip");
            if (skip != null)
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = skip; skip.SendEvent(submit); }
            for (int i = 0; i < 240 && rt.Ui.DebugBusy; i++) yield return null;
            Assert.IsFalse(rt.Ui.DebugBusy);
            var wood = rt.Db.FindResource("wood");
            rt.State.backpack.Clear();
            rt.Ui.DebugShop(0);
            yield return null;
            var chip = rt.Ui.DebugRoot.Q<Button>("find-resource-" + wood.id);
            Assert.IsNotNull(chip, "Economy shop costs should expose the real missing-resource control.");
            Assert.IsNotNull(chip.Q<Label>());
            var origin = chip.GetFirstAncestorOfType<FrontierModal>();
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = chip; chip.SendEvent(submit); }
            yield return null;
            var overlay = rt.Ui.DebugRoot.Q("shortage-overlay");
            Assert.IsNotNull(overlay);
            Assert.AreSame(origin, rt.Ui.DebugRoot.Q<FrontierModal>(), "The originating panel stays underneath the overlay.");
            var sheet = overlay.Q(className: "fr-shortage");
            Assert.Less(sheet.resolvedStyle.width, origin.resolvedStyle.width, "Resource help is narrower than a normal panel.");
            var actions = overlay.Q("shortage-actions");
            Assert.AreEqual(1, overlay.Query(className: "fr-shortage-actions").ToList().Count,
                "Source and manual actions share a single horizontal strip.");
            Assert.AreEqual(FlexDirection.Row, actions.resolvedStyle.flexDirection);
            var sourceAction = actions.Q<Button>("shortage-build-producer");
            var manualAction = actions.Q<Button>("shortage-manual");
            Assert.IsNotNull(sourceAction);
            Assert.IsNotNull(manualAction);
            Assert.That(manualAction.layout.y, Is.EqualTo(sourceAction.layout.y).Within(1f));
            Assert.Greater(manualAction.layout.x, sourceAction.layout.x);
            foreach (var button in overlay.Query<Button>().ToList())
            {
                Assert.AreEqual(0, button.Query<Label>().ToList().Count, "Shortage actions use icons, not full text rows.");
                Assert.IsFalse(string.IsNullOrEmpty(button.tooltip), "Every icon keeps a localized action label.");
            }
            Assert.IsTrue(rt.Ui.HandleBack());
            Assert.IsNull(rt.Ui.DebugRoot.Q("shortage-overlay"));
            Assert.AreSame(origin, rt.Ui.DebugRoot.Q<FrontierModal>(), "Back dismisses only the resource overlay.");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = chip; chip.SendEvent(submit); }
            yield return null;
            var produce = rt.Ui.DebugRoot.Q<Button>("shortage-build-producer");
            Assert.IsNotNull(produce);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = produce; produce.SendEvent(submit); }
            yield return null;
            var source = rt.State.FindResourceSource(wood);
            Assert.AreEqual(ResourceSourceKind.Build, source.kind);
            var buildCard = rt.Ui.DebugRoot.Q("shop-structure-" + source.build.id);
            Assert.IsNotNull(buildCard);
            var shopScroll = buildCard.GetFirstAncestorOfType<ScrollView>();
            Assert.IsNotNull(shopScroll);
            yield return null;
            Assert.LessOrEqual(buildCard.worldBound.yMin, shopScroll.contentViewport.worldBound.yMax,
                "The required structure's shop card must scroll into view.");
            rt.State.DebugPlace(source.build, 0);
            yield return null;
            rt.Ui.DebugResourceSource(wood);
            yield return null;
            Assert.IsNotNull(rt.Ui.DebugRoot.Q(className: "fr-popup"));
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label =>
                label.text == UiText.Get("structure.header", source.build.LocalizedName.ToUpper(), 1)));
            Assert.AreEqual(0, rt.State.PlanetCount(wood), "Finding materials cannot grant them.");

            var station = rt.Db.StructureOfKind(StructureKind.Refinery);
            rt.State.DebugPlace(station, 1);
            yield return null;
            RecipeSO recipe = null;
            foreach (var candidate in rt.Db.recipes)
                if (candidate.stationId == station.id) { recipe = candidate; break; }
            Assert.IsNotNull(recipe);
            var processed = rt.State.FindResourceSource(recipe.outputs[0].resource);
            Assert.AreEqual(ResourceSourceKind.Structure, processed.kind);
            rt.Ui.DebugResourceSource(recipe.outputs[0].resource);
            yield return null;
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("recipe-" + processed.recipe.id));

            ResourceSource remote = null;
            rt.State.RecordMapDiscoveries(new List<PlanetData>(rt.State.universe.AuthoredPlanets).ConvertAll(p => p.Star));
            foreach (var resource in rt.Db.resources)
            {
                var candidate = rt.State.FindResourceSource(resource);
                if (candidate.kind == ResourceSourceKind.Planet) { remote = candidate; break; }
            }
            Assert.IsNotNull(remote);
            var home = rt.State.planet.Id;
            rt.Ui.DebugResourceSource(remote.resource);
            yield return null;
            Assert.AreEqual(remote.planet.Id, rt.Ui.DebugMapSelectedId);
            Assert.AreEqual(home, rt.State.planet.Id, "Guidance cannot teleport the player.");

            rt.Ui.DebugReset();
            rt.Ui.DebugMenu("debug");
            yield return null;
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("service-diagnostics"));
            var testSuite = rt.Ui.DebugRoot.Q<Button>("debug-ad-test-suite");
            Assert.IsNotNull(testSuite, "Ad diagnostics must be discoverable even in the Editor.");
            Assert.IsFalse(testSuite.enabledSelf, "The Editor cannot show native iOS ads.");
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label =>
                label.text == UiText.Get("service.ads.test-editor")));
            rt.Ui.DebugReset();
            rt.Ui.DebugMenu("leaderboard");
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("leaderboard-enable"),
                "The dedicated leaderboard page must preserve explicit online-services opt-in.");
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
        public IEnumerator SurfaceViewShowsStructuresAndDebrisWithoutResourceObjectsOrMovingSlots()
        {
            new GameObject("SurfaceInteractionBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            yield return null;
            var skip = rt.Ui.DebugRoot.Q<Button>("intro-skip");
            if (skip != null)
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = skip; skip.SendEvent(submit); }
            for (int i = 0; i < 240 && rt.Ui.DebugBusy; i++) yield return null;
            Assert.IsFalse(rt.Ui.DebugBusy);
            rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Lumber), 0);
            rt.Ui.DebugReset();
            yield return null;
            var lumber = rt.State.StructureOfKind(rt.State.planet, StructureKind.Lumber);
            var structureAnchor = rt.Planet.Spin.Find("Struct_" + lumber.def.id);
            Assert.IsNotNull(structureAnchor);
            Vector3 anchor = structureAnchor.localPosition;
            var visualRoot = structureAnchor.Find("StructureVisualRoot");
            Assert.AreEqual(-0.045f, visualRoot.localPosition.y, 0.0001f, "Do not move the building.");
            var model = visualRoot.GetChild(0);
            Assert.AreEqual(0f, model.localPosition.y, 0.001f, "Foundation depth must not lift the model.");
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(-0.36f, mesh.bounds.min.y, 0.0001f, "The extra foundation extends below its original origin.");
            Vector3 overview = rt.Camera.transform.position;
            var toggle = rt.Ui.DebugRoot.Q<Button>("world-view-toggle");
            Assert.IsEmpty(toggle.text, "The world view switch should be an icon, not a text button.");
            Assert.IsNotNull(toggle.Q(className: "fr-icon"));
            Assert.AreEqual(UiText.Get("world.view.close"), toggle.tooltip);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = toggle; toggle.SendEvent(submit); }
            yield return null;
            yield return null;
            Assert.IsTrue(rt.Planet.CloseView);
            Assert.Greater(Mathf.Abs(rt.Camera.transform.position.z), Mathf.Abs(overview.z) * 0.5f,
                "Entering surface view must animate instead of snapping to the destination.");
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.Less(Mathf.Abs(rt.Camera.transform.position.z), Mathf.Abs(overview.z) * 0.5f);
            Assert.AreEqual(UiText.Get("world.view.orbit"), toggle.tooltip);
            Assert.AreEqual(anchor, structureAnchor.localPosition, "Saved building slots must not move when resource visuals are removed.");
            foreach (var t in rt.Planet.Spin.GetComponentsInChildren<Transform>(true))
            {
                Assert.IsFalse(t.name.StartsWith("Gather_", StringComparison.Ordinal));
                Assert.AreNotEqual("ResourceSeam", t.name);
            }
            var resource = rt.State.planet.SpotsFor(MinigameKind.Chop)[0].resource;
            Assert.IsTrue(rt.Surface.SimulateTap(lumber));
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(l => l.text == resource.LocalizedName));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q<Button>("harvest-start-" + resource.id + "-" + MinigameKind.Chop));
            rt.Ui.DebugReset();
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = toggle; toggle.SendEvent(submit); }
            yield return null;
            yield return null;
            Assert.IsFalse(rt.Planet.CloseView);
            yield return WaitForCamera(rt.Camera, overview);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = toggle; toggle.SendEvent(submit); }
            yield return new WaitForSecondsRealtime(0.15f);
            Vector3 midTransition = rt.Camera.transform.position;
            Assert.AreNotEqual(overview, midTransition);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = toggle; toggle.SendEvent(submit); }
            yield return null;
            Assert.Less(Vector3.Distance(midTransition, rt.Camera.transform.position), 0.5f,
                "Reversing the camera mid-flight must start from its current position.");
            yield return WaitForCamera(rt.Camera, overview);

            rt.State.backpack.Set(resource.id, 0);
            bool found = false;
            var costs = UiKit.CostRow(new List<ItemAmount> { new ItemAmount { resource = resource, amount = 1 } },
                rt.State, true, r => found = r == resource);
            costs.style.position = Position.Absolute;
            costs.style.left = 200;
            costs.style.top = 300;
            rt.Ui.DebugRoot.Add(costs);
            yield return null;
            var find = costs.Q<Button>("find-resource-" + resource.id);
            Assert.IsNotNull(find);
            Assert.AreEqual(UiText.Get("resource.find", resource.LocalizedName), find.tooltip);
            Assert.IsTrue(find.focusable);
            Assert.IsEmpty(find.text, "the resource icon replaces the separate FIND label");
            Assert.IsNotNull(find.Q<Label>(), "The complete resource chip includes its balance inside the button.");
            Assert.IsTrue(find.ClassListContains("fr-itemchip"));
            Assert.IsNotNull(find.Q(className: "fr-icon"));
            Assert.IsTrue(find.ClassListContains("fr-itemchip-find"));
            Assert.IsFalse(find.ClassListContains("fr-resource-find-icon"));
            Assert.AreEqual(1, costs.Query<Button>().ToList().Count);
            Assert.AreSame(find, find.panel.Pick(find.Q<Label>().worldBound.center),
                "Tapping the balance must hit the same button as the resource icon.");
            Assert.AreEqual(UiKit.Size.Touch, find.resolvedStyle.minHeight.value);
            Assert.That(find.resolvedStyle.height, Is.EqualTo(UiKit.Size.Touch).Within(1f / rt.Ui.DebugPanelScale),
                "Touch targets may round by at most one rendered pixel at the current panel scale.");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = find; find.SendEvent(submit); }
            Assert.IsTrue(found, "native keyboard activation must still find the resource");
            costs.RemoveFromHierarchy();
            rt.State.backpack.Set(resource.id, 1);
            var affordable = UiKit.CostRow(new List<ItemAmount> { new ItemAmount(resource, 1) },
                rt.State, true, _ => Assert.Fail("Owned resources must not expose a missing-item action."));
            Assert.IsNull(affordable.Q<Button>());
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = toggle; toggle.SendEvent(submit); }
            yield return null;
            Assert.IsTrue(rt.Planet.CloseView);
            Assert.IsTrue(rt.Defense.StartWave(1));
            yield return null;
            yield return null;
            Assert.IsFalse(rt.Planet.CloseView, "called waves need the full planet visible");
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(overview, rt.Camera.transform.position);
        }

        [Test]
        public void RemovingResourceDirectionsPreservesEveryExistingSlotPrefix()
        {
            foreach (int count in new[] { 12, 18, 30 })
            {
                var existing = WorldSurface.SlotDirections(count + 9);
                var current = WorldSurface.SlotDirections(count);
                for (int slot = 0; slot < count; slot++) Assert.AreEqual(existing[slot], current[slot], "slot " + slot);
            }
        }

        [UnityTest]
        public IEnumerator BuildingHarvestAccessCollectsSurfaceScrapOnceAndRoutesPendingJobs()
        {
            new GameObject("HarvestListBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            yield return null;
            var skip = rt.Ui.DebugRoot.Q<Button>("intro-skip");
            if (skip != null)
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = skip; skip.SendEvent(submit); }
            for (int i = 0; i < 240 && rt.Ui.DebugBusy; i++) yield return null;
            Assert.IsFalse(rt.Ui.DebugBusy);
            var wood = rt.Db.FindResource("wood");
            var scrap = rt.Db.scrap;
            rt.State.backpack.Clear();
            rt.Ui.DebugShop(1);
            Assert.IsNull(rt.Ui.DebugRoot.Q("build-harvest"));
            Assert.IsNull(rt.Ui.DebugRoot.Q("build-jobs"));
            Assert.IsNull(rt.Ui.DebugRoot.Q<Button>("harvest-start-" + wood.id + "-Chop"));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("shop-structure-" + rt.Db.StructureOfKind(StructureKind.Lumber).id));
            var lumber = rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Lumber), 0);
            var mine = rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Mine), 1);
            Assert.IsNotNull(lumber); Assert.IsNotNull(mine);
            yield return null;
            rt.Ui.DebugReset();
            Assert.IsTrue(rt.Surface.SelectStructure(mine));
            var start = rt.Ui.DebugRoot.Q<Button>("harvest-start-" + scrap.id + "-Mine");
            Assert.IsNotNull(start, "Surface scrap remains harvestable by tapping the Mining Rig.");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = start; start.SendEvent(submit); }
            Assert.IsTrue(rt.Ui.DebugBusy);
            Assert.IsInstanceOf<VeinGame>(rt.Ui.DebugActiveGather);
            rt.Ui.DebugActiveGather.DebugComplete(0.8f, 1, 1);
            deadline = Time.realtimeSinceStartup + 5;
            while (rt.Ui.DebugRoot.Q<Button>("minigame-collect-resources") == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            var collect = rt.Ui.DebugRoot.Q<Button>("minigame-collect-resources");
            Assert.IsNotNull(collect);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = collect; collect.SendEvent(submit); }
            int earned = rt.State.Count(scrap);
            Assert.Greater(earned, 0);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = collect; collect.SendEvent(submit); }
            Assert.AreEqual(earned, rt.State.Count(scrap));
            Assert.IsFalse(rt.Ui.DebugBusy);
            yield return null;

            Assert.IsTrue(rt.Surface.SimulateTap(lumber));
            start = rt.Ui.DebugRoot.Q<Button>("harvest-start-" + wood.id + "-Chop");
            Assert.IsNotNull(start);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = start; start.SendEvent(submit); }
            Assert.IsInstanceOf<NodeGame>(rt.Ui.DebugActiveGather);
            rt.Ui.DebugActiveGather.DebugComplete(0.8f, 1, 1);
            deadline = Time.realtimeSinceStartup + 5;
            while (rt.Ui.DebugRoot.Q<Button>("minigame-collect-resources") == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            collect = rt.Ui.DebugRoot.Q<Button>("minigame-collect-resources");
            Assert.IsNotNull(collect);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = collect; collect.SendEvent(submit); }
            Assert.Greater(rt.State.Count(wood), 0);

            mine = rt.State.StructureOfKind(rt.State.planet, StructureKind.Mine);
            rt.State.DebugSetBuilderJob(mine, 600, 2);
            yield return null;
            rt.Ui.DebugReset();
            Assert.IsTrue(rt.Surface.SelectStructure(mine));
            Assert.IsNull(rt.Ui.DebugRoot.Q<Button>("harvest-start-" + scrap.id + "-Mine"));
            var pending = rt.Ui.DebugRoot.Q<Button>("structure-open-jobs");
            Assert.IsNotNull(pending, "Tapping an upgrading building must retain access to job controls and crews.");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = pending; pending.SendEvent(submit); }
            Assert.IsFalse(rt.Ui.DebugBusy);
            Assert.AreEqual(earned, rt.State.Count(scrap));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("builder-jobs"));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("builder-finish-" + mine.planetId + "-" + mine.slot));
        }

        static IEnumerator WaitForCamera(Camera camera, Vector3 target)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!camera.transform.position.Equals(target) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(target, camera.transform.position);
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
        public void WaveVictoryAwardsUnlockingXpOnceAndDefeatAwardsNone()
        {
            var state = new GameState(GameDatabase.Load(), false);
            var host = new GameObject("WaveXpProbe");
            try
            {
                var defense = host.AddComponent<DefenseController>();
                defense.enabled = false;
                defense.Init(null, null, state);
                state.DebugSetLevel(1);
                int threshold = new Progression(() => FrontierSettings.I.xpPerLevelBase).XpForLevel(2);
                int reward = defense.Preview(1).xp;
                state.AddXp(Math.Max(0, threshold - reward));
                Assert.IsNotNull(state.PlayerLevelLockReason(2));
                int before = state.xp;
                Assert.IsTrue(defense.StartWave(1));
                defense.DebugEnd(false);
                Assert.AreEqual(before, state.xp, "Defeated waves must not pay victory XP.");
                Assert.IsNotNull(state.PlayerLevelLockReason(2));

                Assert.IsTrue(defense.StartWave(1));
                reward = defense.Current.xp;
                defense.DebugEnd(true);
                Assert.AreEqual(before + reward, state.xp);
                Assert.IsNull(state.PlayerLevelLockReason(2), "Winning waves must advance the same level used by purchase gates.");
                defense.DebugEnd(true);
                Assert.AreEqual(before + reward, state.xp, "A settled wave must not award XP again.");
                Assert.IsTrue(state.Save());
                var loaded = new GameState(state.db, true);
                Assert.AreEqual(state.xp, loaded.xp);
                Assert.IsNull(loaded.PlayerLevelLockReason(2));
            }
            finally { Object.Destroy(host); }
        }

        [UnityTest]
        public IEnumerator ResearchAndShipExplainXpLevelsAndRouteToWaveCaller()
        {
            new GameObject("XpProgressionBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            rt.Ui.DebugResearch();
            yield return null;
            var progress = rt.Ui.DebugRoot.Q("player-xp-progression");
            Assert.IsNotNull(progress);
            Assert.IsTrue(progress.Query<Label>().ToList().Exists(l => l.text == UiText.Get("progression.xp-status",
                rt.State.PlayerLevel, rt.State.XpToNextLevel, rt.State.PlayerLevel + 1)));
            Assert.IsTrue(progress.Query<Label>().ToList().Exists(l => l.text == UiText.Get("progression.xp-help")));
            var waves = rt.Ui.DebugRoot.Q<Button>("progression-open-waves");
            Assert.IsNotNull(waves);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = waves; waves.SendEvent(submit); }
            yield return null;
            var alarm = rt.State.StructureOfKind(rt.State.planet, StructureKind.Alarm);
            if (alarm == null)
            {
                var def = rt.Db.StructureOfKind(StructureKind.Alarm);
                Assert.IsNotNull(rt.Ui.DebugRoot.Q("shop-structure-" + def.id),
                    "Players without an Alarm must be sent to its Build entry.");
                alarm = rt.State.DebugPlace(def, 0);
                Assert.IsNotNull(alarm);
                yield return null;
            }
            rt.Ui.DebugShip(1);
            waves = rt.Ui.DebugRoot.Q<Button>("progression-open-waves");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = waves; waves.SendEvent(submit); }
            yield return null;
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("player-xp-progression"), "The Alarm preview should also explain XP.");
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(l => l.text == UiText.Get("wave.call-help")));
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
            Assert.AreEqual(Vector3.down * 0.045f, anchor.Find("StructureVisualRoot").localPosition);
            state.DebugPlace(state.db.FindStructure("gunner"), 1);
            Assert.AreSame(anchor, rt.Planet.Spin.Find("Struct_warehouse"), "Unrelated builds must not recreate or reanimate existing models.");
            first.record.buildEndUtc = UtcText.Format(DateTime.UtcNow.AddHours(1));
            state.Notify();
            yield return null;
            anchor = rt.Planet.Spin.Find("Struct_warehouse");
            Assert.IsNotNull(anchor.Find("ConstructionScan"));
            rt.Ui.DebugBuilders();
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
                .Exists(label => label.text == state.BuilderJobStatus(first)),
                "The tapped structure keeps its live job status.");
            rt.Ui.DebugReset();
            first.record.buildEndUtc = "";
            state.Notify();
            yield return null;
            anchor = rt.Planet.Spin.Find("Struct_warehouse");
            Assert.IsNull(anchor.Find("ConstructionScan"));
            Assert.Greater(anchor.Find("StructureVisualRoot").localScale.x, 1f, "Completion should pulse, not replay the small build-in.");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.Less(Vector3.Distance(Vector3.one, anchor.Find("StructureVisualRoot").localScale), 0.01f);
            var home = state.planet;
            rt.Planet.Show(home, false, state.TerraformerStageOn(home));
            yield return null;
            Assert.AreEqual(Vector3.down * 0.045f,
                rt.Planet.Spin.Find("Struct_warehouse/StructureVisualRoot").localPosition,
                "Returning to the world keeps the small placement offset.");
            Assert.Less(Vector3.Distance(Vector3.one,
                rt.Planet.Spin.Find("Struct_warehouse/StructureVisualRoot").localScale), 0.01f,
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
            complete.GetEntry("nav.command").Value = "LOCALIZED RESEARCH";
            complete.GetEntry("nav.menu").Value = "LOCALIZED MENU";
            complete.GetEntry("settings.language").Value = "LOCALIZED LANGUAGE";
            complete.GetEntry("guide.skip").Value = "LOCALIZED SKIP";
            complete.GetEntry("service.deletion.partial").Value = "LOCALIZED PARTIAL {0}";
            complete.GetEntry("service.cloud.conflict").Value = "LOCALIZED CONFLICT";
            complete.GetEntry("service.notifications.denied").Value = "LOCALIZED NOTIFICATIONS";
            foreach (var entry in source.entries)
                if (entry.key.StartsWith("settings.quality-preset.", StringComparison.Ordinal))
                    complete.GetEntry(entry.key).Value = "LOCALIZED QUALITY";
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
            string notificationError = NotificationPreferences.LastError;
            try
            {
                var partialDeletion = ServiceMessage.Get("service.deletion.partial",
                    ServiceMessage.Get("service.cloud.conflict"));
                runtime.Ui.GetType().GetField("accountDeletionFailure",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(runtime.Ui, partialDeletion);
                NotificationPreferences.ReportFailure(ServiceMessage.Get("service.notifications.denied"));
                runtime.Ui.DebugMenu("settings");
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == partialDeletion.ToString()));
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
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == "LOCALIZED RESEARCH"));
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == "LOCALIZED MENU"));
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == "LOCALIZED LANGUAGE"));
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList()
                    .Exists(label => label.text == "LOCALIZED PARTIAL LOCALIZED CONFLICT"));
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList()
                    .Exists(label => label.text != null && label.text.Contains("LOCALIZED NOTIFICATIONS")));
                Assert.IsTrue(runtime.Ui.DebugRoot.Q("settings-quality").Query<Label>().ToList()
                    .Exists(label => label.text == "QUALITY: LOCALIZED QUALITY"));
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
                runtime.Ui.DebugResearch();
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList()
                    .Exists(label => label.text == research.LocalizedName.ToUpper()));
                runtime.Ui.DebugShip(1);
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList()
                    .Exists(label => label.text == ladder.LocalizedName.ToUpper()));
                runtime.Ui.DebugMenu("goals");
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == objective.LocalizedTitle));
                runtime.Ui.DebugMenu("ranks");
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text ==
                    "YOUR FRONTIER: LOCALIZED CONTENT.ERA." + runtime.State.Era.ToString().ToUpperInvariant() + ".NAME"));
                foreach (var requirement in Frontier.Economy.EraRules.NextRequirements(runtime.State.Era, runtime.State.EraProgress))
                    Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text ==
                        "LOCALIZED content.era-requirement." + requirement.Id + ".label"));

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
                runtime.Ui.DebugMenu("settings");
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == partialDeletion.ToString()));
                Assert.AreEqual(ServiceMessage.Get("service.notifications.denied").ToString(), NotificationPreferences.LastError);
            }
            finally
            {
                NotificationPreferences.LastError = notificationError;
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
            Assert.AreEqual("RESEARCH", UiText.Get("nav.command"));
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
            foreach (var key in new[] { "ranks.score", "ranks.record", "ranks.era.help" })
                Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get(key)), key);
            Assert.IsFalse(runtime.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("ranks.enable")),
                "Local Records must not contain online leaderboard controls.");
            runtime.Ui.DebugMenu("leaderboard");
            Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("ranks.enable")),
                "Online consent belongs on the separate Leaderboard page.");
            runtime.Ui.DebugMenu("shards");
            Assert.IsTrue(runtime.Ui.DebugRoot.Query<Label>().ToList().Exists(label =>
                label.text == UiText.Get("shards.balance", runtime.State.ShardBalance.ToString("N0"))));
            Assert.IsNotNull(runtime.Ui.DebugRoot.Query<Label>().ToList()
                .Find(label => label.text == UiText.Get("shards.ad.title")));
            Assert.IsNull(runtime.Ui.DebugRoot.Q<Button>("shards-watch-ad"),
                "The Shards page must not expose an ad action before the provider is release-ready and opted into.");
            runtime.Ui.DebugMenu("ranks");
            runtime.Ui.DebugMenu("ranks");
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
            var pinToggle = runtime.Ui.DebugRoot.Q<Toggle>("map-pin-toggle");
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
            Assert.IsNull(assist, "travel settings must not expose an easy mode");
            rt.Ui.DebugResearch();
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get("research.branch")));
            rt.Ui.DebugShip(1);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get("tools.title")));
            Assert.AreEqual(1, rt.Ui.DebugRoot.Query<Label>().ToList().FindAll(label => label.text == UiText.Get("tools.mining")).Count);
            Assert.IsFalse(rt.Ui.DebugRoot.Query<Button>().ToList().Exists(button =>
                button.Query<Label>().ToList().Exists(label => label.text == "PICKAXE" || label.text == "DRILL")),
                "the legacy pick/drill tab bar must not survive above the shared upgrade list");
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

        sealed class RanksFixture : IAuthService, ILeaderboardService
        {
            public bool IsConfigured { get { return true; } }
            public string CurrentPlayerId { get { return "rank-player"; } }
            public bool CanRecoverAccount { get { return false; } }
            public string RecoveryProviderName { get { return ""; } }
            public int signIns, submissions, reads;
            public string board;
            public long score;
            public TaskCompletionSource<ServiceResult<LeaderboardEntry>> pending;
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<AuthIdentity>> SignInAnonymouslyAsync()
            {
                signIns++;
                return Task.FromResult(ServiceResult<AuthIdentity>.Success(new AuthIdentity(CurrentPlayerId, true)));
            }
            public Task<ServiceResult<AuthIdentity>> LinkRecoveryAccountAsync() { throw new InvalidOperationException(); }
            public Task<ServiceResult<AuthIdentity>> SignInWithRecoveryAccountAsync() { throw new InvalidOperationException(); }
            public Task<ServiceResult> DeleteAccountAsync(string id) { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<LeaderboardEntry>> SubmitScoreAsync(string boardId, long value)
            {
                submissions++;
                board = boardId;
                score = value;
                return pending != null ? pending.Task : Task.FromResult(ServiceResult<LeaderboardEntry>.Success(
                    new LeaderboardEntry(CurrentPlayerId, "", value, 27, true)));
            }
            public Task<ServiceResult<IReadOnlyList<LeaderboardEntry>>> GetScoresAsync(string boardId, int limit)
            {
                reads++;
                return Task.FromResult(ServiceResult<IReadOnlyList<LeaderboardEntry>>.Success(
                    new List<LeaderboardEntry> { new LeaderboardEntry("other", "", 9999, 1, true) }));
            }
        }

        [UnityTest]
        public IEnumerator OnlineLeaderboardOptInAndPageOpeningUploadTheActualScore()
        {
            new GameObject("RanksBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            yield return null;
            var consent = new ConsentService(new DeletionConsentStore());
            var provider = new RanksFixture();
            var unavailable = new UnavailableFrontierProvider();
            var services = new FrontierServices(consent, unavailable, unavailable, provider, provider,
                unavailable, unavailable, unavailable, new LocalTimeService(), unavailable, "Lifetime_Earnings_Leaderboard");
            rt.Ui.DebugSetServices(services);
            rt.Ui.DebugMenu("");
            void Press(string name)
            {
                var button = rt.Ui.DebugRoot.Q<Button>(name);
                Assert.IsNotNull(button, name);
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
            }
            Assert.AreEqual(0, provider.signIns + provider.submissions + provider.reads);
            Press("menu-leaderboard");
            Assert.AreEqual(0, provider.signIns + provider.submissions + provider.reads,
                "Opening the page does not bypass online-services opt-in.");
            Press("leaderboard-enable");
            yield return null;
            Assert.IsTrue(consent.Current.onlineServicesAllowed);
            Assert.IsFalse(consent.Current.analyticsAllowed || consent.Current.adsAllowed);
            Assert.AreEqual(1, provider.signIns);
            Assert.AreEqual(1, provider.submissions);
            Assert.AreEqual(rt.State.FrontierScore.total, provider.score);
            Assert.AreEqual("Lifetime_Earnings_Leaderboard", provider.board);
            Assert.AreEqual(1, provider.reads);
            Assert.AreEqual(UiText.Get("ranks.your-rank", 27, provider.score.ToString("N0")),
                rt.Ui.DebugRoot.Q<Label>("leaderboard-player-rank").text);
            var podium = rt.Ui.DebugRoot.Q("leaderboard-podium");
            Assert.IsNotNull(podium, "The top three have a distinct podium.");
            Assert.IsNull(rt.Ui.DebugRoot.Q("leaderboard-publish"), "Opening the page replaces manual posting.");
            Assert.IsTrue(podium.Query<Label>().ToList().Exists(l =>
                l.text == UiText.Get("service.leaderboard.unknown-pilot")));

            provider.pending = new TaskCompletionSource<ServiceResult<LeaderboardEntry>>();
            rt.Ui.DebugMenu("");
            Press("menu-leaderboard");
            Assert.IsNull(rt.Ui.DebugRoot.Q<Button>("leaderboard-retry"), "Loading starts automatically and exposes no extra upload action.");
            Assert.AreEqual(2, provider.submissions);
            provider.pending.SetResult(ServiceResult<LeaderboardEntry>.Failed("Network unavailable"));
            yield return null;
            Assert.IsTrue(rt.Ui.DebugRoot.Q<Button>("leaderboard-retry").enabledSelf);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(l => l.text.Contains("Network unavailable")));
            Assert.AreEqual(1, provider.reads, "A failed submission cannot claim a successful refresh.");
            provider.pending = null;
            Press("leaderboard-retry");
            yield return null;
            Assert.AreEqual(2, provider.reads);

            provider.pending = new TaskCompletionSource<ServiceResult<LeaderboardEntry>>();
            rt.Ui.DebugMenu("");
            Press("menu-leaderboard");
            consent.SavePreferences(false, false, false);
            consent.SavePreferences(false, false, true);
            provider.pending.SetResult(ServiceResult<LeaderboardEntry>.Success(
                new LeaderboardEntry("rank-player", "", provider.score, 27, true)));
            yield return null;
            Assert.AreEqual(2, provider.reads, "Withdrawal must prevent the next request, even after re-opt-in.");
            Assert.IsNull(rt.Ui.DebugRoot.Q("leaderboard-player-rank"));
        }

        sealed class AnalyticsFixture : IAnalyticsService
        {
            public bool IsConfigured { get { return true; } }
            public int initializations;
            public bool allowed, initialized, collecting;
            public TaskCompletionSource<ServiceResult> pending;
            public void SetDataCollectionAllowed(bool value)
            {
                allowed = value;
                collecting = initialized && allowed;
            }
            public async Task<ServiceResult> InitializeAsync()
            {
                initializations++;
                var result = pending != null ? await pending.Task : ServiceResult.Success();
                initialized = result.succeeded;
                collecting = initialized && allowed;
                return result;
            }
            public ServiceResult RecordEvent(string name, IReadOnlyDictionary<string, string> parameters)
            {
                throw new InvalidOperationException("Activation must not emit custom gameplay events.");
            }
        }

        static FrontierServices AnalyticsServices(ConsentService consent, AnalyticsFixture provider)
        {
            var unavailable = new UnavailableFrontierProvider();
            return new FrontierServices(consent, unavailable, unavailable, unavailable, unavailable,
                unavailable, provider, unavailable, new LocalTimeService(), unavailable);
        }

        [UnityTest]
        public IEnumerator AnalyticsStartsOnlyAfterOptInAndWithdrawalDuringInitializationStopsCollection()
        {
            var consent = new ConsentService(new DeletionConsentStore());
            var provider = new AnalyticsFixture { pending = new TaskCompletionSource<ServiceResult>() };
            var app = new GameObject("AnalyticsLifecycle").AddComponent<FrontierApp>();
            app.Init(null, AnalyticsServices(consent, provider));
            yield return null;
            Assert.AreEqual(0, provider.initializations);
            Assert.IsFalse(provider.allowed);
            consent.SavePreferences(true, false, false);
            Assert.AreEqual(1, provider.initializations);
            Assert.IsFalse(consent.Current.onlineServicesAllowed);
            Assert.IsFalse(provider.collecting);
            app.SendMessage("OnApplicationFocus", true);
            Assert.AreEqual(1, provider.initializations, "In-flight initialization must be deduplicated.");
            consent.SavePreferences(false, false, false);
            provider.pending.SetResult(ServiceResult.Success());
            yield return null;
            Assert.IsFalse(provider.collecting, "Late initialization cannot undo consent withdrawal.");
            consent.SavePreferences(true, false, false);
            Assert.IsTrue(provider.collecting);
            Assert.AreEqual(1, provider.initializations);
            consent.SavePreferences(false, false, false);
            Assert.IsFalse(provider.collecting);
            Object.Destroy(app.gameObject);
            yield return null;
            consent.SavePreferences(true, false, false);
            Assert.AreEqual(1, provider.initializations, "Destroyed app must unsubscribe.");
        }

        [UnityTest]
        public IEnumerator AnalyticsStartsFromSavedConsentAndStopsWhenTheAppIsDestroyed()
        {
            var store = new DeletionConsentStore();
            new ConsentService(store).SavePreferences(true, false, false);
            var provider = new AnalyticsFixture();
            var app = new GameObject("AnalyticsSavedConsent").AddComponent<FrontierApp>();
            app.Init(null, AnalyticsServices(new ConsentService(store), provider));
            yield return null;
            Assert.AreEqual(1, provider.initializations);
            Assert.IsTrue(provider.collecting);
            Object.Destroy(app.gameObject);
            yield return null;
            Assert.IsFalse(provider.collecting);
        }

        [UnityTest]
        public IEnumerator AnalyticsFailedInitializationCanRetryOnReturningToTheApp()
        {
            var consent = new ConsentService(new DeletionConsentStore());
            consent.SavePreferences(true, false, false);
            var provider = new AnalyticsFixture { pending = new TaskCompletionSource<ServiceResult>() };
            var app = new GameObject("AnalyticsRetry").AddComponent<FrontierApp>();
            app.Init(null, AnalyticsServices(consent, provider));
            LogAssert.Expect(LogType.Warning, "Frontier: analytics initialization failed: Offline");
            provider.pending.SetResult(ServiceResult.Failed("Offline"));
            yield return null;
            Assert.IsFalse(provider.collecting);
            provider.pending = null;
            app.SendMessage("OnApplicationFocus", true);
            yield return null;
            Assert.AreEqual(2, provider.initializations);
            Assert.IsTrue(provider.collecting);
        }

        sealed class CloudFixture : ICloudSaveService
        {
            readonly RanksFixture auth;
            public int reads, writes;
            public CloudSaveRecord record = new CloudSaveRecord(false, "", "");
            public CloudFixture(RanksFixture authentication) { auth = authentication; }
            public bool IsConfigured { get { return true; } }
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<CloudSaveRecord>> ReadAsync()
            {
                Assert.Greater(auth.signIns, 0, "Compare must sign in before reading.");
                reads++;
                return Task.FromResult(ServiceResult<CloudSaveRecord>.Success(record));
            }
            public Task<ServiceResult<CloudSaveRecord>> WriteAsync(string payload, string writeLock)
            {
                Assert.AreEqual(record.writeLock, writeLock);
                writes++;
                record = new CloudSaveRecord(true, payload, "cloud-test-lock");
                return Task.FromResult(ServiceResult<CloudSaveRecord>.Success(record));
            }
            public Task<ServiceResult> DeleteSaveAsync(string id) { throw new InvalidOperationException(); }
        }

        [UnityTest]
        public IEnumerator CloudCompareSignsInWithoutRanksAndBackupRequiresConfirmation()
        {
            new GameObject("CloudBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            yield return null;
            var consent = new ConsentService(new DeletionConsentStore());
            var auth = new RanksFixture();
            var cloud = new CloudFixture(auth);
            var unavailable = new UnavailableFrontierProvider();
            var services = new FrontierServices(consent, unavailable, unavailable, auth, auth, cloud,
                unavailable, unavailable, new LocalTimeService(), unavailable);
            rt.Ui.DebugSetServices(services);
            rt.Ui.DebugMenu("settings");
            Button Find(string key)
            {
                var label = rt.Ui.DebugRoot.Query<Label>().ToList().Find(l => l.text == UiText.Get(key));
                return label != null ? label.GetFirstAncestorOfType<Button>() : null;
            }
            void Press(string key)
            {
                var button = Find(key);
                Assert.IsNotNull(button, key);
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
            }
            Assert.IsNull(Find("cloud.compare"), "Cloud requests must remain behind online consent.");
            Assert.AreEqual(0, auth.signIns + cloud.reads + cloud.writes);
            consent.SavePreferences(false, false, true);
            rt.Ui.DebugMenu("settings");
            Press("cloud.compare");
            yield return null;
            Assert.AreEqual(1, auth.signIns);
            Assert.AreEqual(1, cloud.reads);
            Assert.AreEqual(0, cloud.writes);
            Assert.AreEqual(0, auth.submissions + auth.reads, "Cloud Save cannot publish ranks.");
            Assert.IsFalse(consent.Current.analyticsAllowed || consent.Current.adsAllowed);
            Press("cloud.create");
            yield return null;
            Assert.AreEqual(0, cloud.writes, "First click is a warning, not an upload.");
            Press("cloud.create.confirm");
            yield return null;
            Assert.AreEqual(1, cloud.writes);
            Assert.AreEqual(2, cloud.reads, "Confirm must reread before writing.");
            Assert.IsNotEmpty(cloud.record.payload);
            Assert.IsNotNull(Find("cloud.restore"));
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
            bool naturalArrivalComplete = false, observedTouchdownImpulse = false, observedLandingBoost = false, observedBoostCutoff = false;
            IEnumerator NaturalArrival()
            {
                yield return ui.PlayArrivalIntro(false);
                naturalArrivalComplete = true;
            }
            ui.StartCoroutine(NaturalArrival());
            bool flewLeft = false, flewRight = false, flewBelow = false, observedCruise = false;
            float arrivalStarted = Time.realtimeSinceStartup;
            float naturalDeadline = arrivalStarted + 7f;
            while (!naturalArrivalComplete && Time.realtimeSinceStartup < naturalDeadline)
            {
                if (Vector3.Distance(naturalCameraOrigin, runtime.Camera.transform.position) > 0.0001f)
                    observedTouchdownImpulse = true;
                var lander = Object.FindFirstObjectByType<ShipRig>();
                if (lander != null)
                {
                    observedLandingBoost |= lander.LandingThrust > 0.4f;
                    observedBoostCutoff |= observedLandingBoost && lander.LandingThrust == 0f;
                    var center = runtime.Planet.Spin.position;
                    var shipScreen = runtime.Camera.WorldToViewportPoint(lander.transform.position);
                    var centerScreen = runtime.Camera.WorldToViewportPoint(center);
                    flewLeft |= shipScreen.x < centerScreen.x - 0.1f;
                    flewRight |= shipScreen.x > centerScreen.x + 0.1f;
                    flewBelow |= shipScreen.y < centerScreen.y - 0.1f;
                    observedCruise |= lander.Throttle > 0.4f && lander.Gear < 0.1f;
                    var air = lander.transform.Find("Hull/LandingAir_1");
                    if (air != null && lander.LandingThrust > 0f)
                        Assert.Greater(Vector3.Dot(-air.forward, -lander.transform.up), 0.99f);
                    if (lander.Gear == 1f && lander.Throttle == 0f)
                    {
                        Assert.Greater(shipScreen.y, centerScreen.y);
                        Assert.Greater(Vector3.Dot(lander.transform.forward, runtime.Camera.transform.right), 0.99f);
                    }
                }
                yield return null;
            }
            Assert.IsTrue(naturalArrivalComplete, "The natural arrival must finish and release input.");
            Assert.Greater(Time.realtimeSinceStartup - arrivalStarted, 4.6f);
            Assert.IsTrue(flewLeft && flewRight && flewBelow, "The ship makes a full loop, not just a vertical drop.");
            Assert.IsTrue(observedCruise, "The orbital approach flies with retracted gear and rear engines.");
            Assert.IsTrue(observedTouchdownImpulse, "Touchdown should produce a visible, bounded camera settle.");
            Assert.IsTrue(observedLandingBoost);
            Assert.IsTrue(observedBoostCutoff, "Air boosters shut off at touchdown.");
            Assert.AreEqual(planet, state.planet.Id);
            Assert.AreEqual(fuel, state.Count(state.db.fuel));
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

            rt.Ui.DebugFirstSessionGuideAction(6);
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("shop-structure-" + rt.Db.StructureOfKind(StructureKind.ResearchLab).id));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("first-session-cue-6"));
            rt.Ui.DebugReset();
            rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.ResearchLab), 3);
            rt.Ui.DebugFirstSessionGuideAction(6);
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("research-navigation"), "An existing lab routes to technology research.");
            rt.Ui.DebugFirstSessionGuideAction(7);
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("ship-navigation"));

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
                rt.Ui.DebugBuilders();
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
            rt.Ui.DebugBuilders();
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
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("shop-structure-" + rt.Db.StructureOfKind(StructureKind.Lumber).id),
                "No gatherer should lead to its building shop.");
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
        public IEnumerator EmptyStorageAndRelay_ExplainTheNextLocalAction()
        {
            new GameObject("EmptyLogisticsTestBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            for (int i = 0; i < 10; i++) yield return null;
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
                .Exists(label => label.text == UiText.Get("map.price.manual",
                    (long)preview.totalFuel * GameState.GallonsPerEnergy, originalFuel * GameState.GallonsPerEnergy, preview.totalSeconds)));
            Assert.AreEqual(UiText.Get("map.pin"), rt.Ui.DebugRoot.Q<Toggle>("map-pin-toggle").label);
            yield return null;
            var travel = rt.Ui.DebugRoot.Q<Button>("map-travel-action");
            var details = travel.parent.Q<ScrollView>(className: "fr-map-details");
            Assert.AreSame(travel, travel.parent.ElementAt(travel.parent.childCount - 1), "Travel is the card footer.");
            Assert.IsNull(travel.GetFirstAncestorOfType<ScrollView>(), "Travel must not scroll with the details.");
            Assert.LessOrEqual(details.worldBound.yMax, travel.worldBound.yMin + 1);
            Assert.Greater(details.contentViewport.resolvedStyle.height, 0);
            float footerY = travel.worldBound.y;
            details.scrollOffset = new Vector2(0, 10000);
            yield return null;
            Assert.AreEqual(footerY, travel.worldBound.y, 1, "Scrolling leaves the route action pinned.");
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
                    (long)rt.Ui.DebugMapPreview.totalFuel * GameState.GallonsPerEnergy,
                    originalFuel * GameState.GallonsPerEnergy, rt.Ui.DebugMapPreview.totalSeconds)));
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

        [UnityTest]
        public IEnumerator PlanetNetwork_PreviewsFreeTravel_SkipsFlight_AndReplaysASkippableFinaleWithoutReset()
        {
            new GameObject("PlanetNetworkBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue(rt.State.TryCreateCloudSaveSnapshot(out var save, out string error), error);
            var target = rt.State.universe.Get("glacia");
            save.network.links.Add(new PlanetLink { fromPlanetId = rt.State.planet.Id, toPlanetId = target.Id });
            save.player.upgrades.Add(new IdLevel { id = "hull_cold", level = 3 });
            Assert.IsTrue(rt.State.TryRestoreCloudSave(save, out error), error);
            rt.Ui.DebugFuelTanks();
            Assert.IsNotNull(rt.Ui.DebugRoot.Q<Button>("fuel-open-upgrades"));
            rt.Ui.DebugMap(target, 12);
            deadline = Time.realtimeSinceStartup + 10;
            while (rt.Ui.DebugMapPreview == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(rt.Ui.DebugMapPreview);
            Assert.IsTrue(rt.Ui.DebugMapPreview.instant);
            Assert.IsTrue(rt.Ui.DebugMapPreview.reachable);
            Assert.AreEqual(0, rt.Ui.DebugMapPreview.totalFuel);
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(l => l.text == UiText.Get("network.route.free")));
            Assert.IsFalse(rt.Ui.DebugRoot.Query<Toggle>().ToList().Exists(t => t.label == UiText.Get("map.autopilot.help")),
                "Free gate travel cannot show an irrelevant fuel-multiplier control.");
            long fuel = rt.State.FuelGallons;
            rt.Ui.DebugStartTravel(target, true);
            Assert.AreEqual(target.Id, rt.State.planet.Id, "Linked arrival is durable immediately, before its presentation.");
            Assert.IsNull(rt.State.PendingTravel);
            deadline = Time.realtimeSinceStartup + 8;
            while (rt.Ui.DebugBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(rt.Ui.DebugBusy);
            Assert.IsFalse(rt.Ui.IsTravelling);
            Assert.IsNull(rt.Ui.DebugRoot.Q("hyperspace-hud"));
            Assert.AreEqual(fuel, rt.State.FuelGallons);
            Assert.IsTrue(rt.State.TryCreateCloudSaveSnapshot(out save, out error), error);
            save.network.completed = true;
            save.network.archivePlanetId = target.Id;
            Assert.IsTrue(rt.State.TryRestoreCloudSave(save, out error), error);
            ulong seed = rt.State.universe.Seed;
            rt.Ui.DebugNetworkFinale();
            yield return null;
            var skip = rt.Ui.DebugRoot.Q<Button>("network-finale-skip");
            Assert.IsNotNull(skip);
            Assert.AreEqual(DisplayStyle.None, rt.Ui.DebugRoot.Q(className: "fr-topbar").parent.resolvedStyle.display);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = skip; skip.SendEvent(submit); }
            yield return null;
            yield return null;
            Assert.IsNotNull(rt.Ui.DebugRoot.Q<Button>("network-finale-explore"));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q<Button>("network-finale-prestige"));
            Assert.IsTrue(rt.Ui.HandleBack());
            yield return null;
            Assert.IsNull(rt.Ui.DebugRoot.Q("network-finale"));
            Assert.IsFalse(rt.Ui.DebugBusy);
            Assert.IsTrue(rt.State.NetworkComplete);
            Assert.AreEqual(seed, rt.State.universe.Seed, "Watching/skipping the ending cannot trigger prestige.");
            Assert.AreEqual(fuel, rt.State.FuelGallons);
        }

        [UnityTest]
        public IEnumerator ReturnSummaryRendersAndDismissesWithoutChangingStoredOutput()
        {
            new GameObject("ReturnSummaryBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            int before = rt.State.Count(rt.Db.FindResource("wood"));
            rt.State.ReturnItems.Add(new SessionReturnItem { planetId = rt.State.planet.Id, slot = 0 });
            rt.State.ReturnItems.Add(new SessionReturnItem { planetId = rt.State.planet.Id, slot = 1,
                resourceId = "wood", amount = 24 });
            rt.Ui.DebugProject();
            yield return null;
            var dismiss = rt.Ui.DebugRoot.Q<Button>("project-dismiss-summary");
            Assert.IsNotNull(dismiss);
            Assert.AreEqual(UiText.Get("project.dismiss-summary"), dismiss.Q<Label>(className: "fr-btn-label").text);
            foreach (var label in rt.Ui.DebugRoot.Q(className: "fr-project-body").Query<Label>(className: "fr-small").ToList())
                Assert.AreEqual(WhiteSpace.Normal, label.resolvedStyle.whiteSpace, "Return text must wrap rather than clip.");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = dismiss; dismiss.SendEvent(submit); }
            yield return null;
            Assert.IsNull(rt.Ui.DebugRoot.Q("project-dismiss-summary"));
            Assert.AreEqual(before, rt.State.Count(rt.Db.FindResource("wood")));
            Assert.AreEqual(2, rt.State.ReturnItems.Count, "Dismissal hides the report; it never grants or consumes stored output.");
        }

        [UnityTest]
        public IEnumerator ConstructionAdsRequireFlagsAndPreserveTheNormalActionOnNoFill()
        {
            new GameObject("RewardBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            var consent = new ConsentService(new DeletionConsentStore());
            consent.SavePreferences(false, true, false);
            var ads = new GameplayAdsFixture();
            var unavailable = new UnavailableFrontierProvider();
            rt.Ui.DebugSetServices(new FrontierServices(consent, ads, unavailable, unavailable, unavailable,
                unavailable, unavailable, unavailable, new LocalTimeService(), unavailable));
            var rules = ScriptableObject.CreateInstance<GameplayRewardRules>();
            try
            {
                var job = rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Warehouse), 0);
                rt.State.DebugSetBuilderJob(job, 120);
                rt.Ui.DebugGameplayRewardRules(rules);
                rt.Ui.DebugBuilders();
                Assert.IsNull(rt.Ui.DebugRoot.Q("construction-reward"));
                rules.constructionEnabled = true; rules.constructionSeconds = 60;
                rt.Ui.DebugBuilders();
                var watch = rt.Ui.DebugRoot.Q<Button>("construction-reward");
                Assert.IsNotNull(watch);
                Assert.IsTrue(watch.Query<Label>().ToList().Exists(label => label.text ==
                    UiText.Get("reward.construction", UiText.Get("duration.minutes-seconds", 1, 0))));
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = watch; watch.SendEvent(submit); }
                Assert.AreEqual(1, ads.calls);
                Assert.IsFalse(watch.enabledSelf);
                ads.pending.SetResult(ServiceResult<RewardedAdResult>.Failed("No fill"));
                yield return null;
                Assert.AreEqual("cancelled", rt.State.GameplayReward.state);
                Assert.Greater(rt.State.BuilderJobSecondsRemaining(job), 100);
                Assert.IsNotNull(rt.Ui.DebugRoot.Q("builder-finish-" + job.planetId + "-" + job.slot));
            }
            finally { Object.Destroy(rules); }
        }

        sealed class GameplayAdsFixture : IAdsService
        {
            public bool IsConfigured { get { return true; } }
            public int calls;
            public TaskCompletionSource<ServiceResult<RewardedAdResult>> pending = new TaskCompletionSource<ServiceResult<RewardedAdResult>>();
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<RewardedAdResult>> ShowRewardedAsync(string placement) { calls++; return pending.Task; }
        }

        [UnityTest]
        public IEnumerator ShortagesKeepQuantitiesAndTrackedProjectsWithoutHistoryButtons()
        {
            new GameObject("ShortageBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            Assert.IsFalse(rt.Services.Consent.Current.hasDecision, "Isolated saves must not inherit player consent.");
            rt.State.backpack.Clear();
            rt.Ui.DebugShip(0);
            yield return null;
            var upgrade = rt.Ui.DebugRoot.Q("upgrade-pack");
            var chip = upgrade.Q<Button>(className: "fr-itemchip-find");
            Assert.IsNotNull(chip);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = chip; chip.SendEvent(submit); }
            yield return null;
            Assert.IsNull(rt.Ui.DebugRoot.Q("shortage-return"));
            Assert.IsNull(rt.Ui.DebugRoot.Q("shortage-previous"));
            foreach (var label in rt.Ui.DebugRoot.Q(className: "fr-project-body").Query<Label>(className: "fr-small").ToList())
                Assert.AreEqual(WhiteSpace.Normal, label.resolvedStyle.whiteSpace, "Exact shortfalls must stay readable.");
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text.Contains("Still missing:")));
            var track = rt.Ui.DebugRoot.Q("shortage-overlay").Q<Button>("shortage-track");
            Assert.IsTrue(track.parent.ClassListContains("fr-modal-head"), "Tracking uses the header, not another action row.");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = track; track.SendEvent(submit); }
            Assert.AreEqual("pack", rt.State.TrackedProject.id);
            Assert.IsTrue(track.ClassListContains("fr-project-track-on"), "The overlay header refreshes without being rebuilt.");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = track; track.SendEvent(submit); }
            Assert.IsFalse(rt.State.HasTrackedProject);
            Assert.AreEqual(UiText.Get("project.track"), track.tooltip);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = track; track.SendEvent(submit); }
            rt.Ui.DebugShortage(rt.Db.FindResource("stone"), 17);
            yield return null;
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text.Contains("Need 17")));
            Assert.IsNull(rt.Ui.DebugRoot.Q("shortage-return"));
            Assert.IsNull(rt.Ui.DebugRoot.Q("shortage-previous"));
            Assert.AreEqual(0, rt.State.Count(rt.Db.FindResource("wood")));
            var close = rt.Ui.DebugRoot.Q<Button>("shortage-close");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = close; close.SendEvent(submit); }
            Assert.IsNull(rt.Ui.DebugRoot.Q("shortage-overlay"));
            rt.Ui.DebugShortage(rt.Db.FindResource("wood"), 17);
            var buildSource = rt.Ui.DebugRoot.Q<Button>("shortage-build-producer");
            Assert.IsNotNull(buildSource);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = buildSource; buildSource.SendEvent(submit); }
            Assert.IsNull(rt.Ui.DebugRoot.Q("shortage-overlay"));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("shop-structure-" + rt.Db.StructureOfKind(StructureKind.Lumber).id));
            rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Lumber), 0);
            yield return null;
            rt.Ui.DebugShortage(rt.Db.FindResource("wood"), 17);
            var produce = rt.Ui.DebugRoot.Q<Button>("shortage-produce");
            Assert.IsNotNull(produce);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = produce; produce.SendEvent(submit); }
            Assert.IsNull(rt.Ui.DebugRoot.Q("shortage-overlay"));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("harvest-start-wood-Chop"), "The producer icon opens the real building.");
            Assert.AreEqual(0, rt.State.Count(rt.Db.FindResource("wood")), "Finding a source never grants resources.");
            rt.Ui.DebugShortage(rt.Db.FindResource("wood"), 17);
            var overlay = rt.Ui.DebugRoot.Q("shortage-overlay");
            using (var down = PointerDownEvent.GetPooled()) { down.target = overlay; overlay.SendEvent(down); }
            Assert.IsNull(rt.Ui.DebugRoot.Q("shortage-overlay"), "Tapping the backdrop dismisses only the shortage.");
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("harvest-start-wood-Chop"));
            rt.Ui.DebugProject();
            yield return null;
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("project-open"));
            var open = rt.Ui.DebugRoot.Q<Button>("project-open");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = open; open.SendEvent(submit); }
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("upgrade-pack"), "Explicitly tracked projects still open their target.");
            Assert.IsNull(rt.Ui.DebugRoot.Q("shortage-return"));
            rt.Ui.DebugMap(null);
            Assert.IsNull(rt.Ui.DebugRoot.Q("shortage-previous"));
            rt.Ui.DebugMenu("tutorial");
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(l => l.text == UiText.Get("tutorial.map.body")));
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(l => l.text == UiText.Get("network.help")));
        }

        [UnityTest]
        public IEnumerator ShortageActionsWrapTogetherOnNarrowPanelsWithoutShrinkingTouchTargets()
        {
            new GameObject("ShortageLayoutBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            rt.State.backpack.Clear();
            var wood = rt.Db.FindResource("wood");
            var warehouse = rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.Warehouse), 0);
            var storage = rt.State.StorageFor(warehouse);
            storage.slots[0].resourceId = wood.id;
            storage.slots[0].count = 12;
            rt.Ui.DebugShortage(wood, 17);
            var overlay = rt.Ui.DebugRoot.Q("shortage-overlay");
            Assert.IsNotNull(overlay);
            overlay.Q(className: "fr-shortage").style.width = 440;
            yield return null;
            yield return null;
            var actions = overlay.Q("shortage-actions");
            var buttons = actions.Query<Button>().ToList();
            Assert.GreaterOrEqual(buttons.Count, 5, "Exercise storage, production and manual actions together.");
            Assert.AreEqual(Wrap.Wrap, actions.resolvedStyle.flexWrap);
            Assert.That(buttons[1].layout.y, Is.EqualTo(buttons[0].layout.y).Within(1f),
                "Actions must fill the row before wrapping, not stack individually.");
            Assert.Greater(buttons[1].layout.x, buttons[0].layout.x);
            Assert.Greater(buttons[buttons.Count - 1].layout.y, buttons[0].layout.y,
                "Extra actions wrap rather than overflow the narrow panel.");
            foreach (var button in buttons)
            {
                Assert.That(button.layout.width, Is.EqualTo(UiKit.Size.Touch).Within(1f));
                Assert.That(button.layout.height, Is.EqualTo(UiKit.Size.Touch).Within(1f));
                Assert.GreaterOrEqual(button.layout.x, 0);
                Assert.LessOrEqual(button.layout.xMax, actions.layout.width + 1);
            }
            StringAssert.Contains(rt.State.planet.Name, actions.Q<Button>("shortage-withdraw").tooltip);
            Assert.AreEqual(12, WarehouseOps.Held(storage, wood.id), "Changing layout never spends stored resources.");
        }

        [UnityTest]
        public IEnumerator LabShortcutTrackingToggleAndCornerResumeUseLiveDestinations()
        {
            new GameObject("ProjectControlsBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            rt.Ui.DebugResearch();
            yield return null;
            var lab = rt.Ui.DebugRoot.Q<Button>("project-local-lab");
            Assert.IsNotNull(lab);
            Assert.IsTrue(lab.parent.ClassListContains("fr-project-gates-inline"), "Gate icons belong beside the tracking star, not in their own body row.");
            Assert.IsNotNull(lab.parent.parent.Q<Button>(className: "fr-project-track"));
            foreach (var gate in rt.Ui.DebugRoot.Query(className: "fr-project-gates").ToList())
            {
                Assert.AreEqual(FlexDirection.Row, gate.resolvedStyle.flexDirection);
                foreach (var button in gate.Query<Button>().ToList())
                    Assert.AreEqual(0, button.Query<Label>().ToList().Count, "Prerequisite actions are compact icons, not full-width text.");
            }
            Assert.AreEqual(0, lab.Query<Label>().ToList().Count);
            Assert.AreEqual(UiText.Get("project.lab"), lab.tooltip);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = lab; lab.SendEvent(submit); }
            yield return null;
            yield return null;
            var definition = rt.Db.StructureOfKind(StructureKind.ResearchLab);
            var card = rt.Ui.DebugRoot.Q("shop-structure-" + definition.id);
            Assert.IsNotNull(card, "Lab shortcut must select Utility, not Economy.");
            var scroll = card.GetFirstAncestorOfType<ScrollView>();
            yield return null;
            Assert.Less(card.worldBound.yMin, scroll.contentViewport.worldBound.yMax);
            Assert.Greater(card.worldBound.yMax, scroll.contentViewport.worldBound.yMin);

            var track = card.Q<Button>("track-build-" + definition.id);
            Assert.IsNotNull(track);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = track; track.SendEvent(submit); }
            Assert.AreEqual(definition.id, rt.State.TrackedProject.id);
            track = rt.Ui.DebugRoot.Q<Button>("track-build-" + definition.id);
            Assert.IsTrue(track.ClassListContains("fr-project-track-on"));
            Assert.AreEqual(UiText.Get("project.clear"), track.tooltip);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = track; track.SendEvent(submit); }
            Assert.IsFalse(rt.State.HasTrackedProject);
            track = rt.Ui.DebugRoot.Q<Button>("track-build-" + definition.id);
            Assert.IsFalse(track.ClassListContains("fr-project-track-on"));
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = track; track.SendEvent(submit); }
            rt.Ui.DebugReset();
            yield return null;
            yield return null;
            var resume = rt.Ui.DebugRoot.Q<Button>("project-resume");
            Assert.AreEqual(0, resume.Query<Label>().ToList().Count);
            Assert.AreEqual(DisplayStyle.Flex, resume.resolvedStyle.display);
            Assert.AreEqual(UIRoot.HudTopInset + UiKit.Space.S16, resume.resolvedStyle.top, 1);
            Assert.AreEqual(UiKit.Space.S16, resume.style.right.value.value, 1);
            Assert.AreEqual(UiKit.Space.S16, resume.parent.worldBound.xMax - resume.worldBound.xMax, 1);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = resume; resume.SendEvent(submit); }
            var open = rt.Ui.DebugRoot.Q<Button>("project-open");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = open; open.SendEvent(submit); }
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("shop-structure-" + definition.id), "Resuming a Utility project uses the same shop destination.");
            rt.Ui.DebugMenu("tutorial");
            var text = rt.Ui.DebugRoot.Query<Label>().ToList();
            foreach (var key in new[] { "build", "gather", "storage", "refine", "defend", "research", "upgrade", "travel", "expand" })
                Assert.IsTrue(text.Exists(label => label.text == UiText.Get("tutorial." + key + ".body")), key);
        }

        [UnityTest]
        public IEnumerator TrackedMaterialsUseScannerCoverageAndRememberDiscoveriesWithoutSearchLeaks()
        {
            new GameObject("TrackedMapBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            rt.State.backpack.Clear();
            var pack = rt.Db.FindUpgrade("pack");
            var project = new PlayerProject { kind = "upgrade", id = pack.id,
                target = rt.State.Level(pack) + 1, planetId = rt.State.planet.Id };
            Assert.IsTrue(rt.State.TrackProject(project, out var error), error);
            rt.Ui.DebugMap(null);
            yield return null;
            Assert.IsTrue(rt.Ui.DebugMapProjectMatch(rt.State.planet));
            Assert.IsTrue(rt.Ui.DebugRoot.Q<Button>("map-planet-" + rt.State.planet.Id).ClassListContains("fr-map-project-world"));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("map-project-resource-wood"));
            Assert.AreEqual(UiText.Get("map.filter.project"), rt.Ui.DebugRoot.Q<DropdownField>(className: "fr-map-filter").value);
            Assert.AreEqual(rt.State.ScannerRangeLy, rt.Ui.DebugMapSurveyRadius(rt.State.planet), 0.001);
            rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.RadarTower), 0, 3);
            rt.State.Notify();
            Assert.Greater(rt.Ui.DebugMapSurveyRadius(rt.State.planet), rt.State.ScannerRangeLy);
            Assert.AreEqual(rt.State.ScannerRangeLy + rt.State.RadarRangeBonus(rt.State.planet),
                rt.Ui.DebugMapSurveyRadius(rt.State.planet), 0.001);

            var distant = rt.State.universe.FindGenerated(d => d.HasResource("wood"), 8, 0, 1000);
            Assert.IsNotNull(distant);
            Assert.IsFalse(rt.Ui.DebugMapSurveyed(distant));
            string origin = rt.Ui.DebugMapSelectedId;
            rt.Ui.DebugMapSearch(distant.Id);
            Assert.AreEqual(origin, rt.Ui.DebugMapSelectedId, "A canonical id cannot bypass scanners.");
            rt.Ui.DebugMapSearch(distant.Name);
            Assert.AreEqual(origin, rt.Ui.DebugMapSelectedId, "A name cannot bypass scanners.");
            rt.Ui.DebugMapSearch(distant.Star.Id);
            Assert.AreEqual(origin, rt.Ui.DebugMapSelectedId, "A system id cannot bypass scanners.");
            rt.Ui.DebugMapSearch(distant.Star.X.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                distant.Star.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            yield return null;
            Assert.IsFalse(rt.Ui.DebugMapSurveyed(distant), "Panning to coordinates is not discovery.");
            Assert.IsFalse(rt.Ui.DebugMapProjectMatch(distant));
            rt.State.RecordMapDiscoveries(new[] { distant.Star });
            rt.State.Notify();
            rt.Ui.DebugMapSearch(distant.Name);
            Assert.AreEqual(distant.Id, rt.Ui.DebugMapSelectedId);
            Assert.IsTrue(rt.Ui.DebugMapProjectMatch(distant), "Previously scanned materials stay marked outside current coverage.");
            rt.State.Save();
            rt.Ui.DebugMap(null);
            rt.Ui.DebugMapSearch(distant.Id);
            Assert.AreEqual(distant.Id, rt.Ui.DebugMapSelectedId, "Reopening preserves discovered destinations.");
            foreach (var cost in rt.State.ProjectState(project).costs)
                rt.State.backpack.Set(cost.resource.id, cost.amount);
            rt.State.Notify();
            Assert.IsFalse(rt.Ui.DebugMapProjectMatch(distant), "Materials already available no longer need a source highlight.");
            rt.State.backpack.Clear();
            rt.State.Notify();
            Assert.IsTrue(rt.Ui.DebugMapProjectMatch(distant));
            Assert.IsTrue(rt.State.TrackProject(null, out error), error);
            Assert.IsFalse(rt.Ui.DebugMapProjectMatch(distant), "Untracking clears the map highlights.");
            Assert.IsNull(rt.Ui.DebugRoot.Q("map-project-resource-wood"));
        }

        [UnityTest]
        public IEnumerator ResourceCostsWrapInsideTheirContainerWithoutNestedScrolling()
        {
            new GameObject("CostLayoutBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            var costs = new List<ItemAmount>();
            foreach (var id in new[] { "wood", "stone", "fuel", "planks" })
            {
                var resource = rt.Db.FindResource(id);
                if (resource != null) costs.Add(new ItemAmount(resource, 1234567));
            }
            var container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.width = 360;
            var row = UiKit.CostRow(costs, rt.State, true, resource => { }, resource => 123456);
            container.Add(row);
            rt.Ui.DebugRoot.Add(container);
            foreach (float width in new[] { 360f, 700f })
            {
                container.style.width = width;
                for (int i = 0; i < 3; i++) yield return null;
                Assert.IsNull(row.Q<ScrollView>());
                Assert.AreEqual(Wrap.Wrap, row.resolvedStyle.flexWrap);
                foreach (var chip in row.Children())
                {
                    Assert.GreaterOrEqual(chip.worldBound.xMin, row.worldBound.xMin - 1);
                    Assert.LessOrEqual(chip.worldBound.xMax, row.worldBound.xMax + 1);
                }
                Assert.Greater(row.resolvedStyle.height, row.ElementAt(0).resolvedStyle.height, "Costs should form multiple lines.");
            }
            container.RemoveFromHierarchy();
        }

        [UnityTest]
        public IEnumerator ResearchShipNavigationAndDockCapacityUseThePlayerFlow()
        {
            new GameObject("CommandDockBootstrap").AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var rt = FrontierRuntime.Current;
            Assert.IsNotNull(rt);
            var top = rt.Ui.DebugRoot.Q(className: "fr-topbar");
            Assert.AreEqual(3, top.Query(className: "fr-chip").ToList().Count,
                "The top bar contains fuel, Nova Shards and level, not a duplicate backpack chip.");
            var shards = top.Q<Label>("hud-nova-shards");
            Assert.IsNotNull(shards);
            Assert.AreEqual(rt.State.ShardBalance.ToString("N0"), shards.text);
            Assert.IsTrue(rt.State.TryCreateCloudSaveSnapshot(out var shardSave, out string shardError), shardError);
            shardSave.wallet.Clear();
            shardSave.wallet.Add(new WalletEntry { currency = "shards", amount = 1234 });
            Assert.IsTrue(rt.State.TryRestoreCloudSave(shardSave, out shardError), shardError);
            Assert.AreEqual(1234.ToString("N0"), shards.text, "The HUD reflects wallet changes without reopening a panel.");
            Assert.AreEqual(UiText.Get("hyperspace.revive.balance", 1234), shards.tooltip);
            var dockLabels = rt.Ui.DebugRoot.Q(className: "fr-dock").Query<Label>(className: "fr-dock-label").ToList();
            CollectionAssert.AreEqual(new[] { "RESEARCH", "MAP", "SHIP", "BUILD" }, dockLabels.ConvertAll(label => label.text));
            var pack = rt.Ui.DebugRoot.Q<Button>("dock-ship");
            Assert.IsNull(pack.Q("pack-capacity-gauge"));
            Assert.IsNull(pack.Q<Label>("pack-capacity-text"));
            rt.Ui.DebugEmptyPack();
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label =>
                label.text == UiText.Get("pack.capacity", 0, rt.State.Slots)),
                "Cargo retains its capacity information.");
            string resourceId = rt.Db.resources[0].id;
            rt.State.backpack.Set(resourceId, rt.State.backpack.StackSizeOf(resourceId) * rt.State.Slots);
            rt.State.Notify();
            Assert.IsTrue(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label =>
                label.text == UiText.Get("pack.capacity.rearrange", rt.State.SlotsUsed, rt.State.Slots)),
                "Cargo still reports a full hold after it changes.");

            rt.Ui.DebugResearch();
            var navigation = rt.Ui.DebugRoot.Q("research-navigation");
            Assert.AreEqual(2, navigation.childCount, "Research owns technology and global improvements.");
            var labels = navigation.Query<Label>().ToList();
            Assert.IsFalse(labels.Exists(label => label.text == UiText.Get("command.bases") || label.text == UiText.Get("builders.tab")));
            foreach (string key in new[] { "research.tab.tech", "research.tab.improvements" })
                Assert.IsTrue(labels.Exists(label => label.text == UiText.Get(key)), key);
            rt.Ui.DebugResearch(1);
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("upgrade-turret"), "Global defense upgrades belong to Research.");
            Assert.IsNull(rt.Ui.DebugRoot.Q("upgrade-pack"));
            rt.Ui.DebugShip(2);
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("upgrade-fuel_tank"));
            Assert.IsNull(rt.Ui.DebugRoot.Q("upgrade-turret"));
            Assert.IsFalse(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get("research.branch")));
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = pack; pack.SendEvent(submit); }
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("ship-navigation"));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("upgrade-pack"), "The Ship dock always opens Cargo, including its capacity upgrade.");
            Assert.IsNull(rt.Ui.DebugRoot.Q("upgrade-fuel_tank"));
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("pack-open-tanks"));
            rt.Ui.DebugFuelTanks();
            var fuelUpgrade = rt.Ui.DebugRoot.Q<Button>("fuel-open-upgrades");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = fuelUpgrade; fuelUpgrade.SendEvent(submit); }
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("upgrade-fuel_tank"), "Fuel upgrades route to Ship systems, not Research.");
            rt.Ui.DebugMap(null);
            var network = rt.Ui.DebugRoot.Q<Button>("map-open-network");
            Assert.IsNotNull(network);
            Assert.IsFalse(network.enabledSelf, "The network is disabled until a local Warp Gate is completed.");
            Assert.AreEqual(network, network.parent.ElementAt(network.parent.childCount - 1), "Network is the last Map detail.");
            Assert.IsNull(rt.Ui.DebugRoot.Q("map-open-ship"));
            var pin = rt.Ui.DebugRoot.Q<Toggle>("map-pin-toggle");
            Assert.IsTrue(pin.parent.ClassListContains("fr-map-card-actions"));
            Assert.IsTrue(pin.parent.ElementAt(0) is Label);
            var gate = rt.State.DebugPlace(rt.Db.StructureOfKind(StructureKind.WarpGate), 0);
            rt.State.DebugSetBuilderJob(gate, 600);
            rt.Ui.DebugMap(null);
            Assert.IsFalse(rt.Ui.DebugRoot.Q<Button>("map-open-network").enabledSelf,
                "An unfinished gate must not enable the network.");
            rt.State.DebugSetBuilderJob(gate, 0);
            rt.Ui.DebugMap(null);
            network = rt.Ui.DebugRoot.Q<Button>("map-open-network");
            Assert.IsTrue(network.enabledSelf);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = network; network.SendEvent(submit); }
            Assert.IsNull(rt.Ui.DebugRoot.Q<Button>("network-open-map"), "The Map dock already provides this route.");
            Assert.IsNotNull(rt.Ui.DebugRoot.Q<Button>("network-complete"));
            rt.Ui.DebugMenu("goals");
            var expand = rt.Ui.DebugRoot.Q<Button>("goals-toggle-all");
            Assert.IsNotNull(expand);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = expand; expand.SendEvent(submit); }
            var shipObjective = rt.Ui.DebugRoot.Q<Button>("objective-action-jump_range_1");
            Assert.IsNotNull(shipObjective);
            Assert.IsTrue(shipObjective.Query<Label>().ToList().Exists(label => label.text == UiText.Get("goals.action.ship")));
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = shipObjective; shipObjective.SendEvent(submit); }
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("upgrade-jump"), "Jump-range objectives route to Ship systems.");
            rt.Ui.DebugShop(0);
            Assert.IsNull(rt.Ui.DebugRoot.Q("build-jobs"));
            Assert.IsNull(rt.Ui.DebugRoot.Q("build-harvest"));
            rt.Ui.DebugReset();
            Assert.IsTrue(rt.Surface.SelectStructure(gate));
            var jobs = rt.Ui.DebugRoot.Q<Button>("structure-open-jobs");
            Assert.IsNotNull(jobs, "Construction jobs and recruitment remain accessible from buildings.");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = jobs; jobs.SendEvent(submit); }
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("builder-jobs"));
            Assert.IsNull(rt.Ui.DebugRoot.Q("research-navigation"));
            rt.Ui.DebugAtlas(0);
            var atlasFind = rt.Ui.DebugRoot.Q<Button>(className: "fr-resource-find-icon");
            Assert.IsNotNull(atlasFind);
            Assert.IsNull(atlasFind.Q<Label>());
            Assert.IsFalse(rt.Ui.DebugRoot.Query<Label>().ToList().Exists(label => label.text == UiText.Get("atlas.resource.find")));
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = atlasFind; atlasFind.SendEvent(submit); }
            yield return null;
            Assert.IsNotNull(rt.Ui.DebugRoot.Q("map-pin-toggle"), "The Atlas item icon retains deposit-map navigation.");
            rt.Ui.DebugReset();
        }

        [UnityTest]
        public IEnumerator Map_OpensOrbitalChart_WithSelectablePlanets_AndKeepsUniverseNavigation()
        {
            ScreenMetrics.SetVirtual(1080, 1920);
            var targetTexture = new RenderTexture(1080, 1920, 0);
            targetTexture.Create();
            try
            {
                new GameObject("OrbitalMapBootstrap").AddComponent<FrontierBootstrap>();
                float deadline = Time.realtimeSinceStartup + 30;
                while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
                var rt = FrontierRuntime.Current;
                Assert.IsNotNull(rt);
                rt.Document.panelSettings.targetTexture = targetTexture;
                rt.Ui.DebugMap(null);
                for (int i = 0; i < 10; i++) yield return null;
                Assert.IsTrue(rt.Ui.DebugMapInSystem, "The map should start with readable planet orbits, not a star field.");
                var chart = rt.Ui.DebugRoot.Q("map-viewport");
                Assert.IsNotNull(chart);
                var pin = rt.Ui.DebugRoot.Q<Toggle>("map-pin-toggle");
                Assert.IsNotNull(pin);
                Assert.GreaterOrEqual(pin.layout.height, UiKit.Size.Touch - 1, "The compact header checkbox keeps a phone-sized hit target.");
                Assert.Less(pin.layout.width, pin.parent.parent.layout.width * 0.5f,
                    "Pinning belongs under the status at the right, not across the whole card.");
                Assert.Less(Mathf.Abs(pin.Q<Label>(className: "unity-toggle__label").worldBound.center.y -
                    pin.Q(className: "unity-toggle__input").worldBound.center.y), 2f,
                    "Pin text and checkbox share one compact row under the status.");
                Assert.IsFalse(pin.value);
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = pin; pin.SendEvent(submit); }
                Assert.IsTrue(rt.State.IsMapPinned(rt.State.planet.Id));
                pin = rt.Ui.DebugRoot.Q<Toggle>("map-pin-toggle");
                Assert.IsTrue(pin.value);
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = pin; pin.SendEvent(submit); }
                Assert.IsFalse(rt.State.IsMapPinned(rt.State.planet.Id));
                yield return null;
                var worlds = rt.State.universe.PlanetsOf(rt.State.planet.Star);
                Assert.Greater(worlds.Count, 1);
                float previous = 0;
                foreach (var planet in worlds)
                {
                    var node = chart.Q<Button>("map-planet-" + planet.Id);
                    Assert.IsNotNull(node, planet.Name + " needs a native touch/keyboard target.");
                    Assert.AreEqual(DisplayStyle.Flex, node.resolvedStyle.display);
                    Assert.GreaterOrEqual(node.layout.width, 96);
                    float radius = rt.Ui.DebugMapOrbitRadius(planet);
                    Assert.Greater(radius, previous, "Orbit lanes must remain distinct.");
                    Assert.LessOrEqual(radius + 48, Mathf.Min(chart.layout.width, chart.layout.height) * 0.5f);
                    previous = radius;
                }
                var destination = worlds[1];
                var destinationButton = chart.Q<Button>("map-planet-" + destination.Id);
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = destinationButton; destinationButton.SendEvent(submit); }
                Assert.AreEqual(destination.Id, rt.Ui.DebugMapSelectedId);
                var mode = rt.Ui.DebugRoot.Q<Button>("map-switch-view");
                Assert.AreEqual(UiText.Get("map.mode.universe"), mode.text);
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = mode; mode.SendEvent(submit); }
                yield return null;
                Assert.IsFalse(rt.Ui.DebugMapInSystem);
                Assert.AreEqual(UiText.Get("map.mode.orbits"), mode.text);
                Assert.AreEqual(DisplayStyle.None, chart.Q<Button>("map-planet-" + destination.Id).resolvedStyle.display);
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = mode; mode.SendEvent(submit); }
                Assert.IsTrue(rt.Ui.DebugMapInSystem);
                Assert.AreEqual(destination.Id, rt.Ui.DebugMapSelectedId, "Changing chart scale cannot change the destination.");
                rt.Ui.DebugMapSearch("12000,-3000");
                Assert.IsFalse(rt.Ui.DebugMapInSystem, "Coordinates should reveal the universe, not leave a detached orbital chart.");
                rt.Ui.DebugMapSearch("glacia");
                Assert.IsTrue(rt.Ui.DebugMapInSystem, "Finding a planet should reveal its orbits.");
                Assert.AreEqual(rt.State.universe.Get("glacia").Id, rt.Ui.DebugMapSelectedId);
                rt.Ui.DebugReset();
            }
            finally
            {
                if (FrontierRuntime.Current != null) FrontierRuntime.Current.Document.panelSettings.targetTexture = null;
                ScreenMetrics.ClearVirtual();
                targetTexture.Release();
                Object.Destroy(targetTexture);
            }
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
            rt.State.DebugSetUpgrade(rt.Db.FindUpgrade("fuel_tank"), 3);
            rt.State.backpack.Set(rt.Db.fuel.id, 200);
            Assert.LessOrEqual(rt.State.FuelTankUsed, rt.State.FuelTankSlots);
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
