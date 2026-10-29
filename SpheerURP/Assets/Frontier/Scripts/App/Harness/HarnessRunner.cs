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
            string[] order = { "lumber", "mine", "refinery", "fabricator", "orbital_relay", "gunner", "warehouse", "fuel_depot", "cargo_depot", "hangar", "shield_pylon", "auto_harvester" };
            int slot = 0;
            foreach (var id in order)
            {
                var def = db.FindStructure(id);
                if (def == null) continue;
                var placed = gs.DebugPlace(def, slot, 1 + slot % 3);
                if (id == "auto_harvester" && placed != null)
                {
                    var wood = db.FindResource("wood");
                    if (wood != null) gs.Deposit(placed, 0, wood, 8);
                }
                slot++;
            }
            var localPlanet = gs.planet;
            PlanetData relayTarget = null;
            foreach (var candidate in gs.universe.PlanetsOf(localPlanet.Star))
                if (candidate.Id != localPlanet.Id) { relayTarget = candidate; break; }
            if (relayTarget != null)
            {
                gs.DebugTeleport(relayTarget);
                var remoteWarehouse = gs.DebugPlace(db.FindStructure("warehouse"), 0);
                var wood = db.FindResource("wood");
                if (remoteWarehouse != null && wood != null) gs.Deposit(remoteWarehouse, 0, wood, 8);
                gs.DebugTeleport(localPlanet);
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
            rt.Planet.Show(p, false, rt.State.TerraformerStageOn(p));
        }

        void ResetView()
        {
            rt.Ui.DebugReset();
            rt.Planet.CloseView = false;
            if (rt.Planet.Spin != null) rt.Planet.Spin.localRotation = Quaternion.Euler(0f, 25f, 0f);
        }

        IEnumerable<Shot> BuildShots()
        {
            var db = rt.Db; var gs = rt.State; var ui = rt.Ui;
            string showcaseWorld = null;
            string harvestAccessWorld = gs.planet.Id;
            PlacedStructure harvestAccessPending = null;
            PlanetData obstacleShowcase = null;
            int obstacleShowcasePad = -1;
            PlacedStructure At(StructureKind k) { return gs.StructureOfKind(gs.planet, k); }
            void ShowMapFooter()
            {
                ui.DebugMap(null);
                ui.DebugRoot.schedule.Execute(() =>
                {
                    var network = ui.DebugRoot.Q<Button>("map-open-network");
                    if (network == null) throw new InvalidOperationException("Map network footer is missing.");
                    network.GetFirstAncestorOfType<ScrollView>().ScrollTo(network);
                }).StartingIn(200);
            }
            void DrillShot(bool result)
            {
                var world = gs.universe.FindGenerated(d =>
                {
                    foreach (var deposit in d.Deposits)
                        if (deposit.Method == Frontier.Universe.HarvestMethod.Drill) return true;
                    return false;
                }, 2, 0, 10000);
                if (world == null) throw new InvalidOperationException("no generated world with drilling deposits");
                GoTo(world);
                bool started = result ? ui.DebugGatherResult(MinigameKind.Drill) : ui.DebugGather(MinigameKind.Drill);
                if (!started) throw new InvalidOperationException("world has no drill gather spot");
            }

            yield return new Shot("hud", ResetView);
            yield return new Shot("ship-landing-boost", () =>
            {
                ResetView();
                ui.StartCoroutine(ui.PlayArrivalIntro(false));
            }, 0.45f);
            yield return new Shot("ship-landing-settled", () => { }, 0.8f);
            yield return new Shot("surface-view", () =>
            {
                ResetView();
                rt.Planet.CloseView = true;
                rt.State.Notify();
            }, 0.8f);
            bool hadQuality = PlayerPrefs.HasKey(PresentationQuality.QualityKey);
            bool hadFrameRate = PlayerPrefs.HasKey(PresentationQuality.FrameRateKey);
            int savedQuality = PlayerPrefs.GetInt(PresentationQuality.QualityKey);
            int savedFrameRate = PlayerPrefs.GetInt(PresentationQuality.FrameRateKey);
            bool usedLowQuality = false;
            void LowQualityView()
            {
                usedLowQuality = true;
                PresentationQuality.Preset = QualityPreset.Low;
                PresentationQuality.FrameRate = 0;
                ResetView();
            }
            try
            {
                yield return new Shot("quality-low-settings", () => { LowQualityView(); ui.DebugMenu("settings"); });
                yield return new Shot("quality-low-harvest", () =>
                {
                    LowQualityView();
                    if (!ui.DebugGather(MinigameKind.Chop)) throw new InvalidOperationException("Low-quality harvest fixture has no chopping deposit.");
                }, 1.8f);
                yield return new Shot("quality-low-travel", () => { LowQualityView(); HyperspaceShot(3, 900); });
            }
            finally
            {
                if (usedLowQuality)
                {
                    ResetView();
                    if (hadQuality) PlayerPrefs.SetInt(PresentationQuality.QualityKey, savedQuality);
                    else PlayerPrefs.DeleteKey(PresentationQuality.QualityKey);
                    if (hadFrameRate) PlayerPrefs.SetInt(PresentationQuality.FrameRateKey, savedFrameRate);
                    else PlayerPrefs.DeleteKey(PresentationQuality.FrameRateKey);
                    PlayerPrefs.Save();
                    PresentationQuality.Initialize(FrontierSettings.I.targetFrameRate);
                }
            }
            yield return new Shot("minigame-paused", () => { ResetView(); ui.DebugPausedGather(MinigameKind.Chop); });
            yield return new Shot("minigame-abandon-confirm", () => { ResetView(); ui.DebugPausedGather(MinigameKind.Chop, true); });
            yield return new Shot("minigame-result", () => { ResetView(); ui.DebugGatherResult(MinigameKind.Chop); }, 1.2f);
            yield return new Shot("first-session-guide", () => ui.DebugFirstSessionGuide(0));
            yield return new Shot("first-session-guide-defense", () => ui.DebugFirstSessionGuide(4));
            yield return new Shot("first-session-guide-travel", () => ui.DebugFirstSessionGuide(5));
            yield return new Shot("first-session-guide-research", () => ui.DebugFirstSessionGuide(6));
            yield return new Shot("first-session-guide-upgrade", () => ui.DebugFirstSessionGuide(7));
            yield return new Shot("guide-cue-harvest", () =>
            {
                ResetView();
                ui.DebugFirstSessionGuideAction(1);
                if (ui.DebugRoot.Q("first-session-cue-1") == null)
                    throw new InvalidOperationException("Harvest guide cue was not rendered.");
            });
            yield return new Shot("guide-cue-craft", () =>
            {
                ResetView();
                ui.DebugFirstSessionGuideAction(3);
                if (ui.DebugRoot.Q("first-session-cue-3") == null)
                    throw new InvalidOperationException("Craft guide cue needs an affordable recipe.");
            });
            yield return new Shot("build-mode", () => { ResetView(); ui.DebugBuild(db.StructureOfKind(StructureKind.Refinery)); });
            for (int tab = 0; tab < 4; tab++) { int t = tab; yield return new Shot("shop-tab" + t, () => { ResetView(); ui.DebugShop(t); }); }
            yield return new Shot("harvest-access-surface", () =>
            {
                GoTo(harvestAccessWorld); ResetView(); rt.Planet.CloseView = true;
            }, 0.8f);
            yield return new Shot("harvest-access-build", () => { GoTo(harvestAccessWorld); ResetView(); ui.DebugShop(1); });
            yield return new Shot("harvest-access-structure", () =>
            {
                GoTo(harvestAccessWorld); ResetView(); ui.DebugTapStructure(At(StructureKind.Lumber));
            });
            yield return new Shot("harvest-access-pending", () =>
            {
                GoTo(harvestAccessWorld); ResetView();
                var mine = At(StructureKind.Mine);
                if (mine == null) throw new InvalidOperationException("No Mine for pending harvesting fixture.");
                harvestAccessPending = mine;
                gs.DebugSetBuilderJob(mine, 600, mine.level + 1);
                ui.DebugTapStructure(mine);
            });
            if (harvestAccessPending != null) gs.DebugSetBuilderJob(harvestAccessPending, 0);
            yield return new Shot("construction-jobs", ui.DebugBuilders);
            SaveV2 playerFlowBaseline = null;
            void PreparePlayerFlowFixture()
            {
                ui.DebugReset();
                if (playerFlowBaseline == null)
                {
                    if (!gs.TryCreateCloudSaveSnapshot(out playerFlowBaseline, out var error))
                        throw new InvalidOperationException(error);
                }
                else if (!gs.TryRestoreCloudSave(playerFlowBaseline, out var restoreError))
                    throw new InvalidOperationException(restoreError);
            }
            try
            {
                yield return new Shot("player-shortage", () =>
                {
                    PreparePlayerFlowFixture();
                    var wood = db.FindResource("wood");
                    ui.DebugShip(0);
                    ui.DebugShortage(wood, gs.PlanetCount(wood) + 100);
                });
                yield return new Shot("player-project", () =>
                {
                    PreparePlayerFlowFixture();
                    var pack = db.FindUpgrade("pack");
                    if (!gs.TrackProject(new PlayerProject { kind = "upgrade", id = pack.id,
                        target = gs.Level(pack) + 1, planetId = gs.planet.Id }, out var error))
                        throw new InvalidOperationException(error);
                    ui.DebugProject();
                });
                yield return new Shot("player-project-corner", () => { ui.DebugReset(); });
                yield return new Shot("player-project-research", () => ui.DebugResearch());
                yield return new Shot("player-project-requirements", () =>
                {
                    ui.DebugResearch();
                    var action = ui.DebugRoot.Q<Button>("project-required-research");
                    if (action == null) throw new InvalidOperationException("No research prerequisite in layout fixture.");
                    var card = action.parent.parent;
                    var scroll = card.GetFirstAncestorOfType<ScrollView>();
                    scroll.schedule.Execute(() => scroll.ScrollTo(card)).StartingIn(1);
                });
                yield return new Shot("player-project-lab-shop", () =>
                {
                    ui.DebugResearch();
                    var lab = ui.DebugRoot.Q<Button>("project-local-lab");
                    if (lab == null) throw new InvalidOperationException("No lab shortcut in research fixture.");
                    using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = lab; lab.SendEvent(submit); }
                });
                void PrepareTrackedMap(float zoom)
                {
                    PreparePlayerFlowFixture();
                    var pack = db.FindUpgrade("pack");
                    gs.DebugSetUpgrade(pack, 0);
                    gs.backpack.Set("wood", 0);
                    if (!gs.TrackProject(new PlayerProject { kind = "upgrade", id = pack.id,
                        target = 1, planetId = gs.planet.Id }, out var error))
                        throw new InvalidOperationException(error);
                    ui.DebugMap(null, zoom);
                }
                yield return new Shot("player-project-map-scanner", () => PrepareTrackedMap(6f), 1f);
                yield return new Shot("player-project-map-orbits", () => PrepareTrackedMap(12f), 1f);
                yield return new Shot("player-return", () =>
                {
                    PreparePlayerFlowFixture();
                    gs.ReturnItems.Add(new SessionReturnItem { planetId = gs.planet.Id, slot = 0 });
                    gs.ReturnItems.Add(new SessionReturnItem { planetId = gs.planet.Id, slot = 11,
                        resourceId = "wood", amount = 24 });
                    ui.DebugProject();
                });
                yield return new Shot("player-reward-recovery", () =>
                {
                    PreparePlayerFlowFixture();
                    gs.backpack.Clear();
                    if (!gs.PrepareGameplayReward("double-yield", db.FindResource("wood"), 3, null, out _, out var error))
                        throw new InvalidOperationException(error);
                    ui.DebugProject();
                });
            }
            finally
            {
                var ticket = gs.GameplayReward;
                if (ticket != null && ticket.state == "pending" && !gs.CancelUnverifiedGameplayReward(out var cancelError))
                    throw new InvalidOperationException(cancelError);
                if (playerFlowBaseline != null && !gs.TryRestoreCloudSave(playerFlowBaseline, out var error))
                    throw new InvalidOperationException(error);
            }
            yield return new Shot("ship-tools", () => ui.DebugShip(1));
            yield return new Shot("research-tech", () => ui.DebugResearch());
            yield return new Shot("ship-systems", () => ui.DebugShip(2));
            yield return new Shot("research-improvements", () => ui.DebugResearch(1));
            yield return new Shot("pack", () => ui.DebugPack(-1));
            yield return new Shot("pack-selected", () => ui.DebugPack(0));
            var carriedBeforeEmptyShot = new Dictionary<ResourceSO, int>();
            foreach (var resource in db.resources) carriedBeforeEmptyShot[resource] = gs.Count(resource);
            try
            {
                yield return new Shot("pack-empty", () =>
                {
                    ResetView();
                    ui.DebugEmptyPack();
                    if (ui.DebugRoot.Q("empty-pack-action") == null)
                        throw new InvalidOperationException("Empty backpack action was not rendered.");
                });
            }
            finally
            {
                foreach (var carried in carriedBeforeEmptyShot) gs.backpack.Set(carried.Key.id, carried.Value);
                gs.Notify();
            }
            yield return new Shot("menu", () => ui.DebugMenu(""));
            foreach (var page in new[] { "ranks", "goals", "ascension", "tutorial", "about", "settings", "debug" })
            {
                string pg = page;
                yield return new Shot("menu-" + pg, () => ui.DebugMenu(pg));
            }
            yield return new Shot("menu-tutorial-research", () =>
            {
                ui.DebugMenu("tutorial");
                var heading = ui.DebugRoot.Query<Label>().ToList()
                    .Find(label => label.text == UiText.Get("tutorial.research.title"));
                var card = heading.GetFirstAncestorOfType<VisualElement>().parent.parent;
                var scroll = heading.GetFirstAncestorOfType<ScrollView>();
                scroll.schedule.Execute(() => scroll.ScrollTo(card)).StartingIn(1);
            });
            SaveV2 ascensionBaseline = null;
            var ascensionRules = Resources.Load<AscensionRulesSO>("Frontier/AscensionRules");
            bool ascensionWasEnabled = ascensionRules != null && ascensionRules.enableAscension;
            bool ascensionFixtureEnabled = false;
            void PrepareAscensionFixture()
            {
                ResetView();
                if (ascensionRules == null) throw new InvalidOperationException("Ascension fixture rules are missing.");
                ascensionRules.enableAscension = true;
                ascensionFixtureEnabled = true;
                if (!gs.TryCreateCloudSaveSnapshot(out ascensionBaseline, out var problem))
                    throw new InvalidOperationException(problem);
                var candidate = JsonUtility.FromJson<SaveV2>(JsonUtility.ToJson(ascensionBaseline));
                if (!candidate.player.researched.Contains("ascension_protocol"))
                    candidate.player.researched.Add("ascension_protocol");
                PlanetDelta current = null;
                foreach (var delta in candidate.worlds)
                    if (delta.planetId == candidate.location.currentPlanetId) { current = delta; break; }
                if (current == null) throw new InvalidOperationException("Ascension fixture requires a visited current world.");
                var record = new StructureRecord { defId = db.StructureOfKind(StructureKind.Terraformer).id, slot = 0, level = 3 };
                if (current.structures.Count == 0) current.structures.Add(record);
                else { record.slot = current.structures[0].slot; current.structures[0] = record; }
                if (!gs.TryRestoreCloudSave(candidate, out problem)) throw new InvalidOperationException(problem);
            }
            try
            {
                yield return new Shot("ascension-confirmation", () =>
                {
                    PrepareAscensionFixture();
                    ui.DebugMenu("ascension");
                    var review = ui.DebugRoot.Q<Button>("ascension-review");
                    if (review == null) throw new InvalidOperationException("Ascension fixture did not unlock review.");
                    using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = review; review.SendEvent(submit); }
                    if (ui.DebugRoot.Q<Button>("ascension-confirm") == null)
                        throw new InvalidOperationException("Ascension confirmation was not rendered.");
                });
                yield return new Shot("ascension-rank-one", () =>
                {
                    if (ascensionBaseline == null) PrepareAscensionFixture();
                    if (!gs.TryAscend(out var problem)) throw new InvalidOperationException(problem);
                    ui.DebugMenu("ascension");
                });
                yield return new Shot("ascension-halo", () =>
                {
                    if (ascensionBaseline == null)
                    {
                        PrepareAscensionFixture();
                        if (!gs.TryAscend(out var problem)) throw new InvalidOperationException(problem);
                    }
                    ResetView();
                }, 1.0f);
            }
            finally
            {
                try
                {
                    if (ascensionBaseline != null)
                    {
                        ResetView();
                        if (!gs.TryRestoreCloudSave(ascensionBaseline, out var problem))
                            throw new InvalidOperationException("Ascension fixture restoration failed: " + problem);
                        ResetView();
                    }
                }
                finally
                {
                    if (ascensionFixtureEnabled) ascensionRules.enableAscension = ascensionWasEnabled;
                }
            }
            string[] atlasShots = { "resources", "recipes", "worlds", "bestiary", "structures" };
            for (int tab = 0; tab < atlasShots.Length; tab++)
            {
                int t = tab;
                yield return new Shot("menu-atlas-" + atlasShots[tab], () => ui.DebugAtlas(t));
            }
            yield return new Shot("map", () => ui.DebugMap(null), 0.8f);
            yield return new Shot("orbital-chart-home", () => ui.DebugMap(null), 0.8f);
            yield return new Shot("map-network-locked", ShowMapFooter, 0.8f);
            yield return new Shot("orbital-chart-selected", () => ui.DebugMap(gs.universe.Get("glacia")), 0.8f);
            yield return new Shot("orbital-chart-sector", () => ui.DebugMap(null, 2f), 0.8f);
            yield return new Shot("map-selected", () => ui.DebugMap(gs.universe.Get("nyx"), 1f), 0.8f);
            yield return new Shot("map-autopilot", () => { ui.DebugMap(gs.universe.Get("cinder"), 1f); ui.DebugMapAutopilot(); }, 0.8f);
            yield return new Shot("map-zoom-out", () => ui.DebugMap(gs.universe.Get("glacia"), 0.08f), 0.8f);
            yield return new Shot("map-zoom-in", () => ui.DebugMap(gs.universe.Get("ferrum"), 12f), 0.8f);
            int savedMapFuel = gs.Count(db.fuel);
            int savedMapPlasma = gs.backpack.Count(GameState.PlasmaId), savedMapAntimatter = gs.backpack.Count(GameState.AntimatterId);
            yield return new Shot("map-no-fuel", () =>
            {
                gs.backpack.Set(db.fuel.id, 0); gs.backpack.Set(GameState.PlasmaId, 0); gs.backpack.Set(GameState.AntimatterId, 0);
                ui.DebugMap(gs.universe.Get("cinder"), 1f);
            }, 0.8f);
            gs.backpack.Set(db.fuel.id, savedMapFuel);
            gs.backpack.Set(GameState.PlasmaId, savedMapPlasma); gs.backpack.Set(GameState.AntimatterId, savedMapAntimatter);
            yield return new Shot("map-flight-autopilot", ui.DebugAutopilotScreen);

            SaveV2 networkBaseline = null;
            void PrepareNetworkFixture(bool complete = false)
            {
                ResetView();
                if (networkBaseline == null && !gs.TryCreateCloudSaveSnapshot(out networkBaseline, out var error))
                    throw new InvalidOperationException(error);
                var candidate = JsonUtility.FromJson<SaveV2>(JsonUtility.ToJson(networkBaseline)).Normalize();
                candidate.network = new NetworkProgress { completed = complete,
                    archivePlanetId = complete ? candidate.location.currentPlanetId : "" };
                foreach (string id in new[] { "glacia", "ferrum" })
                    candidate.network.links.Add(new PlanetLink { fromPlanetId = candidate.location.currentPlanetId,
                        toPlanetId = gs.universe.Get(id).Id });
                candidate.player.researched.Add("warp_gate_engineering");
                candidate.player.upgrades.Add(new IdLevel { id = "hull_cold", level = 3 });
                candidate.player.fuelTank = new List<StackRecord>
                {
                    new StackRecord { resourceId = db.fuel.id, count = 42 },
                    new StackRecord { resourceId = "plasma", count = 9 },
                    new StackRecord { resourceId = "antimatter", count = 1 }
                };
                var home = candidate.worlds.Find(delta => delta.planetId == candidate.location.currentPlanetId);
                if (home == null) throw new InvalidOperationException("Network fixture requires a visited home world.");
                home.structures.RemoveAll(s => s.slot == 0);
                home.structures.Add(new StructureRecord { defId = "warp_gate", slot = 0, level = 1 });
                if (!gs.TryRestoreCloudSave(candidate, out string problem)) throw new InvalidOperationException(problem);
            }
            try
            {
                yield return new Shot("network-tanks", () => { PrepareNetworkFixture(); ui.DebugFuelTanks(); });
                yield return new Shot("network-project", () => { PrepareNetworkFixture(); ui.DebugNetwork(); });
                yield return new Shot("network-links", () => { PrepareNetworkFixture(); ui.DebugMap(gs.universe.Get("glacia"), 12); }, 0.8f);
                yield return new Shot("network-map-footer", () => { PrepareNetworkFixture(); ShowMapFooter(); }, 0.8f);
                yield return new Shot("network-finale", () => { PrepareNetworkFixture(true); ui.DebugNetworkFinale(); }, 14.2f);
            }
            finally
            {
                if (networkBaseline != null)
                {
                    if (ui.DebugRoot.Q("network-finale") != null) ui.HandleBack();
                    ResetView();
                    if (!gs.TryRestoreCloudSave(networkBaseline, out string problem))
                        throw new InvalidOperationException("Network fixture restoration failed: " + problem);
                }
            }

            foreach (var kind in new[] { StructureKind.Lumber, StructureKind.Mine, StructureKind.Refinery, StructureKind.Fabricator, StructureKind.Satellite, StructureKind.Gunner, StructureKind.Warehouse, StructureKind.FuelDepot, StructureKind.CargoDepot, StructureKind.Hangar, StructureKind.ShieldPylon, StructureKind.AutoHarvester })
            {
                var k = kind;
                yield return new Shot("popup-" + k.ToString().ToLower(), () => { ResetView(); ui.DebugTapStructure(At(k)); });
            }
            yield return new Shot("toast", () => { ResetView(); ui.DebugToast("+14 Wood", UiKit.Lime); ui.DebugToast("Backpack full!", UiKit.Red); }, 0.3f);

            ShipRig shipFixture = null;
            try
            {
                yield return new Shot("ship-rig-landed", () =>
                {
                    ResetView();
                    shipFixture = ShipRig.Create(transform);
                    shipFixture.transform.position = rt.Planet.Spin.position +
                        (rt.Camera.transform.position - rt.Planet.Spin.position).normalized * (rt.Planet.SurfaceRadius() + 0.7f);
                    shipFixture.transform.rotation = Quaternion.Euler(20f, 155f, -8f);
                    shipFixture.transform.localScale = Vector3.one * 0.8f;
                    shipFixture.SetFlight(0f, 1f, 0f);
                });
            }
            finally { if (shipFixture != null) Destroy(shipFixture.gameObject); }

            PlacedStructure constructionFixture = null;
            string constructionEnd = null;
            void PrepareConstructionFixture()
            {
                GoTo("sylva");
                ResetView();
                if (constructionFixture == null)
                {
                    constructionFixture = At(StructureKind.Refinery);
                    if (constructionFixture == null) throw new InvalidOperationException("Missing construction showcase refinery.");
                    constructionEnd = constructionFixture.record.buildEndUtc;
                }
                gs.DebugSetBuilderJob(constructionFixture, 3600);
            }
            try
            {
                yield return new Shot("structure-moments-construction", () =>
                {
                    PrepareConstructionFixture();
                    ui.DebugTapStructure(constructionFixture);
                }, 0.3f);
                yield return new Shot("construction-crews", () =>
                {
                    PrepareConstructionFixture();
                    ui.DebugBuilders();
                }, 0.3f);
            }
            finally
            {
                if (constructionFixture != null)
                {
                    gs.DebugSetBuilderJob(constructionFixture, 0);
                    constructionFixture.record.buildEndUtc = constructionEnd;
                    gs.Notify();
                }
            }

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
            yield return new Shot("game-drill", () => DrillShot(false), 1.8f);
            yield return new Shot("harvest-boost", () => { DrillShot(false); ui.DebugDrillPreview(false); }, 1.8f);
            yield return new Shot("harvest-overheat", () => { DrillShot(false); ui.DebugDrillPreview(true); }, 1.8f);
            yield return new Shot("minigame-drill-result", () => DrillShot(true), 1.2f);
            yield return new Shot("glacia-hud", () => { ResetView(); }, 0.3f);
            yield return new Shot("wave-hud", () => { GoTo("sylva"); ResetView(); rt.Defense.StartWave(3); }, 4.0f);
            yield return new Shot("wave-result", () => rt.Defense.DebugEnd(true), 0.5f);
            void PrepareBossWave()
            {
                if (rt.Defense.Active) rt.Defense.DebugEnd(false);
                GoTo("ferrum");
                ResetView();
                while (gs.NextWave(gs.planet) < 10) gs.WaveCleared(gs.planet);
                gs.backpack.Set(gs.db.fuel.id, 40);
                if (At(StructureKind.Alarm) == null && gs.DebugPlace(db.FindStructure("alarm"), 0) == null)
                    throw new InvalidOperationException("Cannot prepare boss wave alarm fixture.");
            }
            yield return new Shot("wave-boss-preview", () =>
            {
                PrepareBossWave();
                ui.DebugTapStructure(At(StructureKind.Alarm));
            });
            yield return new Shot("wave-boss-hud", () =>
            {
                PrepareBossWave();
                if (!rt.Defense.StartWave(1)) throw new InvalidOperationException("Cannot start boss wave fixture.");
                // Hold a representative ranked pair in view rather than capturing a timing-dependent reactor loss.
                rt.Defense.StopAllCoroutines();
                float depth = Vector3.Dot(rt.Planet.Spin.position - rt.Camera.transform.position, rt.Camera.transform.forward);
                foreach (var rank in new[] { AlienRank.Elite, AlienRank.Boss })
                {
                    var enemy = Array.Find(db.enemies, e => e.rank == rank);
                    if (enemy == null) throw new InvalidOperationException("Missing ranked alien fixture: " + rank);
                    var pos = rt.Camera.ViewportToWorldPoint(new Vector3(rank == AlienRank.Boss ? 0.8f : 0.2f, 0.65f, depth));
                    var alien = AlienActor.Spawn(enemy, rt.Defense.Current.healthMult, pos, rt.Defense);
                    alien.enabled = false;
                    alien.Damage(alien.maxHealth * 0.3f);
                }
            }, 0.5f);
            yield return new Shot("wave-boss-result", () => rt.Defense.DebugEnd(false), 0.5f);
            yield return new Shot("hud-after", ResetView, 0.3f);
            yield return new Shot("menu-release", () => ui.DebugMenuAsRelease(), 0.2f);
            yield return new Shot("icons-ui", () => ui.DebugIconSheet(0), 0.3f);
            yield return new Shot("icons-items", () => ui.DebugIconSheet(1), 0.3f);
            yield return new Shot("icons-economy-resources-1", () => ui.DebugIconSheet(3), 0.3f);
            yield return new Shot("icons-economy-resources-2", () => ui.DebugIconSheet(4), 0.3f);
            yield return new Shot("icons-economy-structures-1", () => ui.DebugIconSheet(5), 0.3f);
            yield return new Shot("icons-economy-structures-2", () => ui.DebugIconSheet(6), 0.3f);
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
            yield return new Shot("map-generated", () => { GoTo(FindWorld(ColossalSpec)); ui.DebugMap(null); }, 0.8f);
            yield return new Shot("menu-debug-teleport", () => ui.DebugMenu("debug", true), 0.5f);
            yield return new Shot("popup-radartower", () => { ResetView(); ui.DebugTapStructure(At(StructureKind.RadarTower)); }, 0.3f);
            yield return new Shot("game-siphon", () =>
            {
                var world = gs.universe.FindGenerated(d =>
                {
                    foreach (var deposit in d.Deposits)
                        if (deposit.Method == Frontier.Universe.HarvestMethod.Siphon) return true;
                    return false;
                }, 2, 0, 10000);
                if (world == null) throw new InvalidOperationException("no generated world with siphon deposits");
                GoTo(world);
                if (!ui.DebugGather(MinigameKind.Siphon)) throw new InvalidOperationException("world has no siphon gather spot");
                ui.DebugHarvestPreview(MinigameKind.Siphon);
            }, 1.8f);
            yield return new Shot("game-excavate", () =>
            {
                var world = gs.universe.FindGenerated(d =>
                {
                    foreach (var deposit in d.Deposits)
                        if (deposit.Method == Frontier.Universe.HarvestMethod.Excavate) return true;
                    return false;
                }, 2, 0, 10000);
                if (world == null) throw new InvalidOperationException("no generated world with excavation deposits");
                GoTo(world);
                if (!ui.DebugGather(MinigameKind.Excavate)) throw new InvalidOperationException("world has no excavation gather spot");
                ui.DebugHarvestPreview(MinigameKind.Excavate);
            }, 1.8f);
            yield return new Shot("game-salvage", () =>
            {
                var world = gs.universe.FindGenerated(d =>
                {
                    foreach (var deposit in d.Deposits)
                        if (deposit.Method == Frontier.Universe.HarvestMethod.Salvage) return true;
                    return false;
                }, 2, 0, 10000);
                if (world == null) throw new InvalidOperationException("no generated world with salvage deposits");
                GoTo(world);
                if (!ui.DebugGather(MinigameKind.Salvage)) throw new InvalidOperationException("world has no salvage gather spot");
                ui.DebugHarvestPreview(MinigameKind.Salvage);
            }, 1.8f);
            yield return new Shot("obstacle-surface", () =>
            {
                obstacleShowcase = gs.universe.FindGenerated(d => d.Tier > 0 &&
                    gs.ObstaclesOn(gs.universe.Get(d)).Exists(o => o.seconds > 0), 1, 0, 10000);
                if (obstacleShowcase == null) throw new InvalidOperationException("no generated world with timed surface obstructions");
                GoTo(obstacleShowcase);
                var obstacle = gs.ObstaclesOn(obstacleShowcase).Find(o => o.seconds > 0);
                obstacleShowcasePad = obstacle.pad;
                ResetView();
                ui.DebugBuild(db.StructureOfKind(StructureKind.Refinery));
            }, 0.3f);
            yield return new Shot("obstacle-clearance", () =>
            {
                if (obstacleShowcase == null || obstacleShowcasePad < 0)
                    throw new InvalidOperationException("obstacle surface showcase did not initialize");
                GoTo(obstacleShowcase);
                if (!ui.DebugTapObstacle(obstacleShowcasePad))
                    throw new InvalidOperationException("surface obstruction could not be selected");
            }, 0.3f);
            yield return new Shot("popup-terraformer", () =>
            {
                var world = FindWorld(ColossalSpec);
                if (world == null) throw new InvalidOperationException("no colossal Terraformer showcase world");
                GoTo(world);
                showcaseWorld = world.Id;
                ResetView();
                var terraformer = gs.StructureOfKind(world, StructureKind.Terraformer);
                if (terraformer == null)
                {
                    int slot = world.BuildingSlots - 1;
                    while (slot >= 0 && gs.StructureAt(world, slot) != null) slot--;
                    if (slot < 0) throw new InvalidOperationException("no free colossal pad for the Terraformer showcase");
                    terraformer = gs.DebugPlace(db.StructureOfKind(StructureKind.Terraformer), slot, 3);
                    if (terraformer == null) throw new InvalidOperationException("Terraformer showcase could not be placed");
                    gs.stats.megaprojectStages = Math.Max(gs.stats.megaprojectStages, terraformer.level);
                    gs.Notify();
                }
                ui.DebugTapStructure(terraformer);
            }, 0.3f);
            yield return new Shot("popup-warpgate", () =>
            {
                var world = FindWorld(ColossalSpec);
                if (world == null) throw new InvalidOperationException("no colossal Warp Gate showcase world");
                GoTo(world);
                showcaseWorld = world.Id;
                ResetView();
                var gate = gs.StructureOfKind(world, StructureKind.WarpGate);
                if (gate == null)
                {
                    int slot = world.BuildingSlots - 1;
                    while (slot >= 0 && gs.StructureAt(world, slot) != null) slot--;
                    if (slot < 0) throw new InvalidOperationException("no free colossal pad for the Warp Gate showcase");
                    gate = gs.DebugPlace(db.StructureOfKind(StructureKind.WarpGate), slot, 3);
                    if (gate == null) throw new InvalidOperationException("Warp Gate showcase could not be placed");
                }
                ui.DebugTapStructure(gate);
            }, 0.3f);
            yield return new Shot("popup-dysoncollector", () =>
            {
                var world = FindWorld(ColossalSpec);
                if (world == null) throw new InvalidOperationException("no colossal Dyson Collector showcase world");
                GoTo(world);
                showcaseWorld = world.Id;
                ResetView();
                var collector = gs.StructureOfKind(world, StructureKind.DysonCollector);
                if (collector == null)
                {
                    int slot = world.BuildingSlots - 1;
                    while (slot >= 0 && gs.StructureAt(world, slot) != null) slot--;
                    if (slot < 0) throw new InvalidOperationException("no free colossal pad for the Dyson Collector showcase");
                    collector = gs.DebugPlace(db.StructureOfKind(StructureKind.DysonCollector), slot, 3);
                    if (collector == null) throw new InvalidOperationException("Dyson Collector showcase could not be placed");
                }
                collector.record.offlineFuelEnergy = collector.def.EffectAt(collector.level) * 3.25;
                gs.world.Touch();
                gs.Notify();
                ui.DebugTapStructure(collector);
            }, 0.3f);

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
                StructureKind.Gunner, StructureKind.Warehouse, StructureKind.Laser, StructureKind.Gunner, StructureKind.RadarTower
            };
            int[] slots = { 0, 4, 9, 13, 18, 22, 27, 31, 36, 40, 45, 49, 54, 58, 61, 63, 62 };
            for (int i = 0; i < kinds.Length; i++)
            {
                var def = db.StructureOfKind(kinds[i]);
                if (def != null) gs.DebugPlace(def, slots[i], 1 + i % 3);
            }
        }
    }
}
#endif
