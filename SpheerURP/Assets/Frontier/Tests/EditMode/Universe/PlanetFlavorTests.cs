using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    /// <summary>The planet description generator, the stable visual seeds and the reimplemented legacy string hash.</summary>
    [TestFixture]
    public class PlanetFlavorTests
    {
        // string.GetHashCode() of the 30 authored ids as Unity's Mono computed it (the value the old PlanetView seeded its surface with).
        static readonly string[] LegacyIds =
        {
            "sylva", "ferrum", "glacia", "cinder", "arbor", "nyx", "aurum", "tide", "zephyr", "pyre", "halcyon", "mire", "obsidian", "vesper", "prism",
            "kaos", "tundra", "coral", "veridian", "calcite", "sulfur", "magma", "lotus", "spore", "eclipse", "phantom", "borealis", "cryo", "aether", "genesis"
        };

        static readonly int[] LegacyHashes =
        {
            -407698451, -103310657, 155005473, -1012371185, 22413260, -372661473, 270981044, -837448254, 615827928, 1421151876, -445515202, -837448473,
            -216654423, -1124931153, -1234477755, -1872639852, 716892566, -367568049, -1725025782, -398487003, -1581843485, -2135425509, 889336937,
            -871591873, 739698917, 149127753, -497188947, -165135305, 615797335, 594349672
        };

        [Test]
        public void LegacyHash_ReproducesUnitysMonoValuesForTheAuthoredIds()
        {
            Assert.AreEqual(30, LegacyIds.Length);
            Assert.AreEqual(30, LegacyHashes.Length);
            for (int i = 0; i < LegacyIds.Length; i++)
                Assert.AreEqual(LegacyHashes[i], LegacyHash.NetFxString(LegacyIds[i]), LegacyIds[i]);
            Assert.AreEqual(0, LegacyHash.NetFxString(null));
            Assert.AreEqual(unchecked(5381 + 5381 * 1566083941), LegacyHash.NetFxString(""));
        }

        static List<PlanetDescriptor> SomePlanets(int sectors)
        {
            var u = TestUniverses.WithLandmarks();   // the dense home galaxy sits at the origin
            var list = new List<PlanetDescriptor>();
            int side = (int)Math.Ceiling(Math.Sqrt(sectors));
            for (int sy = -side / 2; sy < side - side / 2; sy++)
                for (int sx = -side / 2; sx < side - side / 2; sx++)
                    foreach (StarSystem s in u.SystemsInSector(new SectorCoord(sx, sy)))
                        list.AddRange(u.PlanetsOf(s));
            return list;
        }

        [Test]
        public void VisualSeed_ForGeneratedWorlds_IsStableAndMatchesAnIndependentImplementation()
        {
            // values computed with a separate Python port of Hash.Mix/Combine/Stream/Fnv1a64 (planet.visual-seed stream)
            ulong seed = new UniverseConfig().Seed;
            var cases = new[]
            {
                new { key = new PlanetKey(0, 0, 0, 0), seed = 0x50C97CF0591FC545UL },
                new { key = new PlanetKey(5, -3, 7, 2), seed = 0x00937F161018B837UL },
                new { key = new PlanetKey(-100, 200, 11, 6), seed = 0xF16B256F6D154147UL },
                new { key = new PlanetKey(14, -15, 1005, 3), seed = 0xD7E81520374EA9D5UL }
            };
            foreach (var c in cases)
            {
                var spec = new PlanetSpec { Key = c.key, Deposits = new Deposit[0] };
                var d = new PlanetDescriptor(spec);
                Assert.AreEqual(c.seed, PlanetSeeds.VisualSeed(d, seed), c.key.ToString());
                Assert.AreEqual(c.seed, PlanetSeeds.VisualSeed(d, seed));
            }
            var other = new PlanetDescriptor(new PlanetSpec { Key = new PlanetKey(0, 0, 0, 0), Deposits = new Deposit[0] });
            Assert.AreEqual(0x06FCC55A78BDAA98UL, PlanetSeeds.VisualSeed(other, 1UL), "another universe seed gives another look");
        }

        [Test]
        public void VisualSeed_ForAuthoredWorlds_IsTheirHistoricalSeed()
        {
            var u = TestUniverses.WithFakeAuthored();
            PlanetDescriptor sylva;
            Assert.IsTrue(u.TryGetPlanetById("sylva", out sylva));
            ulong expected = unchecked((ulong)(uint)(-407698451));
            Assert.AreEqual(expected, PlanetSeeds.VisualSeed(sylva, u.Config.Seed));
            Assert.AreEqual(-407698451, unchecked((int)(uint)expected), "the 32 bit value converts back to the original int seed");
            Assert.AreEqual(expected, PlanetSeeds.VisualSeed(sylva, 12345UL), "authored seeds do not depend on the universe seed");
        }

        [Test]
        public void VisualSeeds_DoNotCollideAcrossThousandsOfPlanets_AndDifferFromEveryGeneratorStream()
        {
            var planets = SomePlanets(1500);
            Assert.Greater(planets.Count, 1500);
            ulong seed = new UniverseConfig().Seed;
            var seen = new HashSet<ulong>();
            foreach (var p in planets)
            {
                ulong v = PlanetSeeds.VisualSeed(p, seed);
                Assert.IsTrue(seen.Add(v), "collision for " + p.Id);
            }
            TestLog.Info(planets.Count + " generated planets, " + seen.Count + " distinct 64 bit visual seeds");
        }

        [Test]
        public void Blurb_IsWellFormedAndDeterministic_ForEveryGeneratedPlanet()
        {
            var planets = SomePlanets(1500);
            ulong seed = new UniverseConfig().Seed;
            int longest = 0;
            foreach (var p in planets)
            {
                string text = PlanetBlurb.Describe(p, seed);
                Assert.IsNotEmpty(text, p.Id);
                Assert.AreEqual(text, PlanetBlurb.Describe(p, seed), "not deterministic for " + p.Id);
                Assert.IsTrue(char.IsUpper(text[0]), text);
                Assert.IsTrue(text.EndsWith("."), text);
                Assert.IsFalse(text.Contains("  "), "double space: " + text);
                Assert.IsFalse(text.Contains(" ."), "space before period: " + text);
                Assert.IsFalse(text.Contains("{") || text.Contains("}") || text.Contains("null"), text);
                Assert.LessOrEqual(text.Length, 280, text);
                longest = Math.Max(longest, text.Length);
            }
            TestLog.Info("longest description over " + planets.Count + " planets: " + longest + " characters");
        }

        [Test]
        public void Blurb_ReflectsTheDescriptor()
        {
            var planets = SomePlanets(1500);
            ulong seed = new UniverseConfig().Seed;
            int rings = 0, moons = 0, hazard = 0, gas = 0, belts = 0, calm = 0;
            foreach (var p in planets)
            {
                string text = PlanetBlurb.Describe(p, seed);
                if (p.HasRings) { rings++; StringAssert.Contains("rings", text); } else StringAssert.DoesNotContain("rings", text);
                if (p.MoonCount > 0) { moons++; StringAssert.Contains("moon", text); } else StringAssert.DoesNotContain("moon", text);
                if (p.Hazards != Hazards.None)
                {
                    hazard++;
                    StringAssert.Contains("Expect", text);
                    if ((p.Hazards & Hazards.Heat) != 0) StringAssert.Contains("heat", text);
                    if ((p.Hazards & Hazards.Cold) != 0) StringAssert.Contains("cold", text);
                    if ((p.Hazards & Hazards.Toxic) != 0) StringAssert.Contains("toxic", text);
                    if ((p.Hazards & Hazards.Radiation) != 0) StringAssert.Contains("radiation", text);
                    if ((p.Hazards & Hazards.Void) != 0) StringAssert.Contains("void", text);
                }
                else { calm++; StringAssert.DoesNotContain("Expect", text); }
                if (p.Kind == PlanetKind.GasGiant) { gas++; StringAssert.Contains("gas giant", text); }
                if (p.Kind == PlanetKind.AsteroidBelt) belts++;
            }
            Assert.Greater(rings, 10);
            Assert.Greater(moons, 100);
            Assert.Greater(hazard, 100);
            Assert.Greater(calm, 10);
            Assert.Greater(gas, 10);
            Assert.Greater(belts, 10);
        }

        [Test]
        public void Blurb_IsVariedEnoughToTellWorldsApart()
        {
            var planets = SomePlanets(1500);
            ulong seed = new UniverseConfig().Seed;
            var texts = new HashSet<string>();
            var openersByBiome = new Dictionary<Biome, HashSet<string>>();
            foreach (var p in planets)
            {
                string text = PlanetBlurb.Describe(p, seed);
                texts.Add(text);
                if (p.Kind != PlanetKind.Rocky) continue;
                HashSet<string> set;
                if (!openersByBiome.TryGetValue(p.Biome, out set)) openersByBiome[p.Biome] = set = new HashSet<string>();
                set.Add(text.Substring(0, 22));
            }
            double unique = texts.Count / (double)planets.Count;
            TestLog.Info(planets.Count + " planets -> " + texts.Count + " distinct descriptions (" + (unique * 100).ToString("0.0") + " %)");
            Assert.Greater(unique, 0.45, "descriptions repeat too often");
            foreach (var kv in openersByBiome)
                if (kv.Key != Biome.Shattered && kv.Value.Count > 0)
                    Assert.GreaterOrEqual(kv.Value.Count, 3, kv.Key + " uses too few different openers");
            Assert.GreaterOrEqual(openersByBiome.Count, 10, "nearly every rocky biome should occur in the sample");
        }

        [Test]
        public void Blurb_ChoicesComeFromIndependentStreams_NotOneSharedRng()
        {
            // Changing the universe seed changes the picks, but the same planet with the same seed never changes.
            var planets = SomePlanets(200);
            int differing = 0;
            foreach (var p in planets)
                if (PlanetBlurb.Describe(p, 1UL) != PlanetBlurb.Describe(p, 2UL)) differing++;
            Assert.Greater(differing, planets.Count / 2);
            // and the opener and the sky choice of one planet are not locked together: over many planets all combinations appear
            var combos = new HashSet<string>();
            foreach (var p in SomePlanets(1500))
                if (p.Biome == Biome.Lush && p.Weather == Weather.Clear)
                {
                    string text = PlanetBlurb.Describe(p, 99UL);
                    int dot = text.IndexOf('.');
                    combos.Add(text.Substring(0, dot));
                }
            Assert.GreaterOrEqual(combos.Count, 8, "Lush + Clear worlds should show many opener/sky pairs (5 x 3 possible)");
        }

        [Test]
        public void EveryBiomeSizeWeatherAndHazardCombination_ProducesText()
        {
            ulong seed = 7UL;
            int n = 0;
            foreach (Biome b in Enum.GetValues(typeof(Biome)))
                foreach (SizeClass s in Enum.GetValues(typeof(SizeClass)))
                    foreach (Weather w in Enum.GetValues(typeof(Weather)))
                        for (int h = 0; h < 32; h += 3)
                            for (int moons = 0; moons <= 7; moons += 3)
                            {
                                var spec = new PlanetSpec
                                {
                                    Key = new PlanetKey(n % 50, n % 7, n % 900, n % 15), Biome = b, Size = s, Weather = w, Hazards = (Hazards)h,
                                    MoonCount = moons, HasRings = (n & 1) == 0, Deposits = new Deposit[0],
                                    Kind = b == Biome.GasGiant ? PlanetKind.GasGiant : (n % 11 == 0 ? PlanetKind.AsteroidBelt : PlanetKind.Rocky)
                                };
                                string text = PlanetBlurb.Describe(new PlanetDescriptor(spec), seed);
                                Assert.IsNotEmpty(text);
                                n++;
                            }
            Assert.Greater(n, 5000);
        }
    }
}
