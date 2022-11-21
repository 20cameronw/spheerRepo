using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Turret : MonoBehaviour
{
    [Header("Unity Setup Fields")]
    private Transform target;
    public Transform barrel;
    public Transform firePoint;
    public GameObject bulletPrefab;

    [Header("Attributes")]
    public float fireRate = 1f;
    public float range = 100f;

    private float fireCountdown = 0f;

    private IEnumerator coroutine;
    private bool cr_running;

    void Start()
    {
        //Player.Instance.TurretList.Add(this);
        coroutine = checkForTargetInRange();
        StartCoroutine(coroutine);

    }

    // Update is called once per frame
    void Update()
    {
        if (target == null)
        {

            if (cr_running == false)
                StartCoroutine("checkForTargetInRange");

            return;
        }

        float distanceToEnemy = Vector3.Distance(transform.position, target.transform.position);
        if (distanceToEnemy <= range)
        {
            Aim();
            if (fireCountdown <= 0f)
            {
                Shoot();
                fireCountdown = 1f / fireRate;
            }
        }
        else
        {
            target = null;
            return;
        }

        fireCountdown -= Time.deltaTime;
    }

    public IEnumerator checkForTargetInRange()
    {
        cr_running = true;
        
        while (cr_running)
        {
            yield return new WaitForSeconds(0.5f);
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            float shortestDistance = Mathf.Infinity;
            GameObject nearestEnemy = null;
            foreach (GameObject enemy in enemies)
            {
                float distanceToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
                if (distanceToEnemy < shortestDistance)
                {
                    shortestDistance = distanceToEnemy;
                    nearestEnemy = enemy;
                }
            }

            if (nearestEnemy != null && shortestDistance <= range)
            {
                target = nearestEnemy.transform;
            }
            else
            {
                target = null;
            }
        }
        /**{
            float distanceToEnemy = Vector3.Distance(transform.position, enemyPos.position);
            if (distanceToEnemy <= range)
            {
                target = enemyPos;
                StopCoroutine("checkForTargetInRange");
                cr_running = false;
                yield break;
            }
            
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
            target = null;

            yield return new WaitForSeconds(1f);
        }*/
    }

    void Aim()
    {
        //turn the body
        float targetPlaneAngle = vector3AngleOnPlane(target.position, transform.position, -transform.up, transform.forward);
        Vector3 newRotation = new Vector3(0, targetPlaneAngle, 0);
        transform.Rotate(newRotation, Space.Self);

        //move barrel up or down
        float angleX = vector3AngleOnPlane(target.position, barrel.position, -transform.right, transform.forward);
        Vector3 rotationX = new Vector3(angleX, 0, 0);
        barrel.localRotation = Quaternion.Euler(rotationX);
    }

    void Shoot()
    {
        GameObject bulletGO = (GameObject)Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Bullet bullet = bulletGO.GetComponent<Bullet>();

        if (bullet != null)
            bullet.Seek(target);
    }

    float vector3AngleOnPlane(Vector3 from, Vector3 to, Vector3 planeNormal, Vector3 toZeroAngle)
    {
        Vector3 projectedVector = Vector3.ProjectOnPlane(from - to, planeNormal);
        float projectedVectorAngle = Vector3.SignedAngle(projectedVector, toZeroAngle, planeNormal);

        return projectedVectorAngle;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
