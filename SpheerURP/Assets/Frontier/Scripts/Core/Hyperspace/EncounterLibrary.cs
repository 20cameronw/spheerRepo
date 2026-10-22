using System;
using System.Collections.Generic;

namespace Frontier.Hyperspace
{
    /// <summary>
    /// The authored encounter templates (formations + paths). Build one with <see cref="CreateDefault"/> and share it between runs: a library is immutable after construction.
    /// To add a template: write a builder method in one of the EncounterLibrary.*.cs files, register it in <see cref="Register"/>, run the fairness solver (see the integration guide).
    /// </summary>
    public sealed partial class EncounterLibrary
    {
        readonly List<EncounterTemplate> templates = new List<EncounterTemplate>();

        public int Count { get { return templates.Count; } }

        public EncounterTemplate this[int index] { get { return templates[index]; } }

        public static EncounterLibrary CreateDefault()
        {
            var lib = new EncounterLibrary();
            lib.BuildStarter();
            lib.BuildEarly();
            lib.BuildMid();
            lib.BuildHigh();
            lib.BuildIntense();
            lib.BuildBoss();
            lib.BuildComposites();
            return lib;
        }

        public static EncounterLibrary FromTemplates(IEnumerable<EncounterTemplate> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var lib = new EncounterLibrary();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in source)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.id) || !ids.Add(t.id))
                    throw new ArgumentException("Encounter IDs must be nonempty and unique.", nameof(source));
                if (t.minTier < 0 || t.maxTier > 12 || t.maxTier < t.minTier ||
                    !Positive(t.cost) || !Positive(t.weight) ||
                    !Finite(t.declaredSpeed) || t.declaredSpeed < 0f || t.declaredSpeed > 1f ||
                    !Enum.IsDefined(typeof(EncounterRole), t.role) || !Enum.IsDefined(typeof(EncounterFamily), t.family) ||
                    t.entries == null || t.entries.Length == 0 || !Finite(t.durationZ) || t.durationZ < 0f)
                    throw new ArgumentException("Invalid encounter metadata: " + t.id, nameof(source));
                float lastZ = 0f;
                foreach (var entry in t.entries)
                {
                    if (!Enum.IsDefined(typeof(EntityType), entry.type) || !Enum.IsDefined(typeof(PathType), entry.path) ||
                        !Enum.IsDefined(typeof(PickupType), entry.pickup) ||
                        !Finite(entry.x) || !Finite(entry.y) || !Finite(entry.zOffset) || entry.zOffset < lastZ ||
                        !Finite(entry.vzOwn) || !Finite(entry.p0) || !Finite(entry.p1) || !Finite(entry.p2) ||
                        !Finite(entry.p3) || !Finite(entry.p4) || !Finite(entry.p5))
                        throw new ArgumentException("Invalid or unsorted spawn entry: " + t.id, nameof(source));
                    lastZ = entry.zOffset;
                }
                if (t.entries[0].zOffset != 0f || lastZ != t.durationZ)
                    throw new ArgumentException("Encounter offsets must start at zero and span durationZ: " + t.id, nameof(source));
                lib.templates.Add(new EncounterTemplate
                {
                    id = t.id, index = lib.templates.Count, minTier = t.minTier, maxTier = t.maxTier,
                    cost = t.cost, role = t.role, family = t.family, entries = (SpawnEntry[])t.entries.Clone(),
                    durationZ = t.durationZ, declaredSpeed = t.declaredSpeed, weight = t.weight,
                    mirrorable = t.mirrorable, notes = t.notes
                });
            }
            if (lib.Count == 0) throw new ArgumentException("An encounter library cannot be empty.", nameof(source));
            return lib;
        }

        static bool Positive(float value) { return Finite(value) && value > 0f; }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        void Register(TemplateBuilder b)
        {
            EncounterTemplate t = b.Done();
            if (TierCap(t.id) < t.maxTier) t.maxTier = TierCap(t.id);
            t.index = templates.Count;
            templates.Add(t);
        }

        public EncounterTemplate ById(string id)
        {
            for (int i = 0; i < templates.Count; i++)
                if (string.Equals(templates[i].id, id, StringComparison.Ordinal)) return templates[i];
            return null;
        }

        /// <summary>Number of templates allowed at a tier (any role).</summary>
        public int CountAt(int tier)
        {
            int n = 0;
            for (int i = 0; i < templates.Count; i++) if (templates[i].AllowedAt(tier)) n++;
            return n;
        }

        // ---------------------------------------------------------------- tier 0+
        void BuildStarter()
        {
            var line = new TemplateBuilder("drone_line", 0, 12, 5f, EncounterRole.Calm, EncounterFamily.Drones, 0.3f, 1.2f).Notes("Horizontal row of drones; fly over or under it, or shoot gaps.");
            for (int i = 0; i < 5; i++) line.Drone((float)(-2.2 + 1.1 * (double)i), 0.3f, 0f);
            Register(line);

            var v = new TemplateBuilder("drone_v", 0, 12, 5f, EncounterRole.Calm, EncounterFamily.Drones, 0.3f, 1.2f).Notes("V formation, apex first.");
            v.Drone(0f, 0.2f, 0f);
            for (int k = 1; k <= 2; k++)
            {
                v.Drone((float)(-1.0 * (double)k), 0.2f, (float)(8.0 * (double)k));
                v.Drone((float)(1.0 * (double)k), 0.2f, (float)(8.0 * (double)k));
            }
            Register(v);

            var col = new TemplateBuilder("drone_column", 0, 6, 3f, EncounterRole.Calm, EncounterFamily.Drones, 0.2f).Notes("Four drones in one lane; a lesson in sidestepping and shooting.");
            for (int i = 0; i < 4; i++) col.Drone(0.0f, 0.4f, (float)(12.0 * (double)i));
            Register(col);

            var slalom = new TemplateBuilder("asteroid_slalom", 0, 12, 6f, EncounterRole.Build, EncounterFamily.Asteroids, 0.5f).Notes("Three large rocks staggered left, right, left.");
            slalom.Asteroid(AsteroidSize.Large, -0.9f, 0.4f, 0f).Asteroid(AsteroidSize.Large, 0.9f, -0.4f, 40f).Asteroid(AsteroidSize.Large, -0.9f, 0.4f, 80f);
            Register(slalom);

            var wide = new TemplateBuilder("gate_slit_wide", 0, 12, 4f, EncounterRole.Build, EncounterFamily.Gates, 0.4f).Notes("A single wall with a wide static slit: the first lesson about walls.");
            wide.Gate(GateKind.Slit, 1.5f, 0f, 0f, 1.3f);
            Register(wide);

            var trail = new TemplateBuilder("reward_trail", 0, 12, 0.5f, EncounterRole.Calm, EncounterFamily.Reward, 0.4f, 1.3f).Notes("S-shaped trail of fuel canisters ending in a shield: pure positioning reward.");
            trail.Reward(PickupType.Fuel, -1.6f, -2.2f, 0f).Reward(PickupType.Fuel, -1.0f, -1.2f, 14f).Reward(PickupType.Fuel, -0.3f, -0.2f, 28f)
                .Reward(PickupType.Fuel, 0.4f, 0.8f, 42f).Reward(PickupType.Fuel, 1.1f, 1.8f, 56f).Reward(PickupType.Fuel, 1.7f, 2.8f, 70f).Reward(PickupType.Shield, 0.9f, 2.4f, 96f);
            Register(trail);

            var cache = new TemplateBuilder("crate_cache", 0, 12, 2f, EncounterRole.Calm, EncounterFamily.Reward, 0.4f).Notes("Loot crate guarded by two drones.");
            cache.Reward(PickupType.LootCrate, 1.8f, 1.8f, 0f).Drone(-1.0f, 1.0f, 0f).Drone(-1.0f, -1.0f, 15f);
            Register(cache);
        }

        // ---------------------------------------------------------------- tier 1-2+
        void BuildEarly()
        {
            var wedge = new TemplateBuilder("drone_wedge", 1, 12, 7f, EncounterRole.Build, EncounterFamily.Drones, 0.35f).Notes("Filled arrowhead of drones.");
            wedge.Drone(0f, 1.0f, 0f);
            wedge.Drone(-0.6f, 1.0f, 9f).Drone(0.6f, 1.0f, 9f);
            wedge.Drone(-1.2f, 1.0f, 18f).Drone(0f, 1.0f, 18f).Drone(1.2f, 1.0f, 18f);
            Register(wedge);

            var zp = new TemplateBuilder("zigzag_pair", 1, 12, 4f, EncounterRole.Build, EncounterFamily.Drones, 0.4f).Notes("Two zigzagging drones in opposite phase.");
            zp.Zig(-1.4f, 1.2f, 0f, 1.4f, 2.2f, 0f).Zig(1.4f, 1.2f, 14f, 1.4f, 2.2f, HMath.Pi);
            Register(zp);

            var sp = new TemplateBuilder("swooper_pair", 1, 12, 5f, EncounterRole.Build, EncounterFamily.Swoopers, 0.45f).Notes("Two swoopers dive in from the upper corners and cross low.");
            sp.Swoop(-4.2f, 3.2f, 0f, -0.4f, 3.6f, 0.9f, -1.6f, 1.7f).Swoop(4.2f, 3.2f, 8f, 0.4f, 3.6f, -0.9f, -1.6f, 1.7f);
            Register(sp);

            var pincer = new TemplateBuilder("pincer", 1, 12, 6f, EncounterRole.Build, EncounterFamily.Drones, 0.4f).Notes("Two groups of drones close in from the sides at different heights.");
            for (int i = 0; i < 3; i++)
            {
                pincer.DroneDrift(-1.2f, 1.9f, (float)(8.0 * (double)i), 1.5f, 0f);
                pincer.DroneDrift(1.2f, -1.9f, (float)(8.0 * (double)i), -1.5f, 0f);
            }
            Register(pincer);

            var field = new TemplateBuilder("asteroid_field", 1, 12, 8f, EncounterRole.Build, EncounterFamily.Asteroids, 0.55f).Notes("Scattered medium and small rocks; weave through.");
            field.Asteroid(AsteroidSize.Medium, -2.0f, 2.0f, 0f).Asteroid(AsteroidSize.Small, -0.7f, -1.2f, 5f).Asteroid(AsteroidSize.Medium, 1.2f, 0.8f, 12f)
                .Asteroid(AsteroidSize.Small, 2.2f, -2.4f, 18f).Asteroid(AsteroidSize.Medium, -1.6f, -2.0f, 25f).Asteroid(AsteroidSize.Small, 0.2f, 2.6f, 30f)
                .Asteroid(AsteroidSize.Medium, 1.8f, 2.4f, 38f).Asteroid(AsteroidSize.Small, -0.4f, 0.3f, 46f).Asteroid(AsteroidSize.Medium, -2.2f, 0.0f, 55f)
                .Asteroid(AsteroidSize.Small, 1.0f, -0.4f, 62f);
            Register(field);

            var snake = new TemplateBuilder("zigzag_stream", 2, 12, 8f, EncounterRole.Build, EncounterFamily.Drones, 0.5f).Notes("Six zigzaggers tracing one snake.");
            for (int i = 0; i < 6; i++) snake.Zig(0f, 1.2f, (float)(12.0 * (double)i), 1.7f, 2.0f, (float)(0.8 * (double)i));
            Register(snake);

            var arc = new TemplateBuilder("swooper_arc", 2, 12, 9f, EncounterRole.Peak, EncounterFamily.Swoopers, 0.55f).Notes("Four swoopers peel in alternately from both sides.");
            for (int i = 0; i < 4; i++)
            {
                double s = (i % 2 == 0) ? -1f : 1f;
                arc.Swoop((float)(s * 4.2), 3.2f, (float)(10.0 * (double)i), (float)(s * 0.5), 3.6f, (float)(-s * 0.6), -1.4f, 1.6f);
            }
            Register(arc);

            var mines = new TemplateBuilder("minefield_scatter", 2, 12, 8f, EncounterRole.Build, EncounterFamily.Mines, 0.5f).Notes("Sparse mines with big visible blast rings.");
            mines.Mine(-1.8f, 1.5f, 0f).Mine(0.4f, 2.6f, 10f).Mine(1.9f, 0.4f, 20f).Mine(-0.8f, -0.6f, 32f).Mine(1.2f, -2.2f, 44f).Mine(-2.0f, -2.0f, 54f);
            Register(mines);

            var ts = new TemplateBuilder("turret_single", 2, 12, 5f, EncounterRole.Build, EncounterFamily.Turrets, 0.45f).Notes("One turret pod on the edge locks a ring on your position, then fires.");
            ts.Turret(TurretMode.Aimed, 2.0f, 1.4f, 0f, 1.7f);
            Register(ts);

            var slide = new TemplateBuilder("gate_slit_slide", 2, 12, 7f, EncounterRole.Build, EncounterFamily.Gates, 0.5f).Notes("Wall whose slit slides side to side.");
            slide.Gate(GateKind.Slit, 0f, 0f, 0f, 1.3f, 1.2f, 1.2f, 0.5f);
            Register(slide);

            var slot = new TemplateBuilder("gate_slot", 2, 12, 6f, EncounterRole.Build, EncounterFamily.Gates, 0.5f).Notes("Wall with a horizontal slot: move up or down.");
            slot.Gate(GateKind.Slot, 0f, 0.8f, 0f, 1.3f, 1.0f, 1.0f, 1.0f);
            Register(slot);

            var helix = new TemplateBuilder("drone_helix", 2, 12, 8f, EncounterRole.Build, EncounterFamily.Drones, 0.45f).Notes("Corkscrew of drones around a centre line.");
            for (int i = 0; i < 8; i++)
            {
                double a = 0.8 * (double)i;
                helix.Drone((float)(1.6f * HMath.Cos(a)), (float)(0.8f + 1.6f * HMath.Sin(a)), (float)(8.0 * (double)i));
            }
            Register(helix);
        }
    }
}
