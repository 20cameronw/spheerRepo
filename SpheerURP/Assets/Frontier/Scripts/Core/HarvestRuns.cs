using System;

namespace Frontier
{
    public sealed class SiphonHarvestRun
    {
        public const int PulseCount = 7;
        readonly float difficulty;
        float qualitySum;

        public float Elapsed { get; private set; }
        public float Cooldown { get; private set; }
        public float Needle { get; private set; }
        public float Target { get; private set; }
        public float BandWidth { get; }
        public int Pulses { get; private set; }
        public int Perfects { get; private set; }
        public bool Completed { get { return Pulses == PulseCount; } }
        public float Quality { get { return qualitySum / PulseCount; } }

        public SiphonHarvestRun(float difficulty, float tolerance)
        {
            HarvestInput.Finite(difficulty, nameof(difficulty));
            HarvestInput.Positive(tolerance, nameof(tolerance));
            this.difficulty = Math.Max(0f, Math.Min(1f, difficulty));
            BandWidth = Math.Max(0.065f, Math.Min(0.23f, 0.15f * tolerance - this.difficulty * 0.035f));
            UpdatePositions();
        }

        public void Advance(float seconds)
        {
            HarvestInput.Seconds(seconds);
            if (Completed) return;
            Elapsed += seconds;
            Cooldown = Math.Max(0f, Cooldown - seconds);
            UpdatePositions();
        }

        public bool TryPump(out float hit)
        {
            hit = 0f;
            if (Completed || Cooldown > 0f) return false;
            Cooldown = 0.22f;
            hit = Math.Max(0f, Math.Min(1f, 1f - Math.Abs(Needle - Target) / Math.Max(0.01f, BandWidth * 2.2f)));
            qualitySum += hit;
            if (hit >= 0.78f) Perfects++;
            Pulses++;
            return true;
        }

        void UpdatePositions()
        {
            Needle = 0.5f + 0.43f * (float)Math.Sin(Elapsed * (1.2f + difficulty * 1.5f));
            Target = 0.5f + 0.31f * (float)Math.Sin(Elapsed * (0.68f + difficulty * 0.3f) + 1.1f);
        }
    }

    public sealed class ExcavationHarvestRun
    {
        public const int GridSide = 3, CellCount = GridSide * GridSide, SeamCount = 3, MaxPings = 18;
        readonly Random random;
        readonly bool[] revealed = new bool[CellCount];
        public int Target { get; private set; }
        public int Pings { get; private set; }
        public int SeamsFound { get; private set; }
        public bool Completed { get { return SeamsFound == SeamCount || Pings == MaxPings; } }
        public float Quality { get { return SeamsFound == 0 ? 0f : 0.2f + 0.8f * SeamsFound / SeamCount; } }

        public ExcavationHarvestRun(int seed)
        {
            random = new Random(seed);
            Target = random.Next(CellCount);
        }

        public bool IsRevealed(int cell)
        {
            ValidateCell(cell);
            return revealed[cell];
        }

        public bool TryPing(int cell, out int distance)
        {
            ValidateCell(cell);
            distance = -1;
            if (Completed || revealed[cell]) return false;
            revealed[cell] = true;
            Pings++;
            distance = Math.Abs(cell / GridSide - Target / GridSide) + Math.Abs(cell % GridSide - Target % GridSide);
            if (distance == 0)
            {
                SeamsFound++;
                if (!Completed)
                {
                    Target = random.Next(CellCount);
                    Array.Clear(revealed, 0, revealed.Length);
                }
            }
            return true;
        }

        static void ValidateCell(int cell)
        {
            if (cell < 0 || cell >= CellCount) throw new ArgumentOutOfRangeException(nameof(cell));
        }
    }

    public sealed class SalvageHarvestRun
    {
        public const int TargetCount = 6, MaxCasts = 10;
        readonly bool[] captured = new bool[TargetCount];
        public int Casts { get; private set; }
        public int Captured { get; private set; }
        public bool Completed { get { return Captured == TargetCount || Casts == MaxCasts; } }
        public float Quality { get { return Captured == 0 ? 0f : 0.2f + 0.8f * Captured / TargetCount; } }

        public bool IsCaptured(int target)
        {
            ValidateTarget(target);
            return captured[target];
        }

        // -1 is a cast that missed all targets; already collected targets cannot be collected twice.
        public bool TryCast(int target)
        {
            if (target < -1 || target >= TargetCount) throw new ArgumentOutOfRangeException(nameof(target));
            if (Completed || (target >= 0 && captured[target])) return false;
            Casts++;
            if (target >= 0)
            {
                captured[target] = true;
                Captured++;
            }
            return true;
        }

        static void ValidateTarget(int target)
        {
            if (target < 0 || target >= TargetCount) throw new ArgumentOutOfRangeException(nameof(target));
        }
    }

    static class HarvestInput
    {
        public static void Finite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }

        public static void Positive(float value, string name)
        {
            Finite(value, name);
            if (value <= 0f) throw new ArgumentOutOfRangeException(name);
        }

        public static void Seconds(float value)
        {
            Finite(value, nameof(value));
            if (value < 0f) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
