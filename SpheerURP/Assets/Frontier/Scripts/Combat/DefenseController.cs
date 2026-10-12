using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public class WaveInfo
    {
        public int waveNumber;
        public int risk;
        public float budget;
        public float healthMult;
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
        public static readonly float[] RiskMult = { 0.75f, 1f, 1.6f };
        public static readonly string[] RiskName = { "CALM", "NORMAL", "FRENZY" };

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
        readonly List<Transform> turrets = new List<Transform>();
        readonly List<float> turretCooldown = new List<float>();
        float tapCooldown;
        float turretAngle;
        float spawnRadius;
        Coroutine spawnRoutine;

        public void Init(PlanetView pv, Camera camera, GameState state)
        {
            planetView = pv; cam = camera; gs = state;
        }

        // Deterministic so the preview in the UI matches the wave you actually get.
        public WaveInfo Preview(int risk)
        {
            var p = gs.planet;
            var f = gs.db.formula;
            var info = new WaveInfo { waveNumber = gs.NextWave(p), risk = risk };
            float rm = RiskMult[Mathf.Clamp(risk, 0, RiskMult.Length - 1)];
            info.budget = f.Budget(info.waveNumber, p.danger, rm);
            info.healthMult = f.HealthMult(info.waveNumber);
            info.rewardMult = f.RewardMult(info.waveNumber, rm) * gs.RewardBonus;
            var rng = new System.Random(p.id.GetHashCode() ^ (info.waveNumber * 7919) ^ (risk * 104729));
            info.aliens = f.Build(gs.db.enemies, info.waveNumber, info.budget, rng);

            var totals = new Dictionary<ResourceSO, float>();
            foreach (var a in info.aliens)
                foreach (var b in a.bounty)
                {
                    if (b.resource == null) continue;
                    float v; totals.TryGetValue(b.resource, out v);
                    totals[b.resource] = v + b.amount * info.rewardMult;
                }
            foreach (var kv in totals)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(kv.Value));
                info.reward.Add(new ItemAmount(kv.Key, n));
            }
            return info;
        }

        public void StartWave(int risk)
        {
            if (Active) return;
            Current = Preview(risk);
            Active = true;
            Spawned = 0; Killed = 0;
            ReactorMax = gs.ReactorMax;
            ReactorHp = ReactorMax;

            float hh = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Abs(cam.transform.position.z);
            float hw = hh * cam.aspect;
            spawnRadius = Mathf.Sqrt(hh * hh + hw * hw) + 1.2f;

            BuildTurrets();
            spawnRoutine = StartCoroutine(SpawnLoop());
            Sfx.Play(300f, 0.4f, 0.3f, 600f);
        }

        void BuildTurrets()
        {
            ClearTurrets();
            int n = gs.TurretCount;
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Turret");
                go.transform.SetParent(transform, false);
                var tm = ModelLibrary.Spawn("turret", go.transform, 0.55f);
                if (tm == null)
                {
                    Destroy(go);
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(transform, false);
                    go.transform.localScale = Vector3.one * 0.3f;
                    go.GetComponent<MeshRenderer>().sharedMaterial = Fx.AddMat(new Color(2.2f, 0.6f, 2f, 1f), null, 1.4f);
                }
                Fx.GlowQuad(go.transform, new Color(2.2f, 0.6f, 2f, 0.7f), tm == null ? 4f : 1.6f);
                turrets.Add(go.transform);
                turretCooldown.Add(UnityEngine.Random.value);
            }
        }

        void ClearTurrets()
        {
            foreach (var t in turrets) if (t != null) Destroy(t.gameObject);
            turrets.Clear(); turretCooldown.Clear();
        }

        IEnumerator SpawnLoop()
        {
            yield return new WaitForSeconds(0.8f);
            float interval = gs.db.formula.Interval(Current.waveNumber);
            foreach (var e in Current.aliens)
            {
                if (!Active) yield break;
                float ang = UnityEngine.Random.value * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), UnityEngine.Random.Range(-0.5f, 0.5f)) * spawnRadius;
                AlienActor.Spawn(e, Current.healthMult, pos, this);
                Spawned++;
                yield return new WaitForSeconds(interval * UnityEngine.Random.Range(0.6f, 1.3f));
            }
        }

        void Update()
        {
            if (!Active) return;

            turretAngle += Time.deltaTime * 0.9f;
            for (int i = 0; i < turrets.Count; i++)
            {
                float a = turretAngle + i * Mathf.PI * 2f / turrets.Count;
                turrets[i].localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.45f, Mathf.Sin(a) * 0.9f) * (PlanetView.Radius + 0.9f);
                turretCooldown[i] -= Time.deltaTime;
                if (turretCooldown[i] <= 0f)
                {
                    var target = Nearest(turrets[i].position, float.MaxValue);
                    if (target != null)
                    {
                        turretCooldown[i] = 0.9f;
                        Fx.Bolt(turrets[i].position, target.transform.position, new Color(2.2f, 0.6f, 2f, 1f));
                        Sfx.Zap();
                        target.Damage(gs.TurretDamage);
                    }
                    else turretCooldown[i] = 0.2f;
                }
            }

            tapCooldown -= Time.deltaTime;
            HandleTap();

            if (Spawned >= Current.aliens.Count && AlienActor.All.Count == 0 && Active) End(true);
        }

        AlienActor Nearest(Vector3 from, float maxDist)
        {
            AlienActor best = null; float bd = maxDist;
            foreach (var a in AlienActor.All)
            {
                if (a == null) continue;
                float d = (a.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = a; }
            }
            return best;
        }

        void HandleTap()
        {
            if (!Input.GetMouseButtonDown(0) || tapCooldown > 0f) return;
            Vector2 sp = Input.mousePosition;
            if (IsOverUi != null && IsOverUi(sp)) return;
            tapCooldown = 0.1f;

            AlienActor best = null; float bd = Screen.height * 0.09f;
            foreach (var a in AlienActor.All)
            {
                if (a == null) continue;
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
            ReactorHp -= a.data.damage;
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
            ClearTurrets();
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
                Sfx.Good();
            }
            else Sfx.Bad();
            if (WaveEnded != null) WaveEnded(result);
        }
    }
}
