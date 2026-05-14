using System.Collections;
using UnityEngine;

public class BigEnemyIdleState : EnemyState
{
    [SerializeField] private BigEnemyApproachingState enemyApproachingState;
    [SerializeField] private float orbitSpeed = 6f;

    private bool doneCircling = false;
    private bool cr_running   = false;

    public override void OnStateEnter()
    {
        doneCircling = false;
        cr_running   = false;
    }

    public override void OnStateExit()
    {
        StopAllCoroutines();
        cr_running = false;
        LeanTween.cancel(transform.parent.parent.gameObject);
    }

    public override EnemyState RunState()
    {
        if (!cr_running)
            StartCoroutine(CircleOrbit());

        if (doneCircling)
        {
            doneCircling = false;
            return enemyApproachingState;
        }
        return this;
    }

    private IEnumerator CircleOrbit()
    {
        cr_running = true;
        Vector3[] orbitPath = EnemySpawner.Instance.GetCirclingPath(0f);

        foreach (Vector3 waypoint in orbitPath)
        {
            float dist     = Vector3.Distance(transform.parent.parent.position, waypoint);
            float duration = Mathf.Max(0.5f, dist / orbitSpeed);

            LeanTween.cancel(transform.parent.parent.gameObject);
            LeanTween.move(transform.parent.parent.gameObject, waypoint, duration)
                .setEase(LeanTweenType.easeInOutSine);

            yield return new WaitForSeconds(duration);
        }

        doneCircling = true;
        cr_running   = false;
    }
}
