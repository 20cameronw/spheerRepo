#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier.EditorTools
{
    /// <summary>
    /// Icon library checks: every listed icon imports as a 48x48 VectorImage, every SVG on disk is listed, every resource and
    /// structure maps to its own glyph, and the keyword fallback behaves.
    /// </summary>
    public static class FrontierIconChecks
    {
        const string Folder = "Assets/Frontier/Resources/Frontier/Icons";

        [MenuItem("Frontier/Validate Icons")]
        public static void Run()
        {
            var listed = new HashSet<string>();
            foreach (var group in Icons.Groups) foreach (var name in group) listed.Add(name);

            foreach (var name in listed)
            {
                var image = Resources.Load<VectorImage>("Frontier/Icons/" + name);
                Check(image != null, "icon is missing or not imported as a VectorImage: " + name);
                Check(Mathf.Approximately(image.width, 48f) && Mathf.Approximately(image.height, 48f),
                    name + " must keep the 48x48 grid (is the invisible full-viewBox rect still in the SVG?), got " + image.width + "x" + image.height);
            }

            foreach (var file in Directory.GetFiles(Folder, "*.svg"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                Check(listed.Contains(name), "SVG is not listed in Icons.Groups: " + name);
            }

            var seen = new Dictionary<string, string>();
            var db = GameDatabase.Load();
            foreach (var resource in db.resources)
            {
                if (resource == null) continue;
                string glyph = Icons.ForResource(resource);
                Check(glyph != Icons.Fallback && Icons.Has(glyph), "resource without its own icon: " + resource.id);
                string other;
                Check(!seen.TryGetValue(glyph, out other), "resources share a glyph: " + resource.id + " and " + other + " (" + glyph + ")");
                seen[glyph] = resource.id;
            }

            var structureGlyphs = new HashSet<string>();
            foreach (StructureKind kind in Enum.GetValues(typeof(StructureKind)))
            {
                string glyph = Icons.ForStructure(kind);
                Check(glyph != Icons.Fallback && Icons.Has(glyph), "structure without an icon: " + kind);
                Check(structureGlyphs.Add(glyph), "structures share a glyph: " + kind + " (" + glyph + ")");
            }

            Check(Icons.ForResourceId("copper_ore") == "copper_ore", "canonical ids take precedence over keywords");
            Check(Icons.ForResourceId("unknown_copper_ore") == "iron", "keyword fallback: unknown_copper_ore");
            Check(Icons.ForResourceId("helium") == "fuel", "keyword fallback: helium");
            Check(Icons.ForResourceId("???") == Icons.Fallback, "unknown ids use the generic glyph");

            var economy = Resources.Load<GameDatabase>("Frontier/EconomyDatabase");
            Check(economy != null, "staged economy database is missing");
            var economyGlyphs = new HashSet<string>();
            foreach (var resource in economy.resources)
            {
                string glyph = Icons.ForResource(resource);
                Check(glyph == resource.id && Icons.Has(glyph), "economy resource without canonical glyph: " + resource.id);
                Check(economyGlyphs.Add(glyph), "economy resources share a glyph: " + glyph);
            }

            Debug.Log("Frontier: icon checks passed (" + listed.Count + " icons at 48x48, " + db.resources.Length + " resources and " +
                structureGlyphs.Count + " structures have distinct glyphs).");
        }

        static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("Icon check failed: " + label);
        }

    }
}
#endif
