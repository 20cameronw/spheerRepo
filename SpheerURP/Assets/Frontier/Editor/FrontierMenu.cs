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

        [MenuItem("Frontier/Reset Save")]
        public static void ResetSave()
        {
            string p = Path.Combine(Application.persistentDataPath, "frontier_save.json");
            if (File.Exists(p)) File.Delete(p);
        }
    }
}
#endif
