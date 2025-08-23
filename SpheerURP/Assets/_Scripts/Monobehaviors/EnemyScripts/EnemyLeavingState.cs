using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyLeavingState : EnemyState
{
    [SerializeField] private EnemyState enemyIdleState;
    [SerializeField] private Transform leavePoint;
    [SerializeField] private float speed;

    [SerializeField] private float waitTime;
    private bool hasArrived;
    private bool leaving;

    private bool waiting;

    private bool doneWaiting;

    private float timeWaited;

    public ParticleSystem effect;

    public override EnemyState RunState()
    {
        leaving = true;

        if (doneWaiting)
        {
            leaving = false;
            hasArrived = false;
            doneWaiting = false;
            return enemyIdleState;
        }
        return this;
    }

    void Awake()
    {
        leavePoint = EnemySpawner.Instance.getLeavePoint();

        timeWaited = waitTime;
        if (effect != null) {
            Destroy(effect.gameObject);
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
            // Player.Instance.clearTarget();
            waiting = true;
        }
        else
        {
            hasArrived = false;
        }

        if (waiting)
        {
            timeWaited -= Time.deltaTime;
            if (timeWaited <= 0)
            {
                timeWaited = waitTime;
                waiting = false;
                doneWaiting = true;
            }
        }
    }
}

