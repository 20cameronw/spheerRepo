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
        if (target == null)
        {
            laser.enabled = false;
            laserOn = false;

            if (cr_running == false)
                StartCoroutine("checkForTargetInRange");
            return;
        }


        laser.enabled = true;
        laserOn = true;

        if (target != null) {
            lazerEndPosition.transform.position = target.position;
            target.GetComponent<EnemyHealth>().TakeDamage(damage * Player.Instance.getLazerDamageMultiplier() * Time.deltaTime);
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
            // Transform enemyPos = GameObject.FindGameObjectWithTag("Enemy")?.transform;
            Transform enemyPos = Player.Instance.GetTarget();
            if (enemyPos != null)
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
