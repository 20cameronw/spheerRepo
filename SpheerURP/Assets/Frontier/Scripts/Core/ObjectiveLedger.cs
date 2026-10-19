using System;
using System.Collections.Generic;

namespace Frontier
{
    [Serializable]
    public sealed class ObjectiveRecord
    {
        public string id;
        public bool completed;
        public bool claimed;
    }

    public sealed class ObjectiveLedger
    {
        readonly Dictionary<string, ObjectiveRecord> records = new Dictionary<string, ObjectiveRecord>(StringComparer.Ordinal);

        public ObjectiveLedger(IEnumerable<ObjectiveRecord> source = null)
        {
            if (source == null) return;
            foreach (var record in source)
            {
                if (record == null || string.IsNullOrEmpty(record.id)) continue;
                if (!records.TryGetValue(record.id, out var merged))
                {
                    merged = new ObjectiveRecord { id = record.id };
                    records.Add(record.id, merged);
                }
                merged.completed |= record.completed || record.claimed;
                merged.claimed |= record.claimed;
            }
        }

        public bool Completed(string id) { return id != null && records.TryGetValue(id, out var r) && r.completed; }
        public bool Claimed(string id) { return id != null && records.TryGetValue(id, out var r) && r.claimed; }

        public bool Observe(string id, bool completed, bool historical = false)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Objective id is required.", nameof(id));
            if (!completed || Completed(id)) return false;
            if (!records.TryGetValue(id, out var record))
            {
                record = new ObjectiveRecord { id = id };
                records.Add(id, record);
            }
            record.completed = true;
            record.claimed = historical;
            return true;
        }

        public bool TryClaim(string id)
        {
            if (!Completed(id) || Claimed(id)) return false;
            records[id].claimed = true;
            return true;
        }

        public List<ObjectiveRecord> Export()
        {
            var result = new List<ObjectiveRecord>(records.Count);
            foreach (var record in records.Values)
                result.Add(new ObjectiveRecord { id = record.id, completed = record.completed, claimed = record.claimed });
            result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return result;
        }
    }
}
