#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UBiome = Frontier.Universe.Biome;
using USize = Frontier.Universe.SizeClass;
using UDescriptor = Frontier.Universe.PlanetDescriptor;

namespace Frontier
{
    [Serializable]
    public class ShotReport
    {
        public string device;
        public string shot;
        public string file;
        public string error;
        public LintReport lint;
    }

    [Serializable]
    public class HarnessReport
    {
        public string unity;
        public string started;
        public int shots;
        public int errors;
        public List<ShotReport> entries = new List<ShotReport>();
    }

    /// <summary>
    /// Drives a running Frontier session through every screen and state on virtual devices (resolution, density, notch) and
    /// writes PNG screenshots plus a UI lint report. Used for audits, before/after diffs and visual regression review.
    /// Command line: -frontierShotDir &lt;dir&gt; [-frontierShotDevices a,b] [-frontierShotOnly x,y]
    /// </summary>
    public class HarnessRunner : MonoBehaviour
    {
        public const string DirArg = "-frontierShotDir", DevicesArg = "-frontierShotDevices", OnlyArg = "-frontierShotOnly";
        public const string SessionDirKey = "Frontier.Harness.Dir";

        struct Device
        {
            public string name; public int w, h; public float density; public Rect safe;
            /// <summary>Only captured when named explicitly in -frontierShotDevices (keeps the default run at the four standard devices).</summary>
            public bool optIn;
            public Device(string name, int w, int h, float density, Rect safe, bool optIn = false) { this.name = name; this.w = w; this.h = h; this.density = density; this.safe = safe; this.optIn = optIn; }
        }

        struct Shot
        {
            public string name; public Action setup; public float wait;
            public Shot(string name, Action setup, float wait = 0.15f) { this.name = name; this.setup = setup; this.wait = wait; }
        }

        static readonly Device[] AllDevices =
        {
            new Device("phone-1080x1920", 1080, 1920, 2.625f, new Rect(0, 0, 1080, 1920)),
            new Device("phone-1170x2532", 1170, 2532, 3.0f, new Rect(0, 102, 1170, 2532 - 141 - 102)),
            new Device("phone-1440x3200", 1440, 3200, 3.5f, new Rect(0, 0, 1440, 3200 - 110)),
            new Device("tablet-1536x2048", 1536, 2048, 2.0f, new Rect(0, 0, 1536, 2048)),
            // 360 dp wide: the narrowest common phone class (many Samsung/Pixel-A devices).
            new Device("phone-1080x2340", 1080, 2340, 3.0f, new Rect(0, 0, 1080, 2340 - 80), true),
        };

        public static string OutputDir { get; private set; }
        static string devicesFilter = "", onlyFilter = "";

        public static bool Requested { get { return !string.IsNullOrEmpty(OutputDir); } }

        /// <summary>Reads the harness arguments and redirects the save file before the game state is created.</summary>
        public static void PrepareFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == DirArg) OutputDir = args[i + 1];
                else if (args[i] == DevicesArg) devicesFilter = args[i + 1];
                else if (args[i] == OnlyArg) onlyFilter = args[i + 1];
            }
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(OutputDir)) OutputDir = UnityEditor.SessionState.GetString(SessionDirKey, "");
#endif
            if (string.IsNullOrEmpty(OutputDir)) return;
            OutputDir = Path.GetFullPath(OutputDir);
            string saveDir = Path.Combine(OutputDir, "_save");
            Directory.CreateDirectory(saveDir);
            foreach (string file in Directory.GetFiles(saveDir, "frontier_save*")) File.Delete(file);
            GameState.SaveDirectoryOverride = saveDir;
        }

        FrontierRuntime rt;

        public void Begin(FrontierRuntime runtime)
        {
            rt = runtime;
            StartCoroutine(RunAll());
        }

        static bool Match(string value, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            foreach (var token in filter.Split(','))
                if (token.Length > 0 && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static bool MatchExplicit(string value, string filter)
        {
            return !string.IsNullOrEmpty(filter) && Match(value, filter);
        }

        IEnumerator RunAll()
        {
            var report = new HarnessReport { unity = Application.unityVersion, started = DateTime.Now.ToString("s") };
            yield return new WaitForSecondsRealtime(1.0f);
            rt.Planet.IdleSpin = false;
            try { Seed(); }
            catch (Exception e) { Debug.LogError("Frontier harness: seeding failed " + e); report.errors++; }

            bool firstDevice = true;
            foreach (var dev in AllDevices)
            {
                if (dev.optIn ? !MatchExplicit(dev.name, devicesFilter) : !Match(dev.name, devicesFilter)) continue;
                // The backdrop eases from the mood of the last world (the generated shots end somewhere else) to the home world's; let it settle.
                if (!firstDevice) yield return new WaitForSecondsRealtime(4f);
                firstDevice = false;
                yield return RunDevice(dev, report);
            }

            rt.Planet.IdleSpin = true;
            report.shots = report.entries.Count;
            File.WriteAllText(Path.Combine(OutputDir, "report.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(OutputDir, "done.txt"), report.shots + " shots, " + report.errors + " errors");
            Debug.Log("Frontier harness: " + report.shots + " shots, " + report.errors + " errors -> " + OutputDir);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // A mid-game state so every screen has something to show.
        void Seed()
        {
            var gs = rt.State; var db = rt.Db;
            var pack = db.FindUpgrade("pack");
            if (pack != null) gs.DebugSetUpgrade(pack, 4);
            foreach (var r in db.resources) gs.Add(r, r == db.fuel ? 30 : 18 + (r.id.Length * 7) % 40, false);
            string[] order = { "lumber", "mine", "refinery", "fabricator", "alarm", "gunner", "laser", "warehouse" };
            int slot = 0;
            foreach (var id in order)
            {
                var def = db.FindStructure(id);
                if (def == null) continue;
                gs.DebugPlace(def, slot, 1 + slot % 3);
                slot++;
            }
            foreach (var u in db.upgrades) if (u != null && u.category == UpgradeCategory.Tools) gs.DebugSetUpgrade(u, 2);
            gs.DebugSetLevel(7);
            gs.Notify();
        }

        IEnumerator RunDevice(Device dev, HarnessReport report)
        {
            ScreenMetrics.SetVirtual(dev.w, dev.h, dev.safe, dev.density);
            var sceneRT = new RenderTexture(dev.w, dev.h, 24, RenderTextureFormat.ARGB32);
            var uiRT = new RenderTexture(dev.w, dev.h, 24, RenderTextureFormat.ARGB32);
            sceneRT.Create(); uiRT.Create();

            var cam = rt.Camera;
            var panel = rt.Document.panelSettings;
            var prevClear = panel.clearColor; var prevClearColor = panel.colorClearValue;
            cam.targetTexture = sceneRT;
            panel.targetTexture = uiRT;
            panel.clearColor = true;
            panel.colorClearValue = Color.clear;

            var bg = new VisualElement { name = "harness-bg", pickingMode = PickingMode.Ignore };
            bg.style.position = Position.Absolute;
            bg.style.left = 0; bg.style.top = 0; bg.style.right = 0; bg.style.bottom = 0;
            bg.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(sceneRT));
            rt.Document.rootVisualElement.Insert(0, bg);
            rt.Ui.DebugRelayout();
            for (int i = 0; i < 10; i++) yield return null;

            string dir = Path.Combine(OutputDir, dev.name);
            Directory.CreateDirectory(dir);
            int index = 0;
            foreach (var shot in BuildShots())
            {
                if (!Match(shot.name, onlyFilter)) continue;
                index++;
                var entry = new ShotReport { device = dev.name, shot = shot.name };
                try { shot.setup(); }
                catch (Exception e) { entry.error = e.ToString(); report.errors++; Debug.LogError("Frontier harness: " + shot.name + " failed " + e); }
                for (int i = 0; i < 4; i++) yield return null;
                if (shot.wait > 0f) yield return new WaitForSecondsRealtime(shot.wait);
                // WaitForEndOfFrame never fires in a batch-mode editor (no Game view), and the panel renders into its
                // own RenderTexture during the normal player loop, so a couple of plain frames are enough.
                for (int i = 0; i < 3; i++) yield return null;

                var tex = new Texture2D(dev.w, dev.h, TextureFormat.RGB24, false);
                var prev = RenderTexture.active;
                RenderTexture.active = uiRT;
                tex.ReadPixels(new Rect(0, 0, dev.w, dev.h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                entry.file = Path.Combine(dev.name, index.ToString("00") + "-" + shot.name + ".png");
                File.WriteAllBytes(Path.Combine(OutputDir, entry.file), tex.EncodeToPNG());
                Destroy(tex);
                entry.lint = UiLint.Run(rt.Ui.DebugRoot, rt.Ui.DebugPanelScale, dev.density, new Rect(0, 0, dev.w, dev.h));
                report.entries.Add(entry);
                Debug.Log("Frontier harness: captured " + entry.file + (entry.error != null ? " (with error)" : ""));
            }

            rt.Ui.DebugReset();
            bg.RemoveFromHierarchy();
            cam.targetTexture = null;
            panel.targetTexture = null;
            panel.clearColor = prevClear; panel.colorClearValue = prevClearColor;
            sceneRT.Release(); uiRT.Release();
            Destroy(sceneRT); Destroy(uiRT);
            ScreenMetrics.ClearVirtual();
            rt.Ui.DebugRelayout();
            yield return null;
        }

        void GoTo(string planetId)
        {
            GoTo(rt.State.universe.Get(planetId));
        }

        void GoTo(PlanetData p)
        {
            if (p == null) return;
            rt.State.DebugTeleport(p);
            rt.Planet.Show(p, false);
        }

        void ResetView()
        {
            rt.Ui.DebugReset();
            if (rt.Planet.Spin != null) rt.Planet.Spin.localRotation = Quaternion.Euler(0f, 25f, 0f);
        }

        IEnumerable<Shot> BuildShots()
        {
            var db = rt.Db; var gs = rt.State; var ui = rt.Ui;
            string showcaseWorld = null;
            PlacedStructure At(StructureKind k) { return gs.StructureOfKind(gs.planet, k); }

            yield return new Shot("hud", ResetView);
            yield return new Shot("build-mode", () => { ResetView(); ui.DebugBuild(db.StructureOfKind(StructureKind.Refinery)); });
            for (int tab = 0; tab < 4; tab++) { int t = tab; yield return new Shot("shop-tab" + t, () => { ResetView(); ui.DebugShop(t); }); }
            yield return new Shot("command-bases", () => ui.DebugResearch(0));
            yield return new Shot("command-tools-axe", () => ui.DebugResearch(1, 0));
            yield return new Shot("command-tools-pick", () => ui.DebugResearch(1, 1));
            yield return new Shot("command-tools-drill", () => ui.DebugResearch(1, 2));
            yield return new Shot("command-ship", () => ui.DebugResearch(2));
            yield return new Shot("command-defense", () => ui.DebugResearch(3));
            yield return new Shot("pack", () => ui.DebugPack(-1));
            yield return new Shot("pack-selected", () => ui.DebugPack(0));
            yield return new Shot("menu", () => ui.DebugMenu(""));
            foreach (var page in new[] { "ranks", "tutorial", "about", "settings", "debug" })
            {
                string pg = page;
                yield return new Shot("menu-" + pg, () => ui.DebugMenu(pg));
            }
            yield return new Shot("map", () => ui.DebugMap(null, 0.7f), 0.6f);
            yield return new Shot("map-selected", () => ui.DebugMap(gs.universe.Get("nyx"), 0.9f), 0.6f);
            yield return new Shot("map-zoom-out", () => ui.DebugMap(gs.universe.Get("glacia"), 0.14f), 0.6f);
            yield return new Shot("map-zoom-in", () => ui.DebugMap(gs.universe.Get("ferrum"), 1.7f), 0.6f);

            foreach (var kind in new[] { StructureKind.Lumber, StructureKind.Mine, StructureKind.Refinery, StructureKind.Fabricator, StructureKind.Alarm, StructureKind.Gunner, StructureKind.Laser, StructureKind.Warehouse })
            {
                var k = kind;
                yield return new Shot("popup-" + k.ToString().ToLower(), () => { ResetView(); ui.DebugTapStructure(At(k)); });
            }
            yield return new Shot("toast", () => { ResetView(); ui.DebugToast("+14 Wood", UiKit.Lime); ui.DebugToast("Backpack full!", UiKit.Red); }, 0.3f);

            yield return new Shot("game-chop", () => ui.DebugGather(MinigameKind.Chop), 1.8f);
            yield return new Shot("game-mine", () => ui.DebugGather(MinigameKind.Mine), 1.8f);
            yield return new Shot("game-travel", () => ui.DebugTravelGame(), 2.5f);
            yield return new Shot("hyperspace-intro", () => HyperspaceShot(0, 0));
            yield return new Shot("hyperspace-combat", () => HyperspaceShot(3, 900));
            yield return new Shot("hyperspace-downed", () => HyperspaceShot(12, 14000, false));
            yield return new Shot("hyperspace-result", () => HyperspaceShot(0, 14000));
            yield return new Shot("hyperspace-pause", () => { HyperspaceShot(0, 500).Pause(); });
            yield return new Shot("hyperspace-guardian", () =>
            {
                var session = HyperspaceShot(6, 0);
                session.DebugAdvance(Mathf.CeilToInt((session.Run.Route.bossStart + 5) * 60), true, true);
            });
            yield return new Shot("game-drill", () => { GoTo("glacia"); ui.DebugGather(MinigameKind.Drill); }, 1.8f);
            yield return new Shot("glacia-hud", () => { ResetView(); }, 0.3f);
            yield return new Shot("wave-hud", () => { GoTo("sylva"); ResetView(); rt.Defense.StartWave(3); }, 4.0f);
            yield return new Shot("wave-result", () => rt.Defense.DebugEnd(true), 0.5f);
            yield return new Shot("hud-after", ResetView, 0.3f);
            yield return new Shot("menu-release", () => ui.DebugMenuAsRelease(), 0.2f);
            yield return new Shot("icons-ui", () => ui.DebugIconSheet(0), 0.3f);
            yield return new Shot("icons-items", () => ui.DebugIconSheet(1), 0.3f);
            yield return new Shot("icons-color", () => ui.DebugIconSheet(2), 0.3f);

            // ---- generated worlds (appended: shots 1-45 above keep their names and numbers) ----
            // Each shot waits for the backdrop to ease into the new world's mood (time constant under a second) so the picture shows the settled look.
            foreach (var spec in GeneratedShots)
            {
                var s = spec;
                yield return new Shot("gen-" + s.name, () =>
                {
                    var world = FindWorld(s);
                    if (world == null) throw new InvalidOperationException("no generated world for " + s.name);
                    Debug.Log("Frontier harness: world for " + s.name + "\n" + world.Dump());
                    GoTo(world);
                    ResetView();
                }, 2.5f);
            }

            Hyperspace.View.HyperspaceSession HyperspaceShot(int tier, int steps, bool pilot = true)
            {
                ResetView();
                var prefs = TravelPrefs.Store;
                TravelPrefs.Store = new MemoryPrefsStore();
                try
                {
                    var request = rt.Ui.DebugMakeTravelRequest(rt.State.planet, 2UL, tier == 0 ? 5 : 800);
                    request.tier = tier; request.danger = 0.6f + tier * 1.1f;
                    request.fuelCost = 20;
                    var game = new Hyperspace.View.HyperspaceGame(rt.State);
                    rt.Ui.DebugRunTravel(game, request, _ => { });
                    var session = game.Session;
                    session.DebugFrozen = true;
                    session.DebugAdvance(steps, pilot, pilot);
                    return session;
                }
                finally { TravelPrefs.Store = prefs; }
            }
            yield return new Shot("gen-colossal-built", () =>
            {
                var world = FindWorld(ColossalSpec);
                if (world == null) throw new InvalidOperationException("no colossal world");
                GoTo(world);
                showcaseWorld = world.Id;
                PlaceShowcase(gs, db);
                ResetView();
            }, 0.5f);
            yield return new Shot("gen-colossal-pads", () =>
            {
                ResetView();
                ui.DebugBuild(db.StructureOfKind(StructureKind.Refinery));
            }, 0.5f);
            yield return new Shot("gen-colossal-popup", () => { ResetView(); ui.DebugTapStructure(At(StructureKind.Gunner)); }, 0.3f);
            yield return new Shot("map-generated", () => ui.DebugMap(null, 0.4f), 0.6f);
            yield return new Shot("menu-debug-teleport", () => ui.DebugMenu("debug", true), 0.5f);

            // Leave the session as the first shots found it (home world, nothing built on the showcase world), so the next device starts from the same state.
            if (showcaseWorld != null)
                foreach (var record in new List<StructureRecord>(gs.world.StructuresOn(showcaseWorld))) gs.world.Remove(showcaseWorld, record);
            GoTo(gs.universe.Home);
            ResetView();
        }

        struct WorldSpec
        {
            public string name; public UBiome? biome; public USize? size; public int minTier; public Func<UDescriptor, bool> filter;
            public WorldSpec(string name, UBiome? biome, USize? size, int minTier, Func<UDescriptor, bool> filter = null)
            { this.name = name; this.biome = biome; this.size = size; this.minTier = minTier; this.filter = filter; }
        }

        static readonly WorldSpec ColossalSpec = new WorldSpec("colossal", null, USize.Colossal, 4, d => d.Biome != UBiome.GasGiant);

        // One deterministic world per biome (the first match in the universe's ring search), plus the cases the renderer must survive.
        static readonly WorldSpec[] GeneratedShots =
        {
            new WorldSpec("lush", UBiome.Lush, null, 0),
            new WorldSpec("mineral", UBiome.Mineral, null, 1),
            new WorldSpec("glacial", UBiome.Glacial, null, 2),
            new WorldSpec("volcanic", UBiome.Volcanic, null, 3),
            new WorldSpec("fungal", UBiome.Fungal, null, 2),
            new WorldSpec("crystal", UBiome.Crystal, null, 4),
            new WorldSpec("obsidian", UBiome.Obsidian, null, 3),
            new WorldSpec("shattered", UBiome.Shattered, null, 6),
            new WorldSpec("desert", UBiome.Desert, null, 1),
            new WorldSpec("oceanic", UBiome.Oceanic, null, 1),
            new WorldSpec("gasgiant", UBiome.GasGiant, USize.Giant, 2),
            new WorldSpec("gasgiant-rings-moons", UBiome.GasGiant, null, 3, d => d.HasRings && d.MoonCount >= 5),
            new WorldSpec("barren", UBiome.Barren, null, 0),
            new WorldSpec("dwarf", null, USize.Dwarf, 0, d => d.Biome != UBiome.GasGiant),
            ColossalSpec,
        };

        PlanetData FindWorld(WorldSpec spec)
        {
            var universe = rt.State.universe;
            if (spec.filter != null)
            {
                return universe.FindGenerated(d => (!spec.biome.HasValue || d.Biome == spec.biome.Value) && (!spec.size.HasValue || d.Size == spec.size.Value) && spec.filter(d), spec.minTier);
            }
            return universe.FindGenerated(spec.biome, spec.size, spec.minTier);
        }

        // Structures scattered over a 64 pad world: shows the pad layout and the structure scale of a Colossal planet.
        static void PlaceShowcase(GameState gs, GameDatabase db)
        {
            StructureKind[] kinds =
            {
                StructureKind.Lumber, StructureKind.Mine, StructureKind.Drill, StructureKind.Refinery, StructureKind.Fabricator, StructureKind.Alarm,
                StructureKind.Gunner, StructureKind.Laser, StructureKind.Warehouse, StructureKind.Gunner, StructureKind.Laser, StructureKind.Satellite,
                StructureKind.Gunner, StructureKind.Warehouse, StructureKind.Laser, StructureKind.Gunner
            };
            int[] slots = { 0, 4, 9, 13, 18, 22, 27, 31, 36, 40, 45, 49, 54, 58, 61, 63 };
            for (int i = 0; i < kinds.Length; i++)
            {
                var def = db.StructureOfKind(kinds[i]);
                if (def != null) gs.DebugPlace(def, slots[i], 1 + i % 3);
            }
        }
    }
}
#endif
