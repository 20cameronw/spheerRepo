// =====================================================================================================================
// FRONTIER UNIVERSE CORE - generator version 1
//
// The universe is a PURE FUNCTION of (UniverseConfig.Seed, UniverseConfig.GeneratorVersion, the authored content, coordinates).
// Nothing is stored except deltas elsewhere. Rules that keep it that way:
//
//   * Every random attribute has its own named hash stream (Attr / StreamSalts): Hash.Stream(seed, key, "salt-name"). Adding an
//     attribute never shifts an existing one. Sequential Rng draws are only used inside one attribute.
//   * No System.Random, no string.GetHashCode, no Math.Sin/Cos/Exp/Log/Pow/Atan2 (platform dependent in the last bit): use Hash and
//     DetMath, which only use integer maths and IEEE-exact operations. The golden digest is therefore identical on every device.
//   * No float arithmetic: compute in double and round to float ONCE with an explicit cast. Mono (editor), IL2CPP (devices) and CoreCLR
//     (servers) evaluate float expressions with different intermediate precision; double arithmetic is the same everywhere. (SourceHygieneTests
//     enforces this, and crosscheck.ps1 proves identical digests on CoreCLR and Unity's Mono.)
//   * Coordinates are doubles in light-years; ids are exact bit packings (SectorCoord/SystemKey/PlanetKey), never hashes.
//
// VERSIONING (generator V1 is FROZEN once GoldenTests pass with the committed constants)
//   Any change that alters a single V1 output - a default value in UniverseConfig, a weight in a table, a salt name, a DetMath
//   coefficient, the order of draws inside an attribute - is a new generator version, not an edit of V1. To add V2 later:
//     1. Keep every V1 code path and golden test unchanged. Add V2 as new classes (or version-gated branches confined to planet level)
//        and register the version in UniverseConfig.Validate. Never rename or reuse a salt name; to re-roll one attribute give it a new
//        name through StreamSalts (SaltOverrides shows the mechanism).
//     2. V2 may change PLANET content (biomes, deposits, visuals, hazards, names) but must keep the sector layout (system count,
//        positions, ids, names, star classes) identical, because discovered systems are referenced by id from saves.
//     3. Each discovered planet's save record stores the GeneratorVersion it was first generated with. Resolve discovered planets with
//        GetPlanet(key, storedVersion) so they never change under the player; only undiscovered planets use the new default version
//        (UniverseConfig.GeneratorVersion at the time of discovery).
//     4. Add a V2 golden hash next to the V1 one; V1 golden must keep passing forever.
//
// THREAD SAFETY: a Universe may be used from one background worker plus the main thread (or any number of threads). All caches are
// guarded by short locks; generation itself runs outside the locks on immutable inputs, so two threads asking for the same sector may
// both generate it, but both get equal (and eventually identical-instance) results. Returned objects and arrays are immutable by
// convention: never modify them.
// =====================================================================================================================

using System;
using System.Collections.Generic;

namespace Frontier.Universe
{
    /// <summary>Cache hit/miss counters for diagnostics and the performance tests.</summary>
    public struct CacheStats
    {
        public long SectorHits, SectorMisses, BlockHits, BlockMisses;
        public int SectorsCached, BlocksCached;
    }

    /// <summary>The infinite procedural universe: sectors, systems, planets, density, tiers.</summary>
    public sealed class Universe
    {
        readonly UniverseConfig _config;
        readonly ResourceTable _resources;
        readonly StreamSalts _salts;
        readonly GalaxyField _field;
        readonly SystemGenerator _systemGen;
        readonly PlanetGenerator _planetGen;
        readonly BlockGenerator _blockGen;
        readonly Dictionary<SectorCoord, StarSystem[]> _authoredBySector = new Dictionary<SectorCoord, StarSystem[]>();
        readonly Dictionary<SystemKey, PlanetDescriptor[]> _authoredPlanets = new Dictionary<SystemKey, PlanetDescriptor[]>();
        readonly Dictionary<string, PlanetDescriptor> _authoredAlias = new Dictionary<string, PlanetDescriptor>();
        readonly List<StarSystem> _authoredSystems = new List<StarSystem>();

        readonly LruCache<SectorCoord, StarSystem[]> _sectors;
        readonly LruCache<BlockCoord, BlockData> _blocks;
        readonly object _sectorLock = new object();
        readonly object _blockLock = new object();
        long _sectorHits, _sectorMisses, _blockHits, _blockMisses;

        public Universe(UniverseConfig config) : this(config, null) { }

        /// <param name="config">Copied; later changes to the object do not affect the universe.</param>
        /// <param name="authored">Landmark galaxies and Core World systems supplied by the host (part of the universe definition).</param>
        public Universe(UniverseConfig config, AuthoredContent authored)
        {
            if (config == null) throw new ArgumentNullException("config");
            _config = config.Clone();
            _config.Validate();
            _resources = _config.Resources != null ? _config.Resources.Clone() : ResourceTable.CreateDefault(_config.EnablePlannedResources);
            _resources.Freeze();
            _config.Resources = _resources;
            _salts = new StreamSalts(_config.Seed, _config.SaltOverrides);

            var galaxies = authored != null ? authored.Galaxies : new List<GalaxyInfo>();
            var seen = new HashSet<ulong>();
            for (int i = 0; i < galaxies.Count; i++)
            {
                if (galaxies[i] == null) throw new ArgumentException("authored galaxy " + i + " is null");
                if (!seen.Add(galaxies[i].Id)) throw new ArgumentException("duplicate authored galaxy id for '" + galaxies[i].Name + "'");
            }
            _field = new GalaxyField(_config, galaxies);

            if (authored != null) RegisterAuthoredSystems(authored);

            _systemGen = new SystemGenerator(_config, _salts, _field, _authoredBySector);
            _planetGen = new PlanetGenerator(_config, _salts, _resources);
            _blockGen = new BlockGenerator(_config, _planetGen, SystemsInSector, _authoredPlanets);
            _sectors = new LruCache<SectorCoord, StarSystem[]>(_config.SectorCacheCapacity);
            _blocks = new LruCache<BlockCoord, BlockData>(_config.BlockCacheCapacity);
        }

        void RegisterAuthoredSystems(AuthoredContent authored)
        {
            var keys = new HashSet<SystemKey>();
            var bySector = new Dictionary<SectorCoord, List<StarSystem>>();
            for (int i = 0; i < authored.Systems.Count; i++)
            {
                AuthoredSystem a = authored.Systems[i];
                SystemSpec s = a.Spec;
                if (!s.Key.IsValid || !s.Key.IsAuthored)
                    throw new ArgumentException("authored system " + s.Key + " needs a system index in [" + SystemKey.AuthoredBase + ", " + SystemKey.MaxIndex + "]");
                SectorCoord at = SectorCoord.FromLy(s.X, s.Y, _config.SectorSize);
                if (at != s.Key.Sector)
                    throw new ArgumentException("authored system " + s.Key + " lies at (" + s.X + ", " + s.Y + ") which is sector " + at + ", not " + s.Key.Sector);
                if (!keys.Add(s.Key)) throw new ArgumentException("duplicate authored system key " + s.Key);
                if (a.Planets.Length > PlanetKey.MaxIndex + 1) throw new ArgumentException("authored system " + s.Key + " has too many planets");
                for (int p = 0; p < a.Planets.Length; p++)
                {
                    PlanetDescriptor pd = a.Planets[p];
                    if (pd == null || pd.Key.System != s.Key || pd.Key.Index != p)
                        throw new ArgumentException("authored planet " + p + " of " + s.Key + " must have key " + new PlanetKey(s.Key, p));
                    if (pd.AuthoredId != null)
                    {
                        if (_authoredAlias.ContainsKey(pd.AuthoredId)) throw new ArgumentException("duplicate authored planet id '" + pd.AuthoredId + "'");
                        _authoredAlias[pd.AuthoredId] = pd;
                    }
                }
                double hx = s.X - _config.HomeX, hy = s.Y - _config.HomeY;
                if (s.Tier == SystemSpec.AutoTier) s.Tier = _config.TierFromDistance(Math.Sqrt(hx * hx + hy * hy));
                if (s.GalaxyId == SystemSpec.AutoGalaxy)
                {
                    GalaxyInfo g = _field.GalaxyAt(s.X, s.Y);
                    s.GalaxyId = g == null ? 0UL : g.Id;
                }
                s.PlanetCount = a.Planets.Length;
                var sys = new StarSystem(s);
                List<StarSystem> list;
                if (!bySector.TryGetValue(at, out list)) bySector[at] = list = new List<StarSystem>();
                list.Add(sys);
                _authoredPlanets[s.Key] = a.Planets;
                _authoredSystems.Add(sys);
            }
            foreach (var kv in bySector)
            {
                kv.Value.Sort((p, q) => p.Key.Index.CompareTo(q.Key.Index));
                _authoredBySector[kv.Key] = kv.Value.ToArray();
            }
        }

        // ---- configuration -------------------------------------------------------------------------------------------------

        /// <summary>The universe's private copy of the configuration. Treat as read-only.</summary>
        public UniverseConfig Config { get { return _config; } }

        public int GeneratorVersion { get { return _config.GeneratorVersion; } }

        public GalaxyField Galaxies { get { return _field; } }

        /// <summary>The frozen resource tables in use.</summary>
        public ResourceTable Resources { get { return _resources; } }

        /// <summary>Authored systems in registration order.</summary>
        public IReadOnlyList<StarSystem> AuthoredSystems { get { return _authoredSystems; } }

        // ---- coordinates ---------------------------------------------------------------------------------------------------

        public SectorCoord SectorOf(double x, double y)
        {
            return SectorCoord.FromLy(x, y, _config.SectorSize);
        }

        public double DistanceFromHome(double x, double y)
        {
            double dx = x - _config.HomeX, dy = y - _config.HomeY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>tier = clamp(floor(1.6 * log2(1 + d / 60)), 0, 40) for d = distance from home (all constants configurable).</summary>
        public int TierAt(double x, double y)
        {
            return _config.TierFromDistance(DistanceFromHome(x, y));
        }

        // ---- density and galaxies ------------------------------------------------------------------------------------------

        public double Density(double x, double y) { return _field.Density(x, y); }

        public void SampleDensity(double x, double y, out DensitySample sample) { _field.Sample(x, y, out sample); }

        public GalaxyInfo GalaxyAt(double x, double y) { return _field.GalaxyAt(x, y); }

        /// <summary>The galaxy a system belongs to, or null in intergalactic space.</summary>
        public GalaxyInfo GalaxyOf(StarSystem system)
        {
            GalaxyInfo g;
            return _field.TryGetGalaxyById(system.GalaxyId, out g) ? g : null;
        }

        /// <summary>Fills dest[y * width + x] with the density at the centre of each cell of a width x height grid starting at (x0, y0).</summary>
        public void FillDensityGrid(double x0, double y0, double step, int width, int height, float[] dest)
        {
            if (dest == null || dest.Length < width * height) throw new ArgumentException("dest is too small");
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    dest[y * width + x] = (float)_field.Density(x0 + (x + 0.5) * step, y0 + (y + 0.5) * step);
        }

        // ---- systems -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The systems of a sector: generated ones first (indices 0..n-1, possibly with gaps), then authored ones (indices 1000+).
        /// The array is cached and shared; do not modify it. Allocation free on a cache hit.
        /// </summary>
        public StarSystem[] SystemsInSector(SectorCoord sector)
        {
            StarSystem[] r;
            lock (_sectorLock)
            {
                if (_sectors.TryGet(sector, out r))
                {
                    _sectorHits++;
                    return r;
                }
            }
            r = _systemGen.Generate(sector);
            lock (_sectorLock)
            {
                StarSystem[] other;
                if (_sectors.TryGet(sector, out other)) return other;
                _sectors.Put(sector, r);
                _sectorMisses++;
            }
            return r;
        }

        /// <summary>
        /// Appends every system whose position lies inside the rectangle (inclusive) to <paramref name="results"/> and returns how many were added.
        /// Allocation free once the list has grown. Throws for rectangles covering more than one million sectors (use FillDensityGrid for galaxy-scale views).
        /// </summary>
        public int SystemsInRect(double x0, double y0, double x1, double y1, List<StarSystem> results)
        {
            if (results == null) throw new ArgumentNullException("results");
            if (x1 < x0) { double t = x0; x0 = x1; x1 = t; }
            if (y1 < y0) { double t = y0; y0 = y1; y1 = t; }
            SectorCoord a = SectorOf(x0, y0), b = SectorOf(x1, y1);
            long count = ((long)b.X - a.X + 1) * ((long)b.Y - a.Y + 1);
            if (count > 1000000L) throw new ArgumentException("rectangle covers " + count + " sectors");
            int added = 0;
            for (int sy = a.Y; sy <= b.Y; sy++)
            {
                for (int sx = a.X; sx <= b.X; sx++)
                {
                    StarSystem[] sys = SystemsInSector(new SectorCoord(sx, sy));
                    for (int i = 0; i < sys.Length; i++)
                    {
                        StarSystem s = sys[i];
                        if (s.X >= x0 && s.X <= x1 && s.Y >= y0 && s.Y <= y1)
                        {
                            results.Add(s);
                            added++;
                        }
                    }
                }
            }
            return added;
        }

        public StarSystem[] SystemsInRect(double x0, double y0, double x1, double y1)
        {
            var list = new List<StarSystem>();
            SystemsInRect(x0, y0, x1, y1, list);
            return list.ToArray();
        }

        public bool TryGetSystem(SystemKey key, out StarSystem system)
        {
            system = null;
            if (!key.IsValid) return false;
            StarSystem[] sys = SystemsInSector(key.Sector);
            for (int i = 0; i < sys.Length; i++)
            {
                if (sys[i].Key.Index == key.Index)
                {
                    system = sys[i];
                    return true;
                }
            }
            return false;
        }

        public StarSystem GetSystem(SystemKey key)
        {
            StarSystem s;
            return TryGetSystem(key, out s) ? s : null;
        }

        public bool TryGetSystemById(string id, out StarSystem system)
        {
            system = null;
            SystemKey key;
            return SystemKey.TryParse(id, out key) && TryGetSystem(key, out system);
        }

        // ---- planets -------------------------------------------------------------------------------------------------------

        BlockData GetBlock(BlockCoord bc)
        {
            BlockData b;
            lock (_blockLock)
            {
                if (_blocks.TryGet(bc, out b))
                {
                    _blockHits++;
                    return b;
                }
            }
            b = _blockGen.Build(bc);
            lock (_blockLock)
            {
                BlockData other;
                if (_blocks.TryGet(bc, out other)) return other;
                _blocks.Put(bc, b);
                _blockMisses++;
            }
            return b;
        }

        /// <summary>The planets of a system in orbit order (shared array: do not modify). Empty for unknown systems.</summary>
        public PlanetDescriptor[] PlanetsOf(StarSystem system)
        {
            if (system == null) return Array.Empty<PlanetDescriptor>();
            PlanetDescriptor[] authored;
            if (system.IsAuthored) return _authoredPlanets.TryGetValue(system.Key, out authored) ? authored : Array.Empty<PlanetDescriptor>();
            BlockData b = GetBlock(_blockGen.BlockOf(system.Key.Sector));
            return b.PlanetsOf(system.Key) ?? Array.Empty<PlanetDescriptor>();
        }

        public PlanetDescriptor[] PlanetsOf(SystemKey key)
        {
            StarSystem s;
            return TryGetSystem(key, out s) ? PlanetsOf(s) : Array.Empty<PlanetDescriptor>();
        }

        public bool TryGetPlanet(PlanetKey key, out PlanetDescriptor planet)
        {
            planet = null;
            if (!key.IsValid) return false;
            PlanetDescriptor[] all = PlanetsOf(key.System);
            if (key.Index >= all.Length) return false;
            planet = all[key.Index];
            return true;
        }

        public PlanetDescriptor GetPlanet(PlanetKey key)
        {
            PlanetDescriptor p;
            return TryGetPlanet(key, out p) ? p : null;
        }

        /// <summary>
        /// Resolves a planet generated with a specific generator version (saves store the version a planet was discovered with).
        /// Only version 1 exists today; see the versioning notes at the top of this file for how V2 plugs in.
        /// </summary>
        public PlanetDescriptor GetPlanet(PlanetKey key, int generatorVersion)
        {
            if (generatorVersion != UniverseConfig.CurrentGeneratorVersion)
                throw new NotSupportedException("Generator version " + generatorVersion + " is not available in this build");
            return GetPlanet(key);
        }

        /// <summary>Resolves a compact id ("p:12:-4:2:3") or the alias of an authored world (for example "sylva").</summary>
        public bool TryGetPlanetById(string id, out PlanetDescriptor planet)
        {
            planet = null;
            if (string.IsNullOrEmpty(id)) return false;
            PlanetKey key;
            if (PlanetKey.TryParse(id, out key)) return TryGetPlanet(key, out planet);
            return _authoredAlias.TryGetValue(id, out planet);
        }

        // ---- maintenance ---------------------------------------------------------------------------------------------------

        public void ClearCaches()
        {
            lock (_sectorLock) { _sectors.Clear(); }
            lock (_blockLock) { _blocks.Clear(); }
        }

        public CacheStats GetCacheStats()
        {
            var s = new CacheStats();
            lock (_sectorLock) { s.SectorHits = _sectorHits; s.SectorMisses = _sectorMisses; s.SectorsCached = _sectors.Count; }
            lock (_blockLock) { s.BlockHits = _blockHits; s.BlockMisses = _blockMisses; s.BlocksCached = _blocks.Count; }
            return s;
        }

        /// <summary>Number of anchor deposits that were injected into the block containing this sector to complete its resource coverage (diagnostics).</summary>
        public int AnchorsInjectedFor(SectorCoord sector)
        {
            return GetBlock(_blockGen.BlockOf(sector)).AnchorsInjected;
        }
    }
}
