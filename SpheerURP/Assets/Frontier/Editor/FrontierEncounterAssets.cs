using System;
using Frontier.Hyperspace;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class FrontierEncounterAssets
    {
        const string Root = "Assets/Frontier/Resources/Frontier";

        [MenuItem("Assets/Frontier/Validate Encounter Fairness", true)]
        static bool CanValidate() { return Selection.activeObject is EncounterSO; }

        [MenuItem("Assets/Frontier/Validate Encounter Fairness")]
        public static void ValidateSelected()
        {
            var asset = Selection.activeObject as EncounterSO;
            if (asset == null) throw new InvalidOperationException("Select a Frontier Encounter asset.");
            var template = EncounterLibrary.FromTemplates(new[] { asset.template })[0];
            if (template.role == EncounterRole.Boss)
                throw new InvalidOperationException("Guardian behavior requires the dedicated boss-phase fairness tests, not the formation solver.");
            var tuning = new HyperspaceTuning();
            var solver = new FairnessSolver(tuning);
            int checks = 0;
            for (int tier = template.minTier; tier <= template.maxTier; tier++)
                foreach (float fraction in new[] { tuning.speedStartFraction, 1f })
                {
                    float speed = (float)((tuning.speedBase + (double)tuning.speedPerTier * tier + tuning.speedPerDistanceTerm) * fraction);
                    for (int mirror = 0; mirror < (template.mirrorable ? 2 : 1); mirror++)
                    {
                        var result = solver.SolveTemplate(template, tier, mirror == 1, speed, template.declaredSpeed);
                        if (!result.solvable)
                            throw new InvalidOperationException(template.id + ": unsolvable at tier " + tier +
                                ", speed fraction " + fraction + ", mirrored " + (mirror == 1) + ", time " + result.failTime);
                        checks++;
                    }
                }
            Debug.Log(template.id + ": " + checks + " fairness instances passed.", asset);
        }

        [MenuItem("Frontier/Hyperspace/Create Encounter Assets If Missing")]
        public static void CreateAssets()
        {
            const string catalogPath = Root + "/EncounterCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<EncounterCatalogSO>(catalogPath);
            if (catalog == null)
            {
                if (!AssetDatabase.IsValidFolder(Root + "/Encounters"))
                    AssetDatabase.CreateFolder(Root, "Encounters");
                var defaults = EncounterLibrary.CreateDefault();
                catalog = ScriptableObject.CreateInstance<EncounterCatalogSO>();
                catalog.encounters = new EncounterSO[defaults.Count];
                for (int i = 0; i < defaults.Count; i++)
                {
                    var template = defaults[i];
                    string path = Root + "/Encounters/" + template.id + ".asset";
                    var asset = AssetDatabase.LoadAssetAtPath<EncounterSO>(path);
                    if (asset == null)
                    {
                        asset = ScriptableObject.CreateInstance<EncounterSO>();
                        asset.template = template;
                        AssetDatabase.CreateAsset(asset, path);
                    }
                    catalog.encounters[i] = asset;
                }
                catalog.BuildLibrary();
                AssetDatabase.CreateAsset(catalog, catalogPath);
                AssetDatabase.SaveAssets();
            }
            Debug.Log("Frontier authored encounter catalog ready: " + catalog.BuildLibrary().Count + " templates.");
        }
    }
}
