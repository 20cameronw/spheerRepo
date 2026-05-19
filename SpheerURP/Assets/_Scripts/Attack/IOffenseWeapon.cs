using System.Collections;
using UnityEngine;

/// <summary>
/// Implemented by player-side attack weapons deployed during an assault.
/// New weapons (bombs, lasers, fire, EMP, etc.) should implement this so
/// AttackManager can treat them uniformly.
/// </summary>
public interface IOffenseWeapon
{
    AttackWeaponType WeaponType { get; }

    float Damage { get; }

    float Range { get; }

    /// <summary>Fires at <paramref name="target"/>; yields until the attack resolves.</summary>
    IEnumerator FireAt(IAttackable target);
}
