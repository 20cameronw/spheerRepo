using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public class AlienActor : MonoBehaviour
    {
        public static readonly List<AlienActor> All = new List<AlienActor>();

        public EnemySO data;
        public float health, maxHealth;
        public float speed;
        DefenseController owner;
        Transform visual;
        Material mat;
        Color baseColor;
        float flash;
        float spin;
        float wobbleSeed;
        bool dead;
        bool usesModel;

        public static AlienActor Spawn(EnemySO d, float hpMult, Vector3 pos, DefenseController owner)
        {
            var go = new GameObject("Alien_" + d.id);
            go.transform.position = pos;
            var a = go.AddComponent<AlienActor>();
            a.Init(d, hpMult, owner);
            return a;
        }

        void Init(EnemySO d, float hpMult, DefenseController own)
        {
            data = d; owner = own;
            maxHealth = health = d.health * hpMult;
            speed = d.speed;
            baseColor = d.color;
            wobbleSeed = Random.value * 10f;
            spin = Random.Range(40f, 140f);

            var body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            body.transform.localScale = Vector3.one * d.scale;
            visual = body.transform;
            var model = ModelLibrary.Spawn(d.id, body.transform, 1f);
            if (model != null) usesModel = true;
            else
            {
                Destroy(body);
                body = GameObject.CreatePrimitive(d.shape);
                Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(transform, false);
                body.transform.localScale = Vector3.one * d.scale;
                visual = body.transform;
                mat = Fx.AddMat(baseColor, null, 1.6f);
                body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }

            Fx.GlowQuad(transform, new Color(baseColor.r, baseColor.g, baseColor.b, 0.55f), d.scale * 4.2f);

            var tr = gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.5f;
            tr.widthMultiplier = d.scale * 0.55f;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            tr.sharedMaterial = Fx.AddMat(new Color(baseColor.r, baseColor.g, baseColor.b, 0.8f));
            tr.startColor = Color.white;
            tr.endColor = new Color(1, 1, 1, 0);

            All.Add(this);
        }

        void OnDestroy() { All.Remove(this); }

        void Update()
        {
            if (dead) return;
            Vector3 pos = transform.position;
            Vector3 dir = (-pos).normalized;
            Vector3 side = new Vector3(-dir.y, dir.x, 0f);
            float wob = Mathf.Sin(Time.time * 3f + wobbleSeed) * 0.35f;
            transform.position = pos + (dir * speed + side * wob) * Time.deltaTime;
            if (usesModel) visual.Rotate(0f, spin * 0.5f * Time.deltaTime, 0f);
            else visual.Rotate(spin * Time.deltaTime, spin * 0.7f * Time.deltaTime, 0f);

            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 6f);
            if (mat != null) mat.SetColor("_Color", Color.Lerp(baseColor, new Color(3f, 3f, 3f, 1f), flash));
            visual.localScale = Vector3.one * data.scale * (1f + flash * 0.25f);

            if (transform.position.magnitude <= PlanetView.Radius + data.scale * 0.4f)
                owner.AlienImpact(this);
        }

        public void Damage(float amount)
        {
            if (dead) return;
            health -= amount;
            flash = 1f;
            Fx.Burst(transform.position, baseColor, 6, 2f, 0.12f);
            if (health <= 0f) owner.AlienKilled(this);
        }

        public void Die()
        {
            if (dead) return;
            dead = true;
            All.Remove(this);
            Fx.Burst(transform.position, baseColor, 22, 4f, 0.22f);
            Fx.Ripple(transform.position, baseColor, 1.6f);
            if (mat != null) Destroy(mat);
            Destroy(gameObject);
        }
    }
}
