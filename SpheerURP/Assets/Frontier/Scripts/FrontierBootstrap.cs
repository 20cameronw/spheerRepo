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
