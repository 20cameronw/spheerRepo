using System;

namespace Frontier
{
    public enum HarvestStrike
    {
        Miss = -1,
        Glance = 0,
        WeakSpot = 1,
        Perfect = 2
    }

    /// <summary>Engine-free chop/mine rules shared by the rendered game and harvest bots.</summary>
    public sealed class NodeHarvestRun
    {
        public const int StageCount = 4;
        readonly float toolDamage;

        public float StageHealth { get; }
        public float SwingSeconds { get; }
        public float Health { get; private set; }
        public float Cooldown { get; private set; }
        public int CompletedStages { get; private set; }
        public int Swings { get; private set; }
        public int WeakSpotHits { get; private set; }
        public int Perfects { get; private set; }
        public bool Completed { get { return CompletedStages == StageCount; } }
        public float Quality { get { return 0.3f + 0.7f * (Swings > 0 ? (float)WeakSpotHits / Swings : 0f); } }

        public NodeHarvestRun(float toolDamage, float difficulty)
        {
            if (!Finite(toolDamage) || toolDamage <= 0f) throw new ArgumentOutOfRangeException(nameof(toolDamage));
            if (!Finite(difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
            this.toolDamage = toolDamage;
            StageHealth = 2.6f + 2f * Math.Max(0f, Math.Min(1f, difficulty));
            Health = StageHealth;
            SwingSeconds = 0.55f / (float)Math.Sqrt(Math.Max(1f, toolDamage));
        }

        public void Advance(float seconds)
        {
            if (!Finite(seconds) || seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            Cooldown = Math.Max(0f, Cooldown - seconds);
        }

        public bool TryStrike(HarvestStrike strike)
        {
            if (strike < HarvestStrike.Miss || strike > HarvestStrike.Perfect)
                throw new ArgumentOutOfRangeException(nameof(strike));
            if (Completed || Cooldown > 0f) return false;

            Swings++;
            Cooldown = SwingSeconds;
            if (strike >= HarvestStrike.WeakSpot) WeakSpotHits++;
            if (strike == HarvestStrike.Perfect) Perfects++;
            float damage = strike == HarvestStrike.Perfect ? toolDamage * 1.7f
                : strike == HarvestStrike.WeakSpot ? toolDamage
                : strike == HarvestStrike.Glance ? toolDamage * 0.15f : 0f;
            Health = Math.Max(0f, Health - damage);
            if (Health <= 0f)
            {
                CompletedStages++;
                // Overkill never skips a break animation or the next weak-spot stage.
                if (!Completed) Health = StageHealth;
            }
            return true;
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
