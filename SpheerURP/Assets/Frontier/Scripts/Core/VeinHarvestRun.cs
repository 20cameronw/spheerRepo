using System;

namespace Frontier
{
    /// <summary>A descending cutter: read upcoming seams, steer into ore and avoid unstable rock.</summary>
    public sealed class VeinHarvestRun
    {
        public const int Columns = 5, Rows = 18;
        readonly int[,] cells = new int[Rows, Columns];
        readonly float rowSeconds;
        float clock;
        public int Column { get; private set; } = Columns / 2;
        public int Depth { get; private set; }
        public int Integrity { get; private set; } = 4;
        public int Ore { get; private set; }
        public int RichSeams { get; private set; }
        public bool Completed { get { return Depth >= Rows || Integrity <= 0; } }
        public float Progress { get { return clock / rowSeconds; } }
        public float Quality { get { return Math.Min(1f, Ore / (Rows * 3f)); } }

        public VeinHarvestRun(int seed, float difficulty, float toolPower)
        {
            HarvestInput.Finite(difficulty, nameof(difficulty));
            HarvestInput.Positive(toolPower, nameof(toolPower));
            difficulty = Math.Min(1f, Math.Max(0f, difficulty));
            var random = new Random(seed);
            rowSeconds = 1.6f - difficulty * 0.4f + Math.Min(0.3f, Math.Max(0, toolPower - 1f) * 0.03f);
            int safe = Column;
            for (int row = 0; row < Rows; row++)
            {
                safe = Math.Max(0, Math.Min(Columns - 1, safe + random.Next(-1, 2)));
                for (int column = 0; column < Columns; column++)
                {
                    int roll = random.Next(100);
                    cells[row, column] = roll < 30 + difficulty * 20 ? -1 : roll < 75 ? 1 : 3;
                }
                cells[row, safe] = 3;
            }
        }

        public int Cell(int row, int column)
        {
            if (row < 0 || row >= Rows || column < 0 || column >= Columns)
                throw new ArgumentOutOfRangeException();
            return cells[row, column];
        }

        public void Steer(int direction)
        {
            if (!Completed) Column = Math.Max(0, Math.Min(Columns - 1, Column + Math.Sign(direction)));
        }

        public void Advance(float seconds)
        {
            HarvestInput.Seconds(seconds);
            if (Completed) return;
            clock += seconds;
            while (!Completed && clock >= rowSeconds)
            {
                clock -= rowSeconds;
                int value = cells[Depth++, Column];
                if (value < 0) Integrity--;
                else
                {
                    Ore += value;
                    if (value == 3) RichSeams++;
                }
            }
            if (Completed) clock = 0f;
        }
    }
}
