using System;
using System.Collections.Generic;

namespace Frontier.Economy
{
    /// <summary>Maps the ids of today's game (and its saves) to the ids of the new catalog, and back.</summary>
    public sealed class LegacyIdMap
    {
        readonly Dictionary<string, string> _toNew = new Dictionary<string, string>();
        readonly Dictionary<string, string> _toLegacy = new Dictionary<string, string>();

        public LegacyIdMap(EconomyCatalog cat)
        {
            for (int i = 0; i < cat.legacyIds.Length; i++)
            {
                LegacyIdEntry e = cat.legacyIds[i];
                _toNew[Key(e.kind, e.legacyId)] = e.id;
                _toLegacy[Key(e.kind, e.id)] = e.legacyId;
            }
        }

        static string Key(LegacyKind kind, string id) { return (int)kind + ":" + id; }

        /// <summary>The new id for a legacy id, or null when the legacy id is unknown.</summary>
        public string Resolve(LegacyKind kind, string legacyId)
        {
            string v;
            return legacyId != null && _toNew.TryGetValue(Key(kind, legacyId), out v) ? v : null;
        }

        /// <summary>The legacy id of a new id, or null when the id did not exist in the old game.</summary>
        public string ToLegacy(LegacyKind kind, string id)
        {
            string v;
            return id != null && _toLegacy.TryGetValue(Key(kind, id), out v) ? v : null;
        }

        /// <summary>Converts a legacy inventory (id to count) into new ids; counts of ids that map to the same new id are added. Unknown ids are listed.</summary>
        public void MigrateInventory(IDictionary<string, int> legacy, IDictionary<string, int> result, IList<string> unknown)
        {
            foreach (KeyValuePair<string, int> kv in legacy)
            {
                string id = Resolve(LegacyKind.Resource, kv.Key);
                if (id == null) { if (unknown != null) unknown.Add(kv.Key); continue; }
                int have;
                result.TryGetValue(id, out have);
                result[id] = have + kv.Value;
            }
        }
    }
}
