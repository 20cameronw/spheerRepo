using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BigEnemyAttackState : EnemyState
{
    [SerializeField] private EnemyLeavingState enemyLeavingState;
    [SerializeField] private float waitTimer;

    private bool cr_running = false;
    private bool doneAttacking = false;
    public float speed;

    public override EnemyState RunState() {
        if (!cr_running) {
            StartCoroutine("Attack");
        }

        if (doneAttacking) {
            cr_running = false;
            doneAttacking = false;
            return enemyLeavingState;
        }
        return this;
    }

    private IEnumerator Attack() {
        cr_running = true;
        yield return new WaitForSeconds(waitTimer);
        doneAttacking = true;
    }
}
