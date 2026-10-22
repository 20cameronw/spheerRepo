#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Frontier.EditorTools
{
    public static class FrontierMenu
    {
        const string Dir = "Assets/Frontier/Resources/Frontier";

        [MenuItem("Frontier/Generate Editable Data Assets")]
        public static void Generate()
        {
            if (!CanGenerateDefaults(Dir + "/GameDatabase.asset", Dir + "/Data", out var reason))
            {
                Debug.LogWarning("Frontier: default asset bootstrap stopped without changing assets: " + reason +
                    " Use Frontier > Economy > Import JSON to update the reviewed economy content.");
                if (!Application.isBatchMode) EditorUtility.DisplayDialog("Frontier", reason +
                    "\n\nExisting assets were not changed. Use Frontier > Economy > Import JSON to update reviewed economy content.", "OK");
                return;
            }

            var db = DefaultContent.Create();
            Directory.CreateDirectory(Dir + "/Data");

            void Save(Object o, string sub) { AssetDatabase.CreateAsset(o, Dir + "/Data/" + sub + "_" + o.name + ".asset"); }
            foreach (var o in db.resources) Save(o, "Resource");
            foreach (var o in db.planets) Save(o, "Planet");
            foreach (var o in db.upgrades) Save(o, "Upgrade");
            foreach (var o in db.enemies) Save(o, "Alien");
            foreach (var o in db.recipes) Save(o, "Recipe");
            foreach (var o in db.structures) Save(o, "Structure");
            AssetDatabase.CreateAsset(db.formula, Dir + "/Data/WaveFormula.asset");
            AssetDatabase.CreateAsset(db, Dir + "/GameDatabase.asset");
            if (AssetDatabase.LoadAssetAtPath<FrontierSettings>(Dir + "/FrontierSettings.asset") == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<FrontierSettings>(), Dir + "/FrontierSettings.asset");
            AssetDatabase.SaveAssets();
            Selection.activeObject = db;
            Debug.Log("Frontier: data assets generated in " + Dir);
        }

        public static bool CanGenerateDefaults(string databasePath, string dataDirectory, out string reason)
        {
            if (File.Exists(databasePath))
            {
                reason = "A GameDatabase already exists at " + databasePath + ".";
                return false;
            }
            if (Directory.Exists(dataDirectory) && Directory.GetFiles(dataDirectory, "*.asset", SearchOption.AllDirectories).Length > 0)
            {
                reason = "Default data assets already exist under " + dataDirectory + ".";
                return false;
            }
            reason = null;
            return true;
        }

        [MenuItem("Frontier/Setup Scene Objects")]
        public static void SetupScene()
        {
            const string scenePath = "Assets/Frontier/Scenes/Frontier.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != scenePath && File.Exists(scenePath))
                scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            var boot = Object.FindAnyObjectByType<FrontierBootstrap>();
            if (boot == null) { Debug.LogError("Frontier: no FrontierBootstrap in the scene."); return; }

            if (AssetDatabase.LoadAssetAtPath<FrontierSettings>(Dir + "/FrontierSettings.asset") == null)
            {
                Directory.CreateDirectory(Dir);
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<FrontierSettings>(), Dir + "/FrontierSettings.asset");
            }
            string panelPath = Dir + "/FrontierPanelSettings.asset";
            var panel = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(panelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
                panel.themeStyleSheet = Resources.Load<UnityEngine.UIElements.ThemeStyleSheet>("Frontier/FrontierTheme");
                AssetDatabase.CreateAsset(panel, panelPath);
            }

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            var light = Object.FindAnyObjectByType<Light>();
            if (light == null)
            {
                light = new GameObject("Key Light").AddComponent<Light>();
                light.type = LightType.Directional;
            }

            boot.database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Dir + "/GameDatabase.asset");
            boot.settings = AssetDatabase.LoadAssetAtPath<FrontierSettings>(Dir + "/FrontierSettings.asset");
            boot.panelSettings = panel;
            boot.sceneCamera = cam;
            boot.keyLight = light;
            EditorUtility.SetDirty(boot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Frontier: scene objects set up");
        }

        public static void GenerateAll()
        {
            Generate();
            SetupScene();
        }

        [MenuItem("Frontier/Verify Data Assets")]
        public static void Verify()
        {
            var db = Resources.Load<GameDatabase>("Frontier/GameDatabase");
            if (db == null) { Debug.LogError("Frontier: verify - no GameDatabase asset"); return; }
            int tiers = 0;
            foreach (var u in db.upgrades) if (u != null && u.levelCosts != null) tiers += u.levelCosts.Count;
            Debug.Log("Frontier: verify OK res=" + db.resources.Length + " planets=" + db.planets.Length + " upgrades=" + db.upgrades.Length +
                " structs=" + db.structures.Length + " tiers=" + tiers + " fuel=" + (db.fuel != null) + " start=" + (db.startPlanet != null) +
                " systems=" + db.planets[0].systemId + " formula=" + (db.formula != null));
        }

        [MenuItem("Frontier/Generate Deep Space Planets")]
        public static void GenerateDeepSpacePlanets()
        {
            var db = Resources.Load<GameDatabase>("Frontier/GameDatabase");
            if (db == null) { Debug.LogError("Frontier: no GameDatabase asset found."); return; }

            Directory.CreateDirectory(Dir + "/Data");
            var currentPlanets = new List<PlanetSO>(db.planets);
            var roster = PlanetGenerator.GenerateDeepSpaceRoster(db);
            int added = 0;
            foreach (var p in roster)
            {
                if (currentPlanets.Exists(existing => existing != null && existing.id == p.id)) continue;
                string path = Dir + "/Data/Planet_" + p.id + ".asset";
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(p, path);
                currentPlanets.Add(p);
                added++;
            }
            db.planets = currentPlanets.ToArray();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log("Frontier: added " + added + " deep space planets to GameDatabase (total " + db.planets.Length + ").");
        }

        const string PrefabDir = "Assets/Frontier/Art/Models/Prefabs/";
        const string SourceDir = "Assets/Frontier/Art/Models/Source/";

        [MenuItem("Frontier/Build Model Library")]
        public static void BuildModelLibrary()
        {
            // key -> hand-made model. Reassign these in the generated asset to taste.
            string P(string name) { return PrefabDir + name + ".prefab"; }
            string F(string name) { return SourceDir + name + ".fbx"; }
            var map = new (string key, string path, float scale)[]
            {
                ("sylva", P("lerth"), 1f),
                ("ferrum", F("planet_2"), 1f),
                ("glacia", P("purp"), 1f),
                ("cinder", F("planet_2"), 1f),
                ("arbor", F("planet_3"), 1f),
                ("nyx", P("domny"), 1f),
                ("aurum", F("planet_1"), 1f),
                ("tide", P("lerth"), 1f),
                ("zephyr", F("tiny world"), 1f),
                ("pyre", F("tiny world"), 1f),
                ("halcyon", P("purp"), 1f),
                ("mire", F("planet_3"), 1f),
                ("obsidian", P("domny"), 1f),
                ("vesper", F("planet_2"), 1f),
                ("prism", F("planet_1"), 1f),
                ("kaos", F("planet_3"), 1f),
                ("tundra", F("tiny world"), 1f),
                ("coral", P("lerth"), 1f),
                ("scout", F("ufo (2)"), 1f),
                ("swarmer", F("space_trash"), 1f),
                ("raider", F("spaceship"), 1f),
                ("brute", F("ufo (2)"), 1f),
                ("titan", F("ufo (2)"), 1f),
                ("turret", F("gunner"), 1f),
                ("struct_refinery", P("silo_with_missil"), 1f),
                ("struct_alarm", P("sat_dish"), 1f),
                ("struct_mine", F("space_trash"), 1f),
                ("struct_drill", P("drill"), 1f),
                ("struct_gunner", F("gunner"), 1f),
                ("struct_laser", P("lazer"), 1f),
                ("struct_satellite", P("satellite"), 1f),
            };
            var list = new System.Collections.Generic.List<ModelLibrary.Entry>();
            foreach (var m in map)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(m.path);
                if (prefab == null) { Debug.LogWarning("Frontier: missing model " + m.path); continue; }
                list.Add(new ModelLibrary.Entry { key = m.key, prefab = prefab, scale = m.scale });
            }
            Directory.CreateDirectory(Dir);
            string path = Dir + "/ModelLibrary.asset";
            var lib = AssetDatabase.LoadAssetAtPath<ModelLibrary>(path);
            if (lib == null) { lib = ScriptableObject.CreateInstance<ModelLibrary>(); AssetDatabase.CreateAsset(lib, path); }
            lib.entries = list.ToArray();
            ModelScaleCalibration.BakeLibrary(lib);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("Frontier: model library built with " + list.Count + " entries");
        }

        [MenuItem("Frontier/Reset Save")]
        public static void ResetSave()
        {
            // the save, its backup, a leftover temp file and the legacy v1 backup
            if (!Directory.Exists(Application.persistentDataPath)) return;
            foreach (string file in Directory.GetFiles(Application.persistentDataPath, "frontier_save*")) File.Delete(file);
        }
    }
}
#endif
