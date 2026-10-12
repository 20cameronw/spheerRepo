#if UNITY_EDITOR
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
            if (File.Exists(Dir + "/GameDatabase.asset") && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Frontier", "A GameDatabase already exists. Overwrite it with defaults?", "Overwrite", "Cancel"))
                return;

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

        [MenuItem("Frontier/Setup Scene Objects")]
        public static void SetupScene()
        {
            const string scenePath = "Assets/Frontier/Scenes/Frontier.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != scenePath && File.Exists(scenePath))
                scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            var boot = Object.FindFirstObjectByType<FrontierBootstrap>();
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
            var light = Object.FindFirstObjectByType<Light>();
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

        [MenuItem("Frontier/Build Model Library")]
        public static void BuildModelLibrary()
        {
            // key -> hand-made model. Reassign these in the generated asset to taste.
            var map = new (string key, string path, float scale)[]
            {
                ("sylva", "Assets/Prefabs/lerth.prefab", 1f),
                ("ferrum", "Assets/Models/planet_2.fbx", 1f),
                ("glacia", "Assets/Prefabs/purp.prefab", 1f),
                ("cinder", "Assets/Models/planet_2.fbx", 1f),
                ("arbor", "Assets/Models/planet_3.fbx", 1f),
                ("nyx", "Assets/Prefabs/domny.prefab", 1f),
                ("aurum", "Assets/Models/planet_1.fbx", 1f),
                ("tide", "Assets/Prefabs/lerth.prefab", 1f),
                ("zephyr", "Assets/Models/tiny world.fbx", 1f),
                ("pyre", "Assets/Models/tiny world.fbx", 1f),
                ("halcyon", "Assets/Prefabs/purp.prefab", 1f),
                ("mire", "Assets/Models/planet_3.fbx", 1f),
                ("obsidian", "Assets/Prefabs/domny.prefab", 1f),
                ("vesper", "Assets/Models/planet_2.fbx", 1f),
                ("prism", "Assets/Models/planet_1.fbx", 1f),
                ("kaos", "Assets/Models/planet_3.fbx", 1f),
                ("tundra", "Assets/Models/tiny world.fbx", 1f),
                ("coral", "Assets/Prefabs/lerth.prefab", 1f),
                ("scout", "Assets/Models/ufo (2).fbx", 1f),
                ("swarmer", "Assets/Models/space_trash.fbx", 1f),
                ("raider", "Assets/Models/spaceship.fbx", 1f),
                ("brute", "Assets/Models/ufo (2).fbx", 1f),
                ("titan", "Assets/Models/ufo (2).fbx", 1f),
                ("turret", "Assets/Models/gunner.fbx", 1f),
                ("struct_refinery", "Assets/Prefabs/silo_with_missil.prefab", 1f),
                ("struct_alarm", "Assets/Prefabs/sat_dish.prefab", 1f),
                ("struct_mine", "Assets/Models/space_trash.fbx", 1f),
                ("struct_drill", "Assets/Prefabs/drill.prefab", 1f),
                ("struct_gunner", "Assets/Models/gunner.fbx", 1f),
                ("struct_laser", "Assets/Prefabs/lazer.prefab", 1f),
                ("struct_satellite", "Assets/Prefabs/satellite.prefab", 1f),
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
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("Frontier: model library built with " + list.Count + " entries");
        }

        [MenuItem("Frontier/Build Icons")]
        public static void BuildIcons()
        {
            // icon key -> your original sprite. Dark sprites are recolored white so the UI can tint them neon.
            var map = new (string key, string path)[]
            {
                ("build", "Assets/Dark UI/Free/Shop.png"),
                ("map", "Assets/Dark UI/Free/World.png"),
                ("gear", "Assets/Dark UI/Free/Settings.png"),
                ("ranks", "Assets/Dark UI/Free/Ranks.png"),
                ("about", "Assets/Dark UI/Free/Info.png"),
                ("tutorial", "Assets/Dark UI/Free/Missions.png"),
                ("pack", "Assets/Dark UI/Free/Inventory.png"),
                ("grid", "Assets/Images/apps-grid-icon.png"),
                ("research", "Assets/Images/flask-icon.png"),
                ("tools", "Assets/Images/tools-icon.png"),
                ("alien", "Assets/Images/aliens-icon.png"),
            };
            string outDir = "Assets/Frontier/Resources/Frontier/Icons";
            Directory.CreateDirectory(outDir);
            int n = 0;
            foreach (var m in map)
            {
                if (!File.Exists(m.path)) { Debug.LogWarning("Frontier: missing icon " + m.path); continue; }
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(m.path))) continue;
                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++) { px[i].r = 255; px[i].g = 255; px[i].b = 255; }
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(outDir + "/" + m.key + ".png", tex.EncodeToPNG());
                n++;
            }
            AssetDatabase.Refresh();
            Debug.Log("Frontier: built " + n + " icons");
        }

        [MenuItem("Frontier/Reset Save")]
        public static void ResetSave()
        {
            string p = Path.Combine(Application.persistentDataPath, "frontier_save.json");
            if (File.Exists(p)) File.Delete(p);
        }
    }
}
#endif
