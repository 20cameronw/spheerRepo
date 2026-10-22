using System;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    /// <summary>The authoring presets (PlanetBiome) cover every biome the universe generator can produce.</summary>
    [TestFixture]
    public class PlanetBiomeTests
    {
        [Test]
        public void AuthoringBiomes_AreTheGeneratorBiomes_InTheSameOrder()
        {
            CollectionAssert.AreEqual(Enum.GetNames(typeof(Frontier.Universe.Biome)), Enum.GetNames(typeof(PlanetBiome)));
            foreach (PlanetBiome biome in Enum.GetValues(typeof(PlanetBiome)))
                Assert.AreEqual((int)biome, Convert.ToInt32(Enum.Parse(typeof(Frontier.Universe.Biome), biome.ToString())), biome.ToString());
        }

        [Test]
        public void EveryBiome_CanBeAuthored_WithValidPresentationAndGatherSpots()
        {
            var db = GameDatabase.Load();
            foreach (PlanetBiome biome in Enum.GetValues(typeof(PlanetBiome)))
            {
                var p = PlanetGenerator.Create("test-" + biome, "Test " + biome, "A test world.", 4, "sylva", biome, 7, db);
                try
                {
                    Assert.IsNotNull(ModelLibrary.Prefab(p.ModelKey), biome + ": model '" + p.ModelKey + "' is not in the ModelLibrary");
                    foreach (var c in new[] { p.primary, p.secondary, p.rim })
                    {
                        Assert.IsFalse(float.IsNaN(c.r + c.g + c.b) || float.IsInfinity(c.r + c.g + c.b), biome + ": colour is not finite");
                        Assert.GreaterOrEqual(Mathf.Min(c.r, Mathf.Min(c.g, c.b)), 0f, biome.ToString());
                    }
                    Assert.IsTrue(Enum.IsDefined(typeof(WeatherKind), p.weather), biome.ToString());
                    Assert.GreaterOrEqual(p.style, 0);
                    Assert.LessOrEqual(p.style, 4);
                    Assert.Greater(p.surfaceBands, 0f);
                    Assert.Greater(p.glowIntensity, 0f);
                    Assert.GreaterOrEqual(p.spots.Count, 2, biome + " offers too little to gather");
                    foreach (var spot in p.spots)
                    {
                        Assert.IsNotNull(spot.resource, biome + ": spot '" + spot.label + "' has no resource");
                        Assert.Greater(spot.baseYield, 0);
                        Assert.GreaterOrEqual(spot.difficulty, 0f);
                        Assert.LessOrEqual(spot.difficulty, 1f);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(p); }
            }
        }

        [Test]
        public void TheDeepSpaceRoster_StillBuilds()
        {
            var roster = PlanetGenerator.GenerateDeepSpaceRoster(GameDatabase.Load());
            try
            {
                Assert.AreEqual(12, roster.Count);
                foreach (var p in roster) Assert.GreaterOrEqual(p.spots.Count, 2, p.id);
            }
            finally { foreach (var p in roster) UnityEngine.Object.DestroyImmediate(p); }
        }
    }
}
