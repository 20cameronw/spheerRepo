using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyIdleState : EnemyState
{
    [SerializeField] private EnemyApproachingState enemyApproachingState;
    [SerializeField] private bool timeLimitReached;
    [SerializeField] private float waitTime;
    public bool cr_running;


    public override EnemyState RunState()
    {
        if (!cr_running)
        {
            StartCoroutine("Timer");
        }

        if (timeLimitReached)
        {
            timeLimitReached = false;
            return enemyApproachingState;
        }
        return this;
    }

    private IEnumerator Timer()
    {
        cr_running = true;
        yield return new WaitForSeconds(waitTime);
        timeLimitReached = true;
        cr_running = false;
    }
}
