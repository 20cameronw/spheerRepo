using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyIdleState : EnemyState
{
    public EnemyApproachingState enemyApproachingState;
    public bool timeLimitReached;

    public override EnemyState RunState()
    {
        if (timeLimitReached)
        {
            return enemyApproachingState;
        }
        return this;
    }
}
