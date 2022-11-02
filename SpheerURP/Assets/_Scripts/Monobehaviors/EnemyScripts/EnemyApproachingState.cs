using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyApproachingState : EnemyState
{
    [SerializeField] private EnemyAttackState enemyAttackState;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float speed;
    private bool hasArrived;
    private bool cr_running;
    private bool approaching;


    public override EnemyState RunState()
    {
        approaching = true;

        if (hasArrived)
        {
            approaching = false;
            hasArrived = false;
            return enemyAttackState;
        }
        return this;
    }

    void Update()
    {
        if (approaching)
        {
            var step =  speed * Time.deltaTime; 
            transform.parent.parent.position = Vector3.MoveTowards(transform.parent.parent.position, attackPoint.position, step);
        }

        if (Vector3.Distance(transform.parent.parent.position, attackPoint.position) < 0.01f)
        {
            hasArrived = true;
        }
        else
        {
            hasArrived = false;
        }
    }
}
