#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

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
            public Device(string name, int w, int h, float density, Rect safe) { this.name = name; this.w = w; this.h = h; this.density = density; this.safe = safe; }
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
            string saveFile = Path.Combine(saveDir, "frontier_save.json");
            if (File.Exists(saveFile)) File.Delete(saveFile);
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

        IEnumerator RunAll()
        {
            var report = new HarnessReport { unity = Application.unityVersion, started = DateTime.Now.ToString("s") };
            yield return new WaitForSecondsRealtime(1.0f);
            rt.Planet.IdleSpin = false;
            try { Seed(); }
            catch (Exception e) { Debug.LogError("Frontier harness: seeding failed " + e); report.errors++; }

            foreach (var dev in AllDevices)
            {
                if (!Match(dev.name, devicesFilter)) continue;
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
                gs.structures.Add(new PlacedStructure { planetId = gs.planet.id, def = def, slot = slot, level = 1 + slot % 3 });
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
            var p = rt.Db.FindPlanet(planetId);
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
            PlacedStructure At(StructureKind k) { return gs.structures.Find(s => s.planetId == gs.planet.id && s.def.kind == k); }

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
            yield return new Shot("map-selected", () => ui.DebugMap(db.FindPlanet("nyx"), 0.9f), 0.6f);
            yield return new Shot("map-zoom-out", () => ui.DebugMap(db.FindPlanet("glacia"), 0.14f), 0.6f);
            yield return new Shot("map-zoom-in", () => ui.DebugMap(db.FindPlanet("ferrum"), 1.7f), 0.6f);

            foreach (var kind in new[] { StructureKind.Lumber, StructureKind.Mine, StructureKind.Refinery, StructureKind.Fabricator, StructureKind.Alarm, StructureKind.Gunner, StructureKind.Laser, StructureKind.Warehouse })
            {
                var k = kind;
                yield return new Shot("popup-" + k.ToString().ToLower(), () => { ResetView(); ui.DebugTapStructure(At(k)); });
            }
            yield return new Shot("toast", () => { ResetView(); ui.DebugToast("+14 Wood", UiKit.Lime); ui.DebugToast("Backpack full!", UiKit.Red); }, 0.3f);

            yield return new Shot("game-chop", () => ui.DebugGather(MinigameKind.Chop), 1.8f);
            yield return new Shot("game-mine", () => ui.DebugGather(MinigameKind.Mine), 1.8f);
            yield return new Shot("game-travel", () => ui.DebugTravelGame(), 2.5f);
            yield return new Shot("game-drill", () => { GoTo("glacia"); ui.DebugGather(MinigameKind.Drill); }, 1.8f);
            yield return new Shot("glacia-hud", () => { ResetView(); }, 0.3f);
            yield return new Shot("wave-hud", () => { GoTo("sylva"); ResetView(); rt.Defense.StartWave(3); }, 4.0f);
            yield return new Shot("wave-result", () => rt.Defense.DebugEnd(true), 0.5f);
            yield return new Shot("hud-after", ResetView, 0.3f);
        }
    }
}
#endif
