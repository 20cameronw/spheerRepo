#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace Frontier.EditorTools
{
    /// <summary>
    /// Builds the dynamic SDF FontAssets used by Frontier.uss (-unity-font-definition) from the OFL TTFs in Art/Fonts:
    /// Chakra Petch for display text and labels, Barlow for body text. Glyphs for ASCII and Latin are pre-baked, anything
    /// else is rasterised on demand from the source font.
    /// </summary>
    public static class FrontierFonts
    {
        const string Dir = "Assets/Frontier/Art/Fonts";
        static readonly string[] Faces =
        {
            "ChakraPetch-Medium", "ChakraPetch-SemiBold", "ChakraPetch-Bold",
            "Barlow-Regular", "Barlow-Medium", "Barlow-SemiBold"
        };

        [MenuItem("Frontier/Build Font Assets")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var charset = Charset();
            foreach (var face in Faces) BuildFace(face, charset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Frontier: font assets built (" + Faces.Length + ").");
        }

        public static void Run() { Build(); }

        static string Charset()
        {
            var sb = new StringBuilder();
            for (int c = 32; c < 127; c++) sb.Append((char)c);
            for (int c = 160; c < 384; c++) sb.Append((char)c);
            sb.Append("\u2013\u2014\u2018\u2019\u201C\u201D\u2022\u2026\u2190\u2191\u2192\u2193\u00D7\u00B7");
            return sb.ToString();
        }

        static void BuildFace(string face, string charset)
        {
            string ttf = Dir + "/" + face + ".ttf";
            string path = Dir + "/" + face + "-SDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (font == null) { Debug.LogError("Frontier: missing font " + ttf); return; }
            if (AssetDatabase.LoadAssetAtPath<FontAsset>(path) != null) AssetDatabase.DeleteAsset(path);

            var asset = FontAsset.CreateFontAsset(font, 72, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = face + "-SDF";
            asset.TryAddCharacters(charset, out string missing);
            AssetDatabase.CreateAsset(asset, path);
            if (asset.material != null)
            {
                asset.material.name = face + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
            foreach (var atlas in asset.atlasTextures)
            {
                if (atlas == null || AssetDatabase.Contains(atlas)) continue;
                atlas.name = face + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            Debug.Log("Frontier: " + face + " baked " + asset.characterTable.Count + " glyphs on " + asset.atlasTextures.Length + " atlas page(s)"
                + (string.IsNullOrEmpty(missing) ? "" : "; not in font: " + missing.Length));
        }
    }
}
#endif
