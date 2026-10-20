using System.Collections;
using Frontier.Services;
using Frontier.Services.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    /// <summary>
    /// Single entry point for the Frontier scene. Everything (camera, post-processing, planet,
    /// combat, UI Toolkit UI) is created from code, driven by the ScriptableObject GameDatabase.
    /// </summary>
    public class FrontierBootstrap : MonoBehaviour
    {
        [Header("Optional overrides (assign in the Inspector; anything left empty is built from code/Resources)")]
        public GameDatabase database;
        public FrontierSettings settings;
        public PanelSettings panelSettings;
        public Camera sceneCamera;
        public Light keyLight;
        GameDatabase tunedDatabase;

        IEnumerator Start()
        {
            FrontierSettings.Use(settings);
            var cfg = FrontierSettings.I;
            PresentationQuality.Initialize(cfg.targetFrameRate);
            yield return UiText.InitializeLocalization();

            var db = GameDatabase.ForRuntime(database);
            var consent = new ConsentService(new PlayerPrefsConsentStore());
            var serviceConfig = Resources.Load<FrontierUnityServicesConfig>("Frontier/FrontierUnityServicesConfig");
            var services = FrontierUnityServicesFactory.Create(consent, serviceConfig);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            HarnessRunner.PrepareFromCommandLine();
#endif
            var bundledDb = db;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!HarnessRunner.Requested)
#endif
                db = FrontierRemoteTravelTuning.SelectForSession(bundledDb, services);
            if (db != bundledDb) tunedDatabase = db;
            var gs = new GameState(db);
            TravelMinigames.Create = () =>
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (TravelPrefs.UseLegacyGame && (gs.PendingTravel == null ||
                    gs.PendingTravel.nextLeg >= gs.PendingTravel.legs.Count ||
                    gs.PendingTravel.legs[gs.PendingTravel.nextLeg].replay == null)) return new LegacyTravelMinigame();
#endif
                return new Hyperspace.View.HyperspaceGame(gs);
            };
            TravelMinigames.PrepareRecovery = (trip, from, to) => Hyperspace.View.HyperspaceGame.PrepareRecovery(gs, trip, from, to);
            TravelDebug.Actions.Clear();
            TravelDebug.Register("Hyperspace determinism self-check", () =>
            {
                return Hyperspace.View.HyperspaceDiagnostics.Check();
            });
            var app = new GameObject("App");
            app.AddComponent<FrontierApp>().Init(gs);
            app.AddComponent<FrontierRemoteTravelTuning>().Init(bundledDb, services);
            app.AddComponent<FrontierBuilderNotifications>().Init(gs, services.Notifications);

            var cam = sceneCamera != null ? sceneCamera : Camera.main;
            GameObject camGo;
            if (cam == null)
            {
                camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
            }

            void OnDestroy()
            {
                if (tunedDatabase != null) Destroy(tunedDatabase);
            }
            camGo = cam.gameObject;
            camGo.transform.position = new Vector3(0f, 0f, -12f);
            camGo.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cfg.backgroundColor;
            cam.fieldOfView = cfg.cameraFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            // Layer 31 belongs to the offscreen Hyperspace stage, not the world camera.
            cam.cullingMask &= ~(1 << 31);
            cam.allowHDR = true;
            if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            Sfx.Init(camGo);
            if (camGo.GetComponent<HarvestHaptics>() == null) camGo.AddComponent<HarvestHaptics>();
            Fx.SetupPost(cam);
            if (camGo.GetComponent<CameraFit>() == null) camGo.AddComponent<CameraFit>();

            var backdrop = new GameObject("Backdrop").AddComponent<SpaceBackdrop>();
            backdrop.Build(cam);

            var planet = new GameObject("Planet").AddComponent<PlanetView>();
            planet.Rebuilt += backdrop.Apply;
            planet.Show(gs.planet, true, gs.TerraformerStageOn(gs.planet));
            planet.gameObject.AddComponent<AscensionAura>().Init(planet, gs);

            var defense = new GameObject("Defense").AddComponent<DefenseController>();
            defense.Init(planet, cam, gs);

            // Work on a copy: runtime scale/target changes must never be serialized back into the asset.
            var panel = panelSettings != null ? Instantiate(panelSettings) : ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = Mathf.Max(0.2f, ScreenMetrics.ShortSide / Mathf.Max(100f, cfg.uiReferenceHeight));
            panel.clearColor = false;
            if (panel.themeStyleSheet == null) panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("Frontier/FrontierTheme");

            var light = keyLight;
            if (light == null) light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = cfg.lightColor;
            light.intensity = cfg.lightIntensity;
            light.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = cfg.ambientColor;

            var uiGo = new GameObject("UI");
            uiGo.SetActive(false);
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var ui = uiGo.AddComponent<UIRoot>();
            uiGo.SetActive(true);
            yield return null;

            var surface = new GameObject("WorldSurface").AddComponent<WorldSurface>();
            surface.Init(planet, cam, gs);
            surface.IsOverUi = ui.IsOverUi;
            defense.IsOverUi = ui.IsOverUi;
            defense.World = surface;
            ui.Init(doc, gs, defense, planet, backdrop, surface, cam, services);
            app.AddComponent<FrontierSoundscape>().Init(camGo.GetComponent<AudioService>(), ui, defense, gs);

            bool playIntro = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            playIntro = !HarnessRunner.Requested;
#endif
            if (playIntro && gs.PendingTravel == null)
            {
                bool firstLaunch = !TravelPrefs.IntroSeen;
                yield return ui.PlayArrivalIntro(firstLaunch);
                if (firstLaunch) TravelPrefs.MarkIntroSeen();
            }

            FrontierRuntime.Publish(new FrontierRuntime
            {
                Db = db, State = gs, Camera = cam, Backdrop = backdrop, Planet = planet,
                Defense = defense, Surface = surface, Ui = ui, Document = doc, Services = services
            });
            if (gs.PendingTravel != null) ui.ResumeInterruptedTravel();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (HarnessRunner.Requested) new GameObject("Harness").AddComponent<HarnessRunner>().Begin(FrontierRuntime.Current);
#endif
        }
    }

    /// <summary>Keeps the centered planet fully visible whatever the aspect ratio of the device.</summary>
    public class CameraFit : MonoBehaviour
    {
        Camera cam;
        float lastAspect;

        void Awake() { cam = GetComponent<Camera>(); }

        void LateUpdate()
        {
            if (cam == null) return;
            float aspect = ScreenMetrics.Aspect;
            if (Mathf.Approximately(aspect, lastAspect)) return;
            lastAspect = aspect;
            const float need = 3.5f;
            float t = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float d = Mathf.Max(need / (t * aspect), need / t);
            transform.position = new Vector3(0f, 0f, -Mathf.Clamp(d, 8f, 16f));
        }
    }
}
