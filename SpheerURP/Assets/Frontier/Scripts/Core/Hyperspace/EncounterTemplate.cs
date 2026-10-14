using System.Collections.Generic;

namespace Frontier.Hyperspace
{
    /// <summary>Intensity class a template is meant for; the director matches it to the current pacing segment.</summary>
    public enum EncounterRole : byte
    {
        Calm = 0,
        Build = 1,
        Peak = 2,
        Boss = 3
    }

    /// <summary>Variety bucket: the director never serves three templates of one family in a row.</summary>
    public enum EncounterFamily : byte
    {
        Drones = 0,
        Asteroids = 1,
        Gates = 2,
        Turrets = 3,
        Mines = 4,
        Seekers = 5,
        Elites = 6,
        Swoopers = 7,
        Reward = 8,
        Boss = 9,
        Mixed = 10
    }

    /// <summary>Flags of <see cref="SpawnEntry.flags"/> that only matter to the template instantiator.</summary>
    public static class EntryFlags
    {
        /// <summary>The gap of this gate shrinks with tier (see EncounterInstantiator.GapScale).</summary>
        public const int ScaleGap = 1 << 20;
        /// <summary>The entry is an optional reward (pickup) and is not part of the threat set.</summary>
        public const int Reward = 1 << 21;
    }

    /// <summary>
    /// One thing a template spawns. x/y are the lateral/vertical start offsets in play-area units (x right, y up). zOffset is the distance (at nominalSpeed)
    /// between this entry's arrival at the ship plane and the arrival of the first entry of the template; it is converted to seconds, so timing never depends on route speed.
    /// vzOwn is the extra approach speed of enemies that fly toward the ship. Path parameters are documented on <see cref="PathType"/> and <see cref="Hazards"/>.
    /// </summary>
    public struct SpawnEntry
    {
        public EntityType type;
        public PickupType pickup;
        public byte variant;
        public int flags;
        public float x, y;
        public float zOffset;
        public float vzOwn;
        public PathType path;
        public float p0, p1, p2, p3, p4, p5;
        /// <summary>Entries with the same non-zero group share a lock window (aimed shots fired by several turrets at the same moment).</summary>
        public short group;
    }

    /// <summary>Authored formation: what to spawn, where, in which tiers, and what it costs in director budget points.</summary>
    public sealed class EncounterTemplate
    {
        public string id;
        public int index;
        public int minTier;
        public int maxTier;
        /// <summary>Director budget points; the sum of chosen template costs never exceeds the route budget.</summary>
        public float cost;
        public EncounterRole role;
        public EncounterFamily family;
        public SpawnEntry[] entries;
        /// <summary>Extent of the template in z (world units at nominal speed) from its first to its last arrival.</summary>
        public float durationZ;
        /// <summary>Author's claim: the lateral speed (as a fraction of the fair speed, 0..1) a player needs. The solver measures the real minimum and the tests require measured &lt;= declared.</summary>
        public float declaredSpeed = 1f;
        public float weight = 1f;
        /// <summary>The instantiator may flip the template left/right.</summary>
        public bool mirrorable = true;
        public string notes;

        public bool AllowedAt(int tier) { return tier >= minTier && tier <= maxTier; }

        /// <summary>Seconds before the anchor at which the template's first player-dependent activity (a turret lock) begins; the director keeps that window clear of other templates.</summary>
        public float PreSeconds { get { return PreSecondsAt(1f); } }

        /// <summary>PreSeconds when the template's z offsets are compressed in time by timeScale (higher tiers play the same formation faster).</summary>
        public float PreSecondsAt(float timeScale)
        {
            double pre = 0.0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].type != EntityType.TurretPod) continue;
                double lockAt = (double)entries[i].zOffset * timeScale / 30.0 - entries[i].p0;
                if (-lockAt > pre) pre = -lockAt;
            }
            return (float)pre;
        }

        /// <summary>Seconds from the first to the last arrival at the authoring speed of 30 u/s.</summary>
        public float SpanSeconds { get { return (float)((double)durationZ / 30.0); } }

        public float SpanSecondsAt(float timeScale) { return (float)((double)durationZ * timeScale / 30.0); }
    }

    /// <summary>Fluent helper that keeps the template library readable. Entries are added in any order; <see cref="Done"/> normalises z offsets so the earliest arrival is 0.</summary>
    public sealed class TemplateBuilder
    {
        readonly EncounterTemplate t = new EncounterTemplate();
        readonly List<SpawnEntry> list = new List<SpawnEntry>();

        public TemplateBuilder(string id, int minTier, int maxTier, float cost, EncounterRole role, EncounterFamily family, float declaredSpeed = 0.7f, float weight = 1f, bool mirrorable = true)
        {
            t.id = id;
            t.minTier = minTier;
            t.maxTier = maxTier;
            t.cost = cost;
            t.role = role;
            t.family = family;
            t.declaredSpeed = declaredSpeed;
            t.weight = weight;
            t.mirrorable = mirrorable;
        }

        public TemplateBuilder Notes(string text) { t.notes = text; return this; }

        public TemplateBuilder Add(SpawnEntry e) { list.Add(e); return this; }

        public TemplateBuilder Drone(float x, float y, float z, float vz = 6f, short group = 0)
        {
            return Add(new SpawnEntry { type = EntityType.Drone, x = x, y = y, zOffset = z, vzOwn = vz, path = PathType.Static, group = group });
        }

        public TemplateBuilder DroneDrift(float x, float y, float z, float vx, float vy, float vz = 6f)
        {
            return Add(new SpawnEntry { type = EntityType.Drone, x = x, y = y, zOffset = z, vzOwn = vz, path = PathType.Line, p0 = vx, p1 = vy });
        }

        public TemplateBuilder Zig(float x, float y, float z, float amp, float omega, float phase, float vz = 5f, float ampY = 0f, float phaseY = 0f)
        {
            return Add(new SpawnEntry { type = EntityType.Zigzag, x = x, y = y, zOffset = z, vzOwn = vz, path = PathType.Sine, p0 = amp, p1 = omega, p2 = phase, p3 = ampY, p4 = phaseY });
        }

        /// <summary>Swooper: starts at (x, y), bends through (cx, cy) and ends at (ex, ey) after dur seconds, then flies straight on.</summary>
        public TemplateBuilder Swoop(float x, float y, float z, float cx, float cy, float ex, float ey, float dur, float vz = 8f)
        {
            return Add(new SpawnEntry { type = EntityType.Swooper, x = x, y = y, zOffset = z, vzOwn = vz, path = PathType.Curve, p0 = cx, p1 = cy, p2 = ex, p3 = ey, p4 = dur });
        }

        public TemplateBuilder Asteroid(AsteroidSize size, float x, float y, float z, float vx = 0f, float vy = 0f)
        {
            return Add(new SpawnEntry { type = EntityType.Asteroid, variant = (byte)size, x = x, y = y, zOffset = z, path = (vx != 0f || vy != 0f) ? PathType.Line : PathType.Static, p0 = vx, p1 = vy });
        }

        public TemplateBuilder Mine(float x, float y, float z)
        {
            return Add(new SpawnEntry { type = EntityType.Mine, x = x, y = y, zOffset = z, path = PathType.Static });
        }

        /// <summary>Laser gate. half = half size of the opening; amp = slide amplitude; omega/phase drive the slide; ampY only for rings. Bars: half = half length, amp = half thickness.</summary>
        public TemplateBuilder Gate(GateKind kind, float x, float y, float z, float half, float amp = 0f, float omega = 0f, float phase = 0f, float ampY = 0f, float angle = 0f, bool scaleGap = true)
        {
            return Add(new SpawnEntry
            {
                type = EntityType.LaserGate, variant = (byte)kind, x = x, y = y, zOffset = z, path = PathType.Static,
                p0 = angle, p1 = half, p2 = amp, p3 = omega, p4 = phase, p5 = ampY,
                flags = scaleGap && kind != GateKind.Bar ? EntryFlags.ScaleGap : 0
            });
        }

        public TemplateBuilder Turret(TurretMode mode, float x, float y, float z, float offsetSeconds = 0f, short group = 0)
        {
            return Add(new SpawnEntry { type = EntityType.TurretPod, variant = (byte)mode, x = x, y = y, zOffset = z, path = PathType.Static, p0 = offsetSeconds, group = group });
        }

        public TemplateBuilder Seeker(float x, float y, float z, float vz = 0f)
        {
            return Add(new SpawnEntry { type = EntityType.Seeker, x = x, y = y, zOffset = z, vzOwn = vz, path = PathType.Homing });
        }

        public TemplateBuilder Elite(float x, float y, float z, float vz = 3f, float vx = 0f)
        {
            return Add(new SpawnEntry { type = EntityType.Elite, x = x, y = y, zOffset = z, vzOwn = vz, path = vx != 0f ? PathType.Line : PathType.Static, p0 = vx });
        }

        public TemplateBuilder Reward(PickupType type, float x, float y, float z)
        {
            return Add(new SpawnEntry { type = EntityType.None, pickup = type, x = x, y = y, zOffset = z, path = PathType.Static, flags = EntryFlags.Reward });
        }

        public TemplateBuilder Guardian(float x, float y, float z)
        {
            return Add(new SpawnEntry { type = EntityType.Guardian, x = x, y = y, zOffset = z, path = PathType.Hold });
        }

        public EncounterTemplate Done()
        {
            double minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (SpawnEntry e in list)
            {
                if (e.zOffset < minZ) minZ = e.zOffset;
                if (e.zOffset > maxZ) maxZ = e.zOffset;
            }
            if (list.Count == 0) { minZ = 0.0; maxZ = 0.0; }
            var arr = new SpawnEntry[list.Count];
            for (int i = 0; i < arr.Length; i++)
            {
                SpawnEntry e = list[i];
                e.zOffset = (float)(e.zOffset - minZ);
                arr[i] = e;
            }
            // Stable insertion sort: Array.Sort is not stable and may order ties differently between runtimes.
            for (int i = 1; i < arr.Length; i++)
            {
                SpawnEntry key = arr[i];
                int j = i - 1;
                while (j >= 0 && arr[j].zOffset > key.zOffset)
                {
                    arr[j + 1] = arr[j];
                    j--;
                }
                arr[j + 1] = key;
            }
            t.entries = arr;
            t.durationZ = (float)(maxZ - minZ);
            return t;
        }
    }
}
