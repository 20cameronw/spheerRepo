using UnityEngine;

/// <summary>
/// Spins the world in default (non-placement) mode with physics-feel momentum and glide.
///
/// Behaviour:
///  • Drag  → world spins following your finger; velocity is accumulated from swipe speed.
///  • Same-direction swipe → adds momentum on top of existing spin (feels like pushing a top).
///  • Release → world keeps gliding in the last spin direction; friction decays it gradually.
///  • Full revolution (one finger-travel loop ≥ revolutionPixels px) → fires MineResource.
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
    [Tooltip("Total finger-travel distance in pixels that counts as one full revolution. " +
             "Lower = easier to trigger. Tune to match the visual feel of one world spin.")]
    [SerializeField] private float revolutionPixels = 600f;

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

    // Tracks total finger-travel distance (pixels) since the last revolution fired.
    // When it reaches revolutionPixels a mine event is fired and the counter resets.
    private float accumulatedPixels;

    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        accumulatedPixels = 0f;
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

        Transform world = worldSpawner.CurrentWorld.transform;
        world.Rotate(mainCamera.transform.right, velX * Time.deltaTime, Space.World);
        world.Rotate(mainCamera.transform.up,    velY * Time.deltaTime, Space.World);
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
    }

    // ── Spin / velocity accumulation ──────────────────────────────────────────

    /// <summary>
    /// Converts this frame's screen delta into an instantaneous angular velocity,
    /// then blends it with the existing velocity — carrying momentum when swiping
    /// in the same direction, or overriding when reversing.
    /// Also rotates the world by the raw pixel delta so it tracks the finger exactly.
    /// Tracks accumulated finger-travel distance and fires MineResource each time
    /// the player sweeps revolutionPixels worth of distance.
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
        Transform world = worldSpawner.CurrentWorld.transform;
        world.Rotate(mainCamera.transform.up,    -screenDelta.x * spinSensitivity, Space.World);
        world.Rotate(mainCamera.transform.right,  screenDelta.y * spinSensitivity, Space.World);

        // Accumulate raw finger-travel distance (pixels).
        // Using pixels directly decouples revolution detection from spinSensitivity,
        // so tuning the visual spin speed doesn't accidentally change how hard it is
        // to earn a resource.
        // Use a while-loop so an exceptionally fast swipe can award multiple resources.
        accumulatedPixels += screenDelta.magnitude;
        while (accumulatedPixels >= revolutionPixels)
        {
            accumulatedPixels -= revolutionPixels;
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

