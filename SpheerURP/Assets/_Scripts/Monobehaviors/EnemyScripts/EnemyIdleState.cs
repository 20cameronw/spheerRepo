using System.Collections;
using UnityEngine;

public class EnemyIdleState : EnemyState
{
    [SerializeField] private EnemyApproachingState enemyApproachingState;
    [SerializeField] private float flyInSpeed = 10f;

    private bool doneFlyIn  = false;
    private bool cr_running = false;

    public override void OnStateEnter()
    {
        doneFlyIn  = false;
        cr_running = false;
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
            StartCoroutine(FlyIn());

        if (doneFlyIn)
        {
            doneFlyIn = false;
            return enemyApproachingState;
        }
        return this;
    }

    private IEnumerator FlyIn()
    {
        cr_running = true;

        // Fly from wherever we are (off-screen) to a neutral staging area near the planet
        Vector3 stagingPoint = EnemySpawner.Instance.GetStagingPoint();
        float   dist         = Vector3.Distance(transform.parent.parent.position, stagingPoint);
        float   duration     = Mathf.Max(0.5f, dist / flyInSpeed);

        LeanTween.cancel(transform.parent.parent.gameObject);
        LeanTween.move(transform.parent.parent.gameObject, stagingPoint, duration)
            .setEase(LeanTweenType.easeInOutQuad);

        yield return new WaitForSeconds(duration);

        doneFlyIn  = true;
        cr_running = false;
    }
}
