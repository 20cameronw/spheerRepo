using System.Collections;
using UnityEngine;

/// <summary>
/// Attached to the Missile Silo structure.
/// Periodically fires a homing missile at the nearest enemy.
/// </summary>
public class MissileLauncher : MonoBehaviour
{
    [Header("Missile Settings")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float fireInterval = 5f;
    [SerializeField] private float missileDamage = 50f;
    [SerializeField] private float range = 100f;
    [SerializeField] private float missileSpeed = 35f;
    [SerializeField] private float splashRadius = 12f;

    private void Start()
    {
        StartCoroutine(FireRoutine());
    }

    private IEnumerator FireRoutine()
    {
        // Stagger initial fire so not all silos fire at once
        yield return new WaitForSeconds(Random.Range(0f, fireInterval));

        while (true)
        {
            TryFire();
            yield return new WaitForSeconds(fireInterval);
        }
    }

    private void TryFire()
    {
        Transform target = ResolveTarget();
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > range) return;

        // Create missile as a sphere (the trail renderer gives it the rocket look)
        GameObject missileGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        missileGO.name = "Missile";
        missileGO.transform.localScale = Vector3.one * 0.6f;
        missileGO.transform.position = transform.position + transform.up * 0.5f;

        // Missile should not block raycasts
        Destroy(missileGO.GetComponent<Collider>());

        // Silver-gray metallic body
        Renderer rend = missileGO.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = new Color(0.75f, 0.75f, 0.8f);

        MissileProjectile proj = missileGO.AddComponent<MissileProjectile>();
        proj.Initialize(target, missileDamage, missileSpeed, splashRadius, explosionPrefab);
    }

    private Transform ResolveTarget()
    {
        if (Player.Instance.getMissileAutoTargeting())
        {
            // Independent targeting: nearest Enemy-tagged object in range
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            Transform nearest = null;
            float nearestDist = float.MaxValue;
            foreach (GameObject e in enemies)
            {
                if (!e.activeInHierarchy) continue;
                float dist = Vector3.Distance(transform.position, e.transform.position);
                if (dist <= range && dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = e.transform;
                }
            }
            // Fall back to player's manual target when no enemy found (e.g. attack-phase buildings)
            if (nearest != null) return nearest;
        }

        Transform manual = Player.Instance.GetTarget();
        return (manual != null && manual.gameObject.activeInHierarchy) ? manual : null;
    }
}
