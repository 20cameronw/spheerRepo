using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Frontier.Tests
{
    /// <summary>
    /// Planet and model sizing rules, ported from <c>Frontier &gt; Validate Planet Model Sizes</c> (that editor check also covers the
    /// slot layout and the map route maths, which need the Visual/UI assemblies, and stays as the batch gate).
    /// </summary>
    [TestFixture]
    public class PlanetDataTests
    {
        GameDatabase db;
        GameObject holder;

        [OneTimeSetUp]
        public void LoadDatabase()
        {
            db = GameDatabase.Load();
        }

        [SetUp]
        public void SetUp()
        {
            holder = new GameObject("PlanetDataTests");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(holder);
        }

        static float Diameter(Bounds b) { return Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)); }

        [Test]
        public void Planets_HaveUniqueIdsAndValidCapacity()
        {
            Assert.GreaterOrEqual(db.planets.Length, 6);
            var ids = new HashSet<string>();
            foreach (var planet in db.planets)
            {
                Assert.IsFalse(string.IsNullOrEmpty(planet.id), "a planet has no id");
                Assert.IsTrue(ids.Add(planet.id), "duplicate planet id " + planet.id);
                Assert.GreaterOrEqual(planet.buildingSlots, 12, planet.id + " has fewer than the 12 base building slots");
                Assert.GreaterOrEqual(planet.worldSize, 1f, planet.id + " world size");
                Assert.Greater(planet.surfaceRadius, 0.01f, planet.id + " surface radius");
                Assert.AreEqual(1f / planet.worldSize, planet.StructureScale, 1e-5f, planet.id + " structure scale");
            }
        }

        [Test]
        public void SourceModels_ImportAtUnitDiameter()
        {
            var sources = new HashSet<string>();
            foreach (var entry in ModelLibrary.Instance.entries)
                foreach (var path in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(entry.prefab), true))
                    if (AssetImporter.GetAtPath(path) is ModelImporter) sources.Add(path);
            Assert.GreaterOrEqual(sources.Count, 10, "the model library should retain its authored planet and gameplay model sources");
            foreach (var path in sources)
            {
                var imported = ModelLibrary.InstantiateInert(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                imported.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                imported.transform.localScale = Vector3.one;
                Bounds bounds;
                bool hasMesh = ModelLibrary.MeshBounds(imported, out bounds);
                float diameter = hasMesh ? Diameter(bounds) : 0f;
                Object.DestroyImmediate(imported);
                Assert.IsTrue(hasMesh, "no source mesh: " + path);
                Assert.AreEqual(1f, diameter, 0.001f, "source import is not standardized: " + path);
            }
        }

        [Test]
        public void PlanetModels_FitTheStandardDiameter()
        {
            foreach (var planet in db.planets)
            {
                var model = ModelLibrary.Spawn(planet.ModelKey, holder.transform, 3f, false);
                Assert.IsNotNull(model, "missing planet model: " + planet.id);
                Bounds bounds;
                Assert.IsTrue(ModelLibrary.MeshBounds(model.gameObject, out bounds), "no planet mesh: " + planet.id);
                Assert.AreEqual(3f, Diameter(bounds), 0.01f, planet.id + " diameter");
                Object.DestroyImmediate(model.gameObject);
            }
        }

        [Test]
        public void StructureModels_FitTheStandardDiameter_AndShrinkWithWorldSize()
        {
            int structures = 0;
            foreach (var entry in ModelLibrary.Instance.entries)
            {
                if (!entry.key.StartsWith("struct_", System.StringComparison.Ordinal)) continue;
                var model = ModelLibrary.Spawn(entry.key, holder.transform, 0.75f);
                Assert.IsNotNull(model, "missing structure model: " + entry.key);
                Bounds bounds;
                Assert.IsTrue(ModelLibrary.MeshBounds(model.gameObject, out bounds), "no structure mesh: " + entry.key);
                float aboveGroundHeight = bounds.size.y - entry.foundationDepth * model.localScale.y;
                Assert.AreEqual(0.75f, Mathf.Max(bounds.size.x, Mathf.Max(aboveGroundHeight, bounds.size.z)),
                    0.01f, entry.key + " above-ground diameter");
                Object.DestroyImmediate(model.gameObject);
                structures++;
            }
            Assert.Greater(structures, 0);

            foreach (var planet in db.planets)
            {
                var drill = ModelLibrary.Spawn("struct_drill", holder.transform, 0.75f);
                drill.localScale *= planet.StructureScale;
                Bounds bounds;
                Assert.IsTrue(ModelLibrary.MeshBounds(drill.gameObject, out bounds));
                Assert.AreEqual(0.75f / planet.worldSize, Diameter(bounds), 0.001f, planet.id + " relative structure size");
                Object.DestroyImmediate(drill.gameObject);
            }
        }

        [Test]
        public void FoundationBlocksExtendDownwardWithoutChangingTheAuthoredGroundPlane()
        {
            int foundations = 0;
            foreach (var entry in ModelLibrary.Instance.entries)
            {
                if (!entry.key.StartsWith("struct_", System.StringComparison.Ordinal) && entry.key != "colony_hub") continue;
                if (entry.foundationDepth == 0f) continue;
                bool hub = entry.key == "colony_hub";
                float depth = hub ? 0.18f : 0.36f;
                Assert.AreEqual(depth, entry.foundationDepth, 0.0001f, entry.key);
                var instance = ModelLibrary.InstantiateInert(entry.prefab);
                try
                {
                    Assert.IsTrue(ModelLibrary.MeshBounds(instance, out var bounds));
                    Assert.AreEqual(-depth, bounds.min.y, 0.0001f, entry.key + " buried bottom");
                    var mesh = instance.GetComponentInChildren<MeshFilter>().sharedMesh;
                    float blockTop = hub ? 0.09f : 0.18f;
                    int topVertices = 0;
                    foreach (var vertex in mesh.vertices)
                        if (Mathf.Abs(vertex.y - blockTop) < 0.0001f) topVertices++;
                    Assert.GreaterOrEqual(topVertices, 4, entry.key + " original block top stays fixed");
                    Assert.AreEqual(hub ? 0.27f : 0.54f, blockTop + depth, 0.0001f, "Triple-height block.");
                }
                finally { Object.DestroyImmediate(instance); }
                foundations++;
            }
            Assert.AreEqual(28, foundations, "All ground structures, including the Hub; orbital structures stay unchanged.");
        }
    }
}
