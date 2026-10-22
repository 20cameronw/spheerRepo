using System;

namespace Frontier
{
    /// <summary>Pressure, contact and yield rules for drilling, independent of rendering and input devices.</summary>
    public sealed class DrillHarvestRun
    {
        public const float OreRadius = 0.95f, OreCenter = 0.2f, Lift = 1.5f;
        readonly float toolDamage, difficulty;
        float velocity;

        public float Elapsed { get; private set; }
        public float Pressure { get; private set; } = 0.1f;
        public float InBandSeconds { get; private set; }
        public float Progress { get; private set; }
        public float BandCenter { get; private set; } = 0.5f;
        public float BandHalfWidth { get; }
        public float TipY { get; private set; }
        public float ContactHeight { get; private set; }
        public bool Inside { get; private set; }
        public bool Contact { get; private set; }
        public bool Completed { get { return Progress >= 1f; } }
        public float OreScale { get { return 1f - 0.55f * Progress; } }
        public float OreTop { get { return OreCenter + OreRadius * 0.9f * OreScale; } }
        public float Quality { get { return Clamp01(0.3f + 0.7f * InBandSeconds / Math.Max(0.01f, Elapsed)); } }

        public DrillHarvestRun(float toolDamage, float difficulty, float tolerance)
        {
            if (!Finite(toolDamage) || toolDamage <= 0f) throw new ArgumentOutOfRangeException(nameof(toolDamage));
            if (!Finite(difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
            if (!Finite(tolerance) || tolerance <= 0f) throw new ArgumentOutOfRangeException(nameof(tolerance));
            this.toolDamage = toolDamage;
            this.difficulty = Clamp01(difficulty);
            BandHalfWidth = (0.16f - 0.09f * this.difficulty) * tolerance;
            ContactHeight = OreTop;
            TipY = OreTop + Lift;
        }

        public void Advance(float seconds, bool press)
        {
            if (!Finite(seconds) || seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (Completed || seconds == 0f) return;
            Elapsed += seconds;
            float target = press ? 0.7f : -0.8f;
            velocity += (target - velocity) * Clamp01(5f * seconds);
            Pressure = Clamp01(Pressure + velocity * seconds);
            if (Pressure <= 0f || Pressure >= 1f) velocity = 0f;
            BandCenter = 0.5f + 0.3f * (float)Math.Sin(Elapsed * (0.6f + difficulty))
                + 0.08f * (float)Math.Sin(Elapsed * 2.1f);
            Inside = Math.Abs(Pressure - BandCenter) <= BandHalfWidth;

            ContactHeight = OreTop;
            float goalY = press ? ContactHeight - 0.08f : ContactHeight + Lift;
            TipY += (goalY - TipY) * (1f - (float)Math.Exp(-(press ? 22f : 7f) * seconds));
            Contact = press && TipY <= ContactHeight + 0.04f;
            if (Contact && Inside)
            {
                InBandSeconds += seconds;
                Progress = Clamp01(Progress + seconds * 0.11f * toolDamage / (0.6f + difficulty));
            }
        }

        static float Clamp01(float value) { return Math.Max(0f, Math.Min(1f, value)); }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
