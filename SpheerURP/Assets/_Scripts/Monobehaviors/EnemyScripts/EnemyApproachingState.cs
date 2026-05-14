using UnityEngine;

public class EnemyApproachingState : EnemyState
{
    [SerializeField] private EnemyAttackState enemyAttackState;
    [SerializeField] private float speed = 15f;
    [SerializeField] private RectTransform button;

    private bool     isBelow;
    private bool     hasArrived = false;
    private Vector3[] attackPath;

    void Awake()
    {
        // Decide once at spawn whether this enemy attacks from above or below
        isBelow = Random.value > 0.5f;

        if (isBelow)
        {
            transform.parent.parent.Rotate(180f, 0f, 0f);
            if (button != null)
            {
                Vector2 pos = button.anchoredPosition;
                pos.y -= 8f;
                button.anchoredPosition = pos;
            }
        }

        // Reposition to the correct off-screen spawn point for the chosen side
        float ySide = isBelow ? -1f : 1f;
        transform.parent.parent.position = EnemySpawner.Instance.GetOffScreenSpawnPoint(ySide);
    }

    public override void OnStateEnter()
    {
        hasArrived = false;

        float   yOffset = isBelow ? -EnemySpawner.Instance.AttackYOffset
                                  :  EnemySpawner.Instance.AttackYOffset;
        attackPath      = EnemySpawner.Instance.GetAttackSweepPath(yOffset);
        Vector3 target  = attackPath[0]; // start of the horizontal sweep

        float dist     = Vector3.Distance(transform.parent.parent.position, target);
        float duration = Mathf.Max(0.5f, dist / speed);

        LeanTween.cancel(transform.parent.parent.gameObject);
        LeanTween.move(transform.parent.parent.gameObject, target, duration)
            .setEase(LeanTweenType.easeInOutSine)
            .setOnComplete(() => hasArrived = true);
    }

    public override void OnStateExit()
    {
        hasArrived = false;
        LeanTween.cancel(transform.parent.parent.gameObject);
    }

    public override EnemyState RunState()
    {
        if (hasArrived)
        {
            hasArrived             = false;
            enemyAttackState.waypoints = attackPath;
            enemyAttackState.speed     = Mathf.Max(2f, speed - 10f);
            return enemyAttackState;
        }
        return this;
    }
}
