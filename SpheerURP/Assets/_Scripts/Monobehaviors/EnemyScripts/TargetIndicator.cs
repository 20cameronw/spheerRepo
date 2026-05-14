using UnityEngine;

/// <summary>
/// Attach to the root of any enemy prefab.
/// Listens for Player.OnTargetChanged and enables/disables the targetIcon
/// child object to show which enemy is currently targeted.
/// </summary>
public class TargetIndicator : MonoBehaviour
{
    [Tooltip("Child GameObject that acts as the visible target icon (sprite, ring, etc.)")]
    [SerializeField] private GameObject targetIcon;

    void Start()
    {
        // Make sure the icon starts hidden
        if (targetIcon != null)
            targetIcon.SetActive(false);
    }

    void OnEnable()
    {
        Player.OnTargetChanged += HandleTargetChanged;
    }

    void OnDisable()
    {
        Player.OnTargetChanged -= HandleTargetChanged;
    }

    private void HandleTargetChanged(Transform newTarget)
    {
        if (targetIcon != null)
            targetIcon.SetActive(newTarget != null && newTarget == transform);
    }
}
