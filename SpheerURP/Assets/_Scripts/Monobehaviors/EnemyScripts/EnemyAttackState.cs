using System.Collections;
using UnityEngine;

public class EnemyAttackState : EnemyState
{
    [SerializeField] private EnemyLeavingState enemyLeavingState;
    [SerializeField] private int   timesToRaycast   = 5;
    [SerializeField] private float timeBetweenCasts = 0.5f;
    [SerializeField] private float raycastRange     = 10f;
    [SerializeField] private ParticleSystem suckingEffect;

    public float    speed    = 5f;
    public Vector3[] waypoints;

    private int  currentWaypointIndex = 0;
    private bool doneAttacking        = false;
    private bool cr_running           = false;
    private bool gotAHit              = false;
    public  ParticleSystem effect;

    public override void OnStateEnter()
    {
        currentWaypointIndex = 0;
        doneAttacking        = false;
        cr_running           = false;
        gotAHit              = false;
    }

    public override void OnStateExit()
    {
        StopAllCoroutines();
        cr_running    = false;
        doneAttacking = false;
        gotAHit       = false;
    }

    public override EnemyState RunState()
    {
        if (!cr_running)
            StartCoroutine(Attack());

        if (doneAttacking)
        {
            enemyLeavingState.effect = this.effect;
            this.effect              = null;
            currentWaypointIndex     = 0;
            return enemyLeavingState;
        }
        return this;
    }

    private IEnumerator Attack()
    {
        cr_running = true;
        int timesCasted = 0;

        while (timesCasted < timesToRaycast && cr_running)
        {
            yield return new WaitForSeconds(timeBetweenCasts);

            RaycastHit hit;
            Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.down) * raycastRange, Color.yellow);
            if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.down), out hit, raycastRange))
            {
                gotAHit = true;
                AudioManager.Instance.Play("GetSuckedUp");

                GameObject prey = hit.transform.gameObject;
                if (prey && !doneAttacking)
                {
                    prey.GetComponent<GetSuckedUp>().getSuckedUp(transform);
                    effect = Instantiate(suckingEffect, transform.position, Quaternion.identity);
                    effect.transform.SetParent(transform);
                    effect.transform.LookAt(prey.transform);
                    yield return new WaitForSeconds(1f);
                }
                doneAttacking = true;
            }

            timesCasted++;
        }
        doneAttacking = true;
    }

    void Update()
    {
        if (cr_running && !gotAHit)
            MoveTowardsWaypoint();
    }

    private void MoveTowardsWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        float step = speed * Time.deltaTime;
        Transform root = transform.parent.parent;
        root.position = Vector3.MoveTowards(root.position, waypoints[currentWaypointIndex], step);

        if (Vector3.Distance(root.position, waypoints[currentWaypointIndex]) <= 0.2f)
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
    }
}