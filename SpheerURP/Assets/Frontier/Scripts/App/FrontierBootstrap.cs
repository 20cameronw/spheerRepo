using System.Collections;
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

        IEnumerator Start()
        {
            FrontierSettings.Use(settings);
            var cfg = FrontierSettings.I;
            Application.targetFrameRate = cfg.targetFrameRate;

            var db = GameDatabase.ForRuntime(database);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            HarnessRunner.PrepareFromCommandLine();
#endif
            var gs = new GameState(db);
            TravelMinigames.Create = () =>
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (TravelPrefs.UseLegacyGame) return new LegacyTravelMinigame();
#endif
                return new Hyperspace.View.HyperspaceGame(gs);
            };
            TravelDebug.Actions.Clear();
            TravelDebug.Register("Hyperspace determinism self-check", () =>
            {
                return Hyperspace.View.HyperspaceDiagnostics.Check();
            });
            new GameObject("App").AddComponent<FrontierApp>().Init(gs);

            var cam = sceneCamera != null ? sceneCamera : Camera.main;
            GameObject camGo;
            if (cam == null)
            {
                camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
            }
            camGo = cam.gameObject;
            camGo.transform.position = new Vector3(0f, 0f, -12f);
            camGo.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cfg.backgroundColor;
            cam.fieldOfView = cfg.cameraFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.allowHDR = true;
            if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            Sfx.Init(camGo);
            Fx.SetupPost(cam);
            if (camGo.GetComponent<CameraFit>() == null) camGo.AddComponent<CameraFit>();

            var backdrop = new GameObject("Backdrop").AddComponent<SpaceBackdrop>();
            backdrop.Build(cam);

            var planet = new GameObject("Planet").AddComponent<PlanetView>();
            planet.Rebuilt += backdrop.Apply;
            planet.Show(gs.planet, true);

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
            ui.Init(doc, gs, defense, planet, backdrop, surface, cam);

            FrontierRuntime.Publish(new FrontierRuntime
            {
                Db = db, State = gs, Camera = cam, Backdrop = backdrop, Planet = planet,
                Defense = defense, Surface = surface, Ui = ui, Document = doc
            });
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
