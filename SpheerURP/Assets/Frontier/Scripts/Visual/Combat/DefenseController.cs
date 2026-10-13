using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public class WaveInfo
    {
        public int waveNumber;
        public int fuel;
        public int xp;
        public float budget;
        public float healthMult;
        public float damageMult;
        public float speedMult;
        public float rewardMult;
        public List<EnemySO> aliens = new List<EnemySO>();
        public List<ItemAmount> reward = new List<ItemAmount>();
    }

    public class WaveResult
    {
        public bool victory;
        public WaveInfo wave;
        public List<ItemAmount> granted = new List<ItemAmount>();
        public List<ItemAmount> lost = new List<ItemAmount>();
    }

    public class DefenseController : MonoBehaviour
    {
        public static float FuelMult(int fuel) { return 0.5f + 0.25f * Mathf.Max(1, fuel); }

        public bool Active { get; private set; }
        public float ReactorHp { get; private set; }
        public float ReactorMax { get; private set; }
        public int Spawned { get; private set; }
        public int Killed { get; private set; }
        public WaveInfo Current { get; private set; }

        public event Action<WaveResult> WaveEnded;
        public Func<Vector2, bool> IsOverUi;

        PlanetView planetView;
        Camera cam;
        GameState gs;
        readonly List<PlacedStructure> placed = new List<PlacedStructure>();
        readonly List<Transform> placedT = new List<Transform>();
        readonly List<float> placedCd = new List<float>();
        public WorldSurface World;
        float tapCooldown;
        Coroutine spawnRoutine;

        public void Init(PlanetView pv, Camera camera, GameState state)
        {
            planetView = pv; cam = camera; gs = state;
        }

        // Deterministic so the preview in the UI matches the wave you actually get.
        public WaveInfo Preview(int fuel)
        {
            var p = gs.planet;
            var f = gs.db.formula;
            fuel = Mathf.Clamp(fuel, 1, 40);
            var info = new WaveInfo { waveNumber = gs.NextWave(p), fuel = fuel };
            float rm = FuelMult(fuel);
            info.budget = f.Budget(info.waveNumber, p.Danger, rm);
            info.healthMult = f.HealthMult(info.waveNumber) * (1f + f.healthPerFuel * (fuel - 1));
            info.damageMult = 1f + f.damagePerFuel * (fuel - 1);
            info.speedMult = 1f + f.speedPerFuel * (fuel - 1);
            info.rewardMult = f.RewardMult(info.waveNumber, rm) * gs.RewardBonus * gs.AlarmBonus(p);
            info.xp = Mathf.Max(1, Mathf.RoundToInt(info.budget * 6f * gs.AlarmBonus(p)));
            var rng = new System.Random(PlanetSeed(p.AuthoredId ?? p.Id) ^ (info.waveNumber * 7919));
            info.aliens = f.Build(gs.db.enemies, info.waveNumber, info.budget, rng);

            var totals = new Dictionary<ResourceSO, float>();
            foreach (var a in info.aliens)
                foreach (var b in a.bounty)
                {
                    if (b.resource == null) continue;
                    float v; totals.TryGetValue(b.resource, out v);
                    totals[b.resource] = v + b.amount * info.rewardMult;
                }
            var resources = new List<ResourceSO>(totals.Keys);
            resources.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            foreach (var resource in resources)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(totals[resource]));
                info.reward.Add(new ItemAmount(resource, n));
            }
            return info;
        }

        static int PlanetSeed(string id)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in id) hash = hash * 31 + c;
                return hash;
            }
        }

        public bool StartWave(int fuel)
        {
            if (Active || fuel < 1 || fuel > 40) return false;
            if (!gs.Spend(new List<ItemAmount> { new ItemAmount(gs.db.fuel, fuel) })) return false;
            Current = Preview(fuel);
            Active = true;
            Spawned = 0; Killed = 0;
            ReactorMax = gs.ReactorMax;
            ReactorHp = ReactorMax;

            placed.Clear(); placedT.Clear(); placedCd.Clear();
            if (World != null)
                foreach (var kv in World.TurretAnchors()) { placed.Add(kv.Key); placedT.Add(kv.Value); placedCd.Add(UnityEngine.Random.value * 0.5f); }
            spawnRoutine = StartCoroutine(SpawnLoop());
            Sfx.Play(300f, 0.4f, 0.3f, 600f);
            return true;
        }

        IEnumerator SpawnLoop()
        {
            yield return new WaitForSeconds(0.8f);
            float interval = gs.db.formula.Interval(Current.waveNumber);
            foreach (var e in Current.aliens)
            {
                if (!Active) yield break;
                float edge = UnityEngine.Random.Range(0.18f, 0.82f);
                Vector2 viewport = UnityEngine.Random.value < 0.5f ? new Vector2(-0.06f, edge) : new Vector2(1.06f, edge);
                float depth = Vector3.Dot(planetView.Spin.position - cam.transform.position, cam.transform.forward);
                var pos = cam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, depth));
                AlienActor.Spawn(e, Current.healthMult, pos, this);
                Spawned++;
                yield return new WaitForSeconds(interval * UnityEngine.Random.Range(0.6f, 1.3f));
            }
        }

        void Update()
        {
            if (!Active) return;

            FirePlaced();
            tapCooldown -= Time.deltaTime;
            HandleTap();

            if (Spawned >= Current.aliens.Count && AlienActor.All.Count == 0 && Active) End(true);
        }

        void FirePlaced()
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (placedT[i] == null) continue;
                placedCd[i] -= Time.deltaTime;
                if (placedCd[i] > 0f) continue;
                var def = placed[i].def;
                Vector3 from = placedT[i].TransformPoint(Vector3.up * 0.45f);
                var target = Nearest(from, def.range * def.range);
                if (target == null) { placedCd[i] = 0.15f; continue; }
                placedCd[i] = def.fireInterval / gs.TurretFireRate;
                Color c = def.color;
                Fx.Bolt(from, target.transform.position, new Color(c.r, c.g, c.b, 1f), def.beamWidth, def.kind == StructureKind.Laser ? 0.28f : 0.1f);
                Fx.Burst(target.transform.position, c, def.kind == StructureKind.Laser ? 14 : 6, 2.5f, 0.15f);
                Sfx.Zap();
                target.Damage(gs.TurretPower(placed[i]));
            }
        }

        AlienActor Nearest(Vector3 from, float maxDist)
        {
            AlienActor best = null; float bd = maxDist;
            foreach (var a in AlienActor.All)
            {
                if (a == null || !a.Targetable || !OnScreen(a.transform.position)) continue;
                float d = (a.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = a; }
            }
            return best;
        }

        public bool OnScreen(Vector3 position)
        {
            Vector3 vp = cam.WorldToViewportPoint(position);
            return vp.z > 0f && vp.x >= 0.06f && vp.x <= 0.94f && vp.y >= 0.12f && vp.y <= 0.88f;
        }

        public Vector3 Center { get { return planetView.Spin.position; } }
        public float ImpactRadius { get { return planetView.SurfaceRadius(); } }
        public float VisibleApproachTime { get { return gs.db.formula.visibleApproachTime; } }

        void HandleTap()
        {
            if (!Input.GetMouseButtonDown(0) || tapCooldown > 0f) return;
            Vector2 sp = Input.mousePosition;
            if (IsOverUi != null && IsOverUi(sp)) return;
            tapCooldown = 0.1f;

            AlienActor best = null; float bd = ScreenMetrics.Height * 0.09f;
            foreach (var a in AlienActor.All)
            {
                if (a == null || !OnScreen(a.transform.position)) continue;
                var s = cam.WorldToScreenPoint(a.transform.position);
                float d = Vector2.Distance(new Vector2(s.x, s.y), sp);
                if (d < bd) { bd = d; best = a; }
            }

            Vector3 world = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, Mathf.Abs(cam.transform.position.z)));
            world.z = 0f;
            var col = new Color(0.5f, 1.6f, 2.4f, 1f);
            Fx.Ripple(world, col, 0.9f, 0.3f);
            if (best != null)
            {
                Fx.Bolt(Vector3.zero, best.transform.position, col, 0.09f, 0.18f);
                Sfx.Zap();
                best.Damage(gs.TapDamage);
            }
        }

        public void AlienKilled(AlienActor a)
        {
            Killed++;
            Sfx.Play(420f + UnityEngine.Random.Range(0, 200f), 0.1f, 0.18f, 800f);
            a.Die();
        }

        public void AlienImpact(AlienActor a)
        {
            ReactorHp -= a.data.damage * Current.damageMult;
            planetView.Punch();
            Sfx.Boom();
            Fx.Burst(a.transform.position, new Color(3f, 0.4f, 0.3f), 20, 4f, 0.25f);
            Killed++;
            a.Die();
            if (ReactorHp <= 0f) { ReactorHp = 0f; End(false); }
        }

        void End(bool victory)
        {
            if (!Active) return;
            Active = false;
            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
            placed.Clear(); placedT.Clear(); placedCd.Clear();
            foreach (var a in new List<AlienActor>(AlienActor.All)) if (a != null) a.Die();

            var result = new WaveResult { victory = victory, wave = Current };
            if (victory)
            {
                foreach (var r in Current.reward)
                {
                    int ok = gs.Add(r.resource, r.amount, false);
                    if (ok > 0) result.granted.Add(new ItemAmount(r.resource, ok));
                    if (ok < r.amount) result.lost.Add(new ItemAmount(r.resource, r.amount - ok));
                }
                gs.WaveCleared(gs.planet);
                gs.AddXp(Current.xp);
                Sfx.Good();
            }
            else Sfx.Bad();
            if (WaveEnded != null) WaveEnded(result);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Ends the active wave immediately (screenshot harness and tests).</summary>
        public void DebugEnd(bool victory) { End(victory); }
#endif
    }
}
