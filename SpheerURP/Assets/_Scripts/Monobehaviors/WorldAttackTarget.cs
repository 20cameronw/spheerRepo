using UnityEngine;

/// <summary>
/// Added programmatically by <see cref="AttackManager"/> to every Collider-bearing
/// GameObject inside the spawned enemy world.
///
/// When the player taps / clicks this object, the nearest <see cref="IAttackable"/>
/// ancestor is routed through <see cref="Player.targetThis"/> so the existing
/// turret / lazer / missile system automatically starts attacking that target —
/// exactly the same flow used when attacking alien UFOs.
///
/// Priorities: <see cref="AttackBuildingView"/> (individual building) is preferred
/// over <see cref="AttackWorldView"/> (whole world) so a precise building tap
/// always targets that specific building.
/// </summary>
public class WorldAttackTarget : MonoBehaviour
{
    private void OnMouseDown()
    {
        if (Player.Instance == null) return;

        // Try to find the most specific IAttackable on this object or its ancestors.
        AttackBuildingView building = GetComponentInParent<AttackBuildingView>();
        if (building != null && !building.IsDestroyed)
        {
            Player.Instance.targetThis(building.transform);
            return;
        }

        AttackWorldView world = GetComponentInParent<AttackWorldView>();
        if (world != null && !world.IsDestroyed)
        {
            Player.Instance.targetThis(world.transform);
        }
    }
}
