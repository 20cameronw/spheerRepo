using UnityEngine;

/// <summary>
/// ScriptableObject that configures a single attack weapon loadout item.
/// Create via Assets → Create → Attack → Weapon.
/// </summary>
[CreateAssetMenu(fileName = "New Attack Weapon", menuName = "Attack/Weapon")]
public class AttackWeaponSO : ScriptableObject
{
    [Header("Identity")]
    public new string name;
    public string description;
    public Sprite icon;

    [Header("Combat")]
    public AttackWeaponType weaponType;

    [Tooltip("Base damage per hit / per second for beam weapons.")]
    public float baseDamage = 10f;

    [Tooltip("Attack range in world units.")]
    public float baseRange = 15f;

    [Tooltip("Attacks per second (for burst weapons; ignored by beam types).")]
    public float fireRate = 1f;

    [Header("Requirements")]
    [Tooltip("Minimum XP level before the player can deploy this weapon.")]
    public int requiredXPLevel = 0;
}
