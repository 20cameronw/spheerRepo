using System;
using System.Collections.Generic;

namespace Frontier
{
    public static class HarvestEquipmentEffects
    {
        public static float DrillCooling(int level)
        {
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
            return 0.38f + Math.Min(20, level) * 0.045f;
        }
        public static float DrillHeatResistance(int level)
        {
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
            return 1f / (1f + level * 0.06f);
        }
    }

    public enum CutDirection { Left, Up, Right, Down }

    public sealed class DirectionalCutRun
    {
        public const int CutCount = 8, MaxAttempts = 16;
        readonly CutDirection[] directions = new CutDirection[CutCount];
        float score;
        public int Cuts { get; private set; }
        public int Attempts { get; private set; }
        public int Perfects { get; private set; }
        public float Elapsed { get; private set; }
        public CutDirection Direction { get { return directions[Math.Min(Cuts, CutCount - 1)]; } }
        public bool Completed { get { return Cuts >= CutCount || Attempts >= MaxAttempts || Elapsed >= 45f; } }
        public float Quality { get { return score / CutCount; } }

        public DirectionalCutRun(int seed)
        {
            var random = new Random(seed);
            for (int i = 0; i < directions.Length; i++)
                directions[i] = (CutDirection)((i == 0 ? random.Next(4) : (int)directions[i - 1] + random.Next(1, 4)) % 4);
        }

        public void Advance(float seconds) { HarvestInput.Seconds(seconds); if (!Completed) Elapsed += seconds; }

        public bool Cut(float x, float y, out float precision)
        {
            HarvestInput.Finite(x, nameof(x)); HarvestInput.Finite(y, nameof(y));
            precision = 0f;
            if (Completed || x * x + y * y < 0.0225f) return false;
            Attempts++;
            float length = (float)Math.Sqrt(x * x + y * y);
            float alignment = Direction == CutDirection.Left ? -x / length : Direction == CutDirection.Right ? x / length
                : Direction == CutDirection.Up ? -y / length : y / length;
            if (alignment < 0.72f) return true;
            precision = Math.Min(1f, (alignment - 0.72f) / 0.28f);
            score += 0.5f + precision * 0.5f;
            if (precision >= 0.85f) Perfects++;
            Cuts++;
            return true;
        }
    }

    public sealed class ConnectedOreRun
    {
        public const int Side = 5, Count = Side * Side, MaxStrikes = 14;
        readonly int[] cells = new int[Count];
        readonly float[] hardness = new float[Count];
        readonly float power;
        readonly int available;
        public int Strikes { get; private set; }
        public int Ore { get; private set; }
        public int Chains { get; private set; }
        public int Faults { get; private set; }
        public int LastChain { get; private set; }
        public float Elapsed { get; private set; }
        public bool Completed { get { return Strikes >= MaxStrikes || Ore >= available || Elapsed >= 50f; } }
        public float Quality { get { return Math.Max(0f, Math.Min(1f, (float)Ore / available - Faults * 0.06f)); } }

        public ConnectedOreRun(int seed, float power)
        {
            HarvestInput.Positive(power, nameof(power)); this.power = power;
            var random = new Random(seed);
            int count = 0;
            for (int y = 0; y < Side; y++)
                for (int x = 0; x < Side; x++)
                {
                    int i = y * Side + x;
                    cells[i] = x == 4 ? (y % 2 == 0 ? -1 : 0) : 1 + ((x / 2 + y / 2) % 2);
                    hardness[i] = cells[i] > 0 ? 2f + random.Next(2) : 3f;
                    if (cells[i] > 0) count++;
                }
            available = count;
        }

        public int Cell(int cell) { Validate(cell); return cells[cell]; }
        public float Hardness(int cell) { Validate(cell); return hardness[cell]; }
        public void Advance(float seconds) { HarvestInput.Seconds(seconds); if (!Completed) Elapsed += seconds; }

        public int[] Connected(int cell)
        {
            Validate(cell);
            if (cells[cell] <= 0) return new[] { cell };
            var found = new List<int> { cell };
            for (int next = 0; next < found.Count; next++)
            {
                int i = found[next];
                foreach (int neighbor in Neighbors(i))
                    if (cells[neighbor] == cells[cell] && !found.Contains(neighbor)) found.Add(neighbor);
            }
            return found.ToArray();
        }

        public bool Break(int cell)
        {
            Validate(cell); LastChain = 0;
            if (Completed || cells[cell] == -2) return false;
            Strikes++;
            hardness[cell] = Math.Max(0f, hardness[cell] - power);
            if (hardness[cell] > 0f) return true;
            int value = cells[cell];
            var connected = Connected(cell);
            foreach (int i in connected) { cells[i] = -2; hardness[i] = 0f; }
            if (value > 0) { LastChain = connected.Length; Ore += LastChain; if (LastChain >= 3) Chains++; }
            else if (value < 0) { Faults++; Strikes = Math.Min(MaxStrikes, Strikes + 2); }
            return true;
        }

        internal static IEnumerable<int> Neighbors(int cell)
        {
            if (cell % Side > 0) yield return cell - 1;
            if (cell % Side < Side - 1) yield return cell + 1;
            if (cell >= Side) yield return cell - Side;
            if (cell < Count - Side) yield return cell + Side;
        }
        static void Validate(int cell) { if (cell < 0 || cell >= Count) throw new ArgumentOutOfRangeException(nameof(cell)); }
    }

    public sealed class SteeredDrillRun
    {
        public const int Columns = 5, Layers = 18;
        readonly int[,] layers = new int[Layers, Columns];
        readonly float cooling, heatRate;
        float score;
        public float Position { get; private set; } = 2f;
        public int Column { get { return Math.Max(0, Math.Min(Columns - 1, (int)Math.Round(Position))); } }
        public int Depth { get; private set; }
        public float Progress { get; private set; }
        public float Heat { get; private set; }
        public bool Overheated { get; private set; }
        public float Elapsed { get; private set; }
        public int RichLayers { get; private set; }
        public int Collisions { get; private set; }
        public bool Thrusting { get; private set; }
        public bool Completed { get { return Depth >= Layers || Elapsed >= 65f; } }
        public float Quality { get { return Math.Max(0f, Math.Min(1f, score / (Layers * 2f) - Collisions * 0.02f)); } }
        public float Cooling { get { return cooling; } }

        public SteeredDrillRun(int seed, int toolLevel, float difficulty)
        {
            HarvestInput.Finite(difficulty, nameof(difficulty));
            if (toolLevel < 0) throw new ArgumentOutOfRangeException(nameof(toolLevel));
            cooling = HarvestEquipmentEffects.DrillCooling(toolLevel);
            heatRate = (0.22f + Math.Max(0f, Math.Min(1f, difficulty)) * 0.08f) * HarvestEquipmentEffects.DrillHeatResistance(toolLevel);
            var random = new Random(seed);
            for (int row = 0; row < Layers; row++)
            {
                int rich = random.Next(Columns), fault = (rich + random.Next(1, Columns)) % Columns;
                for (int col = 0; col < Columns; col++) layers[row, col] = col == rich ? 3 : col == fault ? -1 : 1;
            }
        }
        public int Cell(int depth, int column)
        {
            if (depth < 0 || depth >= Layers || column < 0 || column >= Columns) throw new ArgumentOutOfRangeException();
            return layers[depth, column];
        }
        public void Aim(float position)
        {
            HarvestInput.Finite(position, nameof(position));
            if (Completed) return;
            int previous = Column;
            Position = Math.Max(0f, Math.Min(Columns - 1f, position));
            if (Column != previous) Progress = 0f;
        }
        public void Advance(float seconds, float steering, bool thrust, bool boost)
        {
            HarvestInput.Seconds(seconds); HarvestInput.Finite(steering, nameof(steering));
            while (seconds > 0f && !Completed)
            {
                float dt = Math.Min(0.02f, seconds); seconds = Math.Max(0f, seconds - dt);
                Elapsed += dt;
                Aim(Position + Math.Max(-1f, Math.Min(1f, steering)) * dt * 3f);
                int value = layers[Depth, Column];
                Thrusting = thrust && !Overheated;
                Heat = Math.Max(0f, Math.Min(1f, Heat + dt * (Thrusting
                    ? heatRate * (boost ? 2f : 1f) * (value < 0 ? 1.8f : 1f) : -cooling)));
                if (Heat >= 1f) { Overheated = true; Thrusting = false; }
                if (Overheated && Heat <= 0.3f) Overheated = false;
                if (!Thrusting) continue;
                Progress += dt * (boost ? 1.6f : 0.8f) / (value < 0 ? 2.2f : 1f);
                if (Progress >= 1f)
                {
                    Progress -= 1f; Depth++;
                    if (value == 3) { RichLayers++; score += 2f; }
                    else if (value > 0) score += 1f;
                    else Collisions++;
                }
            }
        }
        public void CancelInput() { Thrusting = false; }
    }

    public sealed class FlowBalanceRun
    {
        // Bits run clockwise from north. This route climbs above the blocked central manifold.
        readonly int[] pipes = { 6, 10, 12, 9, 0, 3, 3, 10, 9 };
        public const int Side = 3, Count = 9;
        public float Valve { get; private set; } = 0.5f;
        public float Pressure { get; private set; }
        public float Demand { get; private set; } = 0.55f;
        public float Delivered { get; private set; }
        public float Elapsed { get; private set; }
        public float BalancedSeconds { get; private set; }
        public bool Connected { get; private set; }
        public int Leaks { get; private set; }
        public bool Completed { get { return Delivered >= 6f || Elapsed >= 50f; } }
        public float Quality { get { return Math.Min(1f, Delivered / 6f) * (0.5f + 0.5f *
            Math.Min(1f, BalancedSeconds / 8f)); } }

        public FlowBalanceRun(int seed)
        {
            var random = new Random(seed);
            for (int i = 0; i < pipes.Length; i++)
                for (int turn = random.Next(4); turn > 0; turn--) pipes[i] = RotateMask(pipes[i]);
            Evaluate();
        }
        public int Pipe(int cell) { Validate(cell); return pipes[cell]; }
        public bool Rotate(int cell)
        {
            Validate(cell);
            if (Completed || pipes[cell] == 0) return false;
            pipes[cell] = RotateMask(pipes[cell]); Evaluate(); return true;
        }
        public void SetValve(float valve)
        {
            HarvestInput.Finite(valve, nameof(valve));
            if (!Completed) Valve = Math.Max(0f, Math.Min(1f, valve));
        }
        public void Advance(float seconds)
        {
            HarvestInput.Seconds(seconds);
            while (seconds > 0f && !Completed)
            {
                float dt = Math.Min(0.02f, seconds); seconds = Math.Max(0f, seconds - dt);
                Elapsed += dt;
                Demand = 0.55f + 0.18f * (float)Math.Sin(Elapsed * 0.65f);
                Pressure += ((Connected && Leaks == 0 ? Valve : 0f) - Pressure) * Math.Min(1f, dt * 3f);
                bool balanced = Connected && Leaks == 0 && Math.Abs(Pressure - Demand) <= 0.12f;
                if (balanced) { BalancedSeconds += dt; Delivered = Math.Min(6f, Delivered + dt * 0.65f); }
            }
        }
        void Evaluate()
        {
            Leaks = 0; Connected = false;
            if ((pipes[3] & 8) == 0) { Leaks = 1; return; }
            var reached = new List<int> { 3 };
            int[] bits = { 1, 2, 4, 8 }, offsets = { -3, 1, 3, -1 };
            for (int cursor = 0; cursor < reached.Count; cursor++)
            {
                int cell = reached[cursor];
                for (int direction = 0; direction < 4; direction++)
                {
                    if ((pipes[cell] & bits[direction]) == 0) continue;
                    if (cell == 3 && direction == 3) continue;
                    if (cell == 5 && direction == 1) { Connected = true; continue; }
                    bool edge = direction == 0 ? cell < 3 : direction == 2 ? cell >= 6
                        : direction == 1 ? cell % 3 == 2 : cell % 3 == 0;
                    int neighbor = cell + offsets[direction];
                    if (edge || (pipes[neighbor] & bits[(direction + 2) % 4]) == 0) { Leaks++; continue; }
                    if (!reached.Contains(neighbor)) reached.Add(neighbor);
                }
            }
        }
        static int RotateMask(int mask) { return ((mask << 1) & 15) | (mask >> 3); }
        static void Validate(int cell) { if (cell < 0 || cell >= Count) throw new ArgumentOutOfRangeException(nameof(cell)); }
    }

    public sealed class BuriedFindRun
    {
        public const int Side = 5, Count = 25, FindCount = 6, MaxActions = 28;
        readonly int[] soil = new int[Count];
        readonly bool[] finds = new bool[Count], recovered = new bool[Count];
        public int Actions { get; private set; }
        public int Scans { get; private set; }
        public int Found { get; private set; }
        public int Damaged { get; private set; }
        public float Elapsed { get; private set; }
        public bool Completed { get { return Found + Damaged >= FindCount || Actions >= MaxActions || Elapsed >= 60f; } }
        public float Quality { get { return (Found + Damaged * 0.25f) / FindCount; } }
        public BuriedFindRun(int seed)
        {
            var random = new Random(seed);
            for (int i = 0; i < Count; i++) soil[i] = 2 + random.Next(2);
            int[] starts = { random.Next(0, 3), 10 + random.Next(0, 3), 20 + random.Next(0, 3) };
            foreach (int start in starts) { finds[start] = true; finds[start + 1] = true; }
        }
        public int Soil(int cell) { Validate(cell); return soil[cell]; }
        public bool Recovered(int cell) { Validate(cell); return recovered[cell]; }
        public bool Exposed(int cell) { Validate(cell); return soil[cell] == 0 && finds[cell]; }
        public void Advance(float seconds) { HarvestInput.Seconds(seconds); if (!Completed) Elapsed += seconds; }
        public int Scan(int cell)
        {
            Validate(cell); if (Completed || Scans >= 3) return -1;
            Scans++;
            int distance = Count;
            for (int i = 0; i < Count; i++)
                if (finds[i] && !recovered[i])
                    distance = Math.Min(distance, Math.Abs(cell % Side - i % Side) + Math.Abs(cell / Side - i / Side));
            return distance;
        }
        public bool Clear(int cell, bool dig)
        {
            Validate(cell); if (Completed || soil[cell] == 0) return false;
            Actions++;
            bool damage = dig && soil[cell] == 1 && finds[cell];
            soil[cell] = Math.Max(0, soil[cell] - (dig ? 2 : 1));
            if (soil[cell] == 0 && finds[cell])
            {
                recovered[cell] = true;
                if (damage) Damaged++; else Found++;
            }
            return true;
        }
        static void Validate(int cell) { if (cell < 0 || cell >= Count) throw new ArgumentOutOfRangeException(nameof(cell)); }
    }

    public enum SalvagePart { Metal, Circuit, Scrap, Hazard }
    public enum SalvageBin { Metal, Circuit, Reject }
    public sealed class SortingSalvageRun
    {
        public const int PartCount = 18;
        readonly SalvagePart[] parts = new SalvagePart[PartCount];
        readonly int useful;
        public int Sorted { get; private set; }
        public int Saved { get; private set; }
        public int Mistakes { get; private set; }
        public int Chain { get; private set; }
        public int BestChain { get; private set; }
        public float Remaining { get; private set; } = 3f;
        public float Elapsed { get; private set; }
        public SalvagePart Current { get { return parts[Math.Min(Sorted, PartCount - 1)]; } }
        public SalvagePart Next { get { return parts[Math.Min(Sorted + 1, PartCount - 1)]; } }
        public bool Completed { get { return Sorted >= PartCount; } }
        public float Quality { get { return Math.Max(0f, Math.Min(1f, (float)Saved / useful - Mistakes * 0.025f)); } }
        public SortingSalvageRun(int seed)
        {
            var random = new Random(seed); int count = 0;
            for (int i = 0; i < PartCount; i++)
            {
                parts[i] = (SalvagePart)random.Next(4);
                if (parts[i] == SalvagePart.Metal || parts[i] == SalvagePart.Circuit) count++;
            }
            // Always contain useful parts, even when a random seed rolls only waste.
            if (count == 0) { parts[0] = SalvagePart.Circuit; count = 1; }
            useful = count;
        }
        public bool Sort(SalvageBin bin)
        {
            if (!Enum.IsDefined(typeof(SalvageBin), bin)) throw new ArgumentOutOfRangeException(nameof(bin));
            if (Completed) return false;
            bool match = Current == SalvagePart.Metal ? bin == SalvageBin.Metal
                : Current == SalvagePart.Circuit ? bin == SalvageBin.Circuit : bin == SalvageBin.Reject;
            if (match)
            {
                if (bin != SalvageBin.Reject) Saved++;
                Chain++; BestChain = Math.Max(BestChain, Chain);
            }
            else { Mistakes++; Chain = 0; }
            Sorted++; Remaining = Math.Max(1.6f, 3f - Sorted * 0.065f);
            return match;
        }
        public void Advance(float seconds)
        {
            HarvestInput.Seconds(seconds);
            while (seconds > 0f && !Completed)
            {
                float step = Math.Min(seconds, Remaining);
                seconds = Math.Max(0f, seconds - step); Remaining -= step; Elapsed += step;
                if (Remaining <= 0f) { Mistakes++; Chain = 0; Sorted++; Remaining = Math.Max(1.6f, 3f - Sorted * 0.065f); }
            }
        }
    }
}
