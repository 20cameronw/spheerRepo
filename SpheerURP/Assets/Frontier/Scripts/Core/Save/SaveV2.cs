using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Universe;

namespace Frontier
{
    /// <summary>Save schema numbers and file names.</summary>
    public static class SaveSchema
    {
        /// <summary>The schema this build writes. v1 (no schemaVersion field) is the legacy flat format, see <see cref="SaveDataV1"/>.</summary>
        public const int Current = 2;

        public const string FileName = "frontier_save.json";
        public const string BackupSuffix = ".bak";
        public const string TempSuffix = ".tmp";
        /// <summary>Copy of the original v1 file, made before its first migration.</summary>
        public const string LegacyBackupName = "frontier_save.v1.bak.json";
    }

    /// <summary>Which build created a save and which universe it belongs to.</summary>
    [Serializable]
    public class CreatedWithInfo
    {
        public string appVersion = "";
        public int generatorVersion = 1;
        /// <summary>The universe seed as 16 hex digits (64 bit ids are text in the file).</summary>
        public string universeSeed = "";
    }

    [Serializable]
    public class IdLevel
    {
        public string id;
        public int level;
    }

    [Serializable]
    public class PlayerSave
    {
        public int xp;
        public List<IdLevel> upgrades = new List<IdLevel>();
        /// <summary>Purchased economy research node ids (absent from earlier v2 saves).</summary>
        public List<string> researched = new List<string>();
        /// <summary>Purchased catalog tool tiers keyed by tool ladder id (absent from earlier v2 saves).</summary>
        public List<IdLevel> toolTiers = new List<IdLevel>();
        /// <summary>The backpack as stacks per resource (a count, not slot positions).</summary>
        public List<StackRecord> inventory = new List<StackRecord>();
        public List<StackRecord> fuelTank = new List<StackRecord>();
        public int fuelStorageVersion;
    }

    [Serializable]
    public class LocationSave
    {
        /// <summary>Canonical planet id ("p:X:Y:I:P").</summary>
        public string currentPlanetId = "";
    }

    [Serializable]
    public class ClockSave
    {
        /// <summary>UTC, ISO 8601: when this file was written.</summary>
        public string lastSavedUtc = "";
        /// <summary>Greatest observed gameplay UTC; offline production never reuses time before this point.</summary>
        public string highWaterUtc = "";
    }

    [Serializable]
    public sealed class AscensionProgress
    {
        public int rank;
        public int legacyPoints;
    }

    [Serializable]
    public sealed class MapDiscoverySector
    {
        public string sectorId;
        public string bits;
    }

    /// <summary>Stores discovered system indices as one bitset per sector.</summary>
    public static class MapDiscoveryBits
    {
        const int ByteCount = (SystemKey.MaxIndex + 8) / 8;

        public static List<MapDiscoverySector> Encode(IEnumerable<string> systemIds)
        {
            var sectors = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (systemIds != null)
                foreach (string id in systemIds)
                {
                    SystemKey key;
                    if (!SystemKey.TryParse(id, out key) || key.ToString() != id) continue;
                    string sectorId = key.Sector.ToString();
                    byte[] bits;
                    if (!sectors.TryGetValue(sectorId, out bits))
                    {
                        bits = new byte[ByteCount];
                        sectors.Add(sectorId, bits);
                    }
                    bits[key.Index >> 3] |= (byte)(1 << (key.Index & 7));
                }

            var result = new List<MapDiscoverySector>(sectors.Count);
            foreach (var entry in sectors)
            {
                int length = entry.Value.Length;
                while (length > 0 && entry.Value[length - 1] == 0) length--;
                var compact = new byte[length];
                Array.Copy(entry.Value, compact, length);
                result.Add(new MapDiscoverySector { sectorId = entry.Key, bits = Convert.ToBase64String(compact) });
            }
            result.Sort((a, b) => string.CompareOrdinal(a.sectorId, b.sectorId));
            return result;
        }

        public static string[] Decode(IEnumerable<MapDiscoverySector> records)
        {
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            if (records == null) return ids.ToArray();
            foreach (MapDiscoverySector record in records)
            {
                if (record == null) continue;
                SectorCoord sector;
                if (!SectorCoord.TryParse(record.sectorId, out sector) || sector.ToString() != record.sectorId ||
                    string.IsNullOrEmpty(record.bits)) continue;
                byte[] bits;
                try { bits = Convert.FromBase64String(record.bits); }
                catch (FormatException) { continue; }
                if (bits.Length == 0 || bits.Length > ByteCount || bits.Length == ByteCount && (bits[ByteCount - 1] & 0x80) != 0) continue;
                for (int byteIndex = 0; byteIndex < bits.Length; byteIndex++)
                    for (int bit = 0; bit < 8; bit++)
                        if ((bits[byteIndex] & (1 << bit)) != 0)
                        {
                            int index = byteIndex * 8 + bit;
                            if (index <= SystemKey.MaxIndex) ids.Add(new SystemKey(sector, index).ToString());
                        }
            }
            return ids.ToArray();
        }
    }

    // ---- reserved sections: empty today, they exist so later work packages can fill them without a schema bump ----

    /// <summary>Reserved: premium and soft currency balances.</summary>
    [Serializable]
    public class WalletEntry
    {
        public string currency;
        public int amount;
    }

    [Serializable]
    public sealed class PurchaseGrantRecord
    {
        public string store;
        public string transactionId;
        public string productId;
        public int shards;
    }

    /// <summary>Reserved: builder slots and the job each one is busy with.</summary>
    [Serializable]
    public class BuilderSlot
    {
        public string slotId;
        public string target = "";
        public string busyUntilUtc = "";
    }

    /// <summary>Reserved: player settings kept with the save (key/value text).</summary>
    [Serializable]
    public class SettingEntry
    {
        public string key;
        public string value;
    }

    /// <summary>
    /// Save file schema v2: the player's state plus deltas (only what the player changed). Everything else about the universe is
    /// regenerated from <see cref="CreatedWithInfo.universeSeed"/> and the generator version. Planet ids are canonical universe ids.
    /// Plain public fields so Unity's JsonUtility (which has no dictionaries) can read and write it.
    /// </summary>
    [Serializable]
    public class SaveV2
    {
        public int schemaVersion = SaveSchema.Current;
        public string saveId = "";
        /// <summary>Monotonic local-save write sequence used to identify divergent snapshots across devices.</summary>
        public long revision;
        public CreatedWithInfo createdWith = new CreatedWithInfo();
        public PlayerSave player = new PlayerSave();
        public LocationSave location = new LocationSave();
        public List<PlanetDelta> worlds = new List<PlanetDelta>();
        public PlayerStats stats = new PlayerStats();
        public ClockSave clock = new ClockSave();
        public List<WalletEntry> wallet = new List<WalletEntry>();
        public List<PurchaseGrantRecord> purchaseGrants = new List<PurchaseGrantRecord>();
        public List<BuilderSlot> builders = new List<BuilderSlot>();
        public List<string> atlas = new List<string>();
        public List<string> mapPins = new List<string>();
        /// <summary>Legacy v2 map discoveries, migrated to mapDiscoverySectors by Normalize.</summary>
        public List<string> mapDiscoveries = new List<string>();
        public List<MapDiscoverySector> mapDiscoverySectors = new List<MapDiscoverySector>();
        public List<SettingEntry> settings = new List<SettingEntry>();
        public bool objectivesInitialized;
        public List<ObjectiveRecord> objectives = new List<ObjectiveRecord>();
        public AscensionProgress ascension = new AscensionProgress();
        public NetworkProgress network = new NetworkProgress();
        public TravelCheckpoint travel;
        public bool travelPending;

        /// <summary>Repairs nulls a hand-edited or truncated file can leave behind, so the rest of the game never null-checks.</summary>
        public SaveV2 Normalize()
        {
            if (!travelPending) travel = null;
            else
            {
                if (travel == null) travel = new TravelCheckpoint { version = 0 };
                travel.Normalize();
            }
            if (saveId == null) saveId = "";
            if (revision < 0) revision = 0;
            if (createdWith == null) createdWith = new CreatedWithInfo();
            if (createdWith.appVersion == null) createdWith.appVersion = "";
            if (createdWith.universeSeed == null) createdWith.universeSeed = "";
            if (player == null) player = new PlayerSave();
            if (player.upgrades == null) player.upgrades = new List<IdLevel>();
            if (player.researched == null) player.researched = new List<string>();
            if (player.toolTiers == null) player.toolTiers = new List<IdLevel>();
            if (player.inventory == null) player.inventory = new List<StackRecord>();
            if (player.fuelTank == null) player.fuelTank = new List<StackRecord>();
            if (network == null) network = new NetworkProgress();
            if (network.links == null) network.links = new List<PlanetLink>();
            if (location == null) location = new LocationSave();
            if (location.currentPlanetId == null) location.currentPlanetId = "";
            if (worlds == null) worlds = new List<PlanetDelta>();
            if (stats == null) stats = new PlayerStats();
            if (clock == null) clock = new ClockSave();
            if (clock.lastSavedUtc == null) clock.lastSavedUtc = "";
            if (clock.highWaterUtc == null) clock.highWaterUtc = "";
            if (wallet == null) wallet = new List<WalletEntry>();
            if (purchaseGrants == null) purchaseGrants = new List<PurchaseGrantRecord>();
            if (builders == null) builders = new List<BuilderSlot>();
            if (atlas == null) atlas = new List<string>();
            if (mapPins == null) mapPins = new List<string>();
            if (mapDiscoveries == null) mapDiscoveries = new List<string>();
            if (mapDiscoverySectors == null) mapDiscoverySectors = new List<MapDiscoverySector>();
            if (settings == null) settings = new List<SettingEntry>();
            objectives = new ObjectiveLedger(objectives).Export();
            if (ascension == null) ascension = new AscensionProgress();
            ascension.rank = Math.Max(0, ascension.rank);
            ascension.legacyPoints = Math.Max(0, ascension.legacyPoints);
            var atlasEntries = new HashSet<string>(StringComparer.Ordinal);
            atlas.RemoveAll(id => string.IsNullOrEmpty(id) || !atlasEntries.Add(id));
            var pinnedPlanets = new HashSet<string>(StringComparer.Ordinal);
            mapPins.RemoveAll(id => string.IsNullOrEmpty(id) || !pinnedPlanets.Add(id));
            mapDiscoverySectors = MapDiscoveryBits.Encode(
                MapDiscoveryBits.Decode(mapDiscoverySectors).Concat(mapDiscoveries));
            mapDiscoveries.Clear();
            player.upgrades.RemoveAll(u => u == null || string.IsNullOrEmpty(u.id));
            player.researched.RemoveAll(id => string.IsNullOrEmpty(id));
            player.toolTiers.RemoveAll(t => t == null || string.IsNullOrEmpty(t.id) || t.level <= 0);
            player.inventory.RemoveAll(s => s == null || string.IsNullOrEmpty(s.resourceId) || s.count <= 0);
            player.fuelTank.RemoveAll(s => s == null || string.IsNullOrEmpty(s.resourceId) || s.count <= 0);
            if (player.xp < 0) player.xp = 0;
            return this;
        }
    }

    /// <summary>The 64 bit universe seed as text (and back).</summary>
    public static class SeedText
    {
        public static string Format(ulong seed) { return seed.ToString("X16", System.Globalization.CultureInfo.InvariantCulture); }

        public static bool TryParse(string text, out ulong seed)
        {
            seed = 0UL;
            return !string.IsNullOrEmpty(text) && ulong.TryParse(text, System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out seed);
        }
    }
}
