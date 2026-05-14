using UnityEngine;

public class EnemyStateManager : MonoBehaviour
{
    [SerializeField] private EnemyState currentState;

    void Start()
    {
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
}
