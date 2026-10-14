using System;
using System.Collections.Generic;

namespace Frontier.Universe.Tests
{
    /// <summary>Console reporting used by the tests (printed by the runner; shows up in the Unity log under the Test Runner).</summary>
    public static class TestLog
    {
        public static void Info(string message)
        {
            Console.WriteLine("    | " + message);
        }
    }

    /// <summary>Shared universes and fixtures for the tests.</summary>
    public static class TestUniverses
    {
        /// <summary>Plain generator, no authored content.</summary>
        public static Universe Plain()
        {
            return new Universe(new UniverseConfig());
        }

        /// <summary>The default landmark galaxies (Home galaxy at the origin) and no systems.</summary>
        public static Universe WithLandmarks()
        {
            return new Universe(new UniverseConfig(), Landmarks.CreateDefault());
        }

        /// <summary>Landmarks plus six fake authored Core World systems around the origin.</summary>
        public static Universe WithFakeAuthored()
        {
            return new Universe(new UniverseConfig(), FakeAuthored.Create());
        }
    }

    /// <summary>Six fake authored systems (two with several planets) standing in for the game's Core Worlds.</summary>
    public static class FakeAuthored
    {
        /// <summary>System positions in light-years. Sylva is the home system at the origin; one system sits right on a sector border.</summary>
        public static readonly double[][] Positions =
        {
            new[] { 0.0, 0.0 },
            new[] { 143.5, -57.25 },
            new[] { -210.0, 330.75 },
            new[] { 63.99, 64.0 },
            new[] { 400.0, -121.5 },
            new[] { -92.0, -401.0 }
        };

        static readonly string[] Names = { "Sylva", "Emberfall", "Verdance", "Umbra", "Frostline", "The Fracture" };
        static readonly string[] Aliases = { "sylva", "ember", "verdance", "umbra", "frost", "edge" };

        public static AuthoredContent Create(UniverseConfig cfg = null)
        {
            if (cfg == null) cfg = new UniverseConfig();
            var content = Landmarks.CreateDefault();
            for (int i = 0; i < Positions.Length; i++)
            {
                double x = Positions[i][0], y = Positions[i][1];
                var key = new SystemKey(SectorCoord.FromLy(x, y, cfg.SectorSize), SystemKey.AuthoredBase + i);
                int planetCount = i == 0 ? 5 : 3 + (i % 3);
                var planets = new PlanetDescriptor[planetCount];
                for (int p = 0; p < planetCount; p++)
                {
                    var spec = new PlanetSpec();
                    spec.Key = new PlanetKey(key, p);
                    spec.AuthoredId = p == 0 ? Aliases[i] : Aliases[i] + "-" + p;
                    spec.Name = p == 0 ? Names[i] : Names[i] + " " + (p + 1);
                    spec.Designation = Aliases[i].ToUpperInvariant() + "-" + p;
                    spec.Kind = PlanetKind.Rocky;
                    spec.Biome = (Biome)((i + p) % 8);
                    spec.Size = SizeClass.Medium;
                    spec.WorldSize = 1.4f;
                    spec.BuildingSlots = 24;
                    spec.Tier = i;
                    spec.Danger = 0.6f + 1.1f * i;
                    spec.Weather = Weather.Clear;
                    spec.OrbitRadius = 0.5f + p;
                    spec.Primary = new Rgb(0.5f, 0.6f, 0.4f);
                    spec.Secondary = new Rgb(0.1f, 0.1f, 0.1f);
                    spec.Rim = new Rgb(1f, 1f, 1f);
                    spec.ModelKey = "sylva";
                    spec.Deposits = new[] { new Deposit("wood", HarvestMethod.Chop, 1f, 8, 0.25f), new Deposit("stone", HarvestMethod.Mine, 1f, 4, 0.3f) };
                    spec.GeneratorVersion = 1;
                    planets[p] = new PlanetDescriptor(spec);
                }
                var sys = new SystemSpec();
                sys.Key = key;
                sys.Name = Names[i];
                sys.Designation = "AUTH-" + i;
                sys.X = x;
                sys.Y = y;
                sys.StarClass = StarClass.G;
                sys.Color = new Rgb(1f, 0.95f, 0.7f);
                sys.Luminosity = 1f;
                sys.SurfaceTemperature = 5700f;
                sys.HabitableInner = 0.95f;
                sys.HabitableOuter = 1.5f;
                sys.GalaxyId = SystemSpec.AutoGalaxy;
                sys.Tier = SystemSpec.AutoTier;
                content.AddSystem(sys, planets);
            }
            return content;
        }
    }
}
