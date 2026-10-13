using System;
using System.Collections.Generic;

namespace Frontier.Universe
{
    /// <summary>
    /// Fixed-capacity least-recently-used cache backed by arrays (no allocation per insert once warmed up). Not thread safe:
    /// the owner serialises access with a lock.
    /// </summary>
    internal sealed class LruCache<TKey, TValue> where TKey : IEquatable<TKey>
    {
        readonly Dictionary<TKey, int> _map;
        readonly TKey[] _keys;
        readonly TValue[] _values;
        readonly int[] _prev;
        readonly int[] _next;
        int _head = -1;
        int _tail = -1;
        int _count;

        public LruCache(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException("capacity");
            _map = new Dictionary<TKey, int>(capacity);
            _keys = new TKey[capacity];
            _values = new TValue[capacity];
            _prev = new int[capacity];
            _next = new int[capacity];
        }

        public int Count { get { return _count; } }

        public int Capacity { get { return _keys.Length; } }

        public bool TryGet(TKey key, out TValue value)
        {
            int slot;
            if (_map.TryGetValue(key, out slot))
            {
                MoveToFront(slot);
                value = _values[slot];
                return true;
            }
            value = default(TValue);
            return false;
        }

        public void Put(TKey key, TValue value)
        {
            int slot;
            if (_map.TryGetValue(key, out slot))
            {
                _values[slot] = value;
                MoveToFront(slot);
                return;
            }
            if (_count < _keys.Length)
            {
                slot = _count++;
            }
            else
            {
                slot = _tail;
                Unlink(slot);
                _map.Remove(_keys[slot]);
            }
            _keys[slot] = key;
            _values[slot] = value;
            _map[key] = slot;
            LinkFront(slot);
        }

        public void Clear()
        {
            _map.Clear();
            Array.Clear(_keys, 0, _keys.Length);
            Array.Clear(_values, 0, _values.Length);
            _head = _tail = -1;
            _count = 0;
        }

        void MoveToFront(int slot)
        {
            if (_head == slot) return;
            Unlink(slot);
            LinkFront(slot);
        }

        void Unlink(int slot)
        {
            int p = _prev[slot], n = _next[slot];
            if (p >= 0) _next[p] = n; else _head = n;
            if (n >= 0) _prev[n] = p; else _tail = p;
        }

        void LinkFront(int slot)
        {
            _prev[slot] = -1;
            _next[slot] = _head;
            if (_head >= 0) _prev[_head] = slot;
            _head = slot;
            if (_tail < 0) _tail = slot;
        }
    }
}
