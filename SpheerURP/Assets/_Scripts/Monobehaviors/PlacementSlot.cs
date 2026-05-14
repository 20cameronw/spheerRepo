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

    public void Initialize(int slotIndex)
    {
        SlotIndex = slotIndex;
    }
}
