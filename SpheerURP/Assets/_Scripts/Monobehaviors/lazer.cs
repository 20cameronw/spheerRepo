using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class lazer : MonoBehaviour
{

    public Transform target;

    public bool laserOn;

    [SerializeField] private float range;

    [SerializeField] private Transform laserStart;

    [SerializeField] private LineRenderer laser;

    private bool cr_running;

    [SerializeField] private float damage;

    [SerializeField] private Transform lazerEndPosition;



    private float effectiveRange => range * Player.Instance.getLazerRangeMultiplier();

    void Start()
    {
        StartCoroutine("checkForTargetInRange");
    }


    void Update()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            laser.enabled = false;
            laserOn = false;
            target = null;

            if (cr_running == false)
                StartCoroutine("checkForTargetInRange");
            return;
        }


        laser.enabled = true;
        laserOn = true;

        if (target != null) {
            lazerEndPosition.transform.position = target.position;
            float lazerDmg = damage * Player.Instance.getLazerDamageMultiplier() * Time.deltaTime;
            EnemyHealth eh = target.GetComponent<EnemyHealth>();
            if (eh != null)
            {
                eh.TakeDamage(lazerDmg);
            }
            else
            {
                // Target is an enemy world building or the world itself during an attack.
                IAttackable attackable = target.GetComponentInParent<IAttackable>();
                attackable?.TakeDamage(lazerDmg, AttackWeaponType.Laser);
            }
        }

        if (Vector3.Distance(transform.position, target.position) >= effectiveRange)
        {
            target = null;
            return;
        }
    }

    public IEnumerator checkForTargetInRange()
    {
        cr_running = true;
        while (true)
        {
            Transform enemyPos = Player.Instance.GetTarget();
            if (enemyPos != null && enemyPos.gameObject.activeInHierarchy)
            {
                float distanceToEnemy = Vector3.Distance(transform.position, enemyPos.position);
                if (distanceToEnemy <= effectiveRange)
                {
                    target = enemyPos;
                    StopCoroutine("checkForTargetInRange");
                    cr_running = false;
                    yield break;
                }
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        float displayRange = Player.Instance != null ? effectiveRange : range;
        Gizmos.DrawWireSphere(transform.position, displayRange);
    }
}
