using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackState : EnemyState
{
    [SerializeField] private EnemyLeavingState enemyLeavingState;
    [SerializeField] private int timesToRaycast;
    [SerializeField] private float timeBetweenCasts;
    [SerializeField] private float raycastRange;
    [SerializeField] private ParticleSystem suckingEffect;
    private int currentWaypointIndex = 0;

    public float speed;

    public Transform[] waypoints;

    public bool doneAttacking;
    public int timesCasted;
    public bool cr_running;

    private bool gotAHit = false;

    public ParticleSystem effect;

    public override EnemyState RunState()
    {
        if (!cr_running)
        {
            timesCasted = 0;
            StartCoroutine("Attack");
        }

        if (doneAttacking)
        {
            cr_running = false;
            doneAttacking = false;
            timesCasted = 0;
            enemyLeavingState.effect = this.effect;
            gotAHit = false;
            currentWaypointIndex = 0;
            return enemyLeavingState;
        }
        return this;
    }

    private IEnumerator Attack()
    {
        cr_running = true;
        while (timesCasted < timesToRaycast && cr_running)
        {
            yield return new WaitForSeconds(timeBetweenCasts);
            RaycastHit hit;
            Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.down) * raycastRange, Color.yellow);
            if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.down), out hit, raycastRange))
            {
                // Debug.Log("Alien got a hit!");
                gotAHit = true;
                AudioManager.Instance.Play("GetSuckedUp");

                GameObject prey = hit.transform.gameObject;
                if (prey && !doneAttacking)
                {
                    prey.GetComponent<GetSuckedUp>().getSuckedUp(transform);
                    effect = Instantiate(suckingEffect, transform.position, Quaternion.identity);
                    effect.transform.SetParent(transform);
                    effect.transform.LookAt(prey.transform);
                    yield return new WaitForSeconds(1);

                }
                doneAttacking = true;
            }
            else
            {
                // Debug.Log("alien Did Not Hit");
            }

            timesCasted += 1;
        }
        doneAttacking = true;
    }

    void Update() {
        if (cr_running) {
            if (!gotAHit) MoveTowardsWaypoint();

            if (Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position) <= 0.2f) 
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            }
        }
    }

    void MoveTowardsWaypoint() {
        var step = speed * Time.deltaTime;
        transform.parent.parent.position = Vector3.MoveTowards(transform.parent.parent.position,  waypoints[currentWaypointIndex].position, step);
    }

}

    