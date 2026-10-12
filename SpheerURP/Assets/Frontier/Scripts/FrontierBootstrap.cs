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
        IEnumerator Start()
        {
            Application.targetFrameRate = 60;

            var db = GameDatabase.Load();
            var gs = new GameState(db);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 0f, -12f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.012f, 0.04f);
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            Sfx.Init(camGo);
            Fx.SetupPost(cam);
            camGo.AddComponent<CameraFit>();

            var backdrop = new GameObject("Backdrop").AddComponent<SpaceBackdrop>();
            backdrop.Build(cam);

            var planet = new GameObject("Planet").AddComponent<PlanetView>();
            planet.Show(gs.planet, true);

            var defense = new GameObject("Defense").AddComponent<DefenseController>();
            defense.Init(planet, cam, gs);

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = Mathf.Max(0.2f, Mathf.Min(Screen.width, Screen.height) / 1080f);
            panel.clearColor = false;
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("Frontier/FrontierTheme");

            var lightGo = new GameObject("Key Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.9f, 1f);
            light.intensity = 1.6f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.5f);

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
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            if (Mathf.Approximately(aspect, lastAspect)) return;
            lastAspect = aspect;
            const float need = 3.5f;
            float t = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float d = Mathf.Max(need / (t * aspect), need / t);
            transform.position = new Vector3(0f, 0f, -Mathf.Clamp(d, 8f, 16f));
        }
    }
}
