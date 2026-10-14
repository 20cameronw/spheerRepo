using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    public sealed partial class RoutePlanner
    {
        /// <summary>Most systems the leg search expands before it gives up (the plan then says out of range).</summary>
        public const int MaxExpansions = 3000;
        /// <summary>Of the systems within one jump, only this many (the ones closest to the destination) are considered as the next stop.</summary>
        public const int MaxNeighbours = 96;
        // One leg weighs this much more than any distance in light-years, so "fewest legs" always wins and length only breaks ties.
        const double LegWeight = 1.0e6;

        struct SearchNode
        {
            public StarSystem system;
            public double cost;
            public int parent;
        }

        struct HeapItem
        {
            public double f;
            public ulong tie;
            public int node;
        }

        struct Candidate
        {
            public StarSystem system;
            public double toGoal;
        }

        readonly List<SearchNode> _nodes = new List<SearchNode>();
        readonly Dictionary<SystemKey, double> _bestCost = new Dictionary<SystemKey, double>();
        readonly List<HeapItem> _heap = new List<HeapItem>();
        readonly List<Candidate> _candidates = new List<Candidate>();
        bool searchLimited;

        static int CompareCandidates(Candidate a, Candidate b)
        {
            int c = a.toGoal.CompareTo(b.toGoal);
            return c != 0 ? c : a.system.Key.Id.CompareTo(b.system.Key.Id);
        }

        double Heuristic(double toGoal, double maxLeg)
        {
            return Math.Ceiling(toGoal / maxLeg - 1e-9) * LegWeight + toGoal;
        }

        /// <summary>
        /// The chain of systems from <paramref name="a"/> to <paramref name="b"/> (both included) with the fewest legs of at most
        /// <paramref name="maxLeg"/> light-years, the shortest in length among those. Null when none is found.
        /// A* over the systems with an admissible heuristic (ceil(distance / range) legs plus the straight distance).
        /// </summary>
        public List<StarSystem> FindChain(StarSystem a, StarSystem b, double maxLeg)
        {
            searchLimited = false;
            if (a == null || b == null || !(maxLeg > 0.0) || double.IsInfinity(maxLeg)) return null;
            if (a.Key == b.Key) return new List<StarSystem> { a };
            double direct = Dist(a.X, a.Y, b.X, b.Y);
            if (direct <= maxLeg) return new List<StarSystem> { a, b };

            _nodes.Clear();
            _bestCost.Clear();
            _heap.Clear();
            _nodes.Add(new SearchNode { system = a, cost = 0.0, parent = -1 });
            _bestCost[a.Key] = 0.0;
            Push(Heuristic(direct, maxLeg), a.Key.Id, 0);
            int expansions = 0;
            while (_heap.Count > 0 && expansions < MaxExpansions)
            {
                HeapItem top = Pop();
                SearchNode current = _nodes[top.node];
                double known;
                if (_bestCost.TryGetValue(current.system.Key, out known) && current.cost > known + 1e-9) continue;
                if (current.system.Key == b.Key) return Rebuild(top.node);
                expansions++;

                _candidates.Clear();
                _scratch.Clear();
                StarSystem s0 = current.system;
                _universe.SystemsInRect(s0.X - maxLeg, s0.Y - maxLeg, s0.X + maxLeg, s0.Y + maxLeg, _scratch);
                for (int i = 0; i < _scratch.Count; i++)
                {
                    StarSystem s = _scratch[i];
                    if (s.Key == s0.Key) continue;
                    double leg = Dist(s0.X, s0.Y, s.X, s.Y);
                    if (leg > maxLeg) continue;
                    _candidates.Add(new Candidate { system = s, toGoal = Dist(s.X, s.Y, b.X, b.Y) });
                }
                _candidates.Sort(CompareCandidates);
                int take = Math.Min(_candidates.Count, MaxNeighbours);
                if (take < _candidates.Count) searchLimited = true;
                for (int i = 0; i < take; i++)
                {
                    StarSystem s = _candidates[i].system;
                    double cost = current.cost + LegWeight + Dist(s0.X, s0.Y, s.X, s.Y);
                    double best;
                    if (_bestCost.TryGetValue(s.Key, out best) && best <= cost + 1e-9) continue;
                    _bestCost[s.Key] = cost;
                    _nodes.Add(new SearchNode { system = s, cost = cost, parent = top.node });
                    Push(cost + Heuristic(_candidates[i].toGoal, maxLeg), s.Key.Id, _nodes.Count - 1);
                }
                if (_heap.Count > 0) searchLimited = true;
            }
            _scratch.Clear();
            return null;
        }

        List<StarSystem> Rebuild(int node)
        {
            var chain = new List<StarSystem>();
            for (int n = node; n >= 0; n = _nodes[n].parent) chain.Add(_nodes[n].system);
            chain.Reverse();
            return chain;
        }

        // ---- binary min-heap on (f, tie) ---------------------------------------------------------------------------------

        static bool Less(HeapItem x, HeapItem y)
        {
            return x.f < y.f || (x.f == y.f && x.tie < y.tie);
        }

        void Push(double f, ulong tie, int node)
        {
            _heap.Add(new HeapItem { f = f, tie = tie, node = node });
            int i = _heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (!Less(_heap[i], _heap[parent])) break;
                HeapItem t = _heap[i]; _heap[i] = _heap[parent]; _heap[parent] = t;
                i = parent;
            }
        }

        HeapItem Pop()
        {
            HeapItem top = _heap[0];
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            int i = 0;
            int count = _heap.Count;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < count && Less(_heap[l], _heap[m])) m = l;
                if (r < count && Less(_heap[r], _heap[m])) m = r;
                if (m == i) break;
                HeapItem t = _heap[i]; _heap[i] = _heap[m]; _heap[m] = t;
                i = m;
            }
            return top;
        }
    }
}
