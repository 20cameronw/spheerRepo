using System;
using System.Collections.Generic;
using UnityEngine;
using Frontier.Universe;

namespace Frontier
{
    /// <summary>
    /// Converts the hand made worlds (the <see cref="PlanetSO"/> assets plus <see cref="PlanetSO.Systems"/>) into universe content: the
    /// landmark galaxies, six authored star systems with a descriptor for each of the 30 Core Worlds. The assets stay the authoring
    /// format; this class is a pure conversion and can be rebuilt any number of times with the same result.
    ///
    /// Placement: the home system (the one with the start planet) sits at the origin. Every other system lies in the direction it had on
    /// the original map, at the light-year distance where the universe tier formula gives the (rounded) average tier of its planets,
    /// pulled in to 92 % of the home galaxy's radius when that would put it beyond the rim. Keys: system index 1000 + the system's
    /// position in <see cref="PlanetSO.Systems"/>, planet index = position in the canonical orbit order below; both are frozen by tests.
    /// </summary>
    public sealed class AuthoredWorlds
    {
        /// <summary>Where a system ended up and how well it matches its planets' tiers (the placement table).</summary>
        public sealed class SystemPlacement
        {
            public string Id, Name;
            public SystemKey Key;
            public double X, Y;
            public double DistanceFromHomeLy;
            public int TierAtPosition;
            public double AverageTier;
            public int MinTier, MaxTier;
            public bool PulledInsideGalaxy;
            /// <summary>The galaxy the system belongs to; a system at the rim sits below the density of the halo, so it is assigned explicitly.</summary>
            public ulong GalaxyId = SystemSpec.AutoGalaxy;
            public string[] PlanetIds;
        }

        // The canonical orbit order inside each system (inner to outer). Planet keys are positions in these lists, so this table is
        // frozen: new worlds are appended, nothing is ever reordered. Worlds missing from the table are appended in id order.
        static readonly string[][] CanonicalOrder =
        {
            new[] { "sylva", "glacia", "ferrum", "veridian", "calcite" },
            new[] { "cinder", "aurum", "pyre", "sulfur", "magma" },
            new[] { "arbor", "coral", "mire", "tide", "lotus", "spore" },
            new[] { "nyx", "obsidian", "vesper", "eclipse", "phantom" },
            new[] { "halcyon", "zephyr", "tundra", "borealis", "cryo" },
            new[] { "prism", "kaos", "aether", "genesis" }
        };

        static readonly string[] CanonicalSystems = { "sylva", "ember", "verdance", "umbra", "frost", "edge" };

        // Terrain of each Core World in the generator's vocabulary (the assets have no biome field).
        static readonly string[] BiomeTable =
        {
            "sylva:Lush", "veridian:Lush", "arbor:Lush", "ferrum:Mineral", "calcite:Mineral", "glacia:Glacial", "halcyon:Glacial", "tundra:Glacial",
            "borealis:Glacial", "cryo:Glacial", "cinder:Volcanic", "sulfur:Volcanic", "pyre:Volcanic", "magma:Volcanic", "mire:Fungal", "spore:Fungal",
            "prism:Crystal", "aether:Crystal", "nyx:Obsidian", "vesper:Obsidian", "eclipse:Obsidian", "obsidian:Obsidian", "phantom:Obsidian",
            "kaos:Shattered", "genesis:Shattered", "aurum:Desert", "tide:Oceanic", "lotus:Oceanic", "coral:Oceanic", "zephyr:GasGiant"
        };

        /// <summary>Radius of an orbit on the old map (0..1 map units) that is the innermost one: 0.4 AU. 60 AU per map unit beyond that.</summary>
        const double InnermostMapRadius = 0.055, InnerOrbitAu = 0.4, AuPerMapUnit = 60.0;
        /// <summary>The old map drew a system's own planets within this radius; planets further out never had a map position of their own.</summary>
        const double MaxOwnMapRadius = 0.145;
        /// <summary>Orbit spacing used for worlds that never had a position (0.008 map units, like the old layout).</summary>
        const double UnplacedOrbitStep = 0.008;
        /// <summary>A system is kept inside this fraction of the home galaxy's radius.</summary>
        const double GalaxyMargin = 0.92;

        public readonly AuthoredContent Content;
        public readonly IReadOnlyList<SystemPlacement> Placements;
        public readonly string HomePlanetAuthoredId;

        readonly Dictionary<string, PlanetDescriptor> descriptors = new Dictionary<string, PlanetDescriptor>(StringComparer.Ordinal);
        readonly Dictionary<string, PlanetSO> sources = new Dictionary<string, PlanetSO>(StringComparer.Ordinal);
        readonly Dictionary<string, SystemPlacement> placementOfPlanet = new Dictionary<string, SystemPlacement>(StringComparer.Ordinal);
        readonly List<PlanetDescriptor> ordered = new List<PlanetDescriptor>();

        AuthoredWorlds(AuthoredContent content, List<SystemPlacement> placements, string homePlanetId)
        {
            Content = content;
            Placements = placements;
            HomePlanetAuthoredId = homePlanetId;
        }

        /// <summary>The descriptor of a Core World by its authored id ("sylva"), or null.</summary>
        public PlanetDescriptor DescriptorOf(string authoredId)
        {
            PlanetDescriptor d;
            return authoredId != null && descriptors.TryGetValue(authoredId, out d) ? d : null;
        }

        public PlanetSO SourceOf(string authoredId)
        {
            PlanetSO so;
            return authoredId != null && sources.TryGetValue(authoredId, out so) ? so : null;
        }

        public SystemPlacement PlacementOf(string authoredPlanetId)
        {
            SystemPlacement p;
            return authoredPlanetId != null && placementOfPlanet.TryGetValue(authoredPlanetId, out p) ? p : null;
        }

        /// <summary>All Core World descriptors, system by system in canonical orbit order.</summary>
        public IReadOnlyList<PlanetDescriptor> Descriptors { get { return ordered; } }

        // ---- build -----------------------------------------------------------------------------------------------------

        public static AuthoredWorlds Build(GameDatabase db, UniverseConfig config)
        {
            if (db == null) throw new ArgumentNullException("db");
            if (config == null) config = new UniverseConfig();

            // 1. group the planets by system, in canonical orbit order
            var all = new Dictionary<string, PlanetSO>(StringComparer.Ordinal);
            foreach (var so in db.planets)
            {
                if (so == null) continue;
                if (string.IsNullOrEmpty(so.id)) throw new ArgumentException("a planet asset has no id: " + so.name);
                if (all.ContainsKey(so.id)) throw new ArgumentException("duplicate planet id '" + so.id + "'");
                all[so.id] = so;
            }
            var systems = PlanetSO.Systems;
            var members = new List<PlanetSO>[systems.Length];
            for (int i = 0; i < systems.Length; i++) members[i] = new List<PlanetSO>();
            var assigned = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < systems.Length; i++)
            {
                int canonical = Array.IndexOf(CanonicalSystems, systems[i].id);
                if (canonical < 0) continue;
                foreach (string id in CanonicalOrder[canonical])
                {
                    PlanetSO so;
                    if (!all.TryGetValue(id, out so) || !string.Equals(so.systemId, systems[i].id, StringComparison.Ordinal)) continue;
                    members[i].Add(so);
                    assigned.Add(id);
                }
            }
            var rest = new List<string>();
            foreach (string id in all.Keys) if (!assigned.Contains(id)) rest.Add(id);
            rest.Sort(StringComparer.Ordinal);
            foreach (string id in rest)
            {
                var so = all[id];
                int k = Array.FindIndex(systems, s => s.id == so.systemId);
                if (k < 0)
                {
                    k = 0;
                    Debug.LogError("Frontier: planet '" + id + "' has the unknown system '" + so.systemId + "'; it is placed in " + systems[0].name + ".");
                }
                members[k].Add(so);
            }

            // 2. where is home, and which galaxy holds it
            string homeId = db.startPlanet != null ? db.startPlanet.id : "sylva";
            PlanetSO homePlanet;
            if (!all.TryGetValue(homeId, out homePlanet)) throw new ArgumentException("the start planet '" + homeId + "' is not in the planet list");
            int homeSystem = Array.FindIndex(systems, s => s.id == homePlanet.systemId);
            if (homeSystem < 0) homeSystem = 0;
            var content = Landmarks.CreateDefault();
            GalaxyInfo homeGalaxy = null;
            foreach (var g in content.Galaxies) if (g.Contains(config.HomeX, config.HomeY)) { homeGalaxy = g; break; }
            string prefix = homeGalaxy != null ? homeGalaxy.CataloguePrefix : "VS";

            // 3. build every system
            var placements = new List<SystemPlacement>();
            var result = new AuthoredWorlds(content, placements, homeId);
            for (int k = 0; k < systems.Length; k++)
            {
                if (members[k].Count == 0) continue;
                if (members[k].Count > PlanetKey.MaxIndex + 1) throw new ArgumentException("system '" + systems[k].id + "' has more than " + (PlanetKey.MaxIndex + 1) + " planets");
                var placement = Place(config, systems, k, homeSystem, members[k], homeGalaxy);
                placement.Key = new SystemKey(SectorCoord.FromLy(placement.X, placement.Y, config.SectorSize), SystemKey.AuthoredBase + k);
                string designation = prefix + "-A" + (k + 1).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
                var spec = MakeSystem(systems[k], placement, designation);
                var planets = new PlanetDescriptor[members[k].Count];
                var orbits = Orbits(systems[k], members[k]);
                for (int i = 0; i < planets.Length; i++)
                {
                    planets[i] = MakePlanet(members[k][i], new PlanetKey(placement.Key, i), designation, orbits[i], config);
                    result.descriptors[members[k][i].id] = planets[i];
                    result.sources[members[k][i].id] = members[k][i];
                    result.placementOfPlanet[members[k][i].id] = placement;
                    result.ordered.Add(planets[i]);
                }
                placement.PlanetIds = new string[planets.Length];
                for (int i = 0; i < planets.Length; i++) placement.PlanetIds[i] = members[k][i].id;
                content.AddSystem(spec, planets);
                placements.Add(placement);
            }
            return result;
        }

        // ---- placement -------------------------------------------------------------------------------------------------

        static SystemPlacement Place(UniverseConfig cfg, PlanetSO.StarSystem[] systems, int k, int homeSystem, List<PlanetSO> members, GalaxyInfo galaxy)
        {
            var p = new SystemPlacement { Id = systems[k].id, Name = systems[k].name, MinTier = int.MaxValue, MaxTier = int.MinValue };
            double sum = 0.0;
            foreach (var so in members)
            {
                sum += so.tier;
                p.MinTier = Math.Min(p.MinTier, so.tier);
                p.MaxTier = Math.Max(p.MaxTier, so.tier);
            }
            p.AverageTier = sum / members.Count;

            if (k == homeSystem)
            {
                p.X = cfg.HomeX;
                p.Y = cfg.HomeY;
            }
            else
            {
                // direction of the original map, distance from the inverse of the tier formula in the middle of the target tier's band
                double vx = systems[k].center.x - systems[homeSystem].center.x, vy = systems[k].center.y - systems[homeSystem].center.y;
                double len = Math.Sqrt(vx * vx + vy * vy);
                if (len < 1e-9) { vx = 1.0; vy = 0.0; len = 1.0; }
                vx /= len;
                vy /= len;
                double target = Math.Floor(p.AverageTier + 0.5) + 0.5;
                double d = cfg.TierDistanceUnit * (DetMath.Pow(2.0, target / cfg.TierScale) - 1.0);
                double limited = KeepInside(galaxy, cfg.HomeX, cfg.HomeY, vx, vy, d);
                p.PulledInsideGalaxy = limited < d;
                d = limited;
                p.X = Quantize(cfg.HomeX + vx * d);
                p.Y = Quantize(cfg.HomeY + vy * d);
            }
            if (galaxy != null && galaxy.Contains(p.X, p.Y)) p.GalaxyId = galaxy.Id;
            double dx = p.X - cfg.HomeX, dy = p.Y - cfg.HomeY;
            p.DistanceFromHomeLy = Math.Sqrt(dx * dx + dy * dy);
            p.TierAtPosition = cfg.TierFromDistance(p.DistanceFromHomeLy);
            return p;
        }

        // Largest distance <= d along the ray at which the point is inside GalaxyMargin of the galaxy's elliptical radius.
        static double KeepInside(GalaxyInfo galaxy, double hx, double hy, double dirX, double dirY, double d)
        {
            if (galaxy == null || galaxy.NormalizedRadius(hx + dirX * d, hy + dirY * d) <= GalaxyMargin) return d;
            double lo = 0.0, hi = d;
            for (int i = 0; i < 48; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (galaxy.NormalizedRadius(hx + dirX * mid, hy + dirY * mid) <= GalaxyMargin) lo = mid; else hi = mid;
            }
            return Math.Floor(lo * 100.0) / 100.0;
        }

        static double Quantize(double v) { return Math.Round(v * 100.0) / 100.0; }

        // ---- orbits ----------------------------------------------------------------------------------------------------

        struct Orbit { public double Radius, Phase; }

        // Radius and phase from the old map layout: planets that had a position of their own keep their angle and (ordered) radius around the
        // system centre; planets that never had one (the expansion worlds sit at the asset default) get the next free orbits.
        static Orbit[] Orbits(PlanetSO.StarSystem system, List<PlanetSO> members)
        {
            var result = new Orbit[members.Count];
            var placed = new bool[members.Count];
            double maxPlaced = InnermostMapRadius - UnplacedOrbitStep;
            for (int i = 0; i < members.Count; i++)
            {
                double dx = members[i].mapPosition.x - system.center.x, dy = members[i].mapPosition.y - system.center.y;
                double r = Math.Sqrt(dx * dx + dy * dy);
                placed[i] = r > 1e-9 && r <= MaxOwnMapRadius;
                if (!placed[i]) continue;
                result[i].Radius = AuFromMapRadius(r);
                double phase = DetMath.Atan2(dy, dx);
                if (phase < 0.0) phase += DetMath.TwoPi;
                result[i].Phase = phase;
                maxPlaced = Math.Max(maxPlaced, r);
            }
            int extra = 0;
            for (int i = 0; i < members.Count; i++)
            {
                if (placed[i]) continue;
                extra++;
                result[i].Radius = AuFromMapRadius(maxPlaced + UnplacedOrbitStep * extra);
                double phase = (i * 2.39996323 + 7.0 * system.center.x) % DetMath.TwoPi;
                result[i].Phase = phase < 0.0 ? phase + DetMath.TwoPi : phase;
            }
            return result;
        }

        static double AuFromMapRadius(double mapRadius)
        {
            return InnerOrbitAu + Math.Max(0.0, mapRadius - InnermostMapRadius) * AuPerMapUnit;
        }

        // ---- descriptors -----------------------------------------------------------------------------------------------

        static SystemSpec MakeSystem(PlanetSO.StarSystem system, SystemPlacement placement, string designation)
        {
            StarClass cls = StarClassFromColor(system.color);
            double lum = Luminosity(cls);
            double s = Math.Sqrt(lum);
            return new SystemSpec
            {
                Key = placement.Key,
                Name = system.name,
                Designation = designation,
                X = placement.X,
                Y = placement.Y,
                StarClass = cls,
                Color = new Rgb(system.color.r, system.color.g, system.color.b),
                Luminosity = (float)lum,
                SurfaceTemperature = (float)StarTemperature(cls),
                HabitableInner = (float)(0.95 * s),
                HabitableOuter = (float)(1.5 * s),
                GalaxyId = placement.GalaxyId,
                Tier = SystemSpec.AutoTier
            };
        }

        /// <summary>Star class from the system's map colour: the warmer (redder) the colour, the cooler the star.</summary>
        public static StarClass StarClassFromColor(Color c)
        {
            double warmth = (double)c.r - c.b;
            if (warmth >= 0.70) return StarClass.M;
            if (warmth >= 0.50) return StarClass.K;
            if (warmth >= 0.22) return StarClass.G;
            if (warmth >= -0.15) return StarClass.F;
            if (warmth >= -0.33) return StarClass.A;
            if (warmth >= -0.45) return StarClass.B;
            return StarClass.O;
        }

        static double Luminosity(StarClass c)
        {
            switch (c)
            {
                case StarClass.O: return 100000.0;
                case StarClass.B: return 2000.0;
                case StarClass.A: return 20.0;
                case StarClass.F: return 3.0;
                case StarClass.G: return 1.0;
                case StarClass.K: return 0.35;
                default: return 0.05;
            }
        }

        static double StarTemperature(StarClass c)
        {
            switch (c)
            {
                case StarClass.O: return 40000.0;
                case StarClass.B: return 20000.0;
                case StarClass.A: return 8750.0;
                case StarClass.F: return 6750.0;
                case StarClass.G: return 5600.0;
                case StarClass.K: return 4550.0;
                default: return 3150.0;
            }
        }

        static Biome BiomeOf(PlanetSO so)
        {
            foreach (string entry in BiomeTable)
            {
                int colon = entry.IndexOf(':');
                if (string.Equals(entry.Substring(0, colon), so.id, StringComparison.Ordinal))
                    return (Biome)Enum.Parse(typeof(Biome), entry.Substring(colon + 1));
            }
            // a new world nobody classified yet: guess from its weather
            switch (so.weather)
            {
                case WeatherKind.Snow: return Biome.Glacial;
                case WeatherKind.Embers: return Biome.Volcanic;
                case WeatherKind.Spores: return Biome.Fungal;
                case WeatherKind.Rain: return Biome.Oceanic;
                case WeatherKind.Dust: return Biome.Desert;
                case WeatherKind.Storm: return Biome.Obsidian;
                default: return Biome.Lush;
            }
        }

        static SizeClass SizeOf(float worldSize, UniverseConfig cfg)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < cfg.WorldSizeBySize.Length; i++)
            {
                float distance = Mathf.Abs(cfg.WorldSizeBySize[i] - worldSize);
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }
            return (SizeClass)best;
        }

        static double Temperature(Biome b)
        {
            switch (b)
            {
                case Biome.Lush: return 285.0;
                case Biome.Mineral: return 300.0;
                case Biome.Glacial: return 180.0;
                case Biome.Volcanic: return 620.0;
                case Biome.Fungal: return 315.0;
                case Biome.Crystal: return 230.0;
                case Biome.Obsidian: return 300.0;
                case Biome.Shattered: return 200.0;
                case Biome.Desert: return 370.0;
                case Biome.Oceanic: return 290.0;
                case Biome.GasGiant: return 120.0;
                default: return 250.0;
            }
        }

        static Hazards HazardsOf(Biome b, int tier)
        {
            Hazards h = Hazards.None;
            switch (b)
            {
                case Biome.Volcanic:
                case Biome.Desert: h |= Hazards.Heat; break;
                case Biome.Glacial: h |= Hazards.Cold; break;
                case Biome.Fungal:
                case Biome.GasGiant: h |= Hazards.Toxic; break;
                case Biome.Obsidian:
                case Biome.Shattered: h |= Hazards.Radiation; break;
                case Biome.Crystal: if (tier >= 6) h |= Hazards.Radiation; break;
            }
            if (b == Biome.Shattered && tier >= 6) h |= Hazards.Void;
            return h;
        }

        static PlanetDescriptor MakePlanet(PlanetSO so, PlanetKey key, string systemDesignation, Orbit orbit, UniverseConfig cfg)
        {
            Biome biome = BiomeOf(so);
            var deposits = new List<Deposit>();
            foreach (var spot in so.spots)
            {
                if (spot == null || spot.resource == null) continue;
                deposits.Add(new Deposit(spot.resource.id, HarvestMapping.FromMinigame(spot.game), 1f, Math.Max(1, spot.baseYield), Mathf.Clamp01(spot.difficulty)));
            }
            var spec = new PlanetSpec
            {
                Key = key,
                AuthoredId = so.id,
                Name = so.displayName,
                Designation = systemDesignation + " " + (char)('b' + key.Index),
                Kind = biome == Biome.GasGiant ? PlanetKind.GasGiant : PlanetKind.Rocky,
                Biome = biome,
                Size = SizeOf(so.worldSize, cfg),
                WorldSize = so.worldSize,
                BuildingSlots = so.BuildingSlots,
                Tier = so.tier,
                Danger = so.danger,
                Hazards = HazardsOf(biome, so.tier),
                Weather = (Weather)(int)so.weather,
                OrbitRadius = (float)orbit.Radius,
                OrbitPhase = (float)orbit.Phase,
                Temperature = (float)Temperature(biome),
                HasRings = so.hasRings,
                MoonCount = so.tier / 2,
                Primary = new Rgb(so.primary.r, so.primary.g, so.primary.b),
                Secondary = new Rgb(so.secondary.r, so.secondary.g, so.secondary.b),
                Rim = new Rgb(so.rim.r, so.rim.g, so.rim.b),
                Style = so.style,
                SurfaceBands = so.surfaceBands,
                GlowIntensity = so.glowIntensity,
                AtmosphereIntensity = so.atmosphereIntensity,
                ModelKey = so.ModelKey,
                Deposits = deposits.ToArray(),
                GeneratorVersion = UniverseConfig.CurrentGeneratorVersion
            };
            return new PlanetDescriptor(spec);
        }
    }
}
