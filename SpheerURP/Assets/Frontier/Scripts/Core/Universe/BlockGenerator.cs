using System;
using System.Collections.Generic;

namespace Frontier.Universe
{
    /// <summary>Coordinate of a coverage block (AnchorBlockSectors x AnchorBlockSectors sectors).</summary>
    internal readonly struct BlockCoord : IEquatable<BlockCoord>
    {
        public readonly int X, Y;

        public BlockCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(BlockCoord o) { return X == o.X && Y == o.Y; }
        public override bool Equals(object obj) { return obj is BlockCoord && Equals((BlockCoord)obj); }
        public override int GetHashCode() { unchecked { return (int)Hash.Mix(((ulong)(uint)X << 32) | (uint)Y); } }
    }

    /// <summary>The finished planets of every system in one coverage block.</summary>
    internal sealed class BlockData
    {
        public readonly BlockCoord Coord;
        public readonly int Size;
        public readonly StarSystem[][] Systems;
        public readonly PlanetDescriptor[][][] Planets;
        /// <summary>How many anchor deposits had to be injected to complete the coverage of this block.</summary>
        public readonly int AnchorsInjected;

        public BlockData(BlockCoord coord, int size, StarSystem[][] systems, PlanetDescriptor[][][] planets, int anchors)
        {
            Coord = coord;
            Size = size;
            Systems = systems;
            Planets = planets;
            AnchorsInjected = anchors;
        }

        public PlanetDescriptor[] PlanetsOf(SystemKey key)
        {
            int lx = key.Sector.X - Coord.X * Size, ly = key.Sector.Y - Coord.Y * Size;
            if (lx < 0 || ly < 0 || lx >= Size || ly >= Size) return null;
            int ls = ly * Size + lx;
            StarSystem[] sys = Systems[ls];
            for (int i = 0; i < sys.Length; i++) if (sys[i].Key.Index == key.Index) return Planets[ls][i];
            return null;
        }
    }

    /// <summary>
    /// Builds the planets of a whole block and enforces the coverage guarantee: for every anchored raw resource that no planet of the
    /// block carries naturally, one eligible planet (chosen by the lowest anchor hash) gets an injected deposit. The decision depends
    /// only on the block's own content, so it is a pure function of (seed, block).
    /// </summary>
    internal sealed class BlockGenerator
    {
        readonly UniverseConfig _cfg;
        readonly PlanetGenerator _planets;
        readonly ResourceTable _res;
        readonly Func<SectorCoord, StarSystem[]> _sectorSource;
        readonly Dictionary<SystemKey, PlanetDescriptor[]> _authoredPlanets;
        readonly ulong[] _resHash;

        public BlockGenerator(UniverseConfig cfg, PlanetGenerator planets, Func<SectorCoord, StarSystem[]> sectorSource,
            Dictionary<SystemKey, PlanetDescriptor[]> authoredPlanets)
        {
            _cfg = cfg;
            _planets = planets;
            _res = planets.Resources;
            _sectorSource = sectorSource;
            _authoredPlanets = authoredPlanets;
            _resHash = new ulong[_res.Defs.Count];
            for (int i = 0; i < _resHash.Length; i++) _resHash[i] = Hash.Fnv1a64(_res.Defs[i].Id);
        }

        public BlockCoord BlockOf(SectorCoord s)
        {
            int b = _cfg.AnchorBlockSectors;
            return new BlockCoord(DetMath.FloorDiv(s.X, b), DetMath.FloorDiv(s.Y, b));
        }

        public BlockData Build(BlockCoord bc)
        {
            int B = _cfg.AnchorBlockSectors;
            int cells = B * B;
            var systems = new StarSystem[cells][];
            var specs = new PlanetSpec[cells][][];
            int defCount = _res.Defs.Count;
            var present = new bool[defCount];

            for (int ly = 0; ly < B; ly++)
            {
                for (int lx = 0; lx < B; lx++)
                {
                    int ls = ly * B + lx;
                    var sc = new SectorCoord(bc.X * B + lx, bc.Y * B + ly);
                    StarSystem[] sys = sc.IsValid ? _sectorSource(sc) : Array.Empty<StarSystem>();
                    systems[ls] = sys;
                    specs[ls] = new PlanetSpec[sys.Length][];
                    for (int i = 0; i < sys.Length; i++)
                    {
                        if (sys[i].IsAuthored)
                        {
                            PlanetDescriptor[] ap;
                            if (_authoredPlanets.TryGetValue(sys[i].Key, out ap))
                                for (int p = 0; p < ap.Length; p++) MarkPresent(present, ap[p].Deposits);
                            continue;
                        }
                        PlanetSpec[] ps = _planets.GenerateSpecs(sys[i]);
                        specs[ls][i] = ps;
                        for (int p = 0; p < ps.Length; p++) MarkPresent(present, ps[p].Deposits);
                    }
                }
            }

            int injected = 0;
            for (int d = 0; d < defCount; d++)
            {
                ResourceDef def = _res.Defs[d];
                if (!def.Anchored || present[d]) continue;
                if (Inject(d, def, specs, systems, true) || (def.AnchorFallbackSolid && Inject(d, def, specs, systems, false))) injected++;
            }

            var planets = new PlanetDescriptor[cells][][];
            for (int ls = 0; ls < cells; ls++)
            {
                StarSystem[] sys = systems[ls];
                planets[ls] = new PlanetDescriptor[sys.Length][];
                for (int i = 0; i < sys.Length; i++)
                {
                    if (sys[i].IsAuthored)
                    {
                        PlanetDescriptor[] ap;
                        planets[ls][i] = _authoredPlanets.TryGetValue(sys[i].Key, out ap) ? ap : Array.Empty<PlanetDescriptor>();
                        continue;
                    }
                    PlanetSpec[] ps = specs[ls][i];
                    var arr = new PlanetDescriptor[ps.Length];
                    for (int p = 0; p < ps.Length; p++) arr[p] = new PlanetDescriptor(ps[p]);
                    planets[ls][i] = arr;
                }
            }
            return new BlockData(bc, B, systems, planets, injected);
        }

        void MarkPresent(bool[] present, Deposit[] deposits)
        {
            for (int i = 0; i < deposits.Length; i++)
            {
                int d = _res.IndexOf(deposits[i].ResourceId);
                if (d >= 0) present[d] = true;
            }
        }

        bool Inject(int defIndex, ResourceDef def, PlanetSpec[][][] specs, StarSystem[][] systems, bool strict)
        {
            ulong best = ulong.MaxValue;
            int bs = -1, bi = -1, bp = -1;
            for (int ls = 0; ls < specs.Length; ls++)
            {
                PlanetSpec[][] perSystem = specs[ls];
                for (int i = 0; i < perSystem.Length; i++)
                {
                    PlanetSpec[] ps = perSystem[i];
                    if (ps == null) continue;
                    for (int p = 0; p < ps.Length; p++)
                    {
                        if (!Eligible(ref ps[p], defIndex, def, strict)) continue;
                        ulong score = Hash.Stream(_planets.AnchorSeed, Hash.Combine(ps[p].Key.Id, _resHash[defIndex]));
                        if (score < best)
                        {
                            best = score;
                            bs = ls;
                            bi = i;
                            bp = p;
                        }
                    }
                }
            }
            if (bs < 0) return false;
            PlanetSpec[] host = specs[bs][bi];
            Deposit[] old = host[bp].Deposits;
            var grown = new Deposit[old.Length + 1];
            Array.Copy(old, grown, old.Length);
            grown[old.Length] = _planets.MakeAnchorDeposit(defIndex, host[bp].Biome, host[bp].Kind, host[bp].Tier, host[bp].Key.Id);
            host[bp].Deposits = grown;
            return true;
        }

        bool Eligible(ref PlanetSpec p, int defIndex, ResourceDef def, bool strict)
        {
            if (p.Tier < _planets.MinTierFor(p.Biome, p.Kind, defIndex)) return false;
            if (strict) return p.Kind == PlanetKind.AsteroidBelt ? def.AnchorOnBelts : BiomeMask.Has(def.AnchorBiomes, p.Biome);
            return p.Kind != PlanetKind.GasGiant;
        }
    }
}
