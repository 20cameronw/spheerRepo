using UnityEngine;

public class EnemyStateManager : MonoBehaviour
{
    [SerializeField] private EnemyState currentState;

    [Header("Tap Attack")]
    [Tooltip("Damage dealt each time the player taps this enemy.")]
    [SerializeField] private float tapDamage = 10f;

    private UIManager uiManager;

    void Start()
    {
        uiManager = FindObjectOfType<UIManager>();
        currentState?.OnStateEnter();
    }

    void Update()
    {
        RunStateMachine();
    }

    private void RunStateMachine()
    {
        EnemyState nextState = currentState?.RunState();

        if (nextState != null && nextState != currentState)
        {
            SwitchToNextState(nextState);
        }
    }

    private void SwitchToNextState(EnemyState nextState)
    {
        currentState?.OnStateExit();
        LeanTween.cancel(gameObject);
        currentState = nextState;
        currentState.OnStateEnter();
    }

    public void targetThis()
    {
        Player.Instance.targetThis(transform);
    }

    /// <summary>
    /// Call this from the enemy UI button's OnClick list to deal tap damage and show a hit marker.
    /// </summary>
    public void TapEnemy()
    {
        EnemyHealth health = GetComponentInChildren<EnemyHealth>();
        if (health == null) health = GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.TakeDamage(tapDamage);
            if (uiManager != null)
                uiManager.ShowHitMarker(transform.position, tapDamage);
        }
    }
}
