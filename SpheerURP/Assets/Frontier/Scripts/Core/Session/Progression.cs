using System;
using System.Collections.Generic;

namespace Frontier
{
    /// <summary>
    /// Experience, the player level curve and purchased upgrade levels, all id based. Level = 1 + floor(sqrt(xp / base)); the base is
    /// supplied by the host (it lives in a tuning asset). The arithmetic is float on purpose: it matches the original GameState
    /// bit for bit, so existing saves keep their level.
    /// </summary>
    public sealed class Progression
    {
        readonly Func<float> xpBase;
        readonly Dictionary<string, int> upgrades = new Dictionary<string, int>(StringComparer.Ordinal);

        public Progression(Func<float> xpPerLevelBase)
        {
            if (xpPerLevelBase == null) throw new ArgumentNullException("xpPerLevelBase");
            xpBase = xpPerLevelBase;
        }

        public int Xp { get; private set; }

        float Base { get { return Math.Max(1f, xpBase()); } }

        public int Level { get { return 1 + (int)Math.Floor((float)Math.Sqrt(Xp / Base)); } }

        /// <summary>Progress through the current level, 0 to 1.</summary>
        public float LevelProgress
        {
            get
            {
                int l = Level - 1;
                float lo = Base * l * l, hi = Base * (l + 1) * (l + 1);
                float t = (Xp - lo) / (hi - lo);
                return t < 0f ? 0f : (t > 1f ? 1f : t);
            }
        }

        /// <summary>Total xp at which the given level starts (level 1 starts at 0).</summary>
        public int XpForLevel(int level)
        {
            int l = Math.Max(0, level - 1);
            return (int)Math.Round(Base * l * l);
        }

        public long XpToNextLevel { get { return Math.Max(0L, (long)Math.Round(Base * Level * Level) - Xp); } }

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            long total = (long)Xp + amount;
            Xp = total > int.MaxValue ? int.MaxValue : (int)total;
        }

        /// <summary>Sets the xp directly (loading a save, debug tools).</summary>
        public void SetXp(int xp) { Xp = Math.Max(0, xp); }

        /// <summary>Debug: jumps to the first xp value of a level.</summary>
        public void SetLevel(int level) { Xp = XpForLevel(level); }

        public int UpgradeLevel(string id)
        {
            int l;
            return !string.IsNullOrEmpty(id) && upgrades.TryGetValue(id, out l) ? l : 0;
        }

        /// <summary>Sets an upgrade level clamped to [0, maxLevel]; level 0 removes the entry.</summary>
        public void SetUpgradeLevel(string id, int level, int maxLevel)
        {
            if (string.IsNullOrEmpty(id)) return;
            int clamped = level < 0 ? 0 : (level > maxLevel ? maxLevel : level);
            if (clamped == 0) upgrades.Remove(id); else upgrades[id] = clamped;
        }

        /// <summary>Upgrade ids with a non-zero level (live view).</summary>
        public IEnumerable<KeyValuePair<string, int>> Upgrades { get { return upgrades; } }

        public void Clear()
        {
            Xp = 0;
            upgrades.Clear();
        }
    }
}
