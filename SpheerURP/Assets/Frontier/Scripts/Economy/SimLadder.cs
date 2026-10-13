using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    enum GoalKind { Build, Tool, Upgrade, Research, Explore, Mega, Wave, Stock, ExploreTo }

    /// <summary>One thing the player wants. Build goals name a planet and an instance number; Stock goals are "have N of X".</summary>
    sealed class Goal
    {
        public GoalKind kind;
        public int idx = -1;
        public int level;
        public int planet = -1;
        public int no = 1;
        public long amount;
        public string mark;
        public string text;

        public static Goal Build(int str, int level, int planet, int no) { return new Goal { kind = GoalKind.Build, idx = str, level = level, planet = planet, no = no }; }

        public static Goal Research(int node) { return new Goal { kind = GoalKind.Research, idx = node }; }

        public static Goal Explore(int tier) { return new Goal { kind = GoalKind.Explore, level = tier }; }

        public static Goal Upgrade(int upg, int level) { return new Goal { kind = GoalKind.Upgrade, idx = upg, level = level }; }

        public static Goal Stock(int res, long amount) { return new Goal { kind = GoalKind.Stock, idx = res, amount = amount }; }

        public static Goal ExploreTo(int planet) { return new Goal { kind = GoalKind.ExploreTo, planet = planet }; }
    }

    /// <summary>
    /// The headline ladder: what a typical player wants next, in order. Each line is one goal:
    /// build:structure:level[@planet][#instance], tool:ladder:tier, upg:id:level, research:id, explore:tier, mega:structure:stage, wave:n, stock:resource:amount,
    /// optionally followed by =milestone. Prerequisites (research, station levels, reach, plating, ingredients) are resolved automatically.
    /// </summary>
    public sealed partial class SimEngine
    {
        Goal[] ladder;
        bool[] goalDone;
        int ladderFirst;

        /// <summary>The ladder lines used when the settings do not override them (also the "goal ladder" of the reports).</summary>
        public static string[] DefaultLadderLines { get { return SimLadderData.Lines; } }

        void BuildLadder()
        {
            string[] lines = cfg.ladder ?? SimLadderData.Lines;
            var list = new List<Goal>();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                list.Add(ParseGoal(line));
            }
            ladder = list.ToArray();
            goalDone = new bool[ladder.Length];
        }

        int PlanetByName(string name)
        {
            if (name == "home") return homePlanet;
            for (int i = 0; i < world.planets.Length; i++) if (world.planets[i].name == name) return i;
            throw new InvalidOperationException("sim ladder: unknown planet " + name);
        }

        Goal ParseGoal(string line)
        {
            string mark = null;
            int eq = line.IndexOf('=');
            if (eq >= 0) { mark = line.Substring(eq + 1).Trim(); line = line.Substring(0, eq).Trim(); }
            string[] p = line.Split(':');
            Goal g;
            switch (p[0])
            {
                case "build":
                {
                    string lv = p[2];
                    int planet = homePlanet, no = 1;
                    int hash = lv.IndexOf('#');
                    if (hash >= 0) { no = int.Parse(lv.Substring(hash + 1), System.Globalization.CultureInfo.InvariantCulture); lv = lv.Substring(0, hash); }
                    int at = lv.IndexOf('@');
                    if (at >= 0) { planet = PlanetByName(lv.Substring(at + 1)); lv = lv.Substring(0, at); }
                    int level = int.Parse(lv, System.Globalization.CultureInfo.InvariantCulture);
                    int str = SI(p[1]);
                    if (level < 1 || level > cat.structures[str].maxLevel) throw new InvalidOperationException("sim ladder: bad level in '" + line + "'");
                    g = Goal.Build(str, level, planet, no);
                    break;
                }
                case "tool":
                {
                    int l = TI(p[1]);
                    int tier = int.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
                    if (tier < 2 || tier > cat.tools[l].tiers.Length) throw new InvalidOperationException("sim ladder: bad tool tier in '" + line + "'");
                    g = new Goal { kind = GoalKind.Tool, idx = l, level = tier };
                    break;
                }
                case "upg":
                {
                    int u = UI(p[1]);
                    int level = int.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
                    if (level < 1 || level > cat.upgrades[u].maxLevel) throw new InvalidOperationException("sim ladder: bad upgrade level in '" + line + "'");
                    g = Goal.Upgrade(u, level);
                    break;
                }
                case "research": g = Goal.Research(RsI(p[1])); break;
                case "explore": g = Goal.Explore(int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)); break;
                case "mega":
                {
                    int s = SI(p[1]);
                    int m = -1;
                    for (int k = 0; k < 3; k++) if (megaStr[k] == s) m = k;
                    if (m < 0) throw new InvalidOperationException("sim ladder: not a megaproject '" + line + "'");
                    g = new Goal { kind = GoalKind.Mega, idx = m, level = int.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture) };
                    break;
                }
                case "wave": g = new Goal { kind = GoalKind.Wave, level = int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture) }; break;
                case "stock": g = Goal.Stock(RI(p[1]), long.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture)); break;
                default: throw new InvalidOperationException("sim ladder: unknown goal '" + line + "'");
            }
            g.mark = mark;
            g.text = line;
            return g;
        }

        /// <summary>Done-ness of a goal against the current state.</summary>
        bool IsDone(Goal g)
        {
            switch (g.kind)
            {
                case GoalKind.Build: return BuiltLevel(g.planet, g.idx, g.no) >= g.level;
                case GoalKind.Tool: return toolTier[g.idx] >= g.level;
                case GoalKind.Upgrade: return upgLevel[g.idx] >= g.level;
                case GoalKind.Research: return researched[g.idx];
                case GoalKind.Explore: return maxTierLanded >= g.level;
                case GoalKind.Mega: return mega[g.idx].stage >= g.level;
                case GoalKind.Wave: return wavesCalled >= g.level;
                case GoalKind.Stock: return PoolHave(g.idx) >= g.amount;
                case GoalKind.ExploreTo: return landed[g.planet];
                default: return true;
            }
        }

        bool LadderDone(int i)
        {
            if (goalDone[i]) return true;
            if (!IsDone(ladder[i])) return false;
            goalDone[i] = true;
            if (ladder[i].mark != null) Mark(ladder[i].mark);
            res.goals.Add(new SimGoalTime { index = i, text = ladder[i].text, seconds = markClock, playSeconds = res.playSeconds });
            return true;
        }

        int FirstUndone()
        {
            while (ladderFirst < ladder.Length && LadderDone(ladderFirst)) ladderFirst++;
            return ladderFirst;
        }

        /// <summary>When the headline ladder is finished the player keeps buying: every remaining upgrade, cheapest first (the endless part).</summary>
        void ExtendLadder()
        {
            var extra = new List<Goal>();
            var cost = new List<double>();
            for (int s = 0; s < cat.structures.Length; s++)
            {
                StructureDef def = cat.structures[s];
                if (def.role == StructureRole.Megaproject) continue;
                int no = 1;
                for (int l = 1; l <= def.maxLevel; l++)
                {
                    extra.Add(Goal.Build(s, l, homePlanet, no));
                    cost.Add(cat.Prices.Value(def.levels[l - 1].cost) * (1.0 + 0.02 * l));
                }
            }
            for (int t = 0; t < cat.tools.Length; t++)
                for (int tier = 2; tier <= cat.tools[t].tiers.Length; tier++)
                {
                    extra.Add(new Goal { kind = GoalKind.Tool, idx = t, level = tier });
                    cost.Add(cat.Prices.Value(cat.tools[t].tiers[tier - 1].cost));
                }
            for (int u = 0; u < cat.upgrades.Length; u++)
                for (int l = 1; l <= cat.upgrades[u].maxLevel; l++)
                {
                    extra.Add(Goal.Upgrade(u, l));
                    cost.Add(cat.Prices.Value(cat.upgrades[u].levels[l - 1].cost));
                }
            for (int k = 0; k < 3; k++)
                for (int stage = 1; stage <= 5; stage++)
                {
                    extra.Add(new Goal { kind = GoalKind.Mega, idx = k, level = stage });
                    cost.Add(cat.Prices.Value(cat.structures[megaStr[k]].levels[stage - 1].cost) * 0.2);
                }
            var order = new int[extra.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => { int c = cost[a].CompareTo(cost[b]); return c != 0 ? c : a.CompareTo(b); });
            var all = new List<Goal>(ladder);
            for (int i = 0; i < order.Length; i++)
            {
                Goal g = extra[order[i]];
                if (IsDone(g)) continue;
                g.text = "auto:" + g.kind + ":" + g.idx + ":" + g.level;
                all.Add(g);
            }
            ladder = all.ToArray();
            var done = new bool[ladder.Length];
            Array.Copy(goalDone, done, goalDone.Length);
            goalDone = done;
        }
    }
}
