using System;
using System.Collections.Generic;
using Frontier.Economy;
using Frontier.Universe;

namespace Frontier.Economy.Tests
{
    /// <summary>Console reporting used by the tests (printed by the runner; shows up in the Unity log under the Test Runner).</summary>
    public static class TestLog
    {
        public static void Info(string message)
        {
            Console.WriteLine("    | " + message);
        }
    }

    /// <summary>Shared fixtures for the tests.</summary>
    public static class Fx
    {
        public static Tuning NewTuning() { return Tuning.CreateDefault(); }

        public static EconomyCatalog NewCatalog() { return CatalogBuilder.Build(Tuning.CreateDefault()); }
    }

    /// <summary>Simulator fixtures: one world and one catalog shared by all simulation tests, with the finished runs cached by their parameters.</summary>
    public static class SimFx
    {
        static EconomyCatalog _catalog;
        static Tuning _tuning;
        static SimWorld _world;
        static readonly Dictionary<string, SimResult> Runs = new Dictionary<string, SimResult>();

        public static Tuning Tuning { get { if (_tuning == null) _tuning = Tuning.CreateDefault(); return _tuning; } }

        public static EconomyCatalog Catalog { get { if (_catalog == null) _catalog = CatalogBuilder.Build(Tuning); return _catalog; } }

        public static SimWorld World
        {
            get
            {
                if (_world == null) _world = SimWorld.Build(Catalog, StarterSystem.CreateUniverse(Catalog, 1UL), 3600.0);
                return _world;
            }
        }

        public static Archetype Archetype(string name)
        {
            foreach (Archetype a in Frontier.Economy.Archetype.All()) if (a.name == name) return a;
            throw new ArgumentException("unknown archetype " + name);
        }

        /// <summary>Runs (or returns the cached run of) an archetype with the default tuning.</summary>
        public static SimResult Run(string archetype, ulong seed, double days)
        {
            string key = archetype + "/" + seed + "/" + days;
            SimResult r;
            if (Runs.TryGetValue(key, out r)) return r;
            r = SimEngine.Run(Catalog, Tuning, World, Archetype(archetype), new SimSettings { seed = seed, horizonDays = days });
            Runs[key] = r;
            return r;
        }
    }
}
