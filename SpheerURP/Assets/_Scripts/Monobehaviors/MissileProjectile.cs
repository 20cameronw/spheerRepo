using UnityEngine;

/// <summary>
/// The actual in-flight missile.  Homes in on a target, displays a rocket
/// booster trail, and explodes with splash damage on impact.
/// Created programmatically by <see cref="MissileLauncher"/>.
/// </summary>
public class MissileProjectile : MonoBehaviour
{
    private Transform target;
    private float damage;
    private float speed;
    private float splashRadius;
    private GameObject explosionPrefab;

    private bool hasImpacted = false;

    public void Initialize(Transform target, float damage, float speed,
                           float splashRadius, GameObject explosionPrefab)
    {
        this.target         = target;
        this.damage         = damage;
        this.speed          = speed;
        this.splashRadius   = splashRadius;
        this.explosionPrefab = explosionPrefab;
    }

    private void Start()
    {
        AddRocketTrail();
    }

    private void AddRocketTrail()
    {
        TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
        trail.time       = 0.45f;
        trail.startWidth = 0.9f;
        trail.endWidth   = 0f;

        // Use a simple unlit material so the trail is always visible
        Shader unlit = Shader.Find("Sprites/Default");
        if (unlit != null) trail.material = new Material(unlit);

        // Rocket exhaust: orange core → yellow flame → gray smoke
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f,  0.55f, 0f),   0f),
                new GradientColorKey(new Color(1f,  0.9f,  0.15f), 0.35f),
                new GradientColorKey(new Color(0.6f, 0.6f, 0.6f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f,   0f),
                new GradientAlphaKey(0.7f, 0.5f),
                new GradientAlphaKey(0f,   1f)
            }
        );
        trail.colorGradient = gradient;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void FixedUpdate()
    {
        if (hasImpacted) return;

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 dir = target.position - transform.position;
        float distanceThisFrame = speed * Time.fixedDeltaTime;

        if (dir.magnitude <= distanceThisFrame)
        {
            Impact(target.position);
            return;
        }

        // Smooth homing — rotate toward target a bit each frame
        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                               10f * Time.fixedDeltaTime);
        transform.Translate(Vector3.forward * distanceThisFrame, Space.Self);
    }

    private void Impact(Vector3 impactPos)
    {
        hasImpacted = true;

        // Spawn explosion particle effect
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, impactPos, Quaternion.identity);
            ParticleSystem ps = explosion.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
            Destroy(explosion, 2f);
        }

        // Splash damage — hits enemies and any IAttackable targets in the radius.
        Collider[] cols = Physics.OverlapSphere(impactPos, splashRadius);
        // Use a HashSet to avoid dealing damage to the same IAttackable more than once
        // (e.g. a building with multiple child colliders).
        System.Collections.Generic.HashSet<IAttackable> damagedAttackables =
            new System.Collections.Generic.HashSet<IAttackable>();

        foreach (Collider col in cols)
        {
            EnemyHealth eh = col.GetComponentInParent<EnemyHealth>();
            if (eh != null)
            {
                eh.TakeDamage(damage);
                continue;
            }

            // Also damage enemy world buildings / the world sphere during an attack.
            IAttackable attackable = col.GetComponentInParent<IAttackable>();
            if (attackable != null && !damagedAttackables.Contains(attackable))
            {
                damagedAttackables.Add(attackable);
                attackable.TakeDamage(damage, AttackWeaponType.Missile);
            }
        }

        Destroy(gameObject);
    }
}
