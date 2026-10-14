using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the interactive building-placement flow:
/// <list type="number">
///   <item>Player presses Buy in the shop → TransactionManager calls <see cref="EnterPlacementMode"/>.</item>
///   <item>World moves closer (Z axis).  All open UI panels close.  No money is taken yet.</item>
///   <item>Blue slot markers appear on every available surface slot.</item>
///   <item>Player drags one finger to spin the world.</item>
///   <item>Player taps blue dots to toggle them selected (green) / deselected.</item>
///   <item>A bill text field shows the cumulative cost as slots are added / removed.</item>
///   <item>Player taps Confirm → money is deducted and all selected buildings are spawned.</item>
///   <item>Or player taps Cancel → no charge; placement mode exits cleanly.</item>
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
    /// Button that is shown after the player taps at least one slot marker,
    /// allowing them to confirm all selected placements.  The onClick listener is
    /// wired to <see cref="ConfirmAllSelected"/> automatically in code —
    /// you do NOT need to wire it in the Inspector.
    /// </summary>
    [SerializeField] private Button confirmButton;

    /// <summary>
    /// Text field shown in the placement overlay.  Updated whenever the selection
    /// changes to display the running total cost of all selected slots.
    /// </summary>
    [SerializeField] private TMP_Text costBillText;

    [Header("Slot Visuals")]
    /// <summary>
    /// Prefab used for each available-slot indicator (blue dot).
    /// Must have: MeshRenderer, Collider, and PlacementSlot component.
    /// </summary>
    [SerializeField] private GameObject slotMarkerPrefab;

    [Tooltip("Colour applied to slot markers that the player has selected.")]
    [SerializeField] private Color selectedSlotColor = Color.green;

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

    // Multi-slot selection
    private readonly List<int> selectedSlotIndices = new List<int>();
    private readonly List<GameObject> selectedMarkerGOs = new List<GameObject>();

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

        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmAllSelected);
    }

    // ── Entry / exit ──────────────────────────────────────────────────────────

    /// <summary>
    /// Enters placement mode for a surface building.  No money is deducted here;
    /// the full bill is charged only when the player confirms.
    /// </summary>
    public void EnterPlacementMode(int upgradeIndex)
    {
        if (inPlacementMode) return;

        inPlacementMode     = true;
        pendingUpgradeIndex = upgradeIndex;

        selectedSlotIndices.Clear();
        selectedMarkerGOs.Clear();

        worldSpawner.SetAutoRotate(false);
        uiManager.ClosePanel();

        if (placementOverlayUI != null)
            placementOverlayUI.SetActive(true);
        if (confirmButton != null)
            confirmButton.gameObject.SetActive(false);

        UpdateCostBill();

        MoveWorldZ(placementWorldZ, ShowSlotMarkers);
    }

    private void ExitPlacementMode()
    {
        inPlacementMode     = false;
        pendingUpgradeIndex = -1;

        // Deselect all highlighted markers before clearing
        foreach (GameObject go in selectedMarkerGOs)
        {
            if (go == null) continue;
            PlacementSlot ps = go.GetComponent<PlacementSlot>();
            if (ps != null) ps.SetSelected(false, selectedSlotColor);
        }
        selectedSlotIndices.Clear();
        selectedMarkerGOs.Clear();

        ClearMarkers();

        if (placementOverlayUI != null)
            placementOverlayUI.SetActive(false);
        if (confirmButton != null)
            confirmButton.gameObject.SetActive(false);
        if (costBillText != null)
            costBillText.text = string.Empty;

        worldSpawner.SetAutoRotate(true);
        MoveWorldZ(normalWorldZ);
    }

    // ── Player actions ────────────────────────────────────────────────────────

    /// <summary>
    /// Wired to the Confirm button.  Charges the full bill and places all
    /// selected buildings.
    /// </summary>
    public void ConfirmAllSelected()
    {
        if (!inPlacementMode || selectedSlotIndices.Count == 0) return;

        // Calculate total cost (each additional placement costs more).
        int startingCount = Player.Instance.getNumberBuildings(pendingUpgradeIndex);
        float total = 0f;
        for (int i = 0; i < selectedSlotIndices.Count; i++)
            total += TransactionManager.Instance.GetCostAtBuildingCount(pendingUpgradeIndex, startingCount + i);

        if (Player.Instance.getDollars() < total)
        {
            int count = selectedSlotIndices.Count;
            PopupManager.Instance.ShowPopup(
                "Not enough money to place " + count + " building" + (count > 1 ? "s" : "")
                + ". Total cost: $" + total.ToString("F2"));
            return;
        }

        Upgrade upgradeItem = TransactionManager.Instance.structuresPanelInfo
            .shopItemsSO[pendingUpgradeIndex];

        // Phase 3: verify electricity is available for ALL selected slots before committing.
        if (upgradeItem.electricityRequired > 0f)
        {
            float totalElecNeeded = upgradeItem.electricityRequired * selectedSlotIndices.Count;
            if (Player.Instance.getElectricityFree() < totalElecNeeded)
            {
                PopupManager.Instance.ShowPopup(
                    "Not enough electricity for " + selectedSlotIndices.Count + " building"
                    + (selectedSlotIndices.Count > 1 ? "s" : "") + ". Needs "
                    + totalElecNeeded + " ⚡ (available: "
                    + Player.Instance.getElectricityFree().ToString("F0") + " ⚡).");
                return;
            }
        }

        // Deduct the full bill.
        Player.Instance.AddDollars(-total);
        uiManager.CreateAnimatedText("-" + total.ToString("F2"), Color.red, 1f);

        int slotSize = Mathf.Max(1, upgradeItem.slotSize);
        float bonus  = upgradeItem.bonus;

        AudioManager.Instance.Play("Place Building");

        // Place each building.
        foreach (int slotIndex in new List<int>(selectedSlotIndices))
        {
            GameObject marker = GetMarkerBySlotIndex(slotIndex);
            Vector3 spawnPos = marker != null
                ? marker.transform.position
                : worldSpawner.GetWorldCenter();

            worldSpawner.OccupySlot(spawnPos, slotSize);
            worldSpawner.SpawnAtPosition(pendingUpgradeIndex, spawnPos);
            Player.Instance.AddBuildingCount(pendingUpgradeIndex);
            Player.Instance.RoutePassiveIncome(upgradeItem.resourceProduced, bonus);
            // Phase 3: track electricity demand and Town Hall level.
            Player.Instance.OnBuildingPlaced(upgradeItem);
        }

        structuresPanel.LoadCards();
        ExitPlacementMode();
    }

    /// <summary>
    /// Wired to the Cancel button.  Exits placement mode without any charge.
    /// </summary>
    public void CancelPlacement()
    {
        if (!inPlacementMode) return;
        structuresPanel.LoadCards();
        ExitPlacementMode();
    }

    // ── Cost bill ─────────────────────────────────────────────────────────────

    private void UpdateCostBill()
    {
        if (costBillText == null) return;

        if (pendingUpgradeIndex < 0 || selectedSlotIndices.Count == 0)
        {
            costBillText.text = "Select a slot";
            return;
        }

        int startingCount = Player.Instance.getNumberBuildings(pendingUpgradeIndex);
        float total = 0f;
        for (int i = 0; i < selectedSlotIndices.Count; i++)
            total += TransactionManager.Instance.GetCostAtBuildingCount(pendingUpgradeIndex, startingCount + i);

        int count = selectedSlotIndices.Count;
        costBillText.text = count + " building" + (count > 1 ? "s" : "")
            + "\nTotal: $" + total.ToString("F2");
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

        var available = worldSpawner.GetAvailableSlotPositions(slotSize);

        foreach (var (pos, slotIndex) in available)
        {
            GameObject marker = Instantiate(slotMarkerPrefab, pos, Quaternion.identity);
            marker.transform.SetParent(worldSpawner.CurrentWorld.transform);

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

    // ── Update: drag-to-spin + tap-to-select ─────────────────────────────────

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
                TryToggleSlotAtScreenPoint(Input.mousePosition, -1);
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
                    TryToggleSlotAtScreenPoint(touchPos, touch.fingerId);
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
    private void TryToggleSlotAtScreenPoint(Vector3 screenPoint, int fingerId)
    {
        if (EventSystem.current != null)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (EventSystem.current.IsPointerOverGameObject()) return;
#else
            if (fingerId >= 0 && EventSystem.current.IsPointerOverGameObject(fingerId)) return;
#endif
        }

        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(screenPoint);
        RaycastHit[] hits = Physics.RaycastAll(ray);
        foreach (RaycastHit hit in hits)
        {
            PlacementSlot slot = hit.collider.GetComponent<PlacementSlot>();
            if (slot != null)
            {
                ToggleSlot(slot.SlotIndex);
                return;
            }
        }
    }

    /// <summary>
    /// Toggles a slot's selection state.  Selecting adds it to the bill;
    /// deselecting removes it.
    /// </summary>
    private void ToggleSlot(int slotIndex)
    {
        int existingIdx = selectedSlotIndices.IndexOf(slotIndex);
        if (existingIdx >= 0)
        {
            // Deselect
            selectedSlotIndices.RemoveAt(existingIdx);
            GameObject markerGO = selectedMarkerGOs[existingIdx];
            selectedMarkerGOs.RemoveAt(existingIdx);
            if (markerGO != null)
            {
                PlacementSlot ps = markerGO.GetComponent<PlacementSlot>();
                if (ps != null) ps.SetSelected(false, selectedSlotColor);
            }
        }
        else
        {
            // Select
            selectedSlotIndices.Add(slotIndex);
            GameObject markerGO = GetMarkerBySlotIndex(slotIndex);
            selectedMarkerGOs.Add(markerGO);
            if (markerGO != null)
            {
                PlacementSlot ps = markerGO.GetComponent<PlacementSlot>();
                if (ps != null) ps.SetSelected(true, selectedSlotColor);
            }
        }

        // Show confirm button only when at least one slot is selected.
        if (confirmButton != null)
            confirmButton.gameObject.SetActive(selectedSlotIndices.Count > 0);

        UpdateCostBill();
    }
}

