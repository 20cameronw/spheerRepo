using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Spins the world in default (non-placement) mode with physics-feel momentum and glide.
///
/// Behaviour:
///  • Drag  → world spins following your finger; velocity is accumulated from swipe speed.
///  • Same-direction swipe → adds momentum on top of existing spin (feels like pushing a top).
///  • Release → world keeps gliding in the last spin direction; friction decays it gradually.
///  • Every time the world has physically rotated degreesPerRevolution degrees (drag + glide
///    both count) → fires MineResource.
///
/// The Rotate component on the world is no longer needed; this script owns all rotation.
/// PlacementManager's drag-spin is unaffected — this script steps aside when placement mode is active.
///
/// Attach to your Manager GameObject and wire up the Inspector references.
/// </summary>
public class WorldDragSpin : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private WorldSpawner worldSpawner;
    [SerializeField] private UIManager eventManager;
    [SerializeField] private Camera mainCamera;

    [Header("Spin Sensitivity")]
    [Tooltip("Converts swipe pixels-per-second into degrees-per-second of angular velocity.")]
    [SerializeField] private float spinSensitivity = 0.25f;

    [Header("Revolution Detection")]
    [Tooltip("How many degrees the world must physically rotate to earn one resource. " +
             "360 = one full spin. Lower values make it easier to trigger.")]
    [SerializeField] private float degreesPerRevolution = 360f;

    [Header("Glide / Momentum")]
    [Tooltip("Velocity retained per frame at 60 fps. 0.99 = very long glide, 0.95 = short glide.")]
    [Range(0.90f, 0.999f)]
    [SerializeField] private float glide = 0.985f;

    [Tooltip("How much existing velocity is carried forward when swiping in the same direction.")]
    [Range(0f, 1f)]
    [SerializeField] private float momentumCarry = 0.4f;

    [Tooltip("Maximum angular speed in degrees per second.")]
    [SerializeField] private float maxAngularSpeed = 720f;

    // Angular velocity in degrees/second, camera-relative axes.
    // velX → rotation around camera.right  (up/down swipe → tilt)
    // velY → rotation around camera.up     (left/right swipe → yaw)
    private float velX;
    private float velY;

    // Guard against division by zero when deltaTime is nearly zero on the first frame.
    private const float MIN_DELTA_TIME = 0.001f;

    // Minimum squared screen-delta magnitude before a move event is processed.
    // Filters out sub-pixel jitter without needing a serialized threshold.
    private const float MIN_DRAG_DELTA_SQR = 0.01f;

    // Input tracking
    private Vector3 lastInputPos;
    private bool isDragging;

    // Tracks actual world rotation (degrees) since the last revolution fired.
    // Both drag and glide contribute. Fires TriggerMine every degreesPerRevolution.
    private float accumulatedDegrees;

    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        accumulatedDegrees = 0f;
    }

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        // PlacementManager owns the world while placing — step aside.
        if (PlacementManager.Instance != null && PlacementManager.Instance.IsInPlacementMode())
            return;

        // Stop all spinning when any menu panel is open so the player cannot
        // accidentally earn money by scrolling through the shop.
        if (eventManager != null && eventManager.IsAnyPanelOpen())
        {
            velX = 0f;
            velY = 0f;
            isDragging = false;
            return;
        }

        HandleInput();

        // Apply glide every frame when the player is not actively dragging.
        if (!isDragging)
            ApplyGlide();
    }

    // ── Glide ─────────────────────────────────────────────────────────────────

    private void ApplyGlide()
    {
        if (worldSpawner.CurrentWorld == null || mainCamera == null) return;

        // Frame-rate-independent decay: same effective friction regardless of fps.
        float decay = Mathf.Pow(glide, Time.deltaTime * 60f);
        velX *= decay;
        velY *= decay;

        float degX = velX * Time.deltaTime;
        float degY = velY * Time.deltaTime;

        Transform world = worldSpawner.CurrentWorld.transform;
        world.Rotate(mainCamera.transform.right, degX, Space.World);
        world.Rotate(mainCamera.transform.up,    degY, Space.World);

        // Both axes contribute to actual world rotation.
        AddDegrees(Mathf.Sqrt(degX * degX + degY * degY));
    }

    // ── Input handling ────────────────────────────────────────────────────────

    private void HandleInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
            OnInputBegan(Input.mousePosition);
        else if (Input.GetMouseButton(0))
            OnInputMoved(Input.mousePosition);
        else if (Input.GetMouseButtonUp(0))
            OnInputEnded(Input.mousePosition, fingerId: -1);
#else
        if (Input.touchCount != 1) return;
        Touch touch = Input.GetTouch(0);
        Vector3 touchPos = touch.position;

        switch (touch.phase)
        {
            case TouchPhase.Began:
                OnInputBegan(touchPos);
                break;
            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                OnInputMoved(touchPos);
                break;
            case TouchPhase.Ended:
                OnInputEnded(touchPos, touch.fingerId);
                break;
        }
#endif
    }

    private void OnInputBegan(Vector3 screenPos)
    {
        lastInputPos = screenPos;
        isDragging   = false;
    }

    private void OnInputMoved(Vector3 screenPos)
    {
        Vector3 delta = screenPos - lastInputPos;

        if (delta.sqrMagnitude > MIN_DRAG_DELTA_SQR)
        {
            isDragging = true;
            AccumulateSpin(delta);
        }

        lastInputPos = screenPos;
    }

    private void OnInputEnded(Vector3 screenPos, int fingerId)
    {
        isDragging = false;
        // Angular velocity set during the drag carries on — the glide takes over.
        // If this was a tap (no drag), check whether the player hit an enemy.
        TryTapEnemy(screenPos, fingerId);
    }

    // ── Enemy tap ─────────────────────────────────────────────────────────────

    /// <summary>
    /// If the tap (no-drag release) lands on an enemy, deal tap damage and show a hit marker.
    /// Ignored when a UI element is under the pointer.
    /// </summary>
    private void TryTapEnemy(Vector3 screenPos, int fingerId)
    {
        if (mainCamera == null) return;

        if (EventSystem.current != null)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (EventSystem.current.IsPointerOverGameObject()) return;
#else
            if (fingerId >= 0 && EventSystem.current.IsPointerOverGameObject(fingerId)) return;
#endif
        }

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray);
        foreach (RaycastHit hit in hits)
        {
            EnemyStateManager enemy = hit.collider.GetComponentInParent<EnemyStateManager>();
            if (enemy != null)
            {
                enemy.TapEnemy();
                return;
            }
        }
    }

    // ── Spin / velocity accumulation ──────────────────────────────────────────

    /// <summary>
    /// Converts this frame's screen delta into an instantaneous angular velocity,
    /// then blends it with the existing velocity — carrying momentum when swiping
    /// in the same direction, or overriding when reversing.
    /// Also rotates the world by the raw pixel delta so it tracks the finger exactly.
    /// Actual degrees applied to the world are passed to AddDegrees for revolution tracking.
    /// </summary>
    private void AccumulateSpin(Vector3 screenDelta)
    {
        if (worldSpawner.CurrentWorld == null || mainCamera == null) return;

        // dt guard: avoid division by zero on the first frame.
        float dt = Mathf.Max(Time.deltaTime, MIN_DELTA_TIME);

        // Instantaneous velocity from this swipe (degrees / second).
        float instVelY = -screenDelta.x / dt * spinSensitivity;
        float instVelX =  screenDelta.y / dt * spinSensitivity;

        // If swiping in the same direction as current spin, carry existing momentum.
        float carryY = (Mathf.Sign(instVelY) == Mathf.Sign(velY)) ? momentumCarry : 0f;
        float carryX = (Mathf.Sign(instVelX) == Mathf.Sign(velX)) ? momentumCarry : 0f;

        velY = instVelY + velY * carryY;
        velX = instVelX + velX * carryX;

        // Cap to prevent runaway speed.
        velY = Mathf.Clamp(velY, -maxAngularSpeed, maxAngularSpeed);
        velX = Mathf.Clamp(velX, -maxAngularSpeed, maxAngularSpeed);

        // Rotate the world this frame so it tracks the finger directly.
        float degY = -screenDelta.x * spinSensitivity;
        float degX =  screenDelta.y * spinSensitivity;

        Transform world = worldSpawner.CurrentWorld.transform;
        world.Rotate(mainCamera.transform.up,    degY, Space.World);
        world.Rotate(mainCamera.transform.right, degX, Space.World);

        // Accumulate actual world rotation (degrees) for revolution detection.
        AddDegrees(Mathf.Sqrt(degX * degX + degY * degY));
    }

    /// <summary>
    /// Adds <paramref name="degrees"/> to the revolution counter and fires TriggerMine
    /// for each completed revolution.
    /// </summary>
    private void AddDegrees(float degrees)
    {
        accumulatedDegrees += degrees;
        while (accumulatedDegrees >= degreesPerRevolution)
        {
            accumulatedDegrees -= degreesPerRevolution;
            TriggerMine();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void TriggerMine()
    {
        if (eventManager != null)
            eventManager.MineResource();

        Player.Instance.MineResource();
        AudioManager.Instance.Play("click");
    }
}

