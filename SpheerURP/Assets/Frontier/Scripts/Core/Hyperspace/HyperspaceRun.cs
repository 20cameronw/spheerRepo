using Frontier.Universe;

namespace Frontier.Hyperspace
{
    /// <summary>
    /// The Hyperspace Run simulation. Deterministic, fixed step (1/60 s), allocation-free after construction. Drive it with Step(PlayerInput) from a fixed-step accumulator,
    /// drain Events after each step, and read the public state arrays (Player, Enemies, Bullets, Bolts, Pickups) to render. Optional replay journaling allocates its input history.
    /// Never write to the public state.
    /// Coordinates: the ship is at z = 0 and flies toward +z; the world scrolls toward -z. x right, y up. The play area is [-W, W] x [-H, H] (see HyperspaceTuning).
    /// </summary>
    public sealed partial class HyperspaceRun
    {
        public const float Dt = HyperspaceTuning.Dt;

        public readonly HyperspaceTuning Tuning;
        public readonly RouteSpec Route;
        public readonly Schedule Plan;
        public readonly ShipStats Ship;
        public readonly EventRing Events = new EventRing();
        public RunReplay Replay;

        /// <summary>Running until the hull hits 0 (Downed: world frozen until Revive() or Abandon()) or the route ends (Ended).</summary>
        public RunState State;
        /// <summary>Number of steps simulated so far; time = StepIndex / 60.</summary>
        public int StepIndex;
        /// <summary>Current world scroll speed in u/s (renderers use it for streaks and FOV).</summary>
        public float Speed;
        /// <summary>Total distance scrolled.</summary>
        public float Scroll;
        public PlayerState Player;

        public Enemy[] Enemies;
        public int EnemyHigh;
        public int EnemyCount;
        public Bullet[] Bullets;
        public int BulletHigh;
        public Bolt[] Bolts;
        public int BoltHigh;
        public Pickup[] Pickups;
        public int PickupHigh;

        public int Kills;
        public int HitsTaken;
        public int HullHits;
        public int MaxCombo;
        public int PickupsCollected;
        public int PickupsSpawned;
        public int RevivesUsed;
        public int Score;
        public int KillableSpawned;
        public int FuelCanisters;
        public int SpawnFailures;
        public float FuelBonus;
        public bool BossDefeated;
        public bool Abandoned;
        public bool Defeated;
        public int EndStep;

        /// <summary>When set the simulation never damages the ship and never fires: used by the fairness solver to replay a schedule against a ghost.</summary>
        public bool Ghost;

        int nextUid;
        int cursor;
        int[] freeEnemies;
        int freeEnemyCount;
        int[] freeBullets;
        int freeBulletCount;
        int[] freeBolts;
        int freeBoltCount;
        int[] freePickups;
        int freePickupCount;
        readonly int[] lootCounts = new int[3];
        Rng lootRng;
        int killsSinceDrop;
        int lastShotEvent;
        int waveClearedIndex;

        public static HyperspaceRun Create(RunParams p, ShipStats ship, HyperspaceTuning tuning, EncounterLibrary library)
        {
            RouteSpec route = RouteSpec.Create(p, ship, tuning);
            Schedule plan = SpawnDirector.Build(route, library, tuning);
            return new HyperspaceRun(route, plan, ship, tuning);
        }

        /// <summary>Builds a run from an explicit route and schedule (tests, the solver and tools).</summary>
        public HyperspaceRun(RouteSpec route, Schedule plan, ShipStats ship, HyperspaceTuning tuning)
        {
            Tuning = tuning;
            Route = route;
            Plan = plan;
            Ship = ship;
            Enemies = new Enemy[tuning.maxEnemies];
            Bullets = new Bullet[tuning.maxPlayerBullets];
            Bolts = new Bolt[tuning.maxEnemyBolts];
            Pickups = new Pickup[tuning.maxPickups];
            freeEnemies = new int[tuning.maxEnemies];
            freeBullets = new int[tuning.maxPlayerBullets];
            freeBolts = new int[tuning.maxEnemyBolts];
            freePickups = new int[tuning.maxPickups];
            InitBroadPhase();
            Reset();
        }

        /// <summary>Restarts the same route from step 0 (same plan, same seed): the result of a fresh run with identical input.</summary>
        public void Reset()
        {
            Replay = null;
            State = RunState.Running;
            StepIndex = 0;
            Speed = Route.speed[0];
            Scroll = 0f;
            for (int i = 0; i < Enemies.Length; i++) Enemies[i] = default(Enemy);
            for (int i = 0; i < Bullets.Length; i++) Bullets[i] = default(Bullet);
            for (int i = 0; i < Bolts.Length; i++) Bolts[i] = default(Bolt);
            for (int i = 0; i < Pickups.Length; i++) Pickups[i] = default(Pickup);
            freeEnemyCount = Enemies.Length;
            for (int i = 0; i < freeEnemyCount; i++) freeEnemies[i] = freeEnemyCount - 1 - i;
            freeBulletCount = Bullets.Length;
            for (int i = 0; i < freeBulletCount; i++) freeBullets[i] = freeBulletCount - 1 - i;
            freeBoltCount = Bolts.Length;
            for (int i = 0; i < freeBoltCount; i++) freeBolts[i] = freeBoltCount - 1 - i;
            freePickupCount = Pickups.Length;
            for (int i = 0; i < freePickupCount; i++) freePickups[i] = freePickupCount - 1 - i;
            EnemyHigh = BulletHigh = BoltHigh = PickupHigh = 0;
            EnemyCount = 0;
            nextUid = 1;
            cursor = 0;
            Kills = HitsTaken = HullHits = MaxCombo = PickupsCollected = PickupsSpawned = RevivesUsed = Score = KillableSpawned = FuelCanisters = SpawnFailures = 0;
            FuelBonus = 0f;
            BossDefeated = Abandoned = Defeated = false;
            EndStep = Route.totalSteps;
            lootCounts[0] = lootCounts[1] = lootCounts[2] = 0;
            lootRng = new Rng(Hash.Combine(Route.run.seed, 0x4C4F4F54UL));
            killsSinceDrop = 0;
            lastShotEvent = -100;
            waveClearedIndex = 0;
            Events.Reset();
            ResetPlayer();
            ResetBoss();
        }

        void ResetPlayer()
        {
            Player = default(PlayerState);
            Player.maxHull = Ship.hull;
            Player.hull = Ship.hull;
            Player.maxShield = Ship.shield;
            Player.shield = (int)((double)Ship.shield * Tuning.startShieldFraction + 0.5);
            Player.maxBombs = Ship.bombCapacity;
            Player.bombs = (int)((double)Ship.bombCapacity * Tuning.startBombFraction + 0.5);
            Player.maxSpeed = Tuning.MaxShipSpeed(Ship.handling);
            Player.fireCooldown = 0.25f;
        }

        public float Time { get { return (float)((double)StepIndex * Dt); } }

        /// <summary>0..1 progress along the route (jumps to the end when the Guardian is destroyed).</summary>
        public float Progress { get { return (float)(EndStep <= 0 ? 1f : HMath.Clamp01((double)(float)StepIndex / EndStep)); } }

        public bool CanRevive { get { return State == RunState.Downed && RevivesUsed < Tuning.maxRevives; } }

        void Emit(EventType type, int a, int b, int c, float x, float y, float z, float v)
        {
            Events.Push(type, StepIndex, a, b, c, x, y, z, v);
        }

        // ---------------------------------------------------------------- the step
        /// <summary>Advances the simulation by exactly 1/60 s. Does nothing while Downed or Ended.</summary>
        public void Step(in PlayerInput input)
        {
            if (State != RunState.Running) return;
            Replay?.RecordStep(input);
            int k = StepIndex;
            Speed = Route.speed[k < Route.speed.Length ? k : Route.speed.Length - 1];
            Scroll = (float)(Scroll + ((double)Speed * Dt));

            SpawnDue();
            UpdatePlayer(input);
            UpdateBoss();
            UpdateEnemies();
            UpdateWeapons();
            UpdateBolts();
            UpdatePickups();
            BuildBroadPhase();
            CollideBullets();
            CollidePlayer();
            CleanUp();
            UpdateWaveEvents();

            if (State == RunState.Running)
            {
                StepIndex++;
                if (StepIndex >= EndStep) FinishRun(true);
            }
        }

        // ---------------------------------------------------------------- pools
        int AllocEnemy()
        {
            if (freeEnemyCount == 0)
            {
                SpawnFailures++;
                return -1;
            }
            int i = freeEnemies[--freeEnemyCount];
            if (i >= EnemyHigh) EnemyHigh = i + 1;
            EnemyCount++;
            return i;
        }

        void FreeEnemy(int i)
        {
            Enemies[i].active = false;
            freeEnemies[freeEnemyCount++] = i;
            EnemyCount--;
            while (EnemyHigh > 0 && !Enemies[EnemyHigh - 1].active) EnemyHigh--;
        }

        int AllocBullet()
        {
            if (freeBulletCount == 0) return -1;
            int i = freeBullets[--freeBulletCount];
            if (i >= BulletHigh) BulletHigh = i + 1;
            return i;
        }

        void FreeBullet(int i)
        {
            Bullets[i].active = false;
            freeBullets[freeBulletCount++] = i;
            while (BulletHigh > 0 && !Bullets[BulletHigh - 1].active) BulletHigh--;
        }

        int AllocBolt()
        {
            if (freeBoltCount == 0) return -1;
            int i = freeBolts[--freeBoltCount];
            if (i >= BoltHigh) BoltHigh = i + 1;
            return i;
        }

        void FreeBolt(int i)
        {
            Bolts[i].active = false;
            freeBolts[freeBoltCount++] = i;
            while (BoltHigh > 0 && !Bolts[BoltHigh - 1].active) BoltHigh--;
        }

        int AllocPickup()
        {
            if (freePickupCount == 0) return -1;
            int i = freePickups[--freePickupCount];
            if (i >= PickupHigh) PickupHigh = i + 1;
            return i;
        }

        void FreePickup(int i)
        {
            Pickups[i].active = false;
            freePickups[freePickupCount++] = i;
            while (PickupHigh > 0 && !Pickups[PickupHigh - 1].active) PickupHigh--;
        }

        public int ActiveBullets { get { int n = 0; for (int i = 0; i < BulletHigh; i++) if (Bullets[i].active) n++; return n; } }
        public int ActiveBolts { get { int n = 0; for (int i = 0; i < BoltHigh; i++) if (Bolts[i].active) n++; return n; } }
        public int ActivePickups { get { int n = 0; for (int i = 0; i < PickupHigh; i++) if (Pickups[i].active) n++; return n; } }
    }
}
