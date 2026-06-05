/// <summary>
/// The resource types produced and consumed in Spheer.
/// Each building declares which resource it produces via Upgrade.resourceProduced.
///
/// Storage types (NebuliteStorage, PlasmaStorage) follow the same pattern as
/// Electricity: a building's <c>bonus</c> field increases the corresponding
/// player capacity rather than generating per-second income.
/// </summary>
public enum ResourceType
{
    Nebulite,         // Primary building/upgrade resource (was "dollars")
    Plasma,           // Used for research and troops
    Electricity,      // Powers advanced buildings — bonus routes to electricityCapacity
    VoidCrystal,      // Late-game elite resource
    NebuliteStorage,  // Phase 2: building increases Nebulite storage cap (NebuliteVault)
    PlasmaStorage,    // Phase 2: building increases Plasma storage cap (PlasmaTank)
}
