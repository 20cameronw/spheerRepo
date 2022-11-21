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

    [SerializeField] private GameObject LaserHitEffect;

    private bool cr_running;

    [SerializeField] private float damage;


    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine("checkForTargetInRange");
    }

    // Update is called once per frame
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


        laser.SetPosition(0, laserStart.position);
        laser.SetPosition(1, target.position);

        GameObject fireHit = Instantiate(LaserHitEffect, target.transform);
        fireHit.transform.LookAt(laserStart.position);
        //target.GetComponent<Enemy>().takeHit(damage * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.position) >= range)
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
            Transform enemyPos = GameObject.FindGameObjectWithTag("Enemy").transform;
            float distanceToEnemy = Vector3.Distance(transform.position, enemyPos.position);
            if (distanceToEnemy <= range)
            {
                target = enemyPos;
                StopCoroutine("checkForTargetInRange");
                cr_running = false;
                yield break;
            }
            /*
            List<Enemy> enemyList = Player.Instance.enemies;
            for (int i = 0; i < enemyList.Count; i++)
            {
                if (enemyList[i].alive && (Vector3.Distance(transform.position, enemyList[i].transform.position) <= range))
                {
                    target = enemyList[i].transform;
                    cr_running = false;
                    yield break;
                }
            }
            target = null;*/

            yield return new WaitForSeconds(1f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
