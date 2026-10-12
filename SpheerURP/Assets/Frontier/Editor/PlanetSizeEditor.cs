#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Frontier.EditorTools
{
    [CustomEditor(typeof(PlanetSO))]
    public class PlanetSizeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Ground Radius controls planet and placement size. Model Scale fine-tunes the visible mesh only. Atmosphere Scale is its diameter. These controls update live in Play mode.", MessageType.Info);
            if (GUILayout.Button("Reset this planet to standard size"))
            {
                Undo.RecordObject(target, "Reset planet size");
                var planet = (PlanetSO)target;
                planet.surfaceRadius = PlanetView.Radius;
                planet.modelScale = 1f;
                planet.atmosphereScale = 5f;
                EditorUtility.SetDirty(planet);
            }
            DrawDefaultInspector();
        }

        [MenuItem("Frontier/Normalize All Planet Sizes")]
        public static void NormalizeAll()
        {
            foreach (var planet in GameDatabase.Load().planets)
            {
                Undo.RecordObject(planet, "Normalize planet sizes");
                planet.surfaceRadius = PlanetView.Radius;
                planet.modelScale = 1f;
                planet.atmosphereScale = 5f;
                EditorUtility.SetDirty(planet);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Frontier: all planets normalized; ground radius 1.5, model scale 1, atmosphere scale 5.");
            FrontierPlanetChecks.Run();
        }
    }
}
#endif
