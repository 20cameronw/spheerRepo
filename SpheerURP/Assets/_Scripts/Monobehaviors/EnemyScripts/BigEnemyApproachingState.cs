using UnityEngine;

public class BigEnemyApproachingState : EnemyState
{
    [SerializeField] private BigEnemyAttackState enemyAttackState;
    [SerializeField] private float speed = 12f;
    [SerializeField] private RectTransform button;

    private bool  isBelow;
    private bool  hasArrived = false;
    public  float yOffset { get; private set; }

    void Awake()
    {
        isBelow = Random.value > 0.5f;

        if (isBelow)
        {
            transform.parent.parent.Rotate(180f, 0f, 0f);
            if (button != null)
            {
                Vector2 pos = button.anchoredPosition;
                pos.y -= 14f;
                button.anchoredPosition = pos;
            }
        }

        yOffset = isBelow ? -EnemySpawner.Instance.AttackYOffset
                          :  EnemySpawner.Instance.AttackYOffset;

        // Reposition to the correct off-screen spawn point for the chosen side
        float ySide = isBelow ? -1f : 1f;
        transform.parent.parent.position = EnemySpawner.Instance.GetOffScreenSpawnPoint(ySide);
    }

    public override void OnStateEnter()
    {
        hasArrived = false;

        // Fly to the start of the attack sweep path
        Vector3[] sweepPath = EnemySpawner.Instance.GetAttackSweepPath(yOffset);
        Vector3   target    = sweepPath[0];

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
            hasArrived               = false;
            enemyAttackState.speed   = Mathf.Max(2f, speed - 10f);
            enemyAttackState.yOffset = this.yOffset;
            return enemyAttackState;
        }
        return this;
    }
}
