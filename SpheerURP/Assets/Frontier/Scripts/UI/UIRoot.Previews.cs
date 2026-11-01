using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        void BuildUpgradeAppearance(VisualElement body, PlacedStructure structure)
        {
            int next = structure.record.pendingUpgradeLevel > structure.level
                ? structure.record.pendingUpgradeLevel : structure.level + 1;
            if (next > structure.def.maxLevel) return;
            var row = UiKit.Div("fr-row");
            row.name = "structure-upgrade-appearance";
            row.style.justifyContent = Justify.Center;
            foreach (int level in new[] { structure.level, next })
            {
                if (level == next) row.Add(UiKit.Text("->", "fr-h2", UiKit.Cyan));
                var column = UiKit.Div();
                column.style.alignItems = Align.Center;
                column.style.flexShrink = 1;
                var picture = UpgradeAppearanceSnapshot(structure.def, level);
                column.Add(picture);
                column.Add(UiKit.Text(UiText.Get("hud.player-level", level), "fr-small"));
                row.Add(column);
            }
            body.Add(row);
        }

        Image UpgradeAppearanceSnapshot(StructureSO definition, int level)
        {
            var preview = new Image { scaleMode = ScaleMode.ScaleToFit };
            preview.name = "structure-upgrade-preview-" + level;
            preview.tooltip = UiText.Get("structure.header", definition.LocalizedName, level);
            preview.pickingMode = PickingMode.Ignore;
            preview.style.width = 112;
            preview.style.height = 112;
            preview.style.flexShrink = 1;
            var prefab = ModelLibrary.Prefab(definition.modelKey);
            if (prefab == null) throw new System.InvalidOperationException("Missing upgrade model: " + definition.id);
            var stage = new GameObject("UpgradeSnapshot");
            stage.transform.position = new Vector3(2000f, 0f, 0f);
            var materials = new List<Material>();
            var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            bool rendered = false;
            try
            {
                var visual = new GameObject("StructureVisualRoot").transform;
                visual.SetParent(stage.transform, false);
                var model = ModelLibrary.Spawn(definition.modelKey, visual, 0.75f);
                if (model == null || !ModelLibrary.MeshBounds(model.gameObject, out var sourceBounds))
                    throw new System.InvalidOperationException("Missing upgrade geometry: " + definition.id);
                model.localPosition += Vector3.up * (stage.transform.position.y - sourceBounds.min.y -
                    ModelLibrary.FoundationDepth(definition.modelKey) * model.localScale.y);
                StructureAppearance.AddLevelDetails(visual, definition.kind, level, gs.BuildingTheme);
                visual.localRotation = Quaternion.Euler(0, definition.id == "warehouse" || definition.id == "fabricator" ? 180 : 0, 0);
                foreach (var child in stage.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
                foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                {
                    var copies = renderer.sharedMaterials;
                    for (int i = 0; i < copies.Length; i++)
                    {
                        var source = copies[i];
                        if (source == null) continue;
                        var copy = new Material(source);
                        if (source.name == "Structure_Accent")
                        {
                            string property = source.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                            copy.SetColor(property, StructureAppearance.ThemeAccent(gs.BuildingTheme, source.GetColor(property)));
                        }
                        materials.Add(copy);
                        copies[i] = copy;
                    }
                    renderer.sharedMaterials = copies;
                }
                ModelLibrary.MeshBounds(visual.gameObject, out var bounds);
                var cameraObject = new GameObject("UpgradeSnapshotCamera");
                cameraObject.transform.SetParent(stage.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.aspect = 1;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.allowHDR = false;
                float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
                camera.transform.position = bounds.center + new Vector3(3, 2.4f, -4).normalized * radius * 4;
                camera.transform.LookAt(bounds.center);
                camera.orthographicSize = radius * 1.15f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = radius * 8;
                foreach (var rotation in new[] { new Vector3(35, -35, 0), new Vector3(25, 140, 0) })
                {
                    var lamp = new GameObject("UpgradeSnapshotLight");
                    lamp.transform.SetParent(stage.transform, false);
                    lamp.transform.rotation = Quaternion.Euler(rotation);
                    var light = lamp.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.cullingMask = 1 << 31;
                    light.intensity = 1.4f;
                }
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                texture.Apply();
                camera.targetTexture = null;
                preview.image = texture;
                preview.RegisterCallback<DetachFromPanelEvent>(_ => Destroy(texture));
                rendered = true;
                return preview;
            }
            finally
            {
                RenderTexture.active = previous;
                stage.SetActive(false);
                Destroy(stage);
                foreach (var material in materials) Destroy(material);
                target.Release();
                Destroy(target);
                if (!rendered) Destroy(texture);
            }
        }

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
