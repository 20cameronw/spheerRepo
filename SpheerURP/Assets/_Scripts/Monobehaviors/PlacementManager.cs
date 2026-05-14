using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Manages the interactive building-placement flow:
/// <list type="number">
///   <item>Player buys a surface building → TransactionManager calls <see cref="EnterPlacementMode"/>.</item>
///   <item>World moves closer (Z axis).  All open UI panels close.</item>
///   <item>Blue slot markers appear on every available surface slot.</item>
///   <item>Player drags one finger to spin the world.</item>
///   <item>Player taps a blue dot → a Confirm button appears.</item>
///   <item>Player taps Confirm → building spawns there, world moves back out.</item>
///   <item>Or player taps Cancel → purchase is refunded.</item>
/// </list>
/// </summary>
public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance;

    [Header("Scene References")]
    [SerializeField] private WorldSpawner worldSpawner;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private StructuresPanel structuresPanel;
    [SerializeField] private Camera mainCamera;

    [Header("Placement UI")]
    /// <summary>
    /// A Canvas/Panel that is shown only during placement mode.
    /// It should contain a Cancel button wired to <see cref="CancelPlacement"/>.
    /// </summary>
    [SerializeField] private GameObject placementOverlayUI;

    /// <summary>
    /// Button (or panel) that is shown after the player taps a slot marker,
    /// allowing them to confirm the placement.  Wire its onClick to
    /// <see cref="ConfirmSelectedSlot"/>.
    /// </summary>
    [SerializeField] private GameObject confirmButton;

    [Header("Slot Visuals")]
    /// <summary>
    /// Prefab used for each available-slot indicator (blue dot).
    /// Must have: MeshRenderer, Collider, and PlacementSlot component.
    /// A sphere primitive (scale ~0.15) with a blue, slightly transparent
    /// URP Lit material works well.  See README / Editor Setup notes.
    /// </summary>
    [SerializeField] private GameObject slotMarkerPrefab;

    [Header("World Zoom Settings")]
    [Tooltip("Z position of the world during normal gameplay.")]
    [SerializeField] private float normalWorldZ = 0f;
    [Tooltip("Z position of the world when zoomed in for placement (negative = closer to camera).")]
    [SerializeField] private float placementWorldZ = -70f;
    [SerializeField] private float worldZoomDuration = 0.5f;

    [Header("World Spin Sensitivity")]
    [SerializeField] private float spinSensitivity = 0.3f;

    [Tooltip("Minimum pixel movement before a touch is classified as a drag (not a tap).")]
    [SerializeField] private float dragThresholdPixels = 10f;
    private bool inPlacementMode = false;
    private int pendingUpgradeIndex = -1;
    private float pendingCost = 0f;
    private int selectedSlotIndex = -1;

    private readonly List<GameObject> activeMarkers = new List<GameObject>();

    // Touch / drag tracking
    private Vector3 lastInputPos;
    private Vector3 touchStartPos;
    private bool isDragging = false;

    // ─────────────────────────────────────────────────────────────────────────

    public bool IsInPlacementMode() => inPlacementMode;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
                Debug.LogWarning("[PlacementManager] No camera assigned and Camera.main is null. "
                    + "Assign the Main Camera in the PlacementManager Inspector field.");
        }
    }

    // ── Entry / exit ──────────────────────────────────────────────────────────

    /// <summary>
    /// Call this after the cost has been deducted but BEFORE incrementing
    /// building count or spawning the building.  The manager owns the rest.
    /// </summary>
    public void EnterPlacementMode(int upgradeIndex, float cost)
    {
        if (inPlacementMode) return;

        inPlacementMode    = true;
        pendingUpgradeIndex = upgradeIndex;
        pendingCost         = cost;

        // Pause world auto-spin so the player's drag controls it
        worldSpawner.SetAutoRotate(false);

        // Close any currently open UI panel
        uiManager.ClosePanel();

        // Show placement overlay (Cancel button); confirm button hidden until a slot is selected
        if (placementOverlayUI != null)
            placementOverlayUI.SetActive(true);
        if (confirmButton != null)
            confirmButton.SetActive(false);

        // Animate world Z closer then reveal slot markers
        MoveWorldZ(placementWorldZ, ShowSlotMarkers);
    }

    private void ExitPlacementMode()
    {
        inPlacementMode     = false;
        pendingUpgradeIndex = -1;
        pendingCost         = 0f;
        selectedSlotIndex   = -1;

        ClearMarkers();

        if (placementOverlayUI != null)
            placementOverlayUI.SetActive(false);
        if (confirmButton != null)
            confirmButton.SetActive(false);

        worldSpawner.SetAutoRotate(true);

        MoveWorldZ(normalWorldZ);
    }

    // ── Player actions ────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the player taps a blue dot slot marker.
    /// Spawns the building, applies passive income, and exits placement mode.
    /// </summary>
    public void ConfirmPlacement(int slotIndex)
    {
        if (!inPlacementMode) return;

        int slotSize = TransactionManager.Instance.structuresPanelInfo
            .shopItemsSO[pendingUpgradeIndex].slotSize;
        slotSize = Mathf.Max(1, slotSize);

        // Occupy slot(s) — use the slot marker's world position as the anchor
        GameObject marker = GetMarkerBySlotIndex(slotIndex);
        Vector3 spawnPos = marker != null ? marker.transform.position
                                          : worldSpawner.GetWorldCenter();

        worldSpawner.OccupySlot(spawnPos, slotSize);
        worldSpawner.SpawnAtPosition(pendingUpgradeIndex, spawnPos);

        // Now it's safe to register the building in the player state
        Player.Instance.AddBuildingCount(pendingUpgradeIndex);
        Player.Instance.AddPassive(TransactionManager.Instance.structuresPanelInfo
            .shopItemsSO[pendingUpgradeIndex].bonus);

        structuresPanel.LoadCards();

        ExitPlacementMode();
    }

    /// <summary>
    /// Wired to the Cancel button in the placement overlay UI.
    /// Refunds the purchase and exits placement mode.
    /// </summary>
    public void CancelPlacement()
    {
        if (!inPlacementMode) return;

        Player.Instance.AddDollars(pendingCost);

        string msg = "+" + pendingCost.ToString("F2") + " (refunded)";
        uiManager.CreateAnimatedText(msg, Color.yellow, 0.6f);

        structuresPanel.LoadCards();

        ExitPlacementMode();
    }

    // ── World Z zoom ──────────────────────────────────────────────────────────

    private void MoveWorldZ(float targetZ, System.Action onComplete = null)
    {
        GameObject world = worldSpawner.CurrentWorld;
        if (world == null) { onComplete?.Invoke(); return; }

        Vector3 target = world.transform.position;
        target.z = targetZ;

        LeanTween.move(world, target, worldZoomDuration)
            .setEase(LeanTweenType.easeInOutQuad)
            .setOnComplete(() => onComplete?.Invoke());
    }

    // ── Slot markers ──────────────────────────────────────────────────────────

    private void ShowSlotMarkers()
    {
        ClearMarkers();

        if (slotMarkerPrefab == null)
        {
            Debug.LogWarning("[PlacementManager] slotMarkerPrefab is not assigned. "
                + "Create a blue sphere prefab, add a PlacementSlot component, "
                + "and assign it in the Inspector.");
            return;
        }

        int slotSize = TransactionManager.Instance.structuresPanelInfo
            .shopItemsSO[pendingUpgradeIndex].slotSize;
        slotSize = Mathf.Max(1, slotSize);

        // GetAvailableSlotPositions returns (worldPos, slotIndex) pairs in one pass.
        var available = worldSpawner.GetAvailableSlotPositions(slotSize);

        foreach (var (pos, slotIndex) in available)
        {
            GameObject marker = Instantiate(slotMarkerPrefab, pos, Quaternion.identity);
            marker.transform.SetParent(worldSpawner.CurrentWorld.transform);

            // Orient the dot outward from the world centre so it sits flush on the surface
            Vector3 outward = (pos - worldSpawner.GetWorldCenter()).normalized;
            marker.transform.rotation = Quaternion.LookRotation(outward) * Quaternion.Euler(90f, 0f, 0f);

            PlacementSlot slot = marker.GetComponent<PlacementSlot>();
            if (slot != null) slot.Initialize(slotIndex);

            activeMarkers.Add(marker);
        }
    }

    private void ClearMarkers()
    {
        foreach (GameObject m in activeMarkers)
            if (m != null) Destroy(m);
        activeMarkers.Clear();
    }

    private GameObject GetMarkerBySlotIndex(int slotIndex)
    {
        foreach (GameObject m in activeMarkers)
        {
            if (m == null) continue;
            PlacementSlot ps = m.GetComponent<PlacementSlot>();
            if (ps != null && ps.SlotIndex == slotIndex) return m;
        }
        return null;
    }

    // ── Update: drag-to-spin + tap-to-confirm ─────────────────────────────────

    private void Update()
    {
        if (!inPlacementMode) return;
        HandleInput();
    }

    private void HandleInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            touchStartPos = Input.mousePosition;
            lastInputPos  = Input.mousePosition;
            isDragging    = false;
        }
        else if (Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - lastInputPos;
            if (Vector3.Distance(Input.mousePosition, touchStartPos) > dragThresholdPixels)
                isDragging = true;
            if (isDragging && delta.sqrMagnitude > 0.01f)
                SpinWorld(delta);
            lastInputPos = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (!isDragging)
                TryConfirmAtScreenPoint(Input.mousePosition, -1);
            isDragging = false;
        }
#else
        if (Input.touchCount != 1) return;
        Touch touch = Input.GetTouch(0);
        Vector3 touchPos = touch.position;

        switch (touch.phase)
        {
            case TouchPhase.Began:
                touchStartPos = touchPos;
                lastInputPos  = touchPos;
                isDragging    = false;
                break;

            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                Vector3 delta = touchPos - lastInputPos;
                if (Vector3.Distance(touchPos, touchStartPos) > dragThresholdPixels)
                    isDragging = true;
                if (isDragging && delta.sqrMagnitude > 0.01f)
                    SpinWorld(delta);
                lastInputPos = touchPos;
                break;

            case TouchPhase.Ended:
                if (!isDragging)
                    TryConfirmAtScreenPoint(touchPos, touch.fingerId);
                isDragging = false;
                break;
        }
#endif
    }

    private void SpinWorld(Vector3 screenDelta)
    {
        if (worldSpawner.CurrentWorld == null || mainCamera == null) return;
        float rotY = -screenDelta.x * spinSensitivity;
        float rotX =  screenDelta.y * spinSensitivity;
        Transform world = worldSpawner.CurrentWorld.transform;
        world.Rotate(mainCamera.transform.up,    rotY, Space.World);
        world.Rotate(mainCamera.transform.right, rotX, Space.World);
    }

    /// <param name="fingerId">Pass -1 for mouse/standalone; pass the touch finger ID for mobile.</param>
    private void TryConfirmAtScreenPoint(Vector3 screenPoint, int fingerId)
    {
        // Ignore taps that land on UI elements (e.g. the Cancel button).
        // For touch input, pass the finger ID so Unity checks the correct pointer.
        if (EventSystem.current != null)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (EventSystem.current.IsPointerOverGameObject()) return;
#else
            if (fingerId >= 0 && EventSystem.current.IsPointerOverGameObject(fingerId)) return;
#endif
        }

        if (mainCamera == null) return;

        // Use RaycastAll so a planet collider in front of a slot marker never silently
        // blocks the hit — we iterate all hits and find the first PlacementSlot.
        Ray ray = mainCamera.ScreenPointToRay(screenPoint);
        RaycastHit[] hits = Physics.RaycastAll(ray);
        foreach (RaycastHit hit in hits)
        {
            PlacementSlot slot = hit.collider.GetComponent<PlacementSlot>();
            if (slot != null)
            {
                SelectSlot(slot.SlotIndex);
                return;
            }
        }
    }

    /// <summary>
    /// Marks a slot as the pending selection and shows the Confirm button.
    /// </summary>
    private void SelectSlot(int slotIndex)
    {
        selectedSlotIndex = slotIndex;
        if (confirmButton != null)
            confirmButton.SetActive(true);
    }

    /// <summary>
    /// Wired to the Confirm button in the placement overlay UI.
    /// Finalises placement for the previously selected slot.
    /// </summary>
    public void ConfirmSelectedSlot()
    {
        if (!inPlacementMode || selectedSlotIndex < 0) return;
        ConfirmPlacement(selectedSlotIndex);
    }
}
