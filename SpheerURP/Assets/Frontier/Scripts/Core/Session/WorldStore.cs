using System;
using System.Collections.Generic;

namespace Frontier
{
    // Plain serializable records shared by the runtime world store and the save file (schema v2). Public fields only, no
    // dictionaries and no DateTime: Unity's JsonUtility reads and writes exactly this shape. Timestamps are ISO 8601 text
    // (see UtcText), 64 bit ids are text. Adding a field with a sensible default needs no schema bump (missing fields keep it).

    /// <summary>One stack inside a warehouse slot. <see cref="resourceId"/> is null or empty when the slot is empty.</summary>
    [Serializable]
    public class StackRecord
    {
        public string resourceId;
        public int count;
    }

    /// <summary>The contents of one warehouse, identified by the slot of its structure on the planet.</summary>
    [Serializable]
    public class StorageRecord
    {
        public int structureSlot;
        public List<StackRecord> slots = new List<StackRecord>();
    }

    /// <summary>A placed structure with optional construction and upgrade jobs.</summary>
    [Serializable]
    public class StructureRecord
    {
        public string defId;
        public int slot;
        public int level = 1;
        /// <summary>Local timer end in UTC ISO 8601. Empty while no construction runs.</summary>
        public string buildEndUtc = "";
        /// <summary>Local timer end in UTC ISO 8601. Empty while no upgrade runs.</summary>
        public string upgradeEndUtc = "";
        /// <summary>Level to apply when upgradeEndUtc is reached; 0 while idle.</summary>
        public int pendingUpgradeLevel;
    }

    /// <summary>
    /// Everything the player changed on one planet. A planet is identified by its canonical universe id ("p:X:Y:I:P"). Planets the
    /// player never touched have no delta at all: the world itself is regenerated from the universe seed.
    /// </summary>
    [Serializable]
    public class PlanetDelta
    {
        public string planetId;
        public List<StructureRecord> structures = new List<StructureRecord>();
        public List<StorageRecord> storages = new List<StorageRecord>();
        public int wavesCleared;
        public int visits;
        public string firstVisitUtc = "";
        public string lastVisitUtc = "";
        public bool discovered;
        /// <summary>Reserved: indices of obstacles the player has cleared on this planet.</summary>
        public List<int> clearedObstacles = new List<int>();

        public bool HasContent
        {
            get { return structures.Count > 0 || storages.Count > 0 || wavesCleared > 0 || visits > 0 || discovered || clearedObstacles.Count > 0; }
        }
    }

    /// <summary>The raw numbers behind the future Frontier Score. Plain fields so the save can serialize it directly.</summary>
    [Serializable]
    public class PlayerStats
    {
        public double maxDistanceFromHomeLy;
        public int worldsLanded;
        public int totalWavesCleared;
        public double playSeconds;

        /// <summary>A landing on a world: the farthest distance only ever grows, <see cref="worldsLanded"/> counts distinct worlds.</summary>
        public void RecordLanding(double distanceFromHomeLy, bool firstVisit)
        {
            if (distanceFromHomeLy > maxDistanceFromHomeLy) maxDistanceFromHomeLy = distanceFromHomeLy;
            if (firstVisit) worldsLanded++;
        }

        public void RecordWaveCleared() { totalWavesCleared++; }

        public void AddPlaySeconds(double seconds)
        {
            if (seconds > 0.0) playSeconds += seconds;
        }

        public void Clear()
        {
            maxDistanceFromHomeLy = 0.0;
            worldsLanded = 0;
            totalWavesCleared = 0;
            playSeconds = 0.0;
        }

        public PlayerStats Copy() { return (PlayerStats)MemberwiseClone(); }
    }

    /// <summary>
    /// What the player did to each world, keyed by canonical planet id: structures, warehouse contents, waves, visits. This is the only
    /// per-planet state that is ever saved. <see cref="Version"/> grows on every change so views can cheaply notice it.
    /// </summary>
    public sealed class WorldStore
    {
        readonly Dictionary<string, PlanetDelta> worlds = new Dictionary<string, PlanetDelta>(StringComparer.Ordinal);
        static readonly IList<StructureRecord> NoStructures = new StructureRecord[0];

        public int Version { get; private set; }

        public int WorldCount { get { return worlds.Count; } }

        /// <summary>Every planet that has a delta (live view).</summary>
        public IEnumerable<PlanetDelta> All { get { return worlds.Values; } }

        public bool TryGet(string planetId, out PlanetDelta delta)
        {
            delta = null;
            return !string.IsNullOrEmpty(planetId) && worlds.TryGetValue(planetId, out delta);
        }

        /// <summary>The delta of a planet, created empty on first use.</summary>
        public PlanetDelta Get(string planetId)
        {
            if (string.IsNullOrEmpty(planetId)) throw new ArgumentException("planetId is required");
            PlanetDelta d;
            if (!worlds.TryGetValue(planetId, out d))
            {
                d = new PlanetDelta { planetId = planetId };
                worlds[planetId] = d;
            }
            return d;
        }

        /// <summary>Marks the store as changed (call after editing a record you obtained from it).</summary>
        public void Touch() { Version++; }

        // ---- structures ------------------------------------------------------------------------------------------------

        public IList<StructureRecord> StructuresOn(string planetId)
        {
            PlanetDelta d;
            return TryGet(planetId, out d) ? (IList<StructureRecord>)d.structures : NoStructures;
        }

        public int TotalStructures
        {
            get
            {
                int n = 0;
                foreach (var d in worlds.Values) n += d.structures.Count;
                return n;
            }
        }

        public StructureRecord StructureAt(string planetId, int slot)
        {
            var list = StructuresOn(planetId);
            for (int i = 0; i < list.Count; i++) if (list[i].slot == slot) return list[i];
            return null;
        }

        /// <summary>Records a structure; null when the pad is already taken. Costs, limits and pad ranges are the caller's business.</summary>
        public StructureRecord Place(string planetId, string defId, int slot, int level = 1)
        {
            if (string.IsNullOrEmpty(defId) || slot < 0 || StructureAt(planetId, slot) != null) return null;
            var s = new StructureRecord { defId = defId, slot = slot, level = Math.Max(1, level) };
            Get(planetId).structures.Add(s);
            Version++;
            return s;
        }

        public void SetLevel(StructureRecord structure, int level)
        {
            structure.level = Math.Max(1, level);
            Version++;
        }

        public bool Remove(string planetId, StructureRecord structure)
        {
            PlanetDelta d;
            if (!TryGet(planetId, out d) || !d.structures.Remove(structure)) return false;
            Version++;
            return true;
        }

        // ---- warehouse storage -----------------------------------------------------------------------------------------

        /// <summary>The storage of the warehouse on a pad, created on first use and grown to <paramref name="capacity"/> (never shrunk).</summary>
        public StorageRecord StorageFor(string planetId, int structureSlot, int capacity)
        {
            var d = Get(planetId);
            StorageRecord storage = null;
            for (int i = 0; i < d.storages.Count; i++) if (d.storages[i].structureSlot == structureSlot) { storage = d.storages[i]; break; }
            bool changed = false;
            if (storage == null)
            {
                storage = new StorageRecord { structureSlot = structureSlot };
                d.storages.Add(storage);
                changed = true;
            }
            while (storage.slots.Count < capacity)
            {
                storage.slots.Add(new StackRecord());
                changed = true;
            }
            if (changed) Version++;
            return storage;
        }

        // ---- waves and visits ------------------------------------------------------------------------------------------

        public int WavesCleared(string planetId)
        {
            PlanetDelta d;
            return TryGet(planetId, out d) ? d.wavesCleared : 0;
        }

        /// <summary>The number of the next wave on this planet (1 on a planet nothing was cleared on).</summary>
        public int NextWave(string planetId) { return WavesCleared(planetId) + 1; }

        public int RecordWaveCleared(string planetId)
        {
            var d = Get(planetId);
            d.wavesCleared++;
            Version++;
            return d.wavesCleared;
        }

        public int Visits(string planetId)
        {
            PlanetDelta d;
            return TryGet(planetId, out d) ? d.visits : 0;
        }

        public bool IsDiscovered(string planetId)
        {
            PlanetDelta d;
            return TryGet(planetId, out d) && d.discovered;
        }

        /// <summary>Records a landing. Returns true when it is the first visit to the world.</summary>
        public bool RecordVisit(string planetId, DateTime utcNow)
        {
            var d = Get(planetId);
            bool first = d.visits <= 0 && string.IsNullOrEmpty(d.firstVisitUtc);
            d.visits++;
            d.discovered = true;
            string stamp = UtcText.Format(utcNow);
            if (string.IsNullOrEmpty(d.firstVisitUtc)) d.firstVisitUtc = stamp;
            d.lastVisitUtc = stamp;
            Version++;
            return first;
        }

        // ---- persistence -----------------------------------------------------------------------------------------------

        /// <summary>The deltas worth saving: only planets the player changed, ordered by id so the file is stable.</summary>
        public List<PlanetDelta> Export()
        {
            var list = new List<PlanetDelta>();
            foreach (var d in worlds.Values) if (d.HasContent) list.Add(d);
            list.Sort((a, b) => string.CompareOrdinal(a.planetId, b.planetId));
            return list;
        }

        /// <summary>Replaces the whole store with loaded deltas, repairing null lists and merging duplicate planet ids.</summary>
        public void Import(IEnumerable<PlanetDelta> deltas)
        {
            worlds.Clear();
            if (deltas != null)
            {
                foreach (var incoming in deltas)
                {
                    if (incoming == null || string.IsNullOrEmpty(incoming.planetId)) continue;
                    Normalize(incoming);
                    PlanetDelta existing;
                    if (!worlds.TryGetValue(incoming.planetId, out existing)) { worlds[incoming.planetId] = incoming; continue; }
                    Merge(existing, incoming);
                }
            }
            Version++;
        }

        public void Clear()
        {
            worlds.Clear();
            Version++;
        }

        static void Normalize(PlanetDelta d)
        {
            if (d.structures == null) d.structures = new List<StructureRecord>();
            if (d.storages == null) d.storages = new List<StorageRecord>();
            if (d.clearedObstacles == null) d.clearedObstacles = new List<int>();
            if (d.firstVisitUtc == null) d.firstVisitUtc = "";
            if (d.lastVisitUtc == null) d.lastVisitUtc = "";
            d.structures.RemoveAll(s => s == null || string.IsNullOrEmpty(s.defId) || s.slot < 0);
            var seenSlots = new HashSet<int>();
            for (int i = 0; i < d.structures.Count; i++)
            {
                var s = d.structures[i];
                if (!seenSlots.Add(s.slot)) { d.structures.RemoveAt(i); i--; continue; }
                if (s.level < 1) s.level = 1;
                if (s.buildEndUtc == null) s.buildEndUtc = "";
                if (s.upgradeEndUtc == null) s.upgradeEndUtc = "";
                if (s.pendingUpgradeLevel < 0) s.pendingUpgradeLevel = 0;
            }
            d.storages.RemoveAll(s => s == null);
            foreach (var storage in d.storages)
            {
                if (storage.slots == null) storage.slots = new List<StackRecord>();
                for (int i = 0; i < storage.slots.Count; i++)
                    if (storage.slots[i] == null) storage.slots[i] = new StackRecord();
            }
            if (d.visits < 0) d.visits = 0;
            if (d.wavesCleared < 0) d.wavesCleared = 0;
        }

        static void Merge(PlanetDelta into, PlanetDelta other)
        {
            foreach (var s in other.structures)
            {
                bool taken = false;
                foreach (var e in into.structures) if (e.slot == s.slot) { taken = true; break; }
                if (!taken) into.structures.Add(s);
            }
            foreach (var st in other.storages)
            {
                bool exists = false;
                foreach (var e in into.storages) if (e.structureSlot == st.structureSlot) { exists = true; break; }
                if (!exists) into.storages.Add(st);
            }
            into.wavesCleared = Math.Max(into.wavesCleared, other.wavesCleared);
            into.visits = Math.Max(into.visits, other.visits);
            into.discovered |= other.discovered;
            if (string.IsNullOrEmpty(into.firstVisitUtc)) into.firstVisitUtc = other.firstVisitUtc;
            if (string.IsNullOrEmpty(into.lastVisitUtc)) into.lastVisitUtc = other.lastVisitUtc;
            foreach (int o in other.clearedObstacles) if (!into.clearedObstacles.Contains(o)) into.clearedObstacles.Add(o);
        }
    }
}
