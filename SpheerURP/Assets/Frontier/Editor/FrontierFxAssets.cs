using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class FrontierFxAssets
    {
        const string Folder = "Assets/Frontier/Resources/Frontier/FX";

        [MenuItem("Frontier/FX/Create Templates If Missing")]
        public static void CreateTemplates()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Frontier/Resources/Frontier", "FX");
            var glowTexture = Texture("GlowTexture", Fx.Glow);
            var ringTexture = Texture("RingTexture", Fx.Ring);
            var glow = Material("Glow", glowTexture);
            var ring = Material("Ring", ringTexture);
            var line = Material("Line", null);
            Prefab("FxGlow", () => Quad("FxGlow", glow));
            Prefab("FxRipple", () =>
            {
                var root = new GameObject("FxRipple");
                Quad("Ripple Mesh", ring).transform.SetParent(root.transform, false);
                return root;
            });
            Prefab("FxBolt", () =>
            {
                var root = new GameObject("FxBolt");
                root.AddComponent<LineRenderer>().sharedMaterial = line;
                return root;
            });
            Prefab("Burst", () => Burst(glow));
            AssetDatabase.SaveAssets();
            Debug.Log("Frontier FX templates ready: Burst, FxBolt, FxRipple, FxGlow.");
        }

        static Texture2D Texture(string name, Texture2D source)
        {
            string path = Folder + "/" + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (asset != null) return asset;
            asset = Object.Instantiate(source);
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Material Material(string name, Texture texture)
        {
            string path = Folder + "/" + name + ".mat";
            var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (asset != null) return asset;
            asset = Fx.AddMat(Color.white, texture);
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static GameObject Quad(string name, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            return quad;
        }

        static void Prefab(string name, System.Func<GameObject> create)
        {
            string path = Folder + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var root = create();
            try { PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { Object.DestroyImmediate(root); }
        }

        static GameObject Burst(Material material)
        {
            var root = new GameObject("Burst");
            var system = root.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = 0f;
            main.startSize = 0.1f;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 128;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = fade;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return root;
        }
    }
}
