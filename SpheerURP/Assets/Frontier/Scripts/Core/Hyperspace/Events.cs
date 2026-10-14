namespace Frontier.Hyperspace
{
    /// <summary>
    /// One thing that happened during a step, for FX, audio, haptics and HUD. Field use per type:
    /// EnemyHit: a = EntityType, b = uid, v = damage dealt, xyz = hit point. EnemyKilled: a = EntityType, b = uid, c = salvage dropped, v = score gained, xyz = position.
    /// PlayerHit: a = EntityType of the source (0 = bolt), v = hull damage, c = hull left (rounded), xyz = ship position. ShieldBreak: c = charges left.
    /// ShieldRecharged: c = charges. PickupCollected: a = PickupType, b = uid, c = loot count (crates), v = loot kind. BombUsed: c = bombs left, v = damage.
    /// GateTelegraph: b = uid, a = GateKind, v = seconds until the gate arrives, x = gap centre. TurretTelegraph: b = uid, xy = locked aim point, v = seconds to fire.
    /// BossPhase: a = phase (1-3), v = hull fraction. WaveCleared: a = wave index. RunEnded: a = Grade, b = reachedEnd (0/1), c = kills.
    /// PlayerDowned: c = revives used. Revived: c = revives used. OverdriveStart/End: v = seconds. SeekerLaunched / MineArmed / MineBlast / AsteroidSplit: b = uid.
    /// ComboBreak: c = combo lost. BossWarning: v = seconds until the boss arrives. BossDefeated: v = seconds the fight took. PickupSpawned: a = PickupType, b = uid.
    /// ShotFired: v = 1 (bullets are visible in HyperspaceRun.Bullets; this is only for audio and is emitted at most every few steps).
    /// </summary>
    public struct HEvent
    {
        public EventType type;
        /// <summary>Simulation step index (time = step / 60).</summary>
        public int step;
        public int a;
        public int b;
        public int c;
        public float x, y, z;
        public float v;
    }

    /// <summary>
    /// Fixed-size ring buffer of events. The simulation always pushes (the oldest event is dropped when the host does not drain; <see cref="Dropped"/> counts them);
    /// the running <see cref="Digest"/> covers every pushed event, so determinism checks do not depend on how often the host drains.
    /// </summary>
    public sealed class EventRing
    {
        public const int Capacity = 512;
        readonly HEvent[] items = new HEvent[Capacity];
        int head;
        int count;

        public int Count { get { return count; } }
        public int Dropped { get; private set; }
        public int TotalPushed { get; private set; }
        public ulong Digest { get; private set; }

        public void Reset()
        {
            head = 0;
            count = 0;
            Dropped = 0;
            TotalPushed = 0;
            Digest = 0x45564E54UL;
        }

        public void Push(EventType type, int step, int a, int b, int c, float x, float y, float z, float v)
        {
            if (count == Capacity)
            {
                head = (head + 1) & (Capacity - 1);
                count--;
                Dropped++;
            }
            int tail = (head + count) & (Capacity - 1);
            count++;
            items[tail] = new HEvent { type = type, step = step, a = a, b = b, c = c, x = x, y = y, z = z, v = v };
            TotalPushed++;
            ulong h = Digest;
            h = Frontier.Universe.Hash.Combine(h, ((ulong)(uint)step << 8) | (ulong)type);
            h = Frontier.Universe.Hash.Combine(h, ((ulong)(uint)a << 32) | (uint)b);
            h = Frontier.Universe.Hash.Combine(h, (ulong)(uint)c);
            h = Frontier.Universe.Hash.Combine(h, ((ulong)(uint)System.BitConverter.SingleToInt32Bits(x) << 32) | (uint)System.BitConverter.SingleToInt32Bits(y));
            h = Frontier.Universe.Hash.Combine(h, ((ulong)(uint)System.BitConverter.SingleToInt32Bits(z) << 32) | (uint)System.BitConverter.SingleToInt32Bits(v));
            Digest = h;
        }

        /// <summary>Copies the oldest event out and removes it. Returns false when empty.</summary>
        public bool TryDequeue(out HEvent e)
        {
            if (count == 0)
            {
                e = default(HEvent);
                return false;
            }
            e = items[head];
            head = (head + 1) & (Capacity - 1);
            count--;
            return true;
        }

        /// <summary>Reads without removing (index 0 is the oldest).</summary>
        public HEvent Peek(int index)
        {
            return items[(head + index) & (Capacity - 1)];
        }

        /// <summary>Moves up to dest.Length events into dest (oldest first) and returns how many. No allocation.</summary>
        public int Drain(HEvent[] dest)
        {
            int n = count < dest.Length ? count : dest.Length;
            for (int i = 0; i < n; i++) dest[i] = items[(head + i) & (Capacity - 1)];
            head = (head + n) & (Capacity - 1);
            count -= n;
            return n;
        }

        public void Clear()
        {
            head = 0;
            count = 0;
        }
    }
}
