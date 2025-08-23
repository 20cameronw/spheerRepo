using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BigEnemyApproachingState : EnemyState
{
    [SerializeField] private BigEnemyAttackState enemyAttackState;
    private Transform attackPoint;
    [SerializeField] private float speed;

    [SerializeField] private RectTransform button;

    private bool hasArrived;
    private bool approaching;

    public override EnemyState RunState()
    {
        approaching = true;

        if (hasArrived)
        {
            approaching = false;
            hasArrived = false;
            enemyAttackState.speed = this.speed - 10;
            return enemyAttackState;
        }
        return this;
    }

    void Awake()
    {
        attackPoint = EnemySpawner.Instance.getAttackPoint();
        if (attackPoint.gameObject.CompareTag("EnemyAttackPointBelow")) {
            transform.parent.parent.Rotate(180f, 0f, 0f);
            Vector2 newButtonPosition = button.anchoredPosition;
            newButtonPosition.y -= 14;
            button.anchoredPosition = newButtonPosition;
        }
    }
    void Update()
    {
        if (approaching)
        {
            var step = speed * Time.deltaTime;
            transform.parent.parent.position = Vector3.MoveTowards(transform.parent.parent.position, attackPoint.position, step);
        }

        if (Vector3.Distance(transform.parent.parent.position, attackPoint.position) < 0.01f)
        {
            hasArrived = true;
        }
        else
        {
            hasArrived = false;
        }
    }
}
