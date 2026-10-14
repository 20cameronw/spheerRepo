using Frontier.Universe;

namespace Frontier.Hyperspace
{
    /// <summary>How a bot plays: reaction latency, how fast and how precisely it moves, how far it looks ahead and how well it predicts motion.</summary>
    public struct BotSkill
    {
        public string name;
        /// <summary>Seconds between a decision and the ship starting to obey it.</summary>
        public float reactionDelay;
        /// <summary>Fraction of the ship's top speed the bot uses.</summary>
        public float speedFraction;
        /// <summary>Extra clearance (u) the bot keeps around hazards.</summary>
        public float padding;
        /// <summary>Distance (u) within which it prefers to have clearance.</summary>
        public float comfort;
        /// <summary>How many seconds ahead it considers threats.</summary>
        public float horizon;
        /// <summary>Random error (u) added to every decision.</summary>
        public float noise;
        /// <summary>Predicts scripted motion exactly (otherwise extrapolates the current velocity).</summary>
        public bool exactPrediction;
        public int thinkEvery;
        public float pickupGreed;
        public bool usesBomb;
        /// <summary>Hull fraction below which the bot panics and uses a bomb when cornered.</summary>
        public float bombHull;
        /// <summary>How many of the most imminent threats it can keep track of at once.</summary>
        public int maxThreats;
        /// <summary>Misjudgement (u) of where threats are, re-rolled a few times per second.</summary>
        public float perceptionNoise;
        /// <summary>Attention lapses: expected number per second, and how long (s) the bot keeps its old goal without looking at new threats.</summary>
        public float lapsePerSecond;
        public float lapseSeconds;

        public static BotSkill Novice()
        {
            return new BotSkill { name = "novice", reactionDelay = 0.40f, speedFraction = 0.55f, padding = 0.35f, comfort = 0.55f, horizon = 1.1f, noise = 0.35f, exactPrediction = false, thinkEvery = 6, pickupGreed = 0.3f, usesBomb = false, bombHull = 0f, maxThreats = 4, perceptionNoise = 0.30f, lapsePerSecond = 0.30f, lapseSeconds = 0.45f };
        }

        public static BotSkill Average()
        {
            return new BotSkill { name = "average", reactionDelay = 0.30f, speedFraction = 0.80f, padding = 0.25f, comfort = 0.45f, horizon = 1.4f, noise = 0.18f, exactPrediction = true, thinkEvery = 4, pickupGreed = 0.6f, usesBomb = true, bombHull = 0.5f, maxThreats = 6, perceptionNoise = 0.14f, lapsePerSecond = 0.12f, lapseSeconds = 0.40f };
        }

        public static BotSkill Expert()
        {
            return new BotSkill { name = "expert", reactionDelay = 0.20f, speedFraction = 1.0f, padding = 0.14f, comfort = 0.35f, horizon = 1.7f, noise = 0.08f, exactPrediction = true, thinkEvery = 3, pickupGreed = 1.0f, usesBomb = true, bombHull = 0.7f, maxThreats = 8, perceptionNoise = 0.09f, lapsePerSecond = 0.05f, lapseSeconds = 0.35f };
        }

        public static BotSkill ByName(string n)
        {
            if (n == "novice") return Novice();
            if (n == "average") return Average();
            return Expert();
        }
    }

    /// <summary>
    /// Deterministic heuristic dodger. Every few steps it lists the threats that will cross the ship plane within its horizon (predicting their position at that moment), scores a lattice of
    /// candidate positions by whether a straight run from the current position (after its reaction delay, at its speed) would clear each threat, and heads for the best one. Its decision is
    /// delayed by the reaction time and its commanded position moves at a limited speed, like a finger. It never reads anything a player could not see.
    /// </summary>
    public sealed partial class Bot
    {
        public readonly BotSkill Skill;
        readonly HyperspaceTuning t;
        Rng rng;
        float cmdX, cmdY;
        float goalX, goalY;
        readonly float[] qx = new float[96];
        readonly float[] qy = new float[96];
        readonly int[] qs = new int[96];
        int qHead, qCount;
        int lastThink = -1000;
        int lapseUntil;
        float lastBestCost;
        public int BombsUsed;
        public int Decisions;

        public Bot(BotSkill skill, HyperspaceTuning tuning, ulong seed)
        {
            Skill = skill;
            t = tuning;
            rng = new Rng(Hash.Combine(seed, 0x424F54UL, (ulong)skill.thinkEvery));
            threats = new Threat[160];
            pickupsBuf = new PickupTarget[24];
        }

        public void Reset(ulong seed)
        {
            rng = new Rng(Hash.Combine(seed, 0x424F54UL, (ulong)Skill.thinkEvery));
            cmdX = cmdY = goalX = goalY = 0f;
            qHead = qCount = 0;
            lastThink = -1000;
            lapseUntil = 0;
            BombsUsed = 0;
            Decisions = 0;
        }

        /// <summary>Returns the input for the next step of the run.</summary>
        public PlayerInput Decide(HyperspaceRun run)
        {
            int step = run.StepIndex;
            if (step - lastThink >= Skill.thinkEvery)
            {
                lastThink = step;
                if (step >= lapseUntil)
                {
                    if (Skill.lapsePerSecond > 0f && rng.NextDouble() < (double)Skill.lapsePerSecond * Skill.thinkEvery * HyperspaceTuning.Dt)
                        lapseUntil = step + (int)((double)Skill.lapseSeconds * HyperspaceTuning.StepsPerSecond + 0.5);
                    else Think(run);
                }
            }
            while (qCount > 0 && qs[qHead] <= step)
            {
                goalX = qx[qHead];
                goalY = qy[qHead];
                qHead = (qHead + 1) % qs.Length;
                qCount--;
            }
            double vmax = (double)run.Player.maxSpeed * Skill.speedFraction;
            double dx = (double)goalX - cmdX, dy = (double)goalY - cmdY;
            double d = HMath.Length(dx, dy);
            double stepLen = vmax * HyperspaceTuning.Dt;
            if (d <= stepLen)
            {
                cmdX = goalX;
                cmdY = goalY;
            }
            else
            {
                cmdX = (float)(cmdX + (dx / d * stepLen));
                cmdY = (float)(cmdY + (dy / d * stepLen));
            }
            bool bomb = Skill.usesBomb && WantsBomb(run);
            if (bomb) BombsUsed++;
            return new PlayerInput(cmdX, cmdY, bomb);
        }

        bool WantsBomb(HyperspaceRun run)
        {
            if (run.Player.bombs <= 0 || run.Player.bombCooldown > 0f) return false;
            double hullFrac = (double)run.Player.hull / run.Player.maxHull;
            return lastBestCost >= 380f && hullFrac <= Skill.bombHull && run.Player.invulnerable <= 0f;
        }

        void Push(int availableAt, float x, float y)
        {
            if (qCount == qs.Length) return;
            int tail = (qHead + qCount) % qs.Length;
            qx[tail] = x;
            qy[tail] = y;
            qs[tail] = availableAt;
            qCount++;
        }
    }
}
