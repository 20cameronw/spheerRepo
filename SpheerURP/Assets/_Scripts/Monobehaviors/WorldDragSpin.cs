using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Spins the world in default (non-placement) mode with physics-feel momentum and glide.
///
/// Behaviour:
///  • Drag  → world spins following your finger; velocity is accumulated from swipe speed.
///  • Same-direction swipe → adds momentum on top of existing spin (feels like pushing a top).
///  • Release → world keeps gliding in the last spin direction; friction decays it gradually.
///  • Short tap on the world (no drag) → fires MineResource.
///  • Full revolution (360° of accumulated drag) → fires MineResource.
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

    [Tooltip("Minimum pixel movement before a touch is classified as a drag (not a tap).")]
    [SerializeField] private float dragThresholdPixels = 10f;

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

    // Input tracking
    private Vector3 lastInputPos;
    private Vector3 touchStartPos;
    private bool isDragging;

    // Tracks total degrees rotated during the current drag gesture.
    // When it reaches 360° a mine event is fired and the counter resets.
    private float accumulatedRotation;

    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        accumulatedRotation = 0f;
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
        touchStartPos = screenPos;
        lastInputPos  = screenPos;
        isDragging    = false;
        // NOTE: accumulatedRotation is intentionally NOT reset here so that
        // multi-stroke spinning (lift finger, swipe again) still counts toward
        // the 360° revolution threshold.
    }

    private void OnInputMoved(Vector3 screenPos)
    {
        Vector3 delta = screenPos - lastInputPos;

        if (!isDragging && Vector3.Distance(screenPos, touchStartPos) > dragThresholdPixels)
            isDragging = true;

        if (isDragging && delta.sqrMagnitude > 0.01f)
            AccumulateSpin(delta);

        lastInputPos = screenPos;
    }

    private void OnInputEnded(Vector3 screenPos, int fingerId)
    {
        if (!isDragging && !IsPointerOverUI(fingerId) && IsPointerOverWorld(screenPos))
            TriggerClick();

        isDragging = false;
        // Angular velocity set during the drag carries on — the glide takes over.
    }

    // ── Spin / velocity accumulation ──────────────────────────────────────────

    /// <summary>
    /// Converts this frame's screen delta into an instantaneous angular velocity,
    /// then blends it with the existing velocity — carrying momentum when swiping
    /// in the same direction, or overriding when reversing.
    /// Also rotates the world by the raw pixel delta so it tracks the finger exactly.
    /// Tracks accumulated rotation and fires MineResource on each full revolution.
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

        // Accumulate total angular displacement (degrees) for revolution detection.
        // Use vector magnitude so diagonal swipes are measured correctly.
        float degreesThisFrame = screenDelta.magnitude * spinSensitivity;
        accumulatedRotation += degreesThisFrame;
        if (accumulatedRotation >= 360f)
        {
            accumulatedRotation -= 360f;
            TriggerClick();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void TriggerClick()
    {
        if (eventManager != null)
            eventManager.MineResource();

        Player.Instance.MineResource();
        AudioManager.Instance.Play("click");
    }

    private bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;
#if UNITY_EDITOR || UNITY_STANDALONE
        return EventSystem.current.IsPointerOverGameObject();
#else
        return fingerId >= 0 && EventSystem.current.IsPointerOverGameObject(fingerId);
#endif
    }

    /// <summary>
    /// Returns true when a ray from the camera through <paramref name="screenPos"/>
    /// passes within the world's surface radius of the world centre.
    /// Uses geometric sphere intersection so no collider is required on the world prefab.
    /// </summary>
    private bool IsPointerOverWorld(Vector3 screenPos)
    {
        if (worldSpawner.CurrentWorld == null || mainCamera == null) return false;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        Vector3 worldCenter = worldSpawner.GetWorldCenter();
        float   radius      = worldSpawner.GetSurfaceRadius();

        // Project world center onto the ray and find the closest point.
        Vector3 toCenter = worldCenter - ray.origin;
        float   t        = Vector3.Dot(toCenter, ray.direction);
        if (t < 0f) return false; // world is behind the camera

        Vector3 closest = ray.origin + ray.direction * t;
        return Vector3.Distance(closest, worldCenter) <= radius;
    }
}
