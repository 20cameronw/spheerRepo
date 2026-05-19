/// <summary>
/// Implemented by anything on the enemy base that can receive damage during an attack.
/// Concrete implementations: AttackBuildingView (individual buildings), AttackWorldView (the base core).
/// </summary>
public interface IAttackable
{
    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsDestroyed { get; }

    /// <summary>Applies <paramref name="damage"/> of the given <paramref name="weaponType"/>.</summary>
    void TakeDamage(float damage, AttackWeaponType weaponType);
}
