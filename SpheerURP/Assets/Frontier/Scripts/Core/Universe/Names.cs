using System;

namespace Frontier.Universe
{
    /// <summary>Deterministic syllable and word-list name generation. The word tables are constants; the first entries of a table must never be reordered after release.</summary>
    internal static class NameBank
    {
        static readonly string[] SingleOnsets =
        {
            "b", "c", "d", "f", "g", "h", "k", "l", "m", "n", "p", "r", "s", "t", "v", "z", "j", "w"
        };

        static readonly string[] ClusterOnsets =
        {
            "br", "dr", "gr", "kr", "pr", "tr", "th", "sh", "st", "sk", "sl", "vr", "cl", "gl", "fl", "pl", "ch", "kh", "zh"
        };

        static readonly string[] Vowels =
        {
            "a", "e", "i", "o", "u", "a", "e", "i", "o", "ae", "ia", "io", "ou", "ei"
        };

        static readonly string[] MidCodas = { "n", "r", "s", "l", "m" };

        static readonly string[] FinalCodas = { "n", "r", "s", "l", "m", "th", "x", "k", "d", "nd", "rn", "ss" };

        static readonly string[] Adjectives =
        {
            "Verdant", "Ember", "Fractured", "Silent", "Gilded", "Argent", "Hollow", "Crimson", "Azure", "Pale",
            "Veiled", "Radiant", "Sable", "Tarnished", "Lucent", "Forsaken", "Distant", "Amber", "Ivory", "Cobalt",
            "Wandering", "Burning", "Frozen", "Sundered", "Gossamer", "Obsidian", "Opal", "Scarlet", "Violet", "Jade",
            "Ashen", "Gleaming", "Dusky", "Lone", "Nameless", "Ancient", "Hidden", "Restless", "Pallid", "Halcyon",
            "Iron", "Silver", "Golden", "Twilight", "Dawn", "Storm", "Mourning", "Shimmering", "Cinder", "Auric"
        };

        static readonly string[] SpiralNouns = { "Spiral", "Whorl", "Vortex", "Pinwheel", "Cascade", "Wheel", "Coil" };
        static readonly string[] BarredNouns = { "Crossing", "Anvil", "Bridge", "Span", "Gate", "Keystone", "Lattice" };
        static readonly string[] EllipticalNouns = { "Reach", "Expanse", "Drift", "Cloud", "Shoal", "Haze", "Veil", "Deep" };

        static int Append(Span<char> buf, int n, string s)
        {
            for (int i = 0; i < s.Length; i++) buf[n++] = s[i];
            return n;
        }

        static bool IsVowel(char c)
        {
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';
        }

        /// <summary>A pronounceable name such as "Karvelon" with the given syllable count range.</summary>
        public static string Syllabic(ref Rng rng, int minSyllables, int maxSyllables)
        {
            Span<char> buf = stackalloc char[40];
            int n = 0;
            int count = minSyllables + rng.NextInt(maxSyllables - minSyllables + 1);
            for (int i = 0; i < count; i++)
            {
                bool first = i == 0;
                bool last = i == count - 1;
                bool prevConsonant = n > 0 && !IsVowel(buf[n - 1]);
                if (!first || rng.Chance(0.82))
                {
                    if (!prevConsonant && rng.Chance(0.28)) n = Append(buf, n, ClusterOnsets[rng.NextInt(ClusterOnsets.Length)]);
                    else n = Append(buf, n, SingleOnsets[rng.NextInt(SingleOnsets.Length)]);
                }
                n = Append(buf, n, Vowels[rng.NextInt(Vowels.Length)]);
                if (last)
                {
                    if (rng.Chance(0.78)) n = Append(buf, n, FinalCodas[rng.NextInt(FinalCodas.Length)]);
                }
                else if (rng.Chance(0.32))
                {
                    n = Append(buf, n, MidCodas[rng.NextInt(MidCodas.Length)]);
                }
            }
            buf[0] = char.ToUpperInvariant(buf[0]);
            return new string(buf.Slice(0, n));
        }

        /// <summary>"Silent Whorl", "The Gilded Reach": adjective plus a noun that matches the galaxy shape.</summary>
        public static string Galaxy(ref Rng rng, GalaxyKind kind)
        {
            string adj = Adjectives[rng.NextInt(Adjectives.Length)];
            string[] nouns = kind == GalaxyKind.Spiral ? SpiralNouns : (kind == GalaxyKind.BarredSpiral ? BarredNouns : EllipticalNouns);
            string noun = nouns[rng.NextInt(nouns.Length)];
            return rng.Chance(0.35) ? "The " + adj + " " + noun : adj + " " + noun;
        }

        /// <summary>Two-letter catalogue prefix derived from a galaxy name ("The Verdant Spiral" gives "VS"); "IG" for intergalactic space.</summary>
        public static string CataloguePrefix(string galaxyName)
        {
            if (string.IsNullOrEmpty(galaxyName)) return "IG";
            Span<char> p = stackalloc char[2];
            int n = 0;
            int i = 0;
            char secondOfFirst = 'X';
            while (i < galaxyName.Length && n < 2)
            {
                while (i < galaxyName.Length && galaxyName[i] == ' ') i++;
                int start = i;
                while (i < galaxyName.Length && galaxyName[i] != ' ') i++;
                int len = i - start;
                if (len == 0) break;
                if (len == 3 && string.CompareOrdinal(galaxyName, start, "The", 0, 3) == 0) continue;
                if (n == 0 && len > 1) secondOfFirst = char.ToUpperInvariant(galaxyName[start + 1]);
                p[n++] = char.ToUpperInvariant(galaxyName[start]);
            }
            if (n == 0) return "IG";
            if (n == 1) p[1] = secondOfFirst;
            return new string(p.Slice(0, 2));
        }

        public static string Roman(int n)
        {
            switch (n)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                case 5: return "V";
                case 6: return "VI";
                case 7: return "VII";
                case 8: return "VIII";
                case 9: return "IX";
                case 10: return "X";
                default: return n.ToString();
            }
        }
    }
}
