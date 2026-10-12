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
            if (File.Exists(Dir + "/GameDatabase.asset") &&
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
            AssetDatabase.CreateAsset(db.formula, Dir + "/Data/WaveFormula.asset");
            AssetDatabase.CreateAsset(db, Dir + "/GameDatabase.asset");
            AssetDatabase.SaveAssets();
            Selection.activeObject = db;
            Debug.Log("Frontier data assets generated in " + Dir);
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
                ("cinder", "Assets/Prefabs/spiky_planet.prefab", 1f),
                ("arbor", "Assets/Models/planet_3.fbx", 1f),
                ("nyx", "Assets/Prefabs/domny.prefab", 1f),
                ("scout", "Assets/Models/ufo (2).fbx", 1f),
                ("swarmer", "Assets/Models/space_trash.fbx", 1f),
                ("raider", "Assets/Models/spaceship.fbx", 1f),
                ("brute", "Assets/Models/ufo (2).fbx", 1f),
                ("titan", "Assets/Models/ufo (2).fbx", 1f),
                ("turret", "Assets/Models/gunner.fbx", 1f),
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

        [MenuItem("Frontier/Reset Save")]
        public static void ResetSave()
        {
            string p = Path.Combine(Application.persistentDataPath, "frontier_save.json");
            if (File.Exists(p)) File.Delete(p);
        }
    }
}
#endif
