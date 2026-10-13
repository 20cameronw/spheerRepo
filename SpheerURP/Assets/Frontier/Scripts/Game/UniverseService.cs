using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Frontier.Universe;
using Uni = Frontier.Universe.Universe;

namespace Frontier
{
    /// <summary>
    /// The game's door to the universe: owns the deterministic <see cref="Universe"/> (built from <see cref="UniverseConfig"/> with the
    /// game's resource set and the authored Core Worlds), turns descriptors into <see cref="PlanetData"/>, caches a few of them and prices
    /// travel through <see cref="ITravelCostModel"/>. It also implements <see cref="IWorldIdMap"/> so saves can be migrated from legacy ids.
    /// The generator itself is pure and thread safe, but this service touches ScriptableObjects: use it from the main thread, and give a
    /// background worker the raw <see cref="Universe"/> instead.
    /// </summary>
    public sealed class UniverseService : IWorldIdMap
    {
        /// <summary>The universe seed of the shipped game (the generator's default, spelling "FRONTIER").</summary>
        public const ulong DefaultSeed = 0x46524F4E54494552UL;

        const int GeneratedCacheSize = 128;

        static readonly ConditionalWeakTable<GameDatabase, UniverseService> Shared = new ConditionalWeakTable<GameDatabase, UniverseService>();

        public readonly GameDatabase Db;
        public readonly Uni Universe;
        public readonly AuthoredWorlds Authored;
        public readonly ITravelCostModel Travel;

        readonly Dictionary<PlanetKey, PlanetData> authoredCache = new Dictionary<PlanetKey, PlanetData>();
        readonly Dictionary<PlanetKey, PlanetData> generatedCache = new Dictionary<PlanetKey, PlanetData>();
        readonly Queue<PlanetKey> generatedOrder = new Queue<PlanetKey>();
        readonly List<PlanetData> authoredPlanets = new List<PlanetData>();

        /// <summary>The same service for the same database object (building one costs a few milliseconds, tests build many states).</summary>
        public static UniverseService For(GameDatabase db)
        {
            UniverseService service;
            if (!Shared.TryGetValue(db, out service))
            {
                service = new UniverseService(db);
                Shared.Add(db, service);
            }
            return service;
        }

        public UniverseService(GameDatabase db, ITravelCostModel travel = null, ulong seed = DefaultSeed)
        {
            if (db == null) throw new ArgumentNullException("db");
            Db = db;
            Travel = travel ?? new TravelModel();
            var config = new UniverseConfig { Seed = seed, EnablePlannedResources = false, Resources = BuildResourceTable(db, false) };
            Authored = AuthoredWorlds.Build(db, config);
            Universe = new Uni(config, Authored.Content);
            foreach (var so in db.planets)
            {
                if (so == null) continue;
                authoredPlanets.Add(Wrap(Authored.DescriptorOf(so.id)));
            }
            var home = Authored.DescriptorOf(Authored.HomePlanetAuthoredId);
            Home = Wrap(home);
        }

        /// <summary>The planet a new game starts on (Sylva).</summary>
        public PlanetData Home { get; private set; }

        public ulong Seed { get { return Universe.Config.Seed; } }

        /// <summary>The 30 Core Worlds in the order of the planet list of the database.</summary>
        public IReadOnlyList<PlanetData> AuthoredPlanets { get { return authoredPlanets; } }

        // ---- the resource table ----------------------------------------------------------------------------------------

        /// <summary>
        /// The generator's default resource table, validated against the game's resources: every id the generator can place (definitions,
        /// biome profiles, the always present list) must exist as a <see cref="ResourceSO"/>, otherwise a generated world could offer an
        /// item the game cannot hold.
        /// </summary>
        public static ResourceTable BuildResourceTable(GameDatabase db, bool includePlanned)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in db.resources) if (r != null && !string.IsNullOrEmpty(r.id)) known.Add(r.id);
            var table = ResourceTable.CreateDefault(includePlanned);
            foreach (var def in table.Defs)
                if (!known.Contains(def.Id)) throw new InvalidOperationException("the universe can generate '" + def.Id + "' but the game has no such ResourceSO");
            for (int b = 0; b < table.Profiles.Length; b++)
                foreach (var entry in table.Profiles[b].Entries)
                    if (!known.Contains(entry.ResourceId)) throw new InvalidOperationException("biome " + (Biome)b + " offers '" + entry.ResourceId + "', which is not a ResourceSO");
            foreach (var entry in table.BeltProfile.Entries)
                if (!known.Contains(entry.ResourceId)) throw new InvalidOperationException("asteroid belts offer '" + entry.ResourceId + "', which is not a ResourceSO");
            foreach (string id in table.AlwaysPresent)
                if (!known.Contains(id)) throw new InvalidOperationException("every world carries '" + id + "', which is not a ResourceSO");
            return table;
        }

        // ---- planets ---------------------------------------------------------------------------------------------------

        ResourceSO FindResource(string id) { return Db.FindResource(id); }

        PlanetData Wrap(PlanetDescriptor d)
        {
            if (d == null) return null;
            PlanetData p;
            if (authoredCache.TryGetValue(d.Key, out p) || generatedCache.TryGetValue(d.Key, out p)) return p;
            StarSystem star;
            if (!Universe.TryGetSystem(d.Key.System, out star)) throw new InvalidOperationException("planet " + d.Id + " belongs to an unknown system");
            PlanetSO source = d.AuthoredId != null ? Authored.SourceOf(d.AuthoredId) : null;
            p = new PlanetData(d, star, source, Universe.Config.Seed, FindResource, Db.scrap);
            if (d.AuthoredId != null) authoredCache[d.Key] = p;
            else
            {
                generatedCache[d.Key] = p;
                generatedOrder.Enqueue(d.Key);
                while (generatedOrder.Count > GeneratedCacheSize) generatedCache.Remove(generatedOrder.Dequeue());
            }
            return p;
        }

        public PlanetData Get(PlanetDescriptor descriptor) { return Wrap(descriptor); }

        /// <summary>A world by canonical id ("p:X:Y:I:P") or by the legacy id of a Core World ("sylva"); null when it does not exist.</summary>
        public PlanetData Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            PlanetDescriptor d;
            return Universe.TryGetPlanetById(id, out d) ? Wrap(d) : null;
        }

        public bool TryGet(PlanetKey key, out PlanetData planet)
        {
            PlanetDescriptor d;
            planet = Universe.TryGetPlanet(key, out d) ? Wrap(d) : null;
            return planet != null;
        }

        /// <summary>The worlds of a system in orbit order.</summary>
        public List<PlanetData> PlanetsOf(StarSystem system)
        {
            var list = new List<PlanetData>();
            foreach (var d in Universe.PlanetsOf(system)) list.Add(Wrap(d));
            return list;
        }

        // ---- distances and travel --------------------------------------------------------------------------------------

        public TravelNode NodeOf(PlanetData p)
        {
            return new TravelNode(p.Star.Id, p.Star.X, p.Star.Y, p.Descriptor.OrbitRadius, p.Descriptor.OrbitPhase);
        }

        /// <summary>Distance of the planet's system from the home system, in light-years.</summary>
        public double DistanceFromHomeLy(PlanetData p) { return Universe.DistanceFromHome(p.Star.X, p.Star.Y); }

        /// <summary>Fuel for a trip from one world to another (0 for the same world).</summary>
        public int FuelCost(PlanetData from, PlanetData to, float efficiency)
        {
            if (from == null || to == null || from.Id == to.Id) return 0;
            return Travel.FuelCost(NodeOf(from), NodeOf(to), efficiency);
        }

        // ---- IWorldIdMap (save migration) ------------------------------------------------------------------------------

        string IWorldIdMap.HomeId { get { return Home.Id; } }

        bool IWorldIdMap.TryCanonical(string id, out string canonicalId)
        {
            PlanetDescriptor d;
            if (Universe.TryGetPlanetById(id, out d)) { canonicalId = d.Id; return true; }
            canonicalId = null;
            return false;
        }

        double IWorldIdMap.DistanceFromHomeLy(string canonicalId)
        {
            PlanetData p = Get(canonicalId);
            return p == null ? -1.0 : DistanceFromHomeLy(p);
        }

        // ---- searching (debug tools, harness) --------------------------------------------------------------------------

        /// <summary>
        /// The first generated world (not a Core World) that matches, scanning sectors in rings around the home sector, outwards from the
        /// ring where the minimum tier starts. Deterministic: the same query always returns the same planet. <paramref name="skip"/> returns
        /// the n-th match instead of the first (a debug menu can cycle through them). Null when nothing is found within the budget.
        /// </summary>
        public PlanetData FindGenerated(Func<PlanetDescriptor, bool> match, int minTier = 0, int skip = 0, int maxSectors = 60000)
        {
            if (match == null) throw new ArgumentNullException("match");
            double sector = Universe.Config.SectorSize;
            double tierStart = Universe.Config.TierDistanceUnit * (DetMath.Pow(2.0, Math.Max(0, minTier) / Universe.Config.TierScale) - 1.0);
            int ring = Math.Max(0, (int)Math.Floor(tierStart / sector) - 1);
            SectorCoord home = Universe.SectorOf(Universe.Config.HomeX, Universe.Config.HomeY);
            int scanned = 0;
            while (scanned < maxSectors)
            {
                for (int i = 0; i < (ring == 0 ? 1 : 8 * ring); i++)
                {
                    int dx, dy;
                    RingCell(ring, i, out dx, out dy);
                    scanned++;
                    foreach (var system in Universe.SystemsInSector(home.Offset(dx, dy)))
                    {
                        if (system.IsAuthored) continue;
                        foreach (var d in Universe.PlanetsOf(system))
                        {
                            if (d.Tier < minTier || !match(d)) continue;
                            if (skip-- > 0) continue;
                            return Wrap(d);
                        }
                    }
                }
                ring++;
            }
            return null;
        }

        /// <summary>The first generated world of a biome and/or size class (null means any) at or above a tier. See <see cref="FindGenerated(Func{PlanetDescriptor, bool}, int, int, int)"/>.</summary>
        public PlanetData FindGenerated(Biome? biome, SizeClass? size, int minTier = 0, int skip = 0, int maxSectors = 60000)
        {
            return FindGenerated(d => (!biome.HasValue || d.Biome == biome.Value) && (!size.HasValue || d.Size == size.Value), minTier, skip, maxSectors);
        }

        // Cell i of the square ring at Chebyshev distance r: the top row left to right, the bottom row, then the two columns between them.
        static void RingCell(int r, int i, out int dx, out int dy)
        {
            if (r == 0) { dx = 0; dy = 0; return; }
            int side = 2 * r + 1;
            if (i < side) { dx = -r + i; dy = -r; return; }
            i -= side;
            if (i < side) { dx = -r + i; dy = r; return; }
            i -= side;
            if (i < side - 2) { dx = -r; dy = -r + 1 + i; return; }
            i -= side - 2;
            dx = r;
            dy = -r + 1 + i;
        }
    }
}
