using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>One tunable number: default, legal range, current value, and a group/description for the Remote Config listing.</summary>
    [Serializable]
    public sealed class TuningEntry
    {
        public string key;
        public string group;
        public string description;
        public float defaultValue;
        public float min;
        public float max;
        public float value;
    }

    /// <summary>
    /// The single tuning surface of the economy: string key to float, with defaults and min/max. Every balance knob of the catalog
    /// builder, the travel/timer/shard/score models and the simulator lives here, so the app can expose any of them through Remote Config
    /// (apply overrides with <see cref="Set"/>, then rebuild the catalog with <c>CatalogBuilder.Build</c>). Instances are independent
    /// (no static state), so tests can tune a copy freely.
    /// </summary>
    public sealed class Tuning
    {
        /// <summary>Version of the key naming scheme and meaning (part of the exported JSON).</summary>
        public const int SchemaVersion = 1;

        readonly List<TuningEntry> _entries = new List<TuningEntry>();
        readonly Dictionary<string, int> _index = new Dictionary<string, int>();

        /// <summary>A tuning set holding every key with its default value.</summary>
        public static Tuning CreateDefault()
        {
            var t = new Tuning();
            TuningKeys.RegisterAll(t);
            return t;
        }

        public int Count { get { return _entries.Count; } }

        public TuningEntry EntryAt(int i) { return _entries[i]; }

        public IReadOnlyList<TuningEntry> Entries { get { return _entries; } }

        public bool Has(string key) { return _index.ContainsKey(key); }

        /// <summary>Registers a key (idempotent: an existing key keeps its current value).</summary>
        public Tuning Register(string group, string key, double defaultValue, double min, double max, string description)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("tuning key is empty");
            if (min > max || defaultValue < min || defaultValue > max)
                throw new ArgumentException("tuning key " + key + ": default " + defaultValue + " outside [" + min + ", " + max + "]");
            if (_index.ContainsKey(key)) return this;
            _index[key] = _entries.Count;
            _entries.Add(new TuningEntry { key = key, group = group, description = description, defaultValue = (float)defaultValue, min = (float)min, max = (float)max, value = (float)defaultValue });
            return this;
        }

        TuningEntry Find(string key)
        {
            int i;
            if (!_index.TryGetValue(key, out i)) throw new KeyNotFoundException("unknown tuning key '" + key + "'");
            return _entries[i];
        }

        public float Get(string key) { return Find(key).value; }

        /// <summary>The value as double (exact widening of the stored float).</summary>
        public double D(string key) { return Find(key).value; }

        /// <summary>The value rounded to the nearest integer (half away from zero), computed in double.</summary>
        public int I(string key)
        {
            double v = Find(key).value;
            return (int)(v < 0.0 ? Math.Ceiling(v - 0.5) : Math.Floor(v + 0.5));
        }

        /// <summary>Sets a value, clamped into the key's legal range.</summary>
        public void Set(string key, double value)
        {
            TuningEntry e = Find(key);
            double v = value < e.min ? e.min : (value > e.max ? e.max : value);
            e.value = (float)v;
        }

        public void Reset(string key) { TuningEntry e = Find(key); e.value = e.defaultValue; }

        public void ResetAll() { for (int i = 0; i < _entries.Count; i++) _entries[i].value = _entries[i].defaultValue; }

        public Tuning Clone()
        {
            var t = new Tuning();
            for (int i = 0; i < _entries.Count; i++)
            {
                TuningEntry e = _entries[i];
                t._index[e.key] = t._entries.Count;
                t._entries.Add(new TuningEntry { key = e.key, group = e.group, description = e.description, defaultValue = e.defaultValue, min = e.min, max = e.max, value = e.value });
            }
            return t;
        }

        /// <summary>Hash of every key and current value in registration order (regression and determinism checks).</summary>
        public ulong Digest()
        {
            ulong h = 0x7011ABUL;
            for (int i = 0; i < _entries.Count; i++)
            {
                h = Hash.Combine(h, Hash.Fnv1a64(_entries[i].key));
                h = Hash.Combine(h, (ulong)BitConverter.SingleToInt32Bits(_entries[i].value) & 0xFFFFFFFFUL);
            }
            return h;
        }
    }
}
