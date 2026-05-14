using UnityEngine;

/// <summary>
/// Attached to each blue-dot slot marker that appears on the world surface
/// during placement mode.  Stores the slot's index in WorldSpawner's slot
/// list so PlacementManager can confirm the placement without a separate
/// raycast-to-position lookup.
/// </summary>
public class PlacementSlot : MonoBehaviour
{
    public int SlotIndex { get; private set; }

    private Renderer markerRenderer;
    private Material markerMaterial;
    private Color originalColor;

    private void Awake()
    {
        markerRenderer = GetComponent<Renderer>();
        if (markerRenderer != null)
        {
            // Cache the material instance so repeated calls to SetSelected do not
            // create a new material instance each time (which would leak memory).
            markerMaterial = markerRenderer.material;
            originalColor  = markerMaterial.color;
        }
    }

    public void Initialize(int slotIndex)
    {
        SlotIndex = slotIndex;
    }

    /// <summary>
    /// Highlights this marker with <paramref name="selectedColor"/> when
    /// <paramref name="selected"/> is true, or restores its original color.
    /// </summary>
    public void SetSelected(bool selected, Color selectedColor)
    {
        if (markerMaterial == null) return;
        markerMaterial.color = selected ? selectedColor : originalColor;
    }
}
