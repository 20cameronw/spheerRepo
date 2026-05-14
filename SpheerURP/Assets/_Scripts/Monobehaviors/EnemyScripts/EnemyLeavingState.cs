using UnityEngine;

public class EnemyLeavingState : EnemyState
{
    [SerializeField] private EnemyState enemyIdleState;
    [SerializeField] private float speed    = 12f;
    [SerializeField] private float waitTime = 2f;

    private Vector3 leavePosition;
    private bool    isMoving   = false;
    private bool    hasArrived = false;
    private bool    waiting    = false;
    private bool    doneWaiting = false;
    private float   timeWaited;

    public ParticleSystem effect;

    public override void OnStateEnter()
    {
        isMoving    = false;
        hasArrived  = false;
        waiting     = false;
        doneWaiting = false;
        timeWaited  = waitTime;

        // Clean up any suck effect from the attack phase
        if (effect != null)
        {
            Destroy(effect.gameObject);
            effect = null;
        }

        leavePosition = EnemySpawner.Instance.GetDynamicLeavePoint();
        float dist     = Vector3.Distance(transform.parent.parent.position, leavePosition);
        float duration = Mathf.Max(0.5f, dist / speed);
        isMoving = true;

        LeanTween.cancel(transform.parent.parent.gameObject);
        LeanTween.move(transform.parent.parent.gameObject, leavePosition, duration)
            .setEase(LeanTweenType.easeInOutSine)
            .setOnComplete(() =>
            {
                isMoving   = false;
                hasArrived = true;
            });
    }

    public override void OnStateExit()
    {
        isMoving    = false;
        hasArrived  = false;
        waiting     = false;
        doneWaiting = false;
        LeanTween.cancel(transform.parent.parent.gameObject);
    }

    public override EnemyState RunState()
    {
        if (hasArrived && !waiting)
            waiting = true;

        if (waiting)
        {
            timeWaited -= Time.deltaTime;
            if (timeWaited <= 0f)
            {
                waiting     = false;
                doneWaiting = true;
            }
        }

        if (doneWaiting)
        {
            doneWaiting = false;
            return enemyIdleState;
        }
        return this;
    }
}
