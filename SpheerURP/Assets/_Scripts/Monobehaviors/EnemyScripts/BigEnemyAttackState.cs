using System.Collections;
using UnityEngine;

public class BigEnemyAttackState : EnemyState
{
    [SerializeField] private EnemyLeavingState enemyLeavingState;
    [SerializeField] private float waitTimer = 2f;

    public float speed   = 2f;
    public float yOffset = 0f;

    private bool cr_running   = false;
    private bool doneAttacking = false;

    public override void OnStateEnter()
    {
        cr_running    = false;
        doneAttacking = false;
    }

    public override void OnStateExit()
    {
        StopAllCoroutines();
        cr_running    = false;
        doneAttacking = false;
        LeanTween.cancel(transform.parent.parent.gameObject);
    }

    public override EnemyState RunState()
    {
        if (!cr_running)
            StartCoroutine(Attack());

        if (doneAttacking)
        {
            cr_running    = false;
            doneAttacking = false;
            return enemyLeavingState;
        }
        return this;
    }

    private IEnumerator Attack()
    {
        cr_running = true;

        // Sweep across the full attack path (top or bottom of planet)
        Vector3[] sweepPath = EnemySpawner.Instance.GetAttackSweepPath(yOffset);
        int steps = sweepPath.Length;

        for (int i = 0; i < steps && cr_running; i++)
        {
            float dist     = Vector3.Distance(transform.parent.parent.position, sweepPath[i]);
            float duration = Mathf.Max(1f, dist / Mathf.Max(1f, speed));

            LeanTween.cancel(transform.parent.parent.gameObject);
            LeanTween.move(transform.parent.parent.gameObject, sweepPath[i], duration)
                .setEase(LeanTweenType.easeInOutSine);

            yield return new WaitForSeconds(duration);
        }

        yield return new WaitForSeconds(waitTimer);
        doneAttacking = true;
    }
}
