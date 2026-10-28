using System;
using System.Collections.Generic;
using Frontier.Economy;
using UnityEngine;

namespace Frontier
{
    [Serializable]
    public sealed class TuningValue
    {
        public string key;
        public float value;
    }

    [CreateAssetMenu(menuName = "Frontier/Economy/Tuning")]
    public sealed class EconomyTuningSO : ContentSO
    {
        public int schemaVersion;
        public string digest;
        public TuningValue[] values;

        public Tuning Build()
        {
            var tuning = Tuning.CreateDefault();
            if (schemaVersion != Tuning.SchemaVersion) throw new InvalidOperationException("Unsupported tuning schema");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in values)
            {
                if (row == null || !seen.Add(row.key) || !tuning.Has(row.key) || float.IsNaN(row.value) || float.IsInfinity(row.value))
                    throw new InvalidOperationException("Invalid or duplicate tuning key");
                tuning.Set(row.key, row.value);
                if (tuning.Get(row.key) != row.value) throw new InvalidOperationException("Tuning out of range: " + row.key);
            }
            if (seen.Count != tuning.Count || tuning.Digest().ToString("x") != digest)
                throw new InvalidOperationException("Incomplete tuning or digest mismatch");
            return tuning;
        }
    }
}
