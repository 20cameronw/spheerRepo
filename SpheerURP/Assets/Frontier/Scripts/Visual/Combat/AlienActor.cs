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
        Material glowMaterial, trailMaterial;
        Color baseColor;
        float flash;
        float spin;
        float wobbleSeed;
        bool dead;
        bool usesModel;
        readonly List<Material> bodyMaterials = new List<Material>();
        readonly List<Color> bodyColors = new List<Color>();
        float visibleTime, effectTime;
        float approachTime;
        public bool Targetable { get { return visibleTime >= approachTime; } }

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
            speed = d.speed * own.Current.speedMult * 0.75f;
            approachTime = Mathf.Max(0.5f, own.VisibleApproachTime);
            baseColor = d.color;
            wobbleSeed = Random.value * 10f;
            spin = Random.Range(40f, 140f);

            var body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            body.transform.localScale = Vector3.one * d.scale;
            visual = body.transform;
            var model = ModelLibrary.Spawn(d.id, body.transform, 1f);
            if (model != null)
            {
                usesModel = true;
                int layer = 0;
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Color tint = Color.Lerp(new Color(0.08f, 0.1f, 0.16f), Fx.Displayable(baseColor), (layer++ % 2) == 0 ? 0.75f : 0.35f);
                        var material = new Material(Fx.PlanetShader);
                        material.SetColor("_BaseColor", tint);
                        material.SetColor("_AltColor", tint * 0.4f);
                        material.SetColor("_RimColor", baseColor * 1.2f);
                        material.SetFloat("_Style", 3f);
                        material.SetFloat("_Bands", 5f);
                        material.SetFloat("_Seed", wobbleSeed);
                        material.SetFloat("_Lines", 0f);
                        Vector3 center = renderer.localBounds.center;
                        material.SetVector("_Center", new Vector4(center.x, center.y, center.z, 0f));
                        materials[i] = material;
                        bodyMaterials.Add(material); bodyColors.Add(tint);
                    }
                    renderer.sharedMaterials = materials;
                }
            }
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

            glowMaterial = Fx.GlowQuad(transform, new Color(baseColor.r, baseColor.g, baseColor.b, 0.55f), d.scale * 4.2f)
                .GetComponent<MeshRenderer>().sharedMaterial;

            var tr = gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.5f;
            tr.widthMultiplier = d.scale * 0.55f;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trailMaterial = Fx.AddMat(new Color(baseColor.r, baseColor.g, baseColor.b, 0.8f));
            tr.sharedMaterial = trailMaterial;
            tr.startColor = Color.white;
            tr.endColor = new Color(1, 1, 1, 0);

            All.Add(this);
        }

        void OnDestroy()
        {
            All.Remove(this);
            foreach (var material in bodyMaterials) if (material != null) Destroy(material);
            if (mat != null) Destroy(mat);
            if (glowMaterial != null) Destroy(glowMaterial);
            if (trailMaterial != null) Destroy(trailMaterial);
        }

        void Update()
        {
            if (dead) return;
            Vector3 pos = transform.position;
            Vector3 toCenter = owner.Center - pos;
            Vector3 dir = toCenter.normalized;
            Vector3 side = new Vector3(-dir.y, dir.x, 0f);
            float wob = Mathf.Sin(Time.time * 3f + wobbleSeed) * 0.35f;
            if (owner.OnScreen(pos))
            {
                if (visibleTime == 0f)
                {
                    Fx.Ripple(pos, baseColor, data.scale * 2.4f, 0.35f);
                    Fx.Burst(pos, baseColor, 8, 1.2f, 0.1f);
                }
                visibleTime += Time.deltaTime;
            }
            float approachSpeed = speed;
            if (!Targetable)
                approachSpeed = Mathf.Min(speed, Mathf.Max(0f, toCenter.magnitude - owner.ImpactRadius - data.scale * 0.4f) / (Mathf.Max(0f, approachTime - visibleTime) + 0.8f));
            transform.position = pos + (dir * approachSpeed + side * wob) * Time.deltaTime;
            if (usesModel) visual.Rotate(0f, spin * 0.5f * Time.deltaTime, 0f);
            else visual.Rotate(spin * Time.deltaTime, spin * 0.7f * Time.deltaTime, 0f);

            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 6f);
            if (mat != null) mat.SetColor("_Color", Color.Lerp(baseColor, new Color(3f, 3f, 3f, 1f), flash));
            for (int i = 0; i < bodyMaterials.Count; i++)
                bodyMaterials[i].SetColor("_BaseColor", Color.Lerp(bodyColors[i], Color.white * 3f, flash));
            visual.localScale = Vector3.one * data.scale * (1f + flash * 0.25f);
            effectTime -= Time.deltaTime;
            if (effectTime <= 0f && owner.OnScreen(transform.position))
            {
                Fx.Burst(transform.position - dir * data.scale * 0.4f, baseColor, 2, 0.45f, 0.06f);
                effectTime = 0.2f;
            }

            if ((transform.position - owner.Center).magnitude <= owner.ImpactRadius + data.scale * 0.4f)
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
            Destroy(gameObject);
        }
    }
}
