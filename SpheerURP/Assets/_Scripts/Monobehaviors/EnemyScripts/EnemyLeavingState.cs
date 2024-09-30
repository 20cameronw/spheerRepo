using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyLeavingState : EnemyState
{
    [SerializeField] private EnemyIdleState enemyIdleState;
    [SerializeField] private Transform leavePoint;
    [SerializeField] private float speed;
    private bool hasArrived;
    private bool cr_running;
    private bool leaving;

    public override EnemyState RunState()
    {
        leaving = true;

        if (hasArrived)
        {
            leaving = false;
            hasArrived = false;
            return enemyIdleState;
        }
        return this;
    }

    void Awake()
    {
        Transform[] objs = Resources.FindObjectsOfTypeAll<Transform>() as Transform[];
        for (int i = 0; i < objs.Length; i++)
        {
            if (objs[i].gameObject.CompareTag("EnemyLeavePoint"))
            {
                leavePoint = objs[i].gameObject.transform;
            }
        }
    }

    void Update()
    {
        if (leaving)
        {
            var step = speed * Time.deltaTime;
            transform.parent.parent.position = Vector3.MoveTowards(transform.parent.parent.position, leavePoint.position, step);
        }

        if (Vector3.Distance(transform.parent.parent.position, leavePoint.position) < 0.01f)
        {
            hasArrived = true;
        }
        else
        {
            hasArrived = false;
        }
    }
}

