using UnityEngine;

/// <summary>
/// Handles player tap/click input during an attack sequence.
/// Raycasts from the camera into the scene; if it hits an <see cref="IAttackable"/>
/// (an <see cref="AttackBuildingView"/> or the <see cref="AttackWorldView"/>),
/// it deals damage scaled by the player's power stat.
///
/// Place this MonoBehaviour on any active GameObject in the scene (e.g. Game Managers).
/// It self-enables/disables by subscribing to <see cref="AttackManager"/> events —
/// no manual enable/disable needed.
/// </summary>
public class AttackInputHandler : MonoBehaviour
{
    [Header("Combat")]
    [Tooltip("Base damage per tap. Multiplied by Player.getPower() at runtime.")]
    [SerializeField] private float damage = 25f;

    [Tooltip("Weapon type reported to the target so resistances/vulnerabilities apply.")]
    [SerializeField] private AttackWeaponType weaponType = AttackWeaponType.Turret;

    [Header("References")]
    [Tooltip("Camera used for raycasting. Defaults to Camera.main if left empty.")]
    [SerializeField] private Camera attackCamera;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        AttackManager.OnAttackStarted += HandleAttackStarted;
        AttackManager.OnAttackEnded   += HandleAttackEnded;

        // Stay disabled until an attack begins so Update() has no cost at idle.
        enabled = false;
    }

    private void OnDestroy()
    {
        AttackManager.OnAttackStarted -= HandleAttackStarted;
        AttackManager.OnAttackEnded   -= HandleAttackEnded;
    }

    private void Update()
    {
        bool tapped = Input.GetMouseButtonDown(0)
                   || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
        if (!tapped) return;

        Camera cam = attackCamera != null ? attackCamera : Camera.main;
        if (cam == null) return;

        // Support both mouse and touch screen
        Vector3 screenPoint = Input.touchCount > 0
            ? (Vector3)Input.GetTouch(0).position
            : Input.mousePosition;

        Ray ray = cam.ScreenPointToRay(screenPoint);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        // Prefer a specific building hit; fall back to the whole world.
        IAttackable target = hit.collider.GetComponentInParent<AttackBuildingView>()
                          ?? (IAttackable)hit.collider.GetComponentInParent<AttackWorldView>();
        if (target == null || target.IsDestroyed) return;

        float effectiveDamage = damage * (Player.Instance != null ? Player.Instance.getPower() : 1f);
        target.TakeDamage(effectiveDamage, weaponType);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandleAttackStarted(EnemyBaseData _) => enabled = true;
    private void HandleAttackEnded(bool _)             => enabled = false;
}
