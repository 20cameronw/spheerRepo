using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        VisualElement StructurePreview(StructureSO definition, bool compact = false)
        {
            var texture = Resources.Load<Texture2D>("Frontier/StructureThumbnails/" + definition.id);
            VisualElement preview;
            if (texture != null)
                preview = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit };
            else
            {
                Debug.LogError("Missing structure thumbnail: " + definition.id);
                preview = Icons.Make(StructureIcon(definition.kind), UiKit.Ui(definition.color));
            }
            preview.name = "structure-preview-" + definition.id;
            preview.tooltip = definition.LocalizedName;
            preview.pickingMode = PickingMode.Ignore;
            preview.AddToClassList("fr-structure-preview");
            if (compact) preview.AddToClassList("fr-structure-preview-small");
            return preview;
        }

        void BuildResearchPreviews(VisualElement card, ResearchNodeSO node)
        {
            if (node.data.unlocks == null || node.data.unlocks.Length == 0) return;
            var row = UiKit.Div("fr-unlock-previews");
            var seen = new HashSet<string>();
            foreach (var unlock in node.data.unlocks)
            {
                var parts = unlock.Split(':');
                if (parts.Length < 2) continue;
                string kind = parts[0], id = parts[1];
                if (!seen.Add(kind + ":" + id)) continue;
                if (kind == "structure")
                {
                    var definition = gs.db.FindStructure(id);
                    if (definition != null) row.Add(StructurePreview(definition, true));
                    continue;
                }
                if (kind == "recipe")
                {
                    var recipe = gs.db.recipes.FirstOrDefault(item => item != null && item.id == id);
                    if (recipe != null)
                        foreach (var output in recipe.outputs)
                            if (output.resource != null && seen.Add("resource:" + output.resource.id))
                                row.Add(UnlockIcon(Icons.ForResource(output.resource), output.resource.LocalizedName));
                    continue;
                }
                string icon = kind == "tool" ? "tools" :
                    id.StartsWith("hull_") ? "shield" :
                    id == "jump" ? "warp" :
                    id == "scanner_range" ? "radar_tower" :
                    id == "compression" ? "pack" :
                    id.StartsWith("builder_") ? "build" :
                    id == "ascension" ? "star" : "ship";
                row.Add(UnlockIcon(icon, UnlockSummary(new[] { unlock })));
            }
            if (row.childCount > 0) card.Add(row);
        }

        static VisualElement UnlockIcon(string icon, string label)
        {
            var preview = Icons.Make(icon, UiKit.Magenta, UiKit.Size.IconLg);
            preview.tooltip = label;
            preview.AddToClassList("fr-unlock-icon");
            return preview;
        }
    }
}
