using System;
using System.Collections.Generic;
using Frontier.Universe;
using FUniverse = Frontier.Universe.Universe;

namespace Frontier.Economy
{
    /// <summary>An average player: how often and how long they play, how well they hit the mini-games, how much of the time is real play.</summary>
    public sealed class Archetype
    {
        public string name;
        public int sessionsPerDay;
        public double minutesPerSession;
        /// <summary>Mean mini-game quality (0..1) feeding the 0.4 + 1.1 x quality yield curve.</summary>
        public double quality;
        public double qualitySd;
        /// <summary>Share of session time spent on actual actions (the rest is menus, reading, walking around).</summary>
        public double efficiency;
        /// <summary>Local start hour of each session (the first session starts at day 0 + hour).</summary>
        public double[] sessionStartHours;

        public double DailyMinutes { get { return sessionsPerDay * minutesPerSession; } }

        public static Archetype Casual() { return new Archetype { name = "casual", sessionsPerDay = 3, minutesPerSession = 5, quality = 0.62, qualitySd = 0.18, efficiency = 0.70, sessionStartHours = new[] { 8.0, 13.0, 20.5 } }; }

        public static Archetype Regular() { return new Archetype { name = "regular", sessionsPerDay = 3, minutesPerSession = 15, quality = 0.74, qualitySd = 0.14, efficiency = 0.78, sessionStartHours = new[] { 7.5, 12.5, 20.5 } }; }

        public static Archetype Hardcore() { return new Archetype { name = "hardcore", sessionsPerDay = 5, minutesPerSession = 30, quality = 0.86, qualitySd = 0.10, efficiency = 0.88, sessionStartHours = new[] { 7.0, 10.5, 14.5, 18.5, 22.0 } }; }

        public static Archetype[] All() { return new[] { Casual(), Regular(), Hardcore() }; }
    }

    /// <summary>One catalogued world of the simulation (a planet of the real universe generator, plus the authored starter system).</summary>
    public sealed class SimPlanet
    {
        public int index;
        public string id, name;
        public int system;
        public double x, y, distHome;
        public int tier;
        public Hazards hazards;
        public double danger, orbit;
        public int slots;
        public PlanetKind kind;
        public Biome biome;
        public int[] depRes = new int[0];
        public HarvestMethod[] depMethod = new HarvestMethod[0];
        public float[] depRichness = new float[0];
        public int[] depBaseYield = new int[0];

        public int DepositOf(int resourceIndex)
        {
            for (int i = 0; i < depRes.Length; i++) if (depRes[i] == resourceIndex) return i;
            return -1;
        }
    }

    /// <summary>The worlds the simulated player may visit, precomputed once from the real generator.</summary>
    public sealed class SimWorld
    {
        public SimPlanet[] planets;
        public int home;
        public int systemCount;
        public double[] sysX, sysY;

        /// <summary>Collects every planet within <paramref name="radiusLy"/> of home that carries at least one deposit.</summary>
        public static SimWorld Build(EconomyCatalog cat, FUniverse u, double radiusLy)
        {
            var list = new List<SimPlanet>();
            var sx = new List<double>();
            var sy = new List<double>();
            int span = (int)Math.Ceiling(radiusLy / u.Config.SectorSize) + 1;
            for (int gy = -span; gy <= span; gy++)
                for (int gx = -span; gx <= span; gx++)
                    foreach (StarSystem s in u.SystemsInSector(new SectorCoord(gx, gy)))
                    {
                        double d = Math.Sqrt(s.X * s.X + s.Y * s.Y);
                        if (d > radiusLy) continue;
                        int si = sx.Count;
                        sx.Add(s.X);
                        sy.Add(s.Y);
                        foreach (PlanetDescriptor p in u.PlanetsOf(s))
                        {
                            var sp = new SimPlanet();
                            sp.index = list.Count;
                            sp.id = p.Id;
                            sp.name = p.AuthoredId != null ? p.AuthoredId : p.Name;
                            sp.system = si;
                            sp.x = s.X;
                            sp.y = s.Y;
                            sp.distHome = d;
                            sp.tier = p.Tier;
                            sp.hazards = p.Hazards;
                            sp.danger = p.Danger;
                            sp.orbit = p.OrbitRadius;
                            sp.slots = p.BuildingSlots;
                            sp.kind = p.Kind;
                            sp.biome = p.Biome;
                            int n = p.Deposits.Length;
                            sp.depRes = new int[n];
                            sp.depMethod = new HarvestMethod[n];
                            sp.depRichness = new float[n];
                            sp.depBaseYield = new int[n];
                            for (int i = 0; i < n; i++)
                            {
                                sp.depRes[i] = cat.ResourceIndex(p.Deposits[i].ResourceId);
                                sp.depMethod[i] = p.Deposits[i].Method;
                                sp.depRichness[i] = p.Deposits[i].Richness;
                                sp.depBaseYield[i] = p.Deposits[i].BaseYield;
                            }
                            if (p.AuthoredId == "sylva") { }
                            list.Add(sp);
                        }
                    }
            var w = new SimWorld();
            w.planets = list.ToArray();
            w.systemCount = sx.Count;
            w.sysX = sx.ToArray();
            w.sysY = sy.ToArray();
            w.home = -1;
            for (int i = 0; i < w.planets.Length; i++) if (w.planets[i].name == StarterSystem.HomePlanetId) { w.home = i; break; }
            if (w.home < 0) throw new InvalidOperationException("the starter system is missing from the simulated world");
            return w;
        }
    }

    public sealed class SimSample
    {
        public double days;
        public double playHours;
        public double harvestedValue;
        public long score;
        /// <summary>The six score terms before the ascension multiplier: reach, worlds, industry, tech, defense, atlas.</summary>
        public double[] scoreParts = new double[6];
        public int reachTier;
        public int toolSteps;
        public int upgradeLevels;
        public int builders;
        public int structureLevels;
        public int era;
        public double stockValue;
        public double[] categoryValue = new double[5];
        public double maxDistance;
        public int megaStages;
        /// <summary>Player level (XP based), resources discovered, recipes crafted, distinct structure kinds built, started build jobs: what the shard model reads.</summary>
        public int level, discovered, crafted, structureKinds, buildJobs;
    }

    public sealed class SimGoalTime
    {
        public int index;
        public string text;
        public double seconds, playSeconds;
    }

    /// <summary>The recorded timeline of one simulated player.</summary>
    public sealed class SimResult
    {
        public readonly List<SimGoalTime> goals = new List<SimGoalTime>();
        /// <summary>Active play seconds of every visible-progress event between goal completions: a megaproject installment (the stage bar moves) or a first landing on a new world (new deposits, bigger Worlds and Reach score).</summary>
        public readonly List<double> progressPlay = new List<double>();
        /// <summary>Seconds of every ordinary build/upgrade job the player started (the shard model prices skips from these).</summary>
        public readonly List<int> jobSeconds = new List<int>();
        /// <summary>Seconds of every megaproject stage timer.</summary>
        public readonly List<int> megaSeconds = new List<int>();
        public string archetype;
        public ulong seed;
        public double horizonDays;
        public readonly Dictionary<string, double> milestones = new Dictionary<string, double>();
        public readonly List<string> milestoneOrder = new List<string>();
        public readonly List<SimSample> samples = new List<SimSample>();
        public readonly List<string> log = new List<string>();
        public readonly Dictionary<string, double> stats = new Dictionary<string, double>();
        public readonly List<string> statOrder = new List<string>();

        public void Stat(string key, double add)
        {
            double v;
            if (!stats.TryGetValue(key, out v)) statOrder.Add(key);
            stats[key] = v + add;
        }
        public long harvestRuns, expeditions, waves, craftActions, lostItems, buildJobs, actions, stuck, stuckEndless;
        /// <summary>Seconds a player left a session early because nothing useful was left to do, by era at that moment.</summary>
        public readonly double[] idleByEra = new double[6];
        public double playSeconds, idleSeconds, builderBusySeconds, harvestedValue, discardedValue;
        public int endEra;
        public long finalScore;
        public string summary = "";

        public void Mark(string id, double seconds)
        {
            if (milestones.ContainsKey(id)) return;
            milestones[id] = seconds;
            milestoneOrder.Add(id);
        }

        public double Days(string id)
        {
            double s;
            return milestones.TryGetValue(id, out s) ? s / 86400.0 : double.NaN;
        }

        /// <summary>The first sample at or after the given hours of active play (the last sample when the run ends sooner).</summary>
        public SimSample SampleAtPlayHours(double hours)
        {
            for (int i = 0; i < samples.Count; i++) if (samples[i].playHours >= hours) return samples[i];
            return samples[samples.Count - 1];
        }

        /// <summary>The score after the given hours of active play (the first sample at or after it; the last sample when the run ends sooner).</summary>
        public long ScoreAtPlayHours(double hours)
        {
            for (int i = 0; i < samples.Count; i++) if (samples[i].playHours >= hours) return samples[i].score;
            return samples.Count == 0 ? 0 : samples[samples.Count - 1].score;
        }

        /// <summary>The score on the given day (the first sample at or after it; the last sample when the run ends sooner).</summary>
        public long ScoreAtDay(double day)
        {
            for (int i = 0; i < samples.Count; i++) if (samples[i].days >= day) return samples[i].score;
            return samples.Count == 0 ? 0 : samples[samples.Count - 1].score;
        }

        /// <summary>A hash of the whole timeline (determinism checks, cross-runtime comparison).</summary>
        public ulong Digest()
        {
            ulong h = 0xD16E57UL;
            for (int i = 0; i < milestoneOrder.Count; i++)
            {
                h = Hash.Combine(h, Hash.Fnv1a64(milestoneOrder[i]));
                h = Hash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(milestones[milestoneOrder[i]]));
            }
            for (int i = 0; i < samples.Count; i++)
            {
                h = Hash.Combine(h, (ulong)samples[i].score);
                h = Hash.Combine(h, (ulong)(samples[i].reachTier * 131 + samples[i].toolSteps * 17 + samples[i].upgradeLevels));
            }
            h = Hash.Combine(h, (ulong)harvestRuns);
            h = Hash.Combine(h, (ulong)expeditions);
            return h;
        }
    }
}
