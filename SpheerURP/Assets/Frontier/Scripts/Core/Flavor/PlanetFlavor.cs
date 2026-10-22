using System;
using System.Collections.Generic;
using System.Globalization;

namespace Frontier.Universe
{
    /// <summary>
    /// The historical visual seed of the hand made worlds. Before the universe existed, a planet's surface pattern was seeded with
    /// <c>id.GetHashCode()</c>, which in Unity's Mono is the non-randomised .NET Framework string hash. That hash differs between runtimes
    /// (and IL2CPP may use another one), so it is reimplemented here: authored worlds keep exactly the look they were tuned with,
    /// on every platform. Never change this function.
    /// </summary>
    public static class LegacyHash
    {
        public static int NetFxString(string s)
        {
            if (s == null) return 0;
            unchecked
            {
                int hash1 = 5381, hash2 = hash1;
                for (int i = 0; i < s.Length; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ s[i];
                    if (i + 1 >= s.Length) break;
                    hash2 = ((hash2 << 5) + hash2) ^ s[i + 1];
                }
                return hash1 + hash2 * 1566083941;
            }
        }
    }

    /// <summary>Stable 64 bit seeds for the look of a world. Everything that used to hash the planet id with string.GetHashCode uses this.</summary>
    public static class PlanetSeeds
    {
        static readonly ulong VisualSalt = Hash.Fnv1a64("planet.visual-seed");

        /// <summary>
        /// Generated worlds: an independent hash stream of (universe seed, planet key), unrelated to every generator attribute.
        /// Authored worlds: their historical seed (see <see cref="LegacyHash"/>) in the low 32 bits, so their hand tuned look is unchanged.
        /// </summary>
        public static ulong VisualSeed(PlanetDescriptor planet, ulong universeSeed)
        {
            if (planet == null) throw new ArgumentNullException("planet");
            if (planet.AuthoredId != null) return (ulong)(uint)LegacyHash.NetFxString(planet.AuthoredId);
            return Hash.Stream(universeSeed, planet.Key.Id, VisualSalt);
        }
    }

    /// <summary>
    /// A short deterministic description for generated worlds: biome and terrain, weather, hazards, size and rings/moons. Every
    /// choice draws from its own hash stream keyed by the planet (independent of all generator attributes, so the generator stays
    /// frozen), and the text is a pure function of the descriptor.
    /// </summary>
    public static class PlanetBlurb
    {
        static readonly string[][] Openers =
        {
            /* Lush */ new[] { "A verdant world blanketed in deep forest", "A green world of rolling meadows and ancient groves", "A humid world of jungle canopies and winding rivers", "A temperate world of mossy hills and tall timber", "A fertile world of open grassland and dense woods" },
            /* Mineral */ new[] { "A rugged world of exposed ore seams and rust-red cliffs", "A stony world laced with mineral veins", "A rocky world of broken ridges and deep quarries", "A metal-rich world of terraced mesas and bare stone" },
            /* Glacial */ new[] { "A frozen world of glaciers and wind-scoured ice sheets", "An icebound world of crevassed ice fields", "A pale world of frozen seas and drifting snow", "A cold world buried under blue ice" },
            /* Volcanic */ new[] { "A volcanic world of lava rivers and smoking calderas", "A molten world scarred by fissures and ash plains", "A burning world of black basalt and glowing vents", "A hostile world cracked open by magma flows" },
            /* Fungal */ new[] { "A spongy world of towering fungal forests", "A damp world carpeted in glowing spore beds", "A strange world where giant mushrooms pierce the mist", "A murky world of fungal groves and rotting bogs" },
            /* Crystal */ new[] { "A glittering world of faceted crystal spires", "A brilliant world of shimmering crystal fields", "A prismatic world of jagged crystalline ridges", "A resonant world grown over with glassy crystal" },
            /* Obsidian */ new[] { "A dark world of black glass plains and violet veins", "A shadowed world of polished obsidian shelves", "A grim world of volcanic glass and deep ravines", "A dusky world of dark stone and cold lightning" },
            /* Shattered */ new[] { "A broken world of drifting fragments and open voids", "A shattered world torn apart by ancient forces", "A fractured world of floating crags and bright rifts", "A ruined world split by glowing fault lines" },
            /* Desert */ new[] { "A scorched world of endless dunes and wind-carved rock", "A sun-baked world of golden sand seas", "A dry world of salt flats and shifting dunes", "An arid world of cracked basins and red mesas" },
            /* Oceanic */ new[] { "A water world of open seas and scattered islets", "An ocean world of rolling swells and floating kelp forests", "A blue world of deep seas and tidal archipelagos", "A flooded world with only thin ribbons of land" },
            /* GasGiant */ new[] { "A towering gas giant of banded clouds and storm vortices", "A vast gas giant wrapped in swirling cloud bands", "A gas giant of churning storm bands", "A hazy gas giant crossed by slow, enormous storms" },
            /* Barren */ new[] { "A bare, cratered rock with almost no atmosphere", "A lifeless world of dust and impact craters", "A barren world of grey regolith and long shadows", "A silent, airless world of pitted stone" }
        };

        static readonly string[] BeltOpeners =
        {
            "A drifting belt of rubble and tumbling rock", "A dense ring of asteroids and salvageable wreckage", "A scattered belt of rock and metal debris"
        };

        // Weather order: Clear, Rain, Snow, Embers, Spores, Dust, Storm.
        static readonly string[][] Skies =
        {
            new[] { "beneath clear skies", "under calm, open skies", "beneath a quiet sky" },
            new[] { "under steady rain", "beneath endless grey rain", "lashed by warm rain" },
            new[] { "under falling snow", "beneath drifting snowfall", "shrouded in a snowy haze" },
            new[] { "under a slow drift of embers", "beneath showers of embers", "with ash and embers on the wind" },
            new[] { "wreathed in drifting spores", "under clouds of luminous spores", "with spores hanging in the air" },
            new[] { "under dust-laden winds", "beneath a veil of dust", "swept by dry, gritty winds" },
            new[] { "wracked by violent storms", "under constant lightning storms", "battered by howling storms" }
        };

        // Size order: Dwarf, Small, Medium, Large, Giant, Colossal.
        static readonly string[][] Sizes =
        {
            new[] { "Its small size leaves room for only a few structures.", "A tiny world with tight building room." },
            new[] { "A compact world with modest building room.", "Compact, with space for a small outpost." },
            new[] { "There is room for a respectable base.", "A mid-sized world with decent building room." },
            new[] { "A large world with plenty of ground to build on.", "Wide plains and ridges leave generous building room." },
            new[] { "A giant world with sprawling building room.", "Its sheer size offers sprawling room for bases." },
            new[] { "A colossal world with vast tracts of buildable ground.", "Colossal in scale, with room for a whole network of bases." }
        };

        static readonly string[] Calm =
        {
            "Landing conditions are mild.", "A forgiving place to touch down.", "The ground is stable and the air is kind."
        };

        static readonly string[] Numbers = { "no", "a single", "two", "three", "four", "five", "six", "seven" };

        static readonly ulong SeedOpener = Hash.Fnv1a64("blurb.opener");
        static readonly ulong SeedSky = Hash.Fnv1a64("blurb.sky");
        static readonly ulong SeedSize = Hash.Fnv1a64("blurb.size");
        static readonly ulong SeedCalm = Hash.Fnv1a64("blurb.calm");

        static int Pick(ulong universeSeed, ulong key, ulong salt, int count)
        {
            return Hash.Index(Hash.Stream(universeSeed, key, salt), count);
        }

        // Words a sky clause shares with the opener are a repeat ("drifting snow ... under falling snow"): the clause is changed or left out.
        static readonly string[][] SkyStems =
        {
            new string[0], new[] { "rain" }, new[] { "snow" }, new[] { "ember" }, new[] { "spore" }, new[] { "dust" }, new[] { "storm", "lightning" }
        };

        static int ChooseSky(Weather weather, string opener, ulong universeSeed, ulong key)
        {
            string[] skies = Skies[(int)weather];
            string[] stems = SkyStems[(int)weather];
            int start = Pick(universeSeed, key, SeedSky, skies.Length);
            for (int i = 0; i < skies.Length; i++)
            {
                string sky = skies[(start + i) % skies.Length];
                bool repeats = false;
                foreach (string stem in stems)
                    if (opener.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0 && sky.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0) repeats = true;
                if (!repeats) return (start + i) % skies.Length;
            }
            return -1;
        }

        public static IEnumerable<KeyValuePair<string, string>> EnglishEntries()
        {
            for (int biome = 0; biome < Openers.Length; biome++)
                for (int i = 0; i < Openers[biome].Length; i++)
                    yield return Entry("opener." + ((Biome)biome).ToString().ToLowerInvariant() + "." + i, Openers[biome][i]);
            for (int i = 0; i < BeltOpeners.Length; i++) yield return Entry("belt." + i, BeltOpeners[i]);
            for (int weather = 0; weather < Skies.Length; weather++)
                for (int i = 0; i < Skies[weather].Length; i++)
                    yield return Entry("sky." + ((Weather)weather).ToString().ToLowerInvariant() + "." + i, Skies[weather][i]);
            for (int size = 0; size < Sizes.Length; size++)
                for (int i = 0; i < Sizes[size].Length; i++)
                    yield return Entry("size." + ((SizeClass)size).ToString().ToLowerInvariant() + "." + i, Sizes[size][i]);
            for (int i = 0; i < Calm.Length; i++) yield return Entry("calm." + i, Calm[i]);
            yield return Entry("opening", "{0} {1}.");
            yield return Entry("opening-no-sky", "{0}.");
            yield return Entry("join", "{0} {1}");
            yield return Entry("rings", "Pale rings circle it.");
            yield return Entry("moon.one", "A single moon circles it.");
            for (int i = 2; i < Numbers.Length; i++) yield return Entry("moon." + i, Capital(Numbers[i]) + " moons circle it.");
            yield return Entry("hazards", "Expect {0}.");
            yield return Entry("list.more", "{0}, {1}");
            yield return Entry("list.last", "{0} and {1}");
            yield return Entry("hazard.heat", "extreme heat");
            yield return Entry("hazard.cold", "bitter cold");
            yield return Entry("hazard.toxic", "toxic air");
            yield return Entry("hazard.radiation", "hard radiation");
            yield return Entry("hazard.void", "void exposure");
        }

        static KeyValuePair<string, string> Entry(string suffix, string text)
        {
            return new KeyValuePair<string, string>("planet.blurb." + suffix, text);
        }

        public static string Describe(PlanetDescriptor planet, ulong universeSeed)
        {
            return Describe(planet, universeSeed, null);
        }

        /// <summary>Resolves presentation only; seeded choices and generator data never depend on the language.</summary>
        public static string Describe(PlanetDescriptor planet, ulong universeSeed, Func<string, string, string> localize)
        {
            if (planet == null) throw new ArgumentNullException("planet");
            ulong key = planet.Key.Id;
            string Text(string suffix, string english) { return localize == null ? english : localize("planet.blurb." + suffix, english); }
            string Format(string suffix, string english, params object[] args)
            {
                return string.Format(CultureInfo.CurrentCulture, Text(suffix, english), args);
            }
            string Join(string first, string next) { return Format("join", "{0} {1}", first, next); }
            string[] openers = planet.Kind == PlanetKind.AsteroidBelt ? BeltOpeners : Openers[(int)planet.Biome];
            int openerIndex = Pick(universeSeed, key, SeedOpener, openers.Length);
            string opener = openers[openerIndex];
            int skyIndex = ChooseSky(planet.Weather, opener, universeSeed, key);
            opener = Text(planet.Kind == PlanetKind.AsteroidBelt ? "belt." + openerIndex
                : "opener." + planet.Biome.ToString().ToLowerInvariant() + "." + openerIndex, opener);
            string result = skyIndex < 0 ? Format("opening-no-sky", "{0}.", opener)
                : Format("opening", "{0} {1}.", opener,
                    Text("sky." + planet.Weather.ToString().ToLowerInvariant() + "." + skyIndex, Skies[(int)planet.Weather][skyIndex]));

            string[] sizes = Sizes[(int)planet.Size];
            int sizeIndex = Pick(universeSeed, key, SeedSize, sizes.Length);
            result = Join(result, Text("size." + planet.Size.ToString().ToLowerInvariant() + "." + sizeIndex, sizes[sizeIndex]));
            if (planet.HasRings) result = Join(result, Text("rings", "Pale rings circle it."));
            if (planet.MoonCount == 1) result = Join(result, Text("moon.one", "A single moon circles it."));
            else if (planet.MoonCount > 1)
            {
                int moons = Math.Min(planet.MoonCount, Numbers.Length - 1);
                result = Join(result, Text("moon." + moons, Capital(Numbers[moons]) + " moons circle it."));
            }

            if (planet.Hazards == Hazards.None)
            {
                int calmIndex = Pick(universeSeed, key, SeedCalm, Calm.Length);
                result = Join(result, Text("calm." + calmIndex, Calm[calmIndex]));
            }
            else result = Join(result, Format("hazards", "Expect {0}.", HazardList(planet.Hazards, localize)));
            return result;
        }

        static string Capital(string s) { return char.ToUpperInvariant(s[0]) + s.Substring(1); }

        static string HazardList(Hazards h, Func<string, string, string> localize)
        {
            string Text(string suffix, string english) { return localize == null ? english : localize("planet.blurb." + suffix, english); }
            var parts = new string[5];
            int n = 0;
            if ((h & Hazards.Heat) != 0) parts[n++] = Text("hazard.heat", "extreme heat");
            if ((h & Hazards.Cold) != 0) parts[n++] = Text("hazard.cold", "bitter cold");
            if ((h & Hazards.Toxic) != 0) parts[n++] = Text("hazard.toxic", "toxic air");
            if ((h & Hazards.Radiation) != 0) parts[n++] = Text("hazard.radiation", "hard radiation");
            if ((h & Hazards.Void) != 0) parts[n++] = Text("hazard.void", "void exposure");
            if (n == 1) return parts[0];
            string result = parts[0];
            for (int i = 1; i < n; i++)
                result = string.Format(CultureInfo.CurrentCulture, Text(i == n - 1 ? "list.last" : "list.more",
                    i == n - 1 ? "{0} and {1}" : "{0}, {1}"), result, parts[i]);
            return result;
        }
    }
}
