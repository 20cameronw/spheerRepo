using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Allows the player to spin the world by dragging in default (non-placement) mode.
/// A short tap (no drag) still fires the normal <see cref="EventManager.mineResource"/> click action.
/// Attach this MonoBehaviour to the same Manager GameObject that holds
/// <see cref="PlacementManager"/> and wire up the Inspector references.
/// </summary>
public class WorldDragSpin : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private WorldSpawner worldSpawner;
    [SerializeField] private EventManager eventManager;
    [SerializeField] private Camera mainCamera;

    [Header("Spin Settings")]
    [Tooltip("How many degrees the world rotates per pixel of drag movement.")]
    [SerializeField] private float spinSensitivity = 0.3f;

    [Tooltip("Minimum pixel movement before a touch is classified as a drag (not a tap).")]
    [SerializeField] private float dragThresholdPixels = 10f;

    // Internal state
    private Vector3 lastInputPos;
    private Vector3 touchStartPos;
    private bool isDragging = false;
    private bool wasAutoRotating = false;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        // Yield control to PlacementManager when it is active
        if (PlacementManager.Instance != null && PlacementManager.Instance.IsInPlacementMode())
            return;

        HandleInput();
    }

    // ── Input handling ────────────────────────────────────────────────────────

    private void HandleInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            OnInputBegan(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0))
        {
            OnInputMoved(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            OnInputEnded(Input.mousePosition, fingerId: -1);
        }
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
    }

    private void OnInputMoved(Vector3 screenPos)
    {
        Vector3 delta = screenPos - lastInputPos;

        if (!isDragging && Vector3.Distance(screenPos, touchStartPos) > dragThresholdPixels)
        {
            isDragging = true;
            // Pause auto-rotation while the player is manually spinning
            wasAutoRotating = true;
            worldSpawner.SetAutoRotate(false);
        }

        if (isDragging && delta.sqrMagnitude > 0.01f)
            SpinWorld(delta);

        lastInputPos = screenPos;
    }

    private void OnInputEnded(Vector3 screenPos, int fingerId)
    {
        if (isDragging)
        {
            // Resume auto-rotation after the drag finishes
            if (wasAutoRotating)
                worldSpawner.SetAutoRotate(true);
        }
        else
        {
            // Short tap — fire the mine/click action (skip if over a UI element)
            if (!IsPointerOverUI(fingerId))
                TriggerClick();
        }

        isDragging     = false;
        wasAutoRotating = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SpinWorld(Vector3 screenDelta)
    {
        if (worldSpawner.CurrentWorld == null || mainCamera == null) return;

        float rotY = -screenDelta.x * spinSensitivity;
        float rotX =  screenDelta.y * spinSensitivity;
        Transform world = worldSpawner.CurrentWorld.transform;
        world.Rotate(mainCamera.transform.up,    rotY, Space.World);
        world.Rotate(mainCamera.transform.right, rotX, Space.World);
    }

    private void TriggerClick()
    {
        if (eventManager != null)
            eventManager.mineResource();
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
}
