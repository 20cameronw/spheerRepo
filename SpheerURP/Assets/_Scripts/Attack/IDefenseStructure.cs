/// <summary>
/// Implemented by defensive structures placed on the enemy base.
/// Defensive buildings can resist or be vulnerable to specific weapon types,
/// enabling strategic depth when choosing which weapons to bring.
/// </summary>
public interface IDefenseStructure : IAttackable
{
    /// <summary>Weapon types that deal double damage to this structure.</summary>
    AttackWeaponType[] VulnerableTo { get; }

    /// <summary>Weapon types that deal half damage to this structure.</summary>
    AttackWeaponType[] ResistantTo { get; }

    /// <summary>Base armor/defense rating; reduces incoming flat damage.</summary>
    float DefenseRating { get; }
}
