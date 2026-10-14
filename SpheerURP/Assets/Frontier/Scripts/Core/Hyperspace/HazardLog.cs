namespace Frontier.Hyperspace
{
    public struct HazardEntry
    {
        public int step;
        public Hazard hazard;
        public EntityType source;
        public int uid;
    }

    /// <summary>A player-dependent threat: at lockStep the aim point is frozen on the ship's position; at arriveStep a hazard disc (centre = ship position at lockStep + offset) crosses the ship plane.</summary>
    public struct LockEntry
    {
        public int lockStep;
        public int arriveStep;
        public float offX, offY;
        public float radius;
        public int source;
    }

    /// <summary>
    /// Optional recorder the fairness solver attaches to a ghost run. The run appends every hazard that overlaps the ship plane (one entry per step) and every aimed threat.
    /// It grows on demand; it is a tool, not part of the steady-state simulation.
    /// </summary>
    public sealed class HazardLog
    {
        public HazardEntry[] hazards = new HazardEntry[2048];
        public int hazardCount;
        public LockEntry[] locks = new LockEntry[64];
        public int lockCount;

        public void Clear()
        {
            hazardCount = 0;
            lockCount = 0;
        }

        public void AddHazard(int step, in Hazard h, EntityType source, int uid)
        {
            if (hazardCount == hazards.Length) System.Array.Resize(ref hazards, hazards.Length * 2);
            hazards[hazardCount++] = new HazardEntry { step = step, hazard = h, source = source, uid = uid };
        }

        public void AddLock(int lockStep, int arriveStep, float offX, float offY, float radius, int source)
        {
            if (lockCount == locks.Length) System.Array.Resize(ref locks, locks.Length * 2);
            locks[lockCount++] = new LockEntry { lockStep = lockStep, arriveStep = arriveStep, offX = offX, offY = offY, radius = radius, source = source };
        }
    }
}
