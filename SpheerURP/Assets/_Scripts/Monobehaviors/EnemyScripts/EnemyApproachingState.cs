using UnityEngine;

public class EnemyApproachingState : EnemyState
{
    [SerializeField] private EnemyAttackState enemyAttackState;
    [SerializeField] private float speed = 15f;
    [SerializeField] private float attackYOffset = 3f;
    [SerializeField] private RectTransform button;

    private bool isBelow;
    private bool isMoving  = false;
    private bool hasArrived = false;
    private Vector3[] attackPath;

    void Awake()
    {
        // Decide once at spawn whether this enemy comes from above or below
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
    }

    public override void OnStateEnter()
    {
        isMoving   = false;
        hasArrived = false;

        float yOffset  = isBelow ? -attackYOffset : attackYOffset;
        attackPath     = EnemySpawner.Instance.GetDynamicAttackPath(yOffset);
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
            enemyAttackState.waypoints = attackPath;
            enemyAttackState.speed     = Mathf.Max(2f, speed - 10f);
            return enemyAttackState;
        }
        return this;
    }
}
