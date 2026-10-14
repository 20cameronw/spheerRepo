using Frontier.Universe;

namespace Frontier.Hyperspace
{
    public sealed partial class HyperspaceRun
    {
        /// <summary>True once the run ended by flying the whole route (also after revives); false when abandoned.</summary>
        public bool ReachedEnd;

        public static Grade GradeFor(float quality, HyperspaceTuning t)
        {
            if (quality >= t.gradeS) return Grade.S;
            if (quality >= t.gradeA) return Grade.A;
            if (quality >= t.gradeB) return Grade.B;
            return Grade.C;
        }

        /// <summary>Quality 0..1 of the run so far: kills, combo, pickups and a clean-run bonus, minus a penalty per hit and per revive.</summary>
        public float ComputeQuality()
        {
            HyperspaceTuning t = Tuning;
            double killRatio = KillableSpawned > 0 ? HMath.Clamp01((double)(float)Kills / KillableSpawned / t.killRatioForFull) : 1f;
            double comboNorm = HMath.Clamp01((double)(float)MaxCombo / t.comboForFullBonus);
            double pickupRatio = PickupsSpawned > 0 ? HMath.Clamp01((double)(float)PickupsCollected / PickupsSpawned / t.pickupRatioForFull) : 1f;
            bool clean = HitsTaken == 0;
            int shieldHits = HitsTaken - HullHits;
            double q = t.qualityBase + t.qualityKills * killRatio + t.qualityCombo * comboNorm + t.qualityPickups * pickupRatio + (clean ? t.qualityClean : 0f);
            double penalty = (double)shieldHits * 0.4 * t.qualityPerHit + (double)HullHits * t.qualityPerHit;
            if (penalty > t.qualityHitCap) penalty = t.qualityHitCap;
            q -= penalty + (double)t.qualityPerRevive * RevivesUsed;
            if (Route.run.mode == RunMode.Assist && q > t.assistQualityCap) q = t.assistQualityCap;
            if (Route.run.mode == RunMode.Challenge) q += t.challengeQualityBonus;
            return (float)HMath.Clamp01(q);
        }

        /// <summary>Builds the result from the current state. Final once State == Ended; earlier calls give a preview (reachedEnd false).</summary>
        public RunResult BuildResult()
        {
            return BuildResult(State == RunState.Ended && ReachedEnd);
        }

        RunResult BuildResult(bool reachedEnd)
        {
            if (State == RunState.Ended || reachedEnd) ReachedEnd = reachedEnd;
            HyperspaceTuning t = Tuning;
            double q = ComputeQuality();
            bool forfeited = Abandoned || (State == RunState.Downed);
            var r = new RunResult();
            r.quality = (float)q;
            r.grade = GradeFor((float)q, t);
            r.yieldMultiplier = (float)(t.yieldBase + t.yieldPerQuality * q);
            r.killCount = Kills;
            r.hitsTaken = HitsTaken;
            r.maxCombo = MaxCombo;
            r.cleanRun = HitsTaken == 0 && reachedEnd;
            r.reachedEnd = reachedEnd;
            r.revivesUsed = RevivesUsed;
            r.bossDefeated = BossDefeated;
            r.defeated = forfeited;
            r.pickupsCollected = PickupsCollected;
            r.score = Score;
            r.elapsedSeconds = (float)((double)StepIndex * Dt);
            r.durationSeconds = Route.duration;
            r.fuelRefundFraction = (float)(forfeited ? 0f : HMath.Clamp01(HMath.Pow(q, t.refundQualityPower) + FuelBonus));
            r.xp = forfeited ? 0 : (int)((double)Kills * t.xpPerKill * (1.0 + (double)t.xpPerTier * Route.tier) * (0.5 + q) + 0.5);
            int n = 0;
            for (int i = 0; i < 3; i++) if (lootCounts[i] > 0) n++;
            r.lootList = new LootDrop[forfeited ? 0 : n];
            if (!forfeited)
            {
                int k = 0;
                for (int i = 0; i < 3; i++)
                    if (lootCounts[i] > 0) r.lootList[k++] = new LootDrop { kind = (LootKind)i, count = lootCounts[i] };
            }
            r.digest = StateDigest();
            return r;
        }

        public int LootCount(LootKind kind) { return lootCounts[(int)kind]; }

        // ---------------------------------------------------------------- digest
        static ulong Bits(float v) { return (ulong)(uint)System.BitConverter.SingleToInt32Bits(v); }

        /// <summary>Hash of the complete simulation state plus the whole event history: equal digests mean identical runs. Allocation free.</summary>
        public ulong StateDigest()
        {
            ulong h = Hash.Combine(0x48595052UL, (ulong)(uint)StepIndex, (ulong)State, Events.Digest);
            ref PlayerState p = ref Player;
            h = Hash.Combine(h, Bits(p.x), Bits(p.y), Bits(p.hull));
            h = Hash.Combine(h, (ulong)(uint)p.shield, (ulong)(uint)p.bombs, (ulong)(uint)p.combo);
            h = Hash.Combine(h, (ulong)(uint)Kills, (ulong)(uint)HitsTaken, (ulong)(uint)Score);
            h = Hash.Combine(h, (ulong)(uint)lootCounts[0], (ulong)(uint)lootCounts[1], (ulong)(uint)lootCounts[2]);
            for (int i = 0; i < EnemyHigh; i++)
            {
                ref Enemy e = ref Enemies[i];
                if (!e.active) continue;
                h = Hash.Combine(h, (ulong)(uint)e.uid, ((ulong)(byte)e.type << 8) | e.variant, Bits(e.x));
                h = Hash.Combine(h, Bits(e.y), Bits(e.z), Bits(e.hp));
            }
            for (int i = 0; i < BoltHigh; i++)
            {
                ref Bolt b = ref Bolts[i];
                if (!b.active) continue;
                h = Hash.Combine(h, (ulong)(uint)b.uid, Bits(b.x), Bits(b.y));
                h = Hash.Combine(h, Bits(b.z), 0UL);
            }
            for (int i = 0; i < PickupHigh; i++)
            {
                ref Pickup k = ref Pickups[i];
                if (!k.active) continue;
                h = Hash.Combine(h, (ulong)(uint)k.uid, Bits(k.x), Bits(k.y));
            }
            for (int i = 0; i < BulletHigh; i++)
            {
                ref Bullet bu = ref Bullets[i];
                if (!bu.active) continue;
                h = Hash.Combine(h, Bits(bu.x), Bits(bu.y), Bits(bu.z));
            }
            return h;
        }
    }
}
