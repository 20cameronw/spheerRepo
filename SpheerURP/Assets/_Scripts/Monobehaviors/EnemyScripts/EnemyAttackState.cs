using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackState : EnemyState
{
    public EnemyLeavingState enemyLeavingState;
    public bool doneAttacking;
    public override EnemyState RunState()
    {
        if (doneAttacking)
        {
            return enemyLeavingState;
        }
        return this;
    }
}
