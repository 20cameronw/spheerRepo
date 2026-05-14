using UnityEngine;

public abstract class EnemyState : MonoBehaviour
{
    public abstract EnemyState RunState();

    /// <summary>Called by EnemyStateManager when this state becomes active.</summary>
    public virtual void OnStateEnter() { }

    /// <summary>Called by EnemyStateManager just before this state is replaced.</summary>
    public virtual void OnStateExit() { }
}
