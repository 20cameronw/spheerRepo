using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackState : EnemyState
{
    [SerializeField] private EnemyLeavingState enemyLeavingState;
    [SerializeField] private int timesToRaycast;
    [SerializeField] private float timeBetweenCasts;
    [SerializeField] private float raycastRange;
    public bool doneAttacking;
    public int timesCasted;
    public bool cr_running;


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
                Debug.Log("Alien got a hit!");

                GameObject prey = hit.transform.gameObject;
                if (prey)
                {
                    GetSuckedUp getSuckedUp = prey.GetComponent<GetSuckedUp>();
                    if (getSuckedUp) getSuckedUp.getSuckedUp(hit.transform);
                }
                doneAttacking = true;
            }
            else
            {
                Debug.Log("alien Did Not Hit");
            }

            timesCasted += 1;
        }
        doneAttacking = true;
    }
}
