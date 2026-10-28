using System;
using UnityEngine;

namespace Frontier
{
    public static class HarvestToolVisual
    {
        public static void Palette(int level, Color accent, out Color body, out Color rim)
        {
            level = Mathf.Clamp(level, 0, 6);
            if (level == 0) { body = new Color(0.95f, 0.7f, 0.55f); rim = new Color(1f, 0.6f, 0.4f) * 0.4f; }
            else if (level == 1) { body = new Color(0.55f, 0.55f, 0.6f); rim = new Color(0.6f, 0.7f, 0.8f) * 0.5f; }
            else if (level == 2) { body = new Color(0.78f, 0.84f, 0.95f); rim = new Color(0.3f, 0.9f, 1.4f) * 0.8f; }
            else { body = new Color(0.12f, 0.3f, 0.4f); rim = new Color(accent.r, accent.g, accent.b) * 2.4f; }
        }

        public static Transform Build(MinigameStage stage, MinigameKind kind, int level, Color accent, Vector3 position)
        {
            if (kind != MinigameKind.Mine && kind != MinigameKind.Siphon && kind != MinigameKind.Excavate && kind != MinigameKind.Salvage)
                throw new ArgumentOutOfRangeException(nameof(kind), "Use the existing striking or drill rig for this tool.");
            var rig = new GameObject(kind + "ToolRig").transform;
            rig.SetParent(stage.root, false);
            rig.localPosition = position;
            Palette(level, accent, out var body, out var rim);
            var casing = stage.Lit(body, rim, 0f, 6f);
            var active = stage.Lit(body * 0.6f, rim * 1.6f, 0f, 3f);
            if (kind == MinigameKind.Mine)
            {
                stage.AddMesh(MeshKit.Cyl(0.2f, 0f, 0.55f, 8), active, rig, Vector3.zero);
                stage.Prim(PrimitiveType.Capsule, rig, new Vector3(0, -0.25f, 0),
                    new Vector3(0.55f, 0.35f, 0.4f), Vector3.zero, casing);
                for (int side = -1; side <= 1; side += 2)
                    stage.Prim(PrimitiveType.Cylinder, rig, new Vector3(side * 0.32f, -0.25f, 0),
                        new Vector3(0.14f, 0.3f, 0.14f), Vector3.zero, casing);
            }
            else if (kind == MinigameKind.Siphon)
            {
                stage.AddMesh(MeshKit.Cyl(0.3f, 0.18f, 1f, 12), casing, rig, Vector3.zero);
                stage.Prim(PrimitiveType.Cylinder, rig, new Vector3(0f, 0.24f, 0f),
                    new Vector3(0.7f, 0.06f, 0.7f), Vector3.zero, active);
            }
            else if (kind == MinigameKind.Excavate)
            {
                stage.Prim(PrimitiveType.Cylinder, rig, new Vector3(0f, 0.55f, 0f),
                    new Vector3(0.15f, 0.7f, 0.15f), Vector3.zero, casing);
                stage.Prim(PrimitiveType.Cylinder, rig, new Vector3(0f, 1f, 0f),
                    new Vector3(0.62f, 0.08f, 0.62f), Vector3.zero, active);
                stage.Prim(PrimitiveType.Sphere, rig, new Vector3(0f, 1.12f, 0f),
                    Vector3.one * 0.2f, Vector3.zero, active);
            }
            else
            {
                stage.Prim(PrimitiveType.Cylinder, rig, new Vector3(0f, 0.48f, 0f),
                    new Vector3(0.1f, 0.48f, 0.1f), Vector3.zero, casing);
                stage.Prim(PrimitiveType.Sphere, rig, new Vector3(0f, 0.95f, 0f),
                    new Vector3(0.4f, 0.3f, 0.4f), Vector3.zero, active);
                for (int side = -1; side <= 1; side += 2)
                    stage.Prim(PrimitiveType.Cube, rig, new Vector3(side * 0.28f, 1.1f, 0f),
                        new Vector3(0.09f, 0.55f, 0.12f), new Vector3(0f, 0f, side * 24f), casing);
                stage.GlowQuad(rig, new Color(accent.r, accent.g, accent.b, 0.8f),
                    1.2f, new Vector3(0f, 0.95f, -0.3f));
            }
            AddTierHardware(stage, rig, level, casing, active);
            return rig;
        }

        public static void AddTierHardware(MinigameStage stage, Transform rig, int level, Material casing, Material active)
        {
            int tier = Mathf.Clamp(level, 0, 6);
            for (int i = 0; i < tier; i++)
            {
                float angle = 360f * i / Mathf.Max(1, tier);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.right;
                var segment = stage.Prim(PrimitiveType.Cube, rig,
                    new Vector3(direction.x * 0.25f, 0.62f, direction.z * 0.25f),
                    new Vector3(0.08f, 0.24f, 0.08f), new Vector3(0f, angle, 0f), tier >= 3 ? active : casing);
                segment.name = "TierHardware" + i;
            }
        }
    }
}
