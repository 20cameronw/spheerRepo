namespace Frontier.Hyperspace
{
    public enum PacingKind : byte
    {
        Intro = 0,
        Calm = 1,
        Build = 2,
        Peak = 3,
        Relax = 4,
        Boss = 5,
        Outro = 6
    }

    /// <summary>One stretch of the pacing curve. rate is the director's budget income in threat points per second.</summary>
    public struct PacingSegment
    {
        public PacingKind kind;
        public int wave;
        public float start, end;
        public float rate;
    }

    /// <summary>
    /// Everything about a route that follows from RunParams alone: length, difficulty index, budget, pacing curve, speed profile and enemy scaling.
    /// A pure function of (RunParams, ShipStats, tuning): the same inputs always give the same RouteSpec, so a headless bot or a test can rebuild it.
    /// </summary>
    public sealed class RouteSpec
    {
        public RunParams run;
        public int tier;
        /// <summary>Longest possible run in seconds (a Guardian fight ends earlier when the boss is destroyed).</summary>
        public float duration;
        public int totalSteps;
        /// <summary>End of the wave phase (all non-boss encounters arrive before this).</summary>
        public float waveEnd;
        public bool hasBoss;
        public float bossStart;
        /// <summary>Difficulty index: product of the tier, danger, distance and power factors (1 = first hop with a starter ship); monotonic in every input.</summary>
        public float difficulty;
        /// <summary>Budget income at a pacing multiplier of 1 (threat points per second).</summary>
        public float baseRate;
        public float budgetTotal;
        public float enemyHpScale;
        public float enemyDamageScale;
        public float cruiseSpeed;
        public float shipPower;
        public float bossHp;
        public PacingSegment[] segments;
        /// <summary>World scroll speed per step (u/s).</summary>
        public float[] speed;

        public static RouteSpec Create(RunParams p, ShipStats ship, HyperspaceTuning t)
        {
            var r = new RouteSpec();
            r.run = p;
            int tier = HMath.Clamp(p.tier, 0, 12);
            r.tier = tier;
            double dist = HMath.Max(0f, HMath.Finite(p.distanceLy, 0f));
            double danger = HMath.Max(0f, HMath.Finite(p.danger, 0f));
            double power = p.shipPower > 0f ? HMath.Finite(p.shipPower, 1f) : t.ShipPower(ship);
            r.shipPower = (float)power;

            double distN = HMath.Clamp01(HMath.Log10(1.0 + dist) / HMath.Log10(1.0 + (double)t.distanceRefLy));
            double baseDuration = HMath.Clamp(t.durationBase + t.durationScale * HMath.Pow(dist, t.durationExp), t.durationMin, t.durationMax);
            double requestedDuration = HMath.Finite(p.plannedDurationSeconds, 0f);
            bool hasRequestedDuration = requestedDuration > 0.0;
            if (hasRequestedDuration) baseDuration = HMath.Clamp(requestedDuration, t.durationMin, t.durationMax);
            r.hasBoss = dist >= t.bossMinDistanceLy || tier >= t.bossMinTier;

            double modeBudget = p.mode == RunMode.Assist ? t.assistBudgetScale : (p.mode == RunMode.Challenge ? t.challengeBudgetScale : 1f);
            double tierMul = 1.0 + (double)t.budgetPerTier * tier;
            double dangerMul = 1.0 + t.budgetPerDanger * danger;
            double distMul = 1.0 + t.budgetDistanceSpan * distN;
            double powerMul = 1f + t.budgetPerPowerLog * HMath.Clamp(HMath.Log2(HMath.Max(1f, power)), 0f, t.powerLogCap);
            r.difficulty = (float)(tierMul * dangerMul * distMul * powerMul * modeBudget);
            r.baseRate = (float)((double)t.budgetRateBase * r.difficulty);
            r.enemyHpScale = (float)(1.0 + (double)t.hpPerTier * tier + t.hpPerDanger * danger);
            r.enemyDamageScale = (float)((1.0 + (double)t.damagePerTier * tier) * (p.mode == RunMode.Assist ? t.assistDamageScale : 1f));
            r.cruiseSpeed = (float)(t.speedBase + (double)t.speedPerTier * tier + t.speedPerDistanceTerm * distN);

            double outro = t.outroSeconds;
            if (r.hasBoss)
            {
                double slotMax = (double)t.bossIntroSeconds + t.bossArriveSeconds + t.bossMaxSeconds;
                if (hasRequestedDuration)
                    r.waveEnd = (float)HMath.Clamp(baseDuration - outro - slotMax, 16f, t.durationMax - outro - slotMax);
                else
                    r.waveEnd = (float)HMath.Max(HMath.Min(baseDuration - outro, t.durationMax - outro - slotMax), 16f);
                r.bossStart = (float)((double)r.waveEnd + t.bossIntroSeconds);
                r.duration = (float)((double)r.bossStart + t.bossArriveSeconds + t.bossMaxSeconds + outro);
                double dps = (double)ship.damage * ship.fireRate * (1.0 + (double)t.powerPierceDps * ship.pierce);
                r.bossHp = (float)((t.bossFightSeconds + (double)t.bossFightPerTier * tier) * dps * t.bossHitRatio);
            }
            else
            {
                r.duration = (float)baseDuration;
                r.waveEnd = (float)(baseDuration - outro);
            }
            r.totalSteps = (int)((double)r.duration * HyperspaceTuning.StepsPerSecond + 0.5);
            r.BuildSegments(t);
            r.BuildSpeed(t);
            return r;
        }

        void BuildSegments(HyperspaceTuning t)
        {
            double span = (double)waveEnd - t.introSeconds;
            int waves = HMath.Max(1, (int)(span / t.waveSeconds + 0.5));
            double waveLen = span / waves;
            var list = new System.Collections.Generic.List<PacingSegment>();
            list.Add(new PacingSegment { kind = PacingKind.Intro, wave = -1, start = 0f, end = t.introSeconds, rate = 0f });
            double cursor = t.introSeconds;
            double relaxFraction = 1.0 - (double)t.calmFraction - t.buildFraction - t.peakFraction;
            for (int w = 0; w < waves; w++)
            {
                double escalation = waves == 1 ? 1f : 0.85 + 0.3 * (double)w / (waves - 1);
                float[] fr = { t.calmFraction, t.buildFraction, t.peakFraction, (float)relaxFraction };
                float[] mul = { t.calmRate, t.buildRate, t.peakRate, t.relaxRate };
                PacingKind[] kinds = { PacingKind.Calm, PacingKind.Build, PacingKind.Peak, PacingKind.Relax };
                for (int s = 0; s < 4; s++)
                {
                    double end = s == 3 ? t.introSeconds + waveLen * (w + 1) : cursor + waveLen * fr[s];
                    list.Add(new PacingSegment { kind = kinds[s], wave = w, start = (float)cursor, end = (float)end, rate = (float)((double)baseRate * mul[s] * escalation) });
                    cursor = end;
                }
            }
            if (hasBoss) list.Add(new PacingSegment { kind = PacingKind.Boss, wave = waves, start = waveEnd, end = (float)((double)duration - t.outroSeconds), rate = 0f });
            list.Add(new PacingSegment { kind = PacingKind.Outro, wave = -1, start = (float)(hasBoss ? (double)duration - t.outroSeconds : waveEnd), end = duration, rate = 0f });
            segments = list.ToArray();
            double total = 0.0;
            for (int i = 0; i < segments.Length; i++) total += segments[i].rate * ((double)segments[i].end - segments[i].start);
            if (hasBoss) total += t.enemyCost[(int)EntityType.Guardian];
            budgetTotal = (float)total;
        }

        void BuildSpeed(HyperspaceTuning t)
        {
            speed = new float[totalSteps + 2];
            for (int k = 0; k < speed.Length; k++)
            {
                double time = (double)k * HyperspaceTuning.Dt;
                double ramp = t.speedStartFraction + (1.0 - (double)t.speedStartFraction) * HMath.SmoothStep(0f, t.speedRampSeconds, time);
                double brake = 1f - (1.0 - (double)t.speedBrakeFraction) * HMath.SmoothStep((double)duration - t.speedBrakeSeconds, duration, time);
                speed[k] = (float)(cruiseSpeed * ramp * brake);
            }
        }

        /// <summary>Builds a bare constant-speed route for tools: the fairness solver replays one template (or one boss phase) against it.</summary>
        public static RouteSpec ForAnalysis(HyperspaceTuning t, int tier, float constantSpeed, float seconds, bool boss)
        {
            var r = new RouteSpec();
            r.run = new RunParams(1UL, 0f, 0f, tier, 1f, RunMode.Normal);
            r.tier = tier;
            r.duration = seconds;
            r.totalSteps = (int)((double)seconds * HyperspaceTuning.StepsPerSecond + 0.5);
            r.waveEnd = (float)((double)seconds - t.outroSeconds);
            r.hasBoss = boss;
            r.bossStart = 2f;
            r.difficulty = 1f;
            r.baseRate = 1f;
            r.enemyHpScale = 1f;
            r.enemyDamageScale = 1f;
            r.cruiseSpeed = constantSpeed;
            r.shipPower = 1f;
            r.bossHp = 1e9f;
            r.segments = new[] { new PacingSegment { kind = PacingKind.Calm, wave = 0, start = 0f, end = seconds, rate = 0f } };
            r.speed = new float[r.totalSteps + 2];
            for (int i = 0; i < r.speed.Length; i++) r.speed[i] = constantSpeed;
            return r;
        }

        public int SegmentIndexAt(float time)
        {
            for (int i = 0; i < segments.Length; i++)
                if (time < segments[i].end) return i;
            return segments.Length - 1;
        }

        public PacingSegment SegmentAt(float time) { return segments[SegmentIndexAt(time)]; }

        public float RateAt(float time) { return SegmentAt(time).rate; }

        /// <summary>Cumulative budget the director may have spent by this time (the integral of the pacing rate).</summary>
        public float BudgetBy(float time)
        {
            double sum = 0.0;
            for (int i = 0; i < segments.Length; i++)
            {
                PacingSegment s = segments[i];
                if (time <= s.start) break;
                double e = time < s.end ? time : s.end;
                sum += s.rate * (e - s.start);
            }
            return (float)sum;
        }
    }
}
