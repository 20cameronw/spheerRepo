using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyLeavingState : EnemyState
{
    public EnemyIdleState enemyIdleState;
    public bool hasArrived;
    public override EnemyState RunState()
    {
        if (hasArrived)
        {
            return enemyIdleState;
        }
        return this;
    }
}
