using System;

namespace Frontier.Universe
{
    /// <summary>
    /// Stateless 64-bit hashing for the universe generator. Everything here is integer arithmetic, so results are
    /// bit-identical on every platform and runtime. NEVER use string.GetHashCode (randomised per process) for anything
    /// that feeds the generator; use <see cref="Fnv1a64(string)"/>.
    /// </summary>
    public static class Hash
    {
        /// <summary>SplitMix64 increment (2^64 / golden ratio).</summary>
        public const ulong Golden = 0x9E3779B97F4A7C15UL;

        const ulong FnvOffset = 0xCBF29CE484222325UL;
        const ulong FnvPrime = 0x100000001B3UL;

        /// <summary>SplitMix64 finalizer applied to (x + Golden). A bijection on 64 bits with full avalanche; Mix(0) != 0.</summary>
        public static ulong Mix(ulong x)
        {
            unchecked
            {
                ulong z = x + Golden;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Order-dependent combination of two 64-bit values.</summary>
        public static ulong Combine(ulong a, ulong b)
        {
            unchecked { return Mix(Mix(a) + b); }
        }

        public static ulong Combine(ulong a, ulong b, ulong c) { return Combine(Combine(a, b), c); }

        public static ulong Combine(ulong a, ulong b, ulong c, ulong d) { return Combine(Combine(Combine(a, b), c), d); }

        /// <summary>Stable FNV-1a 64-bit hash over the UTF-8 encoding of the string (matches the published test vectors for ASCII).</summary>
        public static ulong Fnv1a64(string s)
        {
            ulong h = FnvOffset;
            if (s == null) return h;
            unchecked
            {
                for (int i = 0; i < s.Length; i++)
                {
                    uint c = s[i];
                    if (c < 0x80)
                    {
                        h = (h ^ c) * FnvPrime;
                        continue;
                    }
                    if (c >= 0xD800 && c <= 0xDBFF && i + 1 < s.Length && s[i + 1] >= 0xDC00 && s[i + 1] <= 0xDFFF)
                    {
                        c = (uint)(0x10000 + ((c - 0xD800) << 10) + (s[i + 1] - 0xDC00u));
                        i++;
                    }
                    if (c < 0x800)
                    {
                        h = (h ^ (0xC0u | (c >> 6))) * FnvPrime;
                        h = (h ^ (0x80u | (c & 0x3Fu))) * FnvPrime;
                    }
                    else if (c < 0x10000)
                    {
                        h = (h ^ (0xE0u | (c >> 12))) * FnvPrime;
                        h = (h ^ (0x80u | ((c >> 6) & 0x3Fu))) * FnvPrime;
                        h = (h ^ (0x80u | (c & 0x3Fu))) * FnvPrime;
                    }
                    else
                    {
                        h = (h ^ (0xF0u | (c >> 18))) * FnvPrime;
                        h = (h ^ (0x80u | ((c >> 12) & 0x3Fu))) * FnvPrime;
                        h = (h ^ (0x80u | ((c >> 6) & 0x3Fu))) * FnvPrime;
                        h = (h ^ (0x80u | (c & 0x3Fu))) * FnvPrime;
                    }
                }
            }
            return h;
        }

        /// <summary>FNV-1a 64 over raw bytes.</summary>
        public static ulong Fnv1a64(byte[] data)
        {
            ulong h = FnvOffset;
            if (data == null) return h;
            unchecked
            {
                for (int i = 0; i < data.Length; i++) h = (h ^ data[i]) * FnvPrime;
            }
            return h;
        }

        /// <summary>
        /// Independent hash stream for one attribute. <paramref name="attributeSeed"/> is Combine(universeSeed, Fnv1a64(saltName))
        /// (see <see cref="SeedFor"/>); distinct salt names give statistically independent streams for the same key, so adding a new
        /// attribute never changes an existing one.
        /// </summary>
        public static ulong Stream(ulong attributeSeed, ulong key)
        {
            unchecked { return Mix(attributeSeed + Mix(key)); }
        }

        /// <summary>Convenience form: Hash.Stream(seed, key, "salt-name"). Slower than the precomputed form (hashes the name each call).</summary>
        public static ulong Stream(ulong seed, ulong key, string saltName)
        {
            return Stream(SeedFor(seed, Fnv1a64(saltName)), key);
        }

        public static ulong Stream(ulong seed, ulong key, ulong saltHash)
        {
            return Stream(SeedFor(seed, saltHash), key);
        }

        /// <summary>Per-attribute seed: Combine(universeSeed, saltHash). Precompute once per attribute for hot paths.</summary>
        public static ulong SeedFor(ulong universeSeed, ulong saltHash)
        {
            return Combine(universeSeed, saltHash);
        }

        /// <summary>Uniform double in [0,1) from the top 53 bits.</summary>
        public static double Unit(ulong h)
        {
            return (h >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>Uniform float in [0,1) from the top 24 bits.</summary>
        public static float UnitFloat(ulong h)
        {
            return (h >> 40) * (1f / 16777216f);
        }

        public static double Range(ulong h, double lo, double hi)
        {
            return lo + (hi - lo) * Unit(h);
        }

        /// <summary>Uniform integer in [0, count) (multiply-high; bias below 2^-32).</summary>
        public static int Index(ulong h, int count)
        {
            if (count <= 1) return 0;
            return (int)(((h >> 32) * (ulong)(uint)count) >> 32);
        }

        public static bool Chance(ulong h, double probability)
        {
            return Unit(h) < probability;
        }
    }

    /// <summary>
    /// Small sequential generator (SplitMix64) seeded from a hash. Use it only for draws inside ONE attribute's stream
    /// (for example "pick N deposits"); never share one Rng between attributes.
    /// </summary>
    public struct Rng
    {
        ulong _state;

        public Rng(ulong seed)
        {
            _state = seed;
        }

        public ulong NextULong()
        {
            unchecked
            {
                ulong z = Hash.Mix(_state);
                _state += Hash.Golden;
                return z;
            }
        }

        /// <summary>Uniform in [0,1).</summary>
        public double NextDouble() { return Hash.Unit(NextULong()); }

        /// <summary>Uniform in [0,1).</summary>
        public float NextFloat() { return Hash.UnitFloat(NextULong()); }

        /// <summary>Uniform integer in [0, count).</summary>
        public int NextInt(int count) { return Hash.Index(NextULong(), count); }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + Hash.Index(NextULong(), maxExclusive - minInclusive);
        }

        public double Range(double lo, double hi) { return lo + (hi - lo) * NextDouble(); }

        public bool Chance(double probability) { return NextDouble() < probability; }
    }
}
