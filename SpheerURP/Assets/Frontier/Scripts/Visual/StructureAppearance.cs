using UnityEngine;

namespace Frontier
{
    public static class StructureAppearance
    {
        // Retained for older harness callers; upgrades no longer change the paint.
        public static Color AccentForLevel(int level)
        {
            return new Color32(224, 91, 20, 255);
        }

        public static Color ThemeAccent(string theme, Color original)
        {
            switch (theme)
            {
                case "ember": return new Color32(198, 48, 36, 255);
                case "jade": return new Color32(47, 175, 101, 255);
                case "cyan": return new Color32(39, 153, 218, 255);
                case "violet": return new Color32(153, 91, 214, 255);
                case "galaxy": return new Color32(61, 38, 117, 255);
                default: return original;
            }
        }

        public static void AddLevelDetails(Transform root, StructureKind kind, int level, string theme)
        {
            Material hardware = null;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && material.name == "Structure_Machinery") hardware = material;
            int stage = Mathf.Clamp(level / 2, 0, 4);
            for (int i = 0; i < stage; i++)
            {
                float x = (i - (stage - 1) * 0.5f) * 0.12f;
                bool barrel = kind == StructureKind.Gunner || kind == StructureKind.Laser;
                bool antenna = kind == StructureKind.ResearchLab || kind == StructureKind.RadarTower;
                var part = GameObject.CreatePrimitive(barrel ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                part.name = barrel ? "UpgradeBarrel" : antenna
                    ? "UpgradeAntenna" : "UpgradeModule";
                Object.Destroy(part.GetComponent<Collider>());
                part.transform.SetParent(root, false);
                part.transform.localPosition = new Vector3(x, barrel ? 0.38f : antenna ? 0.5f : 0.16f,
                    barrel ? -0.15f : antenna ? 0 : -0.22f);
                part.transform.localRotation = barrel ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
                part.transform.localScale = barrel ? new Vector3(0.055f, 0.18f, 0.055f)
                    : antenna ? new Vector3(0.045f, 0.18f + i * 0.035f, 0.045f) : Vector3.one * 0.12f;
                if (hardware != null) part.GetComponent<MeshRenderer>().sharedMaterial = hardware;
            }
            if (theme != "galaxy") return;
            for (int i = 0; i < 12; i++)
            {
                var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                star.name = "GalaxyStar";
                Object.Destroy(star.GetComponent<Collider>());
                star.transform.SetParent(root, false);
                float angle = i * Mathf.PI * 2 / 12;
                star.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.27f, 0.2f + i % 3 * 0.09f, Mathf.Sin(angle) * 0.27f);
                star.transform.localScale = Vector3.one * 0.018f;
                star.GetComponent<MeshRenderer>().sharedMaterial = Fx.GlowMaterial;
            }
        }
    }
}
