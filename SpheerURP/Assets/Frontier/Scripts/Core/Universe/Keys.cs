using System;
using System.Globalization;

namespace Frontier.Universe
{
    /// <summary>
    /// Stable 64-bit ids are exact bit packings (not hashes), so they can never collide:
    /// [X:24][Y:24][system index:12][planet field:4]. Sector ids use the reserved system index 0xFFF, system ids use planet
    /// field 0, planet ids use planet field = planet index + 1. Sector coordinates must lie in [-2^23, 2^23 - 1]
    /// (about +-536 million light-years at 64 ly per sector).
    /// </summary>
    internal static class KeyBits
    {
        public const int SectorSystemIndex = 0xFFF;

        /// <summary>Culture independent integer text (ids must not depend on the device language).</summary>
        public static string Str(int v) { return v.ToString(CultureInfo.InvariantCulture); }

        public static ulong Pack(int x, int y, int system, int planetField)
        {
            unchecked
            {
                return ((ulong)((uint)x & 0xFFFFFFu) << 40) | ((ulong)((uint)y & 0xFFFFFFu) << 16)
                       | ((ulong)(uint)(system & 0xFFF) << 4) | (ulong)(uint)(planetField & 0xF);
            }
        }

        public static int SignExtend24(ulong v)
        {
            int i = (int)(v & 0xFFFFFFUL);
            return (i & 0x800000) != 0 ? i - 0x1000000 : i;
        }

        public static void Unpack(ulong id, out int x, out int y, out int system, out int planetField)
        {
            x = SignExtend24(id >> 40);
            y = SignExtend24(id >> 16);
            system = (int)((id >> 4) & 0xFFFUL);
            planetField = (int)(id & 0xFUL);
        }

        /// <summary>Reads one canonical integer (optional '-', no '+', no leading zeros, no "-0") followed by ':' or the end of the string.</summary>
        public static bool ReadInt(string s, ref int pos, out int value)
        {
            value = 0;
            int i = pos;
            bool neg = false;
            if (i < s.Length && s[i] == '-') { neg = true; i++; }
            int start = i;
            long acc = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
            {
                acc = acc * 10 + (s[i] - '0');
                if (acc > 100000000L) return false;
                i++;
            }
            int digits = i - start;
            if (digits == 0) return false;
            if (digits > 1 && s[start] == '0') return false;
            if (neg && acc == 0) return false;
            value = (int)(neg ? -acc : acc);
            pos = i;
            return true;
        }

        public static bool ReadSeparator(string s, ref int pos)
        {
            if (pos < s.Length && s[pos] == ':') { pos++; return true; }
            return false;
        }
    }

    /// <summary>A 64 ly square cell of the plane. Sector (x, y) covers [x*size, (x+1)*size) on each axis.</summary>
    public readonly struct SectorCoord : IEquatable<SectorCoord>, IComparable<SectorCoord>
    {
        public const int Limit = 1 << 23;
        public const int MinCoord = -Limit;
        public const int MaxCoord = Limit - 1;

        public readonly int X;
        public readonly int Y;

        public SectorCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool IsValid { get { return X >= MinCoord && X <= MaxCoord && Y >= MinCoord && Y <= MaxCoord; } }

        /// <summary>Stable collision-free 64-bit id.</summary>
        public ulong Id { get { return KeyBits.Pack(X, Y, KeyBits.SectorSystemIndex, 0); } }

        public SectorCoord Offset(int dx, int dy) { return new SectorCoord(X + dx, Y + dy); }

        /// <summary>The sector containing a light-year coordinate (floor division, clamped to the valid range).</summary>
        public static SectorCoord FromLy(double x, double y, double sectorSize)
        {
            return new SectorCoord(ToCell(x, sectorSize), ToCell(y, sectorSize));
        }

        static int ToCell(double v, double size)
        {
            double f = Math.Floor(v / size);
            if (double.IsNaN(f)) return 0;
            if (f < MinCoord) return MinCoord;
            if (f > MaxCoord) return MaxCoord;
            return (int)f;
        }

        public double MinX(double sectorSize) { return X * sectorSize; }
        public double MinY(double sectorSize) { return Y * sectorSize; }
        public double CenterX(double sectorSize) { return (X + 0.5) * sectorSize; }
        public double CenterY(double sectorSize) { return (Y + 0.5) * sectorSize; }

        public static bool TryFromId(ulong id, out SectorCoord coord)
        {
            int x, y, sys, pf;
            KeyBits.Unpack(id, out x, out y, out sys, out pf);
            coord = new SectorCoord(x, y);
            return sys == KeyBits.SectorSystemIndex && pf == 0;
        }

        /// <summary>Compact string id: "c:X:Y".</summary>
        public override string ToString() { return "c:" + KeyBits.Str(X) + ":" + KeyBits.Str(Y); }

        public static bool TryParse(string s, out SectorCoord coord)
        {
            coord = default(SectorCoord);
            if (s == null || s.Length < 5 || s[0] != 'c' || s[1] != ':') return false;
            int pos = 2, x, y;
            if (!KeyBits.ReadInt(s, ref pos, out x) || !KeyBits.ReadSeparator(s, ref pos)) return false;
            if (!KeyBits.ReadInt(s, ref pos, out y) || pos != s.Length) return false;
            coord = new SectorCoord(x, y);
            return coord.IsValid;
        }

        public bool Equals(SectorCoord other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is SectorCoord && Equals((SectorCoord)obj); }
        public override int GetHashCode() { unchecked { return (int)Hash.Mix(Id); } }
        public int CompareTo(SectorCoord other) { return Id.CompareTo(other.Id); }
        public static bool operator ==(SectorCoord a, SectorCoord b) { return a.Equals(b); }
        public static bool operator !=(SectorCoord a, SectorCoord b) { return !a.Equals(b); }
    }

    /// <summary>A star system: its sector plus an index inside the sector. Generated systems use 0..999, authored systems use 1000..4094.</summary>
    public readonly struct SystemKey : IEquatable<SystemKey>, IComparable<SystemKey>
    {
        /// <summary>First index reserved for authored (host supplied) systems.</summary>
        public const int AuthoredBase = 1000;
        public const int MaxIndex = KeyBits.SectorSystemIndex - 1;

        public readonly SectorCoord Sector;
        public readonly int Index;

        public SystemKey(SectorCoord sector, int index)
        {
            Sector = sector;
            Index = index;
        }

        public SystemKey(int sectorX, int sectorY, int index)
        {
            Sector = new SectorCoord(sectorX, sectorY);
            Index = index;
        }

        public bool IsValid { get { return Sector.IsValid && Index >= 0 && Index <= MaxIndex; } }

        public bool IsAuthored { get { return Index >= AuthoredBase; } }

        /// <summary>Stable collision-free 64-bit id.</summary>
        public ulong Id { get { return KeyBits.Pack(Sector.X, Sector.Y, Index, 0); } }

        public PlanetKey Planet(int planetIndex) { return new PlanetKey(this, planetIndex); }

        public static bool TryFromId(ulong id, out SystemKey key)
        {
            int x, y, sys, pf;
            KeyBits.Unpack(id, out x, out y, out sys, out pf);
            key = new SystemKey(x, y, sys);
            return pf == 0 && sys != KeyBits.SectorSystemIndex;
        }

        /// <summary>Compact string id: "s:X:Y:I".</summary>
        public override string ToString() { return "s:" + KeyBits.Str(Sector.X) + ":" + KeyBits.Str(Sector.Y) + ":" + KeyBits.Str(Index); }

        public static bool TryParse(string s, out SystemKey key)
        {
            key = default(SystemKey);
            if (s == null || s.Length < 7 || s[0] != 's' || s[1] != ':') return false;
            int pos = 2, x, y, i;
            if (!KeyBits.ReadInt(s, ref pos, out x) || !KeyBits.ReadSeparator(s, ref pos)) return false;
            if (!KeyBits.ReadInt(s, ref pos, out y) || !KeyBits.ReadSeparator(s, ref pos)) return false;
            if (!KeyBits.ReadInt(s, ref pos, out i) || pos != s.Length) return false;
            key = new SystemKey(x, y, i);
            return key.IsValid;
        }

        public bool Equals(SystemKey other) { return Index == other.Index && Sector.Equals(other.Sector); }
        public override bool Equals(object obj) { return obj is SystemKey && Equals((SystemKey)obj); }
        public override int GetHashCode() { unchecked { return (int)Hash.Mix(Id); } }
        public int CompareTo(SystemKey other) { return Id.CompareTo(other.Id); }
        public static bool operator ==(SystemKey a, SystemKey b) { return a.Equals(b); }
        public static bool operator !=(SystemKey a, SystemKey b) { return !a.Equals(b); }
    }

    /// <summary>A planet: its system plus an orbit index (0..14).</summary>
    public readonly struct PlanetKey : IEquatable<PlanetKey>, IComparable<PlanetKey>
    {
        public const int MaxIndex = 14;

        public readonly SystemKey System;
        public readonly int Index;

        public PlanetKey(SystemKey system, int index)
        {
            System = system;
            Index = index;
        }

        public PlanetKey(int sectorX, int sectorY, int systemIndex, int planetIndex)
        {
            System = new SystemKey(sectorX, sectorY, systemIndex);
            Index = planetIndex;
        }

        public bool IsValid { get { return System.IsValid && Index >= 0 && Index <= MaxIndex; } }

        /// <summary>Stable collision-free 64-bit id (never equal to any system or sector id).</summary>
        public ulong Id { get { return KeyBits.Pack(System.Sector.X, System.Sector.Y, System.Index, Index + 1); } }

        public static bool TryFromId(ulong id, out PlanetKey key)
        {
            int x, y, sys, pf;
            KeyBits.Unpack(id, out x, out y, out sys, out pf);
            key = new PlanetKey(x, y, sys, pf - 1);
            return pf != 0 && sys != KeyBits.SectorSystemIndex;
        }

        /// <summary>Compact string id for saves: "p:X:Y:I:P" (sector x, sector y, system index, planet index).</summary>
        public override string ToString()
        {
            return "p:" + KeyBits.Str(System.Sector.X) + ":" + KeyBits.Str(System.Sector.Y) + ":" + KeyBits.Str(System.Index) + ":" + KeyBits.Str(Index);
        }

        public static bool TryParse(string s, out PlanetKey key)
        {
            key = default(PlanetKey);
            if (s == null || s.Length < 9 || s[0] != 'p' || s[1] != ':') return false;
            int pos = 2, x, y, i, p;
            if (!KeyBits.ReadInt(s, ref pos, out x) || !KeyBits.ReadSeparator(s, ref pos)) return false;
            if (!KeyBits.ReadInt(s, ref pos, out y) || !KeyBits.ReadSeparator(s, ref pos)) return false;
            if (!KeyBits.ReadInt(s, ref pos, out i) || !KeyBits.ReadSeparator(s, ref pos)) return false;
            if (!KeyBits.ReadInt(s, ref pos, out p) || pos != s.Length) return false;
            key = new PlanetKey(x, y, i, p);
            return key.IsValid;
        }

        public bool Equals(PlanetKey other) { return Index == other.Index && System.Equals(other.System); }
        public override bool Equals(object obj) { return obj is PlanetKey && Equals((PlanetKey)obj); }
        public override int GetHashCode() { unchecked { return (int)Hash.Mix(Id); } }
        public int CompareTo(PlanetKey other) { return Id.CompareTo(other.Id); }
        public static bool operator ==(PlanetKey a, PlanetKey b) { return a.Equals(b); }
        public static bool operator !=(PlanetKey a, PlanetKey b) { return !a.Equals(b); }
    }
}
