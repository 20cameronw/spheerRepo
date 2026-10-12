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
            camGo.transform.position = new Vector3(0f, -0.9f, -10f);
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

            var backdrop = new GameObject("Backdrop").AddComponent<SpaceBackdrop>();
            backdrop.Build(cam);

            var planet = new GameObject("Planet").AddComponent<PlanetView>();
            planet.Show(gs.planet, true);

            var defense = new GameObject("Defense").AddComponent<DefenseController>();
            defense.Init(planet, cam, gs);

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = Screen.height >= Screen.width ? new Vector2Int(1080, 1920) : new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
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

            defense.IsOverUi = ui.IsOverUi;
            ui.Init(doc, gs, defense, planet, backdrop);
        }
    }
}
