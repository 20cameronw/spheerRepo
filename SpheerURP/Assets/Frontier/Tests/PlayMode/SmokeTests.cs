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
    }
}
