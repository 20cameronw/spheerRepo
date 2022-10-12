using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyApproachingState : EnemyState
{
    public EnemyAttackState enemyAttackState;
    public bool hasArrived;

    public override EnemyState RunState()
    {
        if (hasArrived)
        {
            return enemyAttackState;
        }
        return this;
    }
}
