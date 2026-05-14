using UnityEngine;

public class BigEnemyApproachingState : EnemyState
{
    [SerializeField] private BigEnemyAttackState enemyAttackState;
    [SerializeField] private float speed = 12f;
    [SerializeField] private float attackYOffset = 3f;
    [SerializeField] private RectTransform button;

    private bool  isBelow;
    private bool  isMoving   = false;
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
        yOffset = isBelow ? -attackYOffset : attackYOffset;
    }

    public override void OnStateEnter()
    {
        isMoving   = false;
        hasArrived = false;

        Vector3 target = EnemySpawner.Instance.GetDynamicApproachPoint(yOffset);
        float dist     = Vector3.Distance(transform.parent.parent.position, target);
        float duration = Mathf.Max(0.5f, dist / speed);
        isMoving = true;

        LeanTween.cancel(transform.parent.parent.gameObject);
        LeanTween.move(transform.parent.parent.gameObject, target, duration)
            .setEase(LeanTweenType.easeInOutSine)
            .setOnComplete(() =>
            {
                isMoving   = false;
                hasArrived = true;
            });
    }

    public override void OnStateExit()
    {
        isMoving   = false;
        hasArrived = false;
        LeanTween.cancel(transform.parent.parent.gameObject);
    }

    public override EnemyState RunState()
    {
        if (hasArrived)
        {
            hasArrived = false;
            enemyAttackState.speed   = Mathf.Max(2f, speed - 10f);
            enemyAttackState.yOffset = this.yOffset;
            return enemyAttackState;
        }
        return this;
    }
}
