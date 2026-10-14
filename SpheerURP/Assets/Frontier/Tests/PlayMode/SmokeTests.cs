using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
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

        [SetUp]
        public void SetUp()
        {
            previousOverride = GameState.SaveDirectoryOverride;
            previousState = GameState.I;
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
            try { Directory.Delete(saveDir, true); } catch (IOException) { }
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
        public IEnumerator Bootstrap_BuildsTheWholeSession()
        {
            var host = new GameObject("TestBootstrap");
            host.AddComponent<FrontierBootstrap>();

            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;

            var runtime = FrontierRuntime.Current;
            Assert.IsNotNull(runtime, "FrontierBootstrap did not publish a FrontierRuntime within 30 s");
            Assert.IsNotNull(runtime.Db);
            Assert.IsTrue(runtime.Db.UsesImportedEconomy);
            Assert.IsNotNull(runtime.State);
            Assert.IsNotNull(runtime.State.planet);
            Assert.IsNotNull(runtime.Camera);
            Assert.IsNotNull(runtime.Backdrop);
            Assert.IsNotNull(runtime.Planet);
            Assert.IsNotNull(runtime.Defense);
            Assert.IsNotNull(runtime.Surface);
            Assert.IsNotNull(runtime.Ui);
            Assert.IsNotNull(runtime.Document);
            Assert.AreSame(runtime.State.planet, runtime.Planet.Current, "the planet view must show the player's planet");

            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsNotNull(runtime.Document.rootVisualElement, "the UI document has no visual tree");
            Assert.Greater(runtime.Document.rootVisualElement.childCount, 0, "the UI was not built");
        }

        // Any Debug.LogError or exception during the frames below fails the test (Unity's default), so this also proves the UI
        // refresh handlers survive a state change.
        [UnityTest]
        public IEnumerator Bootstrap_SurvivesAStateChangeWithTheUiAttached()
        {
            var host = new GameObject("TestBootstrap");
            host.AddComponent<FrontierBootstrap>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (FrontierRuntime.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var runtime = FrontierRuntime.Current;
            Assert.IsNotNull(runtime, "FrontierBootstrap did not publish a FrontierRuntime within 30 s");

            var destination = runtime.State.universe.Get(runtime.Db.planets[1].id);
            Assert.AreNotSame(runtime.State.planet, destination);
            runtime.State.DebugTeleport(destination);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreSame(destination, runtime.State.planet);
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
            int manualPrice = preview.totalFuel;
            rt.Ui.DebugMapAutopilot();
            deadline = Time.realtimeSinceStartup + 10;
            while (rt.Ui.DebugMapPreview == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(rt.Ui.DebugMapPreview.totalFuel, manualPrice);
            Assert.AreEqual(12, rt.Ui.DebugMapPreview.totalSeconds);
            rt.State.backpack.Set(rt.Db.fuel.id, 0);
            rt.State.Notify();
            deadline = Time.realtimeSinceStartup + 10;
            while (rt.Ui.DebugMapPreview == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(rt.Ui.DebugMapPreview.Has(RouteReasons.NotEnoughFuel));
            rt.Ui.DebugMapSearch("12000,-3000");
            Assert.AreEqual(12000, rt.Ui.DebugMapCenterX, 0.001);
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
                deadline = Time.realtimeSinceStartup + 25;
                while (rt.Ui.DebugBusy && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsFalse(rt.Ui.DebugBusy, "travel did not finish");
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
                deadline = start + 30;
                while (rt.Ui.DebugBusy && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsFalse(rt.Ui.DebugBusy);
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
