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

    public float baseFireRate = 1f;
    public float range = 30f;

    public float baseRange = 30f;

    public float rangeMultiplier;

    public float rateMultiplier;

    private float fireCountdown = 0f;

    private IEnumerator coroutine;
    private bool cr_running;

    void Start()
    {
        coroutine = checkForTarget();
        StartCoroutine(coroutine);
    }

    // Update is called once per frame
    void Update()
    {
        // Treat inactive (destroyed) targets the same as null so turrets don't
        // keep aiming at buildings that have been deactivated on death.
        if (target != null && !target.gameObject.activeInHierarchy)
            target = null;

        if (target == null)
        {

            if (cr_running == false)
                StartCoroutine("checkForTarget");

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

    public IEnumerator checkForTarget()
    {
        cr_running = true;

        while (cr_running)
        {
            yield return new WaitForSeconds(0.3f);

            float rangeMult = Player.Instance.getTurretRangeMultiplier();

            if (rangeMult != rangeMultiplier)
            {
                rangeMultiplier = rangeMult;
                range = baseRange * rangeMultiplier;
            }

            float rateMult = Player.Instance.getTurretFireRateMultiplier();

            if (rateMult != rateMultiplier)
            {
                rateMultiplier = rateMult;
                fireRate = baseFireRate * rateMultiplier;
            }

            // Always scan for the nearest enemy-tagged object in range (future troop enemies will use this tag)
            target = FindNearestEnemyInRange();
        }
    }

    private Transform FindNearestEnemyInRange()
    {
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
        return nearest;
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
        // AudioManager.Instance.Play("Shot");

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
